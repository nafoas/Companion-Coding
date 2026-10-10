using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CompanionCore.Api;

public sealed class BraincaseStateBusyException : IOException
{
    public BraincaseStateBusyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal sealed record BraincaseJournalRecovery(bool TornTailTruncated, bool CorruptJournalPreserved, int InterruptedOperations);

/// <summary>
/// Append-only, checksummed JSON-lines journal for the bridge's local state. Each append
/// is flushed to disk before it is applied. One exclusive writer handle fences a second
/// bridge on the same location. Recovery truncates one torn trailing line, preserves a
/// corrupt journal aside, and marks operations that were in flight as interrupted.
/// </summary>
internal sealed class BraincaseJournal : IDisposable
{
    internal const long DefaultCompactionThresholdBytes = 512 * 1024;
    internal const long MaximumJournalBytes = 8 * 1024 * 1024;
    private const int ChecksumHexCharacters = 32;

    private readonly object _gate = new();
    private readonly BraincaseStateLocation _location;
    private readonly long _compactionThresholdBytes;
    private FileStream _stream;
    private long _nextSequence;
    private bool _disposed;

    private BraincaseJournal(BraincaseStateLocation location, FileStream stream, long compactionThresholdBytes)
    {
        _location = location;
        _compactionThresholdBytes = compactionThresholdBytes;
        _stream = stream;
    }

    internal BraincaseJournalState State { get; private set; } = new();

    internal BraincaseJournalRecovery Recovery { get; private set; } = new(false, false, 0);

    internal long LengthBytes
    {
        get
        {
            lock (_gate)
            {
                return _stream.Length;
            }
        }
    }

    internal static BraincaseJournal Open(
        BraincaseStateLocation location,
        DateTimeOffset nowUtc,
        long compactionThresholdBytes = DefaultCompactionThresholdBytes)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentOutOfRangeException.ThrowIfLessThan(compactionThresholdBytes, 1024);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(compactionThresholdBytes, MaximumJournalBytes / 2);
        Directory.CreateDirectory(location.RootPath);
        var stream = OpenWriter(location.JournalPath);
        var journal = new BraincaseJournal(location, stream, compactionThresholdBytes);
        try
        {
            journal.Recover(nowUtc);
            return journal;
        }
        catch
        {
            journal.Dispose();
            throw;
        }
    }

    internal void Append(BraincaseJournalEntry entry)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            entry.Seq = _nextSequence;
            var line = Seal(entry);
            var lengthBefore = _stream.Length;
            try
            {
                _stream.Seek(0, SeekOrigin.End);
                _stream.Write(line);
                _stream.Flush(flushToDisk: true);
            }
            catch
            {
                // Never leave a partial line ahead of later appends.
                try
                {
                    _stream.SetLength(lengthBefore);
                    _stream.Flush(flushToDisk: true);
                }
                catch (IOException)
                {
                }

                throw;
            }

