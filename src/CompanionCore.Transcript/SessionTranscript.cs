using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CompanionCore.Privacy;

namespace CompanionCore.Transcript;

public enum TranscriptRefusal
{
    None = 0,
    SessionEnded = 1,
    PrivacyPausedOrStale = 2,
    CredentialEcho = 3,
    InvalidText = 4,
    BoundExceeded = 5,
}

public sealed record TranscriptAppendResult(TranscriptRefusal Refusal, Guid? EventId)
{
    public bool Accepted => Refusal == TranscriptRefusal.None;
}

public sealed class TranscriptBusyException : IOException
{
    public TranscriptBusyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed record TranscriptRecovery(bool TornTailTruncated, bool CorruptTranscriptPreserved, bool ReopenedUnended);

/// <summary>
/// Append-only, checksummed, session-scoped transcript. Each append is flushed to disk
/// before it is acknowledged; a single exclusive writer fences any second writer. Content
/// is admitted only under the current privacy generation and never carries a configured
/// credential. It holds no committed-memory capability of any kind.
/// </summary>
public sealed class SessionTranscript : IDisposable
{
    private const int ChecksumHexCharacters = 32;
    private const int EndReserveBytes = 1024;

    private readonly object _gate = new();
    private readonly TranscriptLocation _location;
    private readonly RuntimePrivacyState _privacy;
    private readonly Func<string, bool>? _containsSecret;
    private readonly TranscriptOptions _options;
    private readonly Dictionary<Guid, Guid> _lastEventByThread = [];
    private readonly Dictionary<Guid, Guid> _lastInterruptionByThread = [];
    private readonly FileStream _writerLock;
    private FileStream _stream;
    private long _nextSequence = 1;
    private int _eventCount;
    private bool _disposed;

    private SessionTranscript(
        TranscriptLocation location,
        Guid sessionId,
        RuntimePrivacyState privacy,
        Func<string, bool>? containsSecret,
        TranscriptOptions options,
        FileStream writerLock,
        FileStream stream)
    {
        _writerLock = writerLock;
        _location = location;
        SessionId = sessionId;
        _privacy = privacy;
        _containsSecret = containsSecret;
        _options = options;
        _stream = stream;
    }

    public Guid SessionId { get; }

    public bool IsEnded { get; private set; }

    public long LastSequence => _nextSequence - 1;

    public TranscriptRecovery Recovery { get; private set; } = new(false, false, false);

    internal int EventCount => _eventCount;

    public static SessionTranscript Open(
        TranscriptLocation location,
        Guid sessionId,
        RuntimePrivacyState privacy,
        DateTimeOffset now,
        Func<string, bool>? containsSecret = null,
        TranscriptOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(privacy);
        if (sessionId == Guid.Empty || now == default)
        {
            throw new ArgumentException("A transcript needs a session and a timestamp.");
        }

        options ??= new TranscriptOptions();
        options.Validate();
        Directory.CreateDirectory(location.RootPath);

        // The exclusive writer fence is a separate lock file, because a shared-read data
        // stream only takes a shared lock on Unix. Readers may then read a live session.
        var writerLock = AcquireWriterLock(location.SessionPath(sessionId) + ".lock");
        FileStream stream;
        try
        {
            stream = OpenWriter(location.SessionPath(sessionId));
        }
        catch
        {
            writerLock.Dispose();
            throw;
        }

        var transcript = new SessionTranscript(
            location,
            sessionId,
            privacy,
            containsSecret,
            options,
            writerLock,
            stream);
        try
        {
            transcript.Recover(now);
            return transcript;
        }
        catch
        {
            transcript.Dispose();
            throw;
        }
    }

    internal Guid? LastEventFor(Guid threadId)
    {
        lock (_gate)
        {
            return _lastEventByThread.TryGetValue(threadId, out var id) ? id : null;
        }
    }

    internal Guid? LastInterruptionFor(Guid threadId)
    {
        lock (_gate)
        {
            return _lastInterruptionByThread.TryGetValue(threadId, out var id) ? id : null;
        }
    }

    /// <summary>Ends the session. Afterwards every append is refused.</summary>
    public TranscriptAppendResult End(DateTimeOffset at)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsEnded)
            {
                return new(TranscriptRefusal.SessionEnded, null);
            }

