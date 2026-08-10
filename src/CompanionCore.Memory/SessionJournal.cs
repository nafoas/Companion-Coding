using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CompanionCore.Memory;

internal sealed class SessionJournal : IAsyncDisposable
{
    private const int ChecksumLength = 32;
    private const int FrameMetadataLength = 1 + sizeof(long) + sizeof(int);
    private const int MinimumBodyLength = FrameMetadataLength + ChecksumLength;
    private const int MaximumBodyLength = 16 * 1024 * 1024;
    private const int RotationBasePayloadLength = sizeof(int) + 16 + sizeof(long);
    private const int RotationBaseFormatVersion = 1;

    internal const int MaximumOperationPayloadLength =
        MaximumBodyLength - FrameMetadataLength - ChecksumLength;

    private static readonly byte[] Header = [(byte)'C', (byte)'C', (byte)'S', (byte)'J', 1, 0, 0, 0];

    private readonly string _journalPath;
    private FileStream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private IReadOnlyList<JournalAppendFrame> _allAppendFrames = Array.Empty<JournalAppendFrame>();
    private IReadOnlyList<JournalAppendFrame> _recoveryTail = Array.Empty<JournalAppendFrame>();
    private JournalRotationBase? _rotationBase;
    private long _nextSequence = 1;
    private long _confirmedThrough;
    private bool _faulted;
    private bool _disposed;

    private SessionJournal(string journalPath, FileStream stream)
    {
        _journalPath = journalPath;
        _stream = stream;
    }

    internal IReadOnlyList<JournalAppendFrame> RecoveryTail => _recoveryTail;

    internal IReadOnlyList<JournalAppendFrame> AllAppendFrames => _allAppendFrames;

    internal JournalRotationBase? RotationBase => _rotationBase;

    internal long ConfirmedThrough => _confirmedThrough;

    internal long HighestAppendSequence => _nextSequence - 1;