            _nextSequence++;
            State.Apply(entry);
        }
    }

    /// <summary>
    /// Replaces an oversized journal with one snapshot line through a flushed temporary file
    /// and an atomic replace. Only called while no operation is open.
    /// </summary>
    internal bool CompactIfNeeded(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stream.Length <= _compactionThresholdBytes || State.OpenOperations.Count != 0)
            {
                return false;
            }

            var snapshot = new BraincaseJournalEntry
            {
                Seq = _nextSequence,
                At = nowUtc,
                Type = BraincaseJournalEntryType.Snapshot,
                Snapshot = State.ToSnapshot(),
            };
            var line = Seal(snapshot);
            using (var temporary = new FileStream(
                       _location.CompactionTemporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            {
                temporary.Write(line);
                temporary.Flush(flushToDisk: true);
            }

            _stream.Dispose();
            try
            {
                File.Move(_location.CompactionTemporaryPath, _location.JournalPath, overwrite: true);
            }
            finally
            {
                _stream = OpenWriter(_location.JournalPath);
                _stream.Seek(0, SeekOrigin.End);
            }

            _nextSequence++;
            return true;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stream.Dispose();
        }
    }

    private void Recover(DateTimeOffset nowUtc)
    {
        if (File.Exists(_location.CompactionTemporaryPath))
        {
            File.Delete(_location.CompactionTemporaryPath);
        }

        var torn = false;
        var corrupt = false;
        var state = new BraincaseJournalState();
        long lastSequence = 0;
        long validLength = 0;

        if (_stream.Length > MaximumJournalBytes)
        {
            corrupt = true;
        }
        else
        {
            var content = new byte[_stream.Length];
            _stream.Seek(0, SeekOrigin.Begin);
            _stream.ReadExactly(content);
            var offset = 0;
            while (offset < content.Length)
            {
                var newline = Array.IndexOf(content, (byte)'\n', offset);
                var end = newline < 0 ? content.Length : newline;
                var isLast = newline < 0 || newline == content.Length - 1;
                var valid = TryReadLine(content.AsSpan(offset, end - offset), lastSequence, out var entry);
                if (valid && newline >= 0)
                {
                    try
                    {
                        state.Apply(entry!);
                        lastSequence = entry!.Seq;
                        validLength = newline + 1;
                        offset = newline + 1;
                        continue;
                    }
                    catch (FormatException)
                    {
                        valid = false;
                    }
                }

                if (isLast)
                {
                    // One torn or unterminated trailing line: the append never completed.
                    torn = true;
                }
                else
                {
                    corrupt = true;
                }

                break;
            }
        }

        if (corrupt)
        {
            PreserveCorruptJournal(nowUtc);
            state = new BraincaseJournalState();
            lastSequence = 0;
        }
        else if (torn)
        {
            _stream.SetLength(validLength);
            _stream.Flush(flushToDisk: true);
        }

        _stream.Seek(0, SeekOrigin.End);
        State = state;
        _nextSequence = lastSequence + 1;

        var interrupted = state.OpenOperations.ToArray();
        foreach (var operation in interrupted)
        {
            Append(new BraincaseJournalEntry
            {
                At = nowUtc,
                Type = BraincaseJournalEntryType.Interrupted,
                Op = operation,
            });
        }

        Recovery = new BraincaseJournalRecovery(torn, corrupt, interrupted.Length);
        CompactIfNeeded(nowUtc);
    }

    private void PreserveCorruptJournal(DateTimeOffset nowUtc)
    {
        _stream.Dispose();
        Directory.CreateDirectory(_location.CorruptDirectoryPath);
        var preserved = Path.Combine(
            _location.CorruptDirectoryPath,
            $"journal-{nowUtc.UtcTicks:D19}-{Guid.NewGuid():N}.jsonl");
        File.Move(_location.JournalPath, preserved);
        _stream = OpenWriter(_location.JournalPath);
    }

    private static bool TryReadLine(ReadOnlySpan<byte> line, long previousSequence, out BraincaseJournalEntry? entry)
    {
        entry = null;
        if (line.IsEmpty)
        {
            return false;
        }

        try
        {
            entry = JsonSerializer.Deserialize<BraincaseJournalEntry>(line, SemanticSchema.StrictOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (entry is null
            || entry.V != 1
            || entry.Sum is not { Length: ChecksumHexCharacters } sum
            || entry.Seq <= 0
            || (previousSequence > 0 && entry.Seq != previousSequence + 1))
        {
            return false;
        }

        entry.Sum = null;
        var expected = Checksum(JsonSerializer.SerializeToUtf8Bytes(entry, SemanticSchema.StrictOptions));
        entry.Sum = sum;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(sum));
    }

    private static byte[] Seal(BraincaseJournalEntry entry)
    {
        entry.Sum = null;
        entry.Sum = Checksum(JsonSerializer.SerializeToUtf8Bytes(entry, SemanticSchema.StrictOptions));
        var json = JsonSerializer.SerializeToUtf8Bytes(entry, SemanticSchema.StrictOptions);
        var line = new byte[json.Length + 1];
        json.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        return line;
    }

    private static string Checksum(ReadOnlySpan<byte> payload) =>
        Convert.ToHexStringLower(SHA256.HashData(payload))[..ChecksumHexCharacters];

    private static FileStream OpenWriter(string path)
    {
        try
        {
            return new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
        }
        catch (IOException exception)
        {
            throw new BraincaseStateBusyException("The bridge journal is already owned by another bridge.", exception);
        }
    }
}