            var id = Write(new TranscriptEvent { At = at, Kind = TranscriptEventKind.SessionEnded });
            return new(TranscriptRefusal.None, id);
        }
    }

    internal TranscriptAppendResult Append(TranscriptEvent record, long expectedGeneration)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsEnded)
            {
                return new(TranscriptRefusal.SessionEnded, null);
            }

            if (!_privacy.IsCurrent(expectedGeneration))
            {
                return new(TranscriptRefusal.PrivacyPausedOrStale, null);
            }

            if (!ValidText(record.Text, _options.MaximumTextCharacters, record.Kind is TranscriptEventKind.Utterance or TranscriptEventKind.UrgentObservation)
                || !ValidText(record.Topic, _options.MaximumTopicCharacters, required: false))
            {
                return new(TranscriptRefusal.InvalidText, null);
            }

            if (_containsSecret is not null
                && (Echoes(record.Text) || Echoes(record.Topic)
                    || (record.Checkpoint is not null && Echoes(JsonSerializer.Serialize(record.Checkpoint, TranscriptJson.Options)))))
            {
                return new(TranscriptRefusal.CredentialEcho, null);
            }

            var line = Seal(Stamp(record));
            if (_eventCount + 1 >= _options.MaximumEvents
                || _stream.Length + line.Length > _options.MaximumBytes - EndReserveBytes)
            {
                _nextSequence--;
                return new(TranscriptRefusal.BoundExceeded, null);
            }

            WriteLine(line, record);
            return new(TranscriptRefusal.None, record.EventId);
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
            try
            {
                _stream.Dispose();
            }
            finally
            {
                _writerLock.Dispose();
            }
        }
    }

    private bool Echoes(string? text) => text is not null && _containsSecret!(text);

    private Guid Write(TranscriptEvent record)
    {
        var line = Seal(Stamp(record));
        WriteLine(line, record);
        return record.EventId;
    }

    private TranscriptEvent Stamp(TranscriptEvent record)
    {
        record.V = 1;
        record.Seq = _nextSequence++;
        record.SessionId = SessionId;
        record.EventId = DeriveEventId(SessionId, record.Seq);
        record.At = record.At.ToUniversalTime();
        return record;
    }

    private void WriteLine(byte[] line, TranscriptEvent record)
    {
        var lengthBefore = _stream.Length;
        try
        {
            _stream.Seek(0, SeekOrigin.End);
            _stream.Write(line);
            _stream.Flush(flushToDisk: true);
        }
        catch
        {
            try
            {
                _stream.SetLength(lengthBefore);
                _stream.Flush(flushToDisk: true);
            }
            catch (IOException)
            {
            }

            _nextSequence--;
            throw;
        }

        Apply(record);
    }

    private void Apply(TranscriptEvent record)
    {
        _eventCount++;
        if (record.ThreadId is { } threadId)
        {
            _lastEventByThread[threadId] = record.EventId;
            if (record.Kind == TranscriptEventKind.BnuyModeInterrupted)
            {
                _lastInterruptionByThread[threadId] = record.EventId;
            }
        }

        if (record.Kind == TranscriptEventKind.SessionEnded)
        {
            IsEnded = true;
        }
    }

    private void Recover(DateTimeOffset now)
    {
        var torn = false;
        var corrupt = false;
        var events = new List<TranscriptEvent>();
        long validLength = 0;
        if (_stream.Length > _options.MaximumBytes)
        {
            corrupt = true;
        }
        else if (_stream.Length > 0)
        {
            var content = new byte[_stream.Length];
            _stream.Seek(0, SeekOrigin.Begin);
            _stream.ReadExactly(content);
            corrupt = !TranscriptParser.TryParse(content, SessionId, events, out validLength, out torn);
        }

        if (corrupt)
        {
            _stream.Dispose();
            Directory.CreateDirectory(_location.CorruptDirectoryPath);
            File.Move(
                _location.SessionPath(SessionId),
                Path.Combine(_location.CorruptDirectoryPath, $"session-{SessionId:N}-{now.UtcTicks:D19}.jsonl"));
            _stream = OpenWriter(_location.SessionPath(SessionId));
            events.Clear();
        }
        else if (torn)
        {
            _stream.SetLength(validLength);
            _stream.Flush(flushToDisk: true);
        }

        foreach (var record in events)
        {
            Apply(record);
        }

        _nextSequence = events.Count == 0 ? 1 : events[^1].Seq + 1;
        var reopened = events.Count > 0 && !IsEnded;
        Recovery = new TranscriptRecovery(torn, corrupt, reopened);
        if (events.Count == 0 || reopened)
        {
            Write(new TranscriptEvent
            {
                At = now,
                Kind = TranscriptEventKind.SessionStarted,
                Recovered = corrupt || reopened ? true : null,
            });
        }
    }

    private static bool ValidText(string? text, int maximum, bool required)
    {
        if (text is null)
        {
            return !required;
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum)
        {
            return false;
        }

        foreach (var character in text)
        {
            if (char.IsControl(character) && character is not '\n' and not '\t')
            {
                return false;
            }
        }

        return true;
    }

    internal static byte[] Seal(TranscriptEvent record)
    {
        record.Sum = null;
        record.Sum = Checksum(JsonSerializer.SerializeToUtf8Bytes(record, TranscriptJson.Options));
        var json = JsonSerializer.SerializeToUtf8Bytes(record, TranscriptJson.Options);
        var line = new byte[json.Length + 1];
        json.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        return line;
    }

    internal static string Checksum(ReadOnlySpan<byte> payload) =>
        Convert.ToHexStringLower(SHA256.HashData(payload))[..ChecksumHexCharacters];

    internal static Guid DeriveEventId(Guid sessionId, long sequence)
    {
        Span<byte> input = stackalloc byte[24];
        sessionId.TryWriteBytes(input, bigEndian: true, out _);
        BinaryPrimitives.WriteInt64BigEndian(input[16..], sequence);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        var bytes = hash[..16].ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private static FileStream AcquireWriterLock(string path)
    {
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, bufferSize: 1, FileOptions.None);
        }
        catch (IOException exception)
        {
            throw new TranscriptBusyException("The session transcript is already open by another writer.", exception);
        }
    }

    private static FileStream OpenWriter(string path) =>
        new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, bufferSize: 1, FileOptions.None);
}