    internal static async Task<SessionJournal> OpenAsync(string journalPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)
            ?? throw new ArgumentException("The journal path must have a parent directory.", nameof(journalPath)));

        var stream = OpenStream(journalPath, FileMode.OpenOrCreate);

        var journal = new SessionJournal(journalPath, stream);
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
            var rawFrame = BuildRawFrame(
                JournalFrameType.AppendOperation,
                sequence,
                canonicalOperationPayload.Span);
            durableWriteStarted = true;
            await WriteFrameDurablyAsync(rawFrame, CancellationToken.None)
                .ConfigureAwait(false);
            var appended = new JournalAppendFrame(
                sequence,
                canonicalOperationPayload.ToArray());
            _allAppendFrames = [.. _allAppendFrames, appended];
            _recoveryTail = [.. _recoveryTail, appended];
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

            var rawFrame = BuildRawFrame(
                JournalFrameType.Checkpoint,
                confirmedThrough,
                ReadOnlySpan<byte>.Empty);
            durableWriteStarted = true;
            await WriteFrameDurablyAsync(rawFrame, CancellationToken.None)
                .ConfigureAwait(false);
            _confirmedThrough = confirmedThrough;
            _recoveryTail = _allAppendFrames
                .Where(frame => frame.Sequence > confirmedThrough)
                .ToArray();
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

    internal async Task RotateThroughAsync(
        long cutSequence,
        Guid backupId,
        IBackupTestHook? testHook,
        CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (cutSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cutSequence));
        }

        if (backupId == Guid.Empty)
        {
            throw new ArgumentException("A journal rotation requires a backup ID.", nameof(backupId));
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporaryPath = null;
        string? rollbackPath = null;
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();
            var existingBase = _rotationBase?.CutSequence ?? 0;
            if (cutSequence < existingBase
                || cutSequence > _confirmedThrough
                || cutSequence > HighestAppendSequence)
            {
                throw new MemoryIntegrityException(
                    "Journal rotation cannot move outside the confirmed promoted cut.");
            }

            var retained = _allAppendFrames
                .Where(frame => frame.Sequence > cutSequence)
                .OrderBy(frame => frame.Sequence)
                .ToArray();
            var expected = cutSequence + 1;
            foreach (var frame in retained)
            {
                if (frame.Sequence != expected)
                {
                    throw new JournalCorruptionException(
                        "Post-cut journal append sequences are not contiguous.");
                }

                expected = checked(expected + 1);
            }

            var parent = Path.GetDirectoryName(_journalPath)
                ?? throw new MemoryIntegrityException("The journal path has no parent directory.");
            temporaryPath = Path.Combine(
                parent,
                $".{Path.GetFileName(_journalPath)}.{Guid.NewGuid():N}.rotation.tmp");
            rollbackPath = Path.Combine(
                parent,
                $".{Path.GetFileName(_journalPath)}.{Guid.NewGuid():N}.rotation.old");

            await WriteRotatedJournalAsync(
                    temporaryPath,
                    new JournalRotationBase(
                        RotationBaseFormatVersion,
                        backupId,
                        cutSequence),
                    retained,
                    _confirmedThrough,
                    cancellationToken)
                .ConfigureAwait(false);

            await using (var validation = await OpenAsync(temporaryPath, cancellationToken)
                             .ConfigureAwait(false))
            {
                var validatedBase = validation.RotationBase;
                if (validatedBase is null
                    || validatedBase.BackupId != backupId
                    || validatedBase.CutSequence != cutSequence
                    || validation.HighestAppendSequence != HighestAppendSequence
                    || validation.ConfirmedThrough != _confirmedThrough
                    || validation.AllAppendFrames.Count != retained.Length)
                {
                    throw new MemoryIntegrityException(
                        "The staged rotated journal failed independent validation.");
                }
            }

            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        BackupTestPoint.BeforeJournalReplacement,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // From the first replacement step onward, cancellation is intentionally
            // ignored. The current stream is reopened before returning or throwing.
            await _stream.DisposeAsync().ConfigureAwait(false);
            try
            {
                File.Replace(temporaryPath, _journalPath, rollbackPath, ignoreMetadataErrors: true);
                temporaryPath = null;
            }
            catch
            {
                _stream = OpenStream(_journalPath, FileMode.Open);
                await InitializeAndScanAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            _stream = OpenStream(_journalPath, FileMode.Open);
            try
            {
                await InitializeAndScanAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
                if (File.Exists(rollbackPath))
                {
                    File.Replace(rollbackPath, _journalPath, destinationBackupFileName: null);
                    rollbackPath = null;
                }

                _stream = OpenStream(_journalPath, FileMode.Open);
                await InitializeAndScanAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        BackupTestPoint.AfterJournalReplacement,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            if (File.Exists(rollbackPath))
            {
                File.Delete(rollbackPath);
                rollbackPath = null;
            }
        }
        finally
        {
            try
            {
                if (temporaryPath is not null)
                {
                    MemoryPathGuard.TryDeleteTaskOwnedFile(
                        Path.GetDirectoryName(_journalPath)
                            ?? throw new MemoryIntegrityException(
                                "The journal path has no parent directory."),
                        temporaryPath);
                }
            }
            finally
            {
                // A rollback file is intentionally retained if an unexpected failure
                // occurred after replacement and before it was safe to discard.
                _writeLock.Release();
            }
        }
    }

    internal static Task BuildRecoveryJournalAsync(
        string path,
        Guid backupId,
        long cutSequence,
        IReadOnlyList<JournalAppendFrame> postCutFrames,
        CancellationToken cancellationToken) =>
        WriteRotatedJournalAsync(
            path,
            new JournalRotationBase(
                RotationBaseFormatVersion,
                backupId,
                cutSequence),
            postCutFrames,
            confirmedThrough: cutSequence,
            cancellationToken);

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
        JournalRotationBase? rotationBase = null;
        var sawNonBaseFrame = false;

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
            if (payloadLength < 0
                || payloadLength != content.Length - FrameMetadataLength)
            {
                throw new JournalCorruptionException("A checksummed journal frame has invalid metadata.");
            }

            var payload = content.Slice(FrameMetadataLength, payloadLength).ToArray();
            switch (frameType)
            {
                case JournalFrameType.RotationBase:
                    if (rotationBase is not null
                        || sawNonBaseFrame
                        || sequence < 0
                        || payloadLength != RotationBasePayloadLength)
                    {
                        throw new JournalCorruptionException(
                            "A rotation-base frame must be the journal's first and only base.");
                    }

                    rotationBase = ParseRotationBase(payload, sequence);
                    highestAppendSequence = sequence;
                    lastCheckpoint = sequence;
                    break;

                case JournalFrameType.AppendOperation:
                    sawNonBaseFrame = true;
                    if (sequence <= 0
                        || payloadLength == 0
                        || sequence != highestAppendSequence + 1)
                    {
                        throw new JournalCorruptionException(
                            "Append-frame sequences must be contiguous and carry payloads.");
                    }

                    highestAppendSequence = sequence;
                    appendFrames.Add(new JournalAppendFrame(sequence, payload));
                    break;

                case JournalFrameType.Checkpoint:
                    sawNonBaseFrame = true;
                    if (sequence <= 0
                        || payloadLength != 0
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
        _rotationBase = rotationBase;
        _allAppendFrames = appendFrames.ToArray();
        _recoveryTail = appendFrames.Where(frame => frame.Sequence > lastCheckpoint).ToArray();
        _stream.Seek(0, SeekOrigin.End);
    }

    private static async Task WriteRotatedJournalAsync(
        string path,
        JournalRotationBase rotationBase,
        IReadOnlyList<JournalAppendFrame> retainedFrames,
        long confirmedThrough,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(Header, cancellationToken).ConfigureAwait(false);
        var basePayload = BuildRotationBasePayload(rotationBase);
        await stream.WriteAsync(
                BuildRawFrame(
                    JournalFrameType.RotationBase,
                    rotationBase.CutSequence,
                    basePayload),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var frame in retainedFrames)
        {
            await stream.WriteAsync(
                    BuildRawFrame(
                        JournalFrameType.AppendOperation,
                        frame.Sequence,
                        frame.CanonicalOperationPayload),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (confirmedThrough > rotationBase.CutSequence)
        {
            await stream.WriteAsync(
                    BuildRawFrame(
                        JournalFrameType.Checkpoint,
                        confirmedThrough,
                        ReadOnlySpan<byte>.Empty),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static byte[] BuildRotationBasePayload(JournalRotationBase rotationBase)
    {
        var payload = new byte[RotationBasePayloadLength];
        BinaryPrimitives.WriteInt32LittleEndian(payload, rotationBase.FormatVersion);
        if (!rotationBase.BackupId.TryWriteBytes(payload.AsSpan(sizeof(int), 16)))
        {
            throw new MemoryIntegrityException("The backup ID could not be encoded for journal rotation.");
        }

        BinaryPrimitives.WriteInt64LittleEndian(
            payload.AsSpan(sizeof(int) + 16, sizeof(long)),
            rotationBase.CutSequence);
        return payload;
    }

    private static JournalRotationBase ParseRotationBase(byte[] payload, long frameSequence)
    {
        var formatVersion = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var backupId = new Guid(payload.AsSpan(sizeof(int), 16));
        var cutSequence = BinaryPrimitives.ReadInt64LittleEndian(
            payload.AsSpan(sizeof(int) + 16, sizeof(long)));
        if (formatVersion != RotationBaseFormatVersion
            || backupId == Guid.Empty
            || cutSequence != frameSequence)
        {
            throw new JournalCorruptionException("The journal rotation-base payload is invalid.");
        }

        return new JournalRotationBase(formatVersion, backupId, cutSequence);
    }

    private static FileStream OpenStream(string path, FileMode mode) =>
        new(
            path,
            mode,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous);

    private async Task WriteFrameDurablyAsync(
        ReadOnlyMemory<byte> rawFrame,
        CancellationToken cancellationToken)
    {
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
        RotationBase = 3,
    }
}

internal sealed record JournalAppendFrame(long Sequence, byte[] CanonicalOperationPayload);

internal sealed record JournalRotationBase(int FormatVersion, Guid BackupId, long CutSequence);
