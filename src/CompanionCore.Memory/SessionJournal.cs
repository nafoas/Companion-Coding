using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CompanionCore.Memory;

internal sealed class SessionJournal : IAsyncDisposable
{
    private const int ChecksumLength = 32;
    private const int FrameMetadataLength = 1 + sizeof(long) + sizeof(int);
    private const int MinimumBodyLength = FrameMetadataLength + ChecksumLength;
    private const int MaximumBodyLength = 16 * 1024 * 1024;

    private static readonly byte[] Header = [(byte)'C', (byte)'C', (byte)'S', (byte)'J', 1, 0, 0, 0];

    private readonly FileStream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private IReadOnlyList<JournalAppendFrame> _recoveryTail = Array.Empty<JournalAppendFrame>();
    private long _nextSequence = 1;
    private long _confirmedThrough;
    private bool _faulted;
    private bool _disposed;

    private SessionJournal(FileStream stream)
    {
        _stream = stream;
    }

    internal IReadOnlyList<JournalAppendFrame> RecoveryTail => _recoveryTail;

    internal long ConfirmedThrough => _confirmedThrough;

    internal long HighestAppendSequence => _nextSequence - 1;

    internal static async Task<SessionJournal> OpenAsync(string journalPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)
            ?? throw new ArgumentException("The journal path must have a parent directory.", nameof(journalPath)));

        var stream = new FileStream(
            journalPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous);

        var journal = new SessionJournal(stream);
        try
        {
            await journal.InitializeAndScanAsync(cancellationToken).ConfigureAwait(false);
            return journal;
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            journal._writeLock.Dispose();
            throw;
        }
    }

    internal async Task<long> AppendOperationAsync(
        ReadOnlyMemory<byte> canonicalOperationPayload,
        CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (canonicalOperationPayload.IsEmpty)
        {
            throw new ArgumentException("A journal append requires a payload.", nameof(canonicalOperationPayload));
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var durableWriteStarted = false;
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();
            var sequence = _nextSequence;
            durableWriteStarted = true;
            await WriteFrameDurablyAsync(
                    JournalFrameType.AppendOperation,
                    sequence,
                    canonicalOperationPayload,
                    CancellationToken.None)
                .ConfigureAwait(false);
            _nextSequence = checked(sequence + 1);
            return sequence;
        }
        catch
        {
            if (durableWriteStarted)
            {
                _faulted = true;
            }

            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal async Task AppendCheckpointAsync(long confirmedThrough, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var durableWriteStarted = false;
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();
            if (confirmedThrough < _confirmedThrough || confirmedThrough >= _nextSequence)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(confirmedThrough),
                    "A checkpoint must advance through an append sequence that exists in this journal.");
            }

            if (confirmedThrough == _confirmedThrough)
            {
                return;
            }

            durableWriteStarted = true;
            await WriteFrameDurablyAsync(
                    JournalFrameType.Checkpoint,
                    confirmedThrough,
                    ReadOnlyMemory<byte>.Empty,
                    CancellationToken.None)
                .ConfigureAwait(false);
            _confirmedThrough = confirmedThrough;
        }
        catch
        {
            if (durableWriteStarted)
            {
                _faulted = true;
            }

            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Internal deterministic process-death injection used only by the friend test
    /// assembly. It writes and flushes a strict prefix of a real production frame, then
    /// permanently faults this journal instance so no live code can continue on it.
    /// </summary>
    internal async Task AppendTornFrameForTestAsync(
        ReadOnlyMemory<byte> canonicalOperationPayload,
        int bytesToWrite,
        CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();
            var rawFrame = BuildRawFrame(
                JournalFrameType.AppendOperation,
                _nextSequence,
                canonicalOperationPayload.Span);
            if (bytesToWrite <= 0 || bytesToWrite >= rawFrame.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bytesToWrite),
                    "A torn-frame injection must write a strict, non-empty frame prefix.");
            }

            _stream.Seek(0, SeekOrigin.End);
            await _stream.WriteAsync(rawFrame.AsMemory(0, bytesToWrite), CancellationToken.None)
                .ConfigureAwait(false);
            await _stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            _stream.Flush(flushToDisk: true);
            _faulted = true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stream.DisposeAsync().ConfigureAwait(false);
        _writeLock.Dispose();
    }

    private async Task InitializeAndScanAsync(CancellationToken cancellationToken)
    {
        if (_stream.Length == 0)
        {
            await _stream.WriteAsync(Header, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            _stream.Flush(flushToDisk: true);
        }

        if (_stream.Length < Header.Length)
        {
            throw new JournalCorruptionException("The session journal header is truncated.");
        }

        _stream.Position = 0;
        var actualHeader = new byte[Header.Length];
        await ReadExactlyAsync(_stream, actualHeader, cancellationToken).ConfigureAwait(false);
        if (!actualHeader.AsSpan().SequenceEqual(Header))
        {
            throw new JournalCorruptionException("The session journal header or schema version is invalid.");
        }

        var appendFrames = new List<JournalAppendFrame>();
        long highestAppendSequence = 0;
        long lastCheckpoint = 0;

        while (_stream.Position < _stream.Length)
        {
            var frameStart = _stream.Position;
            var prefix = new byte[sizeof(int)];
            var prefixRead = await ReadUpToAsync(_stream, prefix, cancellationToken).ConfigureAwait(false);
            if (prefixRead < prefix.Length)
            {
                TruncateTornTail(frameStart);
                break;
            }

            var bodyLength = BinaryPrimitives.ReadInt32LittleEndian(prefix);
            var remaining = _stream.Length - _stream.Position;
            if (bodyLength < MinimumBodyLength || bodyLength > MaximumBodyLength)
            {
                throw new JournalCorruptionException("A journal frame declares an invalid bounded length.");
            }

            if (bodyLength > remaining)
            {
                TruncateTornTail(frameStart);
                break;
            }

            var body = new byte[bodyLength];
            await ReadExactlyAsync(_stream, body, cancellationToken).ConfigureAwait(false);
            var content = body.AsSpan(0, body.Length - ChecksumLength);
            var storedChecksum = body.AsSpan(body.Length - ChecksumLength, ChecksumLength);
            var actualChecksum = SHA256.HashData(content);
            if (!CryptographicOperations.FixedTimeEquals(storedChecksum, actualChecksum))
            {
                if (_stream.Position == _stream.Length)
                {
                    TruncateTornTail(frameStart);
                    break;
                }

                throw new JournalCorruptionException("A non-trailing journal frame failed checksum validation.");
            }

            var frameType = (JournalFrameType)content[0];
            var sequence = BinaryPrimitives.ReadInt64LittleEndian(content.Slice(1, sizeof(long)));
            var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(
                content.Slice(1 + sizeof(long), sizeof(int)));
            if (sequence <= 0
                || payloadLength < 0
                || payloadLength != content.Length - FrameMetadataLength)
            {
                throw new JournalCorruptionException("A checksummed journal frame has invalid metadata.");
            }

            var payload = content.Slice(FrameMetadataLength, payloadLength).ToArray();
            switch (frameType)
            {
                case JournalFrameType.AppendOperation:
                    if (payloadLength == 0 || sequence != highestAppendSequence + 1)
                    {
                        throw new JournalCorruptionException(
                            "Append-frame sequences must be contiguous and carry payloads.");
                    }

                    highestAppendSequence = sequence;
                    appendFrames.Add(new JournalAppendFrame(sequence, payload));
                    break;

                case JournalFrameType.Checkpoint:
                    if (payloadLength != 0
                        || sequence > highestAppendSequence
                        || sequence < lastCheckpoint)
                    {
                        throw new JournalCorruptionException("A checkpoint frame does not describe a valid append cut.");
                    }

                    lastCheckpoint = sequence;
                    break;

                default:
                    throw new JournalCorruptionException("The journal contains an unknown frame type.");
            }
        }

        _nextSequence = checked(highestAppendSequence + 1);
        _confirmedThrough = lastCheckpoint;
        _recoveryTail = appendFrames.Where(frame => frame.Sequence > lastCheckpoint).ToArray();
        _stream.Seek(0, SeekOrigin.End);
    }

    private async Task WriteFrameDurablyAsync(
        JournalFrameType frameType,
        long sequence,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        var rawFrame = BuildRawFrame(frameType, sequence, payload.Span);
        _stream.Seek(0, SeekOrigin.End);
        await _stream.WriteAsync(rawFrame, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        _stream.Flush(flushToDisk: true);
    }

    private static byte[] BuildRawFrame(
        JournalFrameType frameType,
        long sequence,
        ReadOnlySpan<byte> payload)
    {
        var contentLength = checked(FrameMetadataLength + payload.Length);
        var bodyLength = checked(contentLength + ChecksumLength);
        if (bodyLength > MaximumBodyLength)
        {
            throw new MemoryValidationException("The canonical operation exceeds the bounded journal-frame size.");
        }

        var rawFrame = new byte[sizeof(int) + bodyLength];
        BinaryPrimitives.WriteInt32LittleEndian(rawFrame, bodyLength);
        var content = rawFrame.AsSpan(sizeof(int), contentLength);
        content[0] = (byte)frameType;
        BinaryPrimitives.WriteInt64LittleEndian(content.Slice(1, sizeof(long)), sequence);
        BinaryPrimitives.WriteInt32LittleEndian(
            content.Slice(1 + sizeof(long), sizeof(int)),
            payload.Length);
        payload.CopyTo(content.Slice(FrameMetadataLength));

        var checksum = SHA256.HashData(content);
        checksum.CopyTo(rawFrame.AsSpan(sizeof(int) + contentLength, ChecksumLength));
        return rawFrame;
    }

    private void TruncateTornTail(long frameStart)
    {
        _stream.SetLength(frameStart);
        _stream.Flush(flushToDisk: true);
        _stream.Position = frameStart;
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            total += read;
        }
    }

    private static async Task<int> ReadUpToAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_faulted)
        {
            throw new InvalidOperationException(
                "The journal is faulted after a partial/failed write and must be reopened through recovery.");
        }
    }

    private enum JournalFrameType : byte
    {
        AppendOperation = 1,
        Checkpoint = 2,
    }
}

internal sealed record JournalAppendFrame(long Sequence, byte[] CanonicalOperationPayload);