/// <summary>Shared strict parser for the writer's recovery and the read-only reader.</summary>
internal static class TranscriptParser
{
    /// <summary>
    /// Parses valid lines in order. A single invalid or unterminated trailing line is a torn
    /// tail; any other invalid line, sequence gap, or foreign session is corruption.
    /// </summary>
    internal static bool TryParse(
        byte[] content,
        Guid sessionId,
        List<TranscriptEvent> events,
        out long validLength,
        out bool torn)
    {
        validLength = 0;
        torn = false;
        var offset = 0;
        long previous = 0;
        while (offset < content.Length)
        {
            var newline = Array.IndexOf(content, (byte)'\n', offset);
            var end = newline < 0 ? content.Length : newline;
            var isLast = newline < 0 || newline == content.Length - 1;
            if (newline >= 0 && TryRead(content.AsSpan(offset, end - offset), sessionId, previous, out var record))
            {
                events.Add(record!);
                previous = record!.Seq;
                validLength = newline + 1;
                offset = newline + 1;
                continue;
            }

            if (isLast)
            {
                torn = true;
                return true;
            }

            return false;
        }

        return true;
    }

    private static bool TryRead(ReadOnlySpan<byte> line, Guid sessionId, long previous, out TranscriptEvent? record)
    {
        record = null;
        if (line.IsEmpty)
        {
            return false;
        }

        try
        {
            record = JsonSerializer.Deserialize<TranscriptEvent>(line, TranscriptJson.Options);
        }
        catch (JsonException)
        {
            return false;
        }

        if (record is null
            || record.V != 1
            || record.SessionId != sessionId
            || record.Seq != previous + 1
            || record.EventId != SessionTranscript.DeriveEventId(sessionId, record.Seq)
            || !Enum.IsDefined(record.Kind)
            || record.Sum is not { Length: 32 } sum)
        {
            return false;
        }

        record.Sum = null;
        var expected = SessionTranscript.Checksum(JsonSerializer.SerializeToUtf8Bytes(record, TranscriptJson.Options));
        record.Sum = sum;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(sum));
    }
}
