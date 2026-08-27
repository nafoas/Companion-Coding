using System.Security.Cryptography;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed class VisualObservationPipeline : IDisposable
{
    private const long CompositionReserveBytes = 16L * 1024 * 1024;

    private readonly object _gate = new();
    private readonly FrameChangeDetector _changeDetector = new();
    private readonly StaggeredRegionScheduler _scheduler = new();
    private NormalizedRegion? _manualRegion;
    private bool _orientationPending = true;
    private bool _forceNextSheet;
    private long _stateVersion = 1;
    private long _changedFrames;
    private long _duplicateFrames;
    private long _producedSheets;
    private long _orientationSheets;
    private long _droppedSheets;
    private int _currentSheets;
    private int _maximumSheets;
    private long _currentSheetBytes;
    private long _maximumSheetBytes;
    private long _currentWorkingBytes;
    private long _maximumWorkingBytes;
    private double _lastChangeScore;
    private bool _disposed;

    internal async ValueTask<OwnedWorkerAttentionSheet?> ProcessAsync(
        CaptureSourceFrame frame,
        CaptureIpcAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(authorization);
        if (!frame.HasPixels)
        {
            return null;
        }

        using var pixels = await frame.CopyPixelsAsync(cancellationToken).ConfigureAwait(false);
        if (pixels is null || pixels.Width != frame.Width || pixels.Height != frame.Height)
        {
            throw new InvalidOperationException("Source pixel geometry does not match frame metadata.");
        }

        if (pixels.ByteLength > CaptureWorkerMetrics.VisualWorkingBudgetBytes - CompositionReserveBytes)
        {
            lock (_gate)
            {
                _droppedSheets++;
            }

            return null;
        }

        SetWorkingBytes(pixels.ByteLength);
        long stateVersion;
        bool orientation;
        IReadOnlyList<VisualRegion> regions;
        FrameChangeResult change;
        lock (_gate)
        {
            if (_currentSheets >= AttentionSheet.MaximumRetainedSheets)
            {
                _droppedSheets++;
                SetWorkingBytesUnsafe(0);
                return null;
            }

            change = _changeDetector.Evaluate(pixels);
            _lastChangeScore = change.Score;
            if (change.GeometryChanged)
            {
                _orientationPending = true;
                _scheduler.Reset();
            }

            orientation = _orientationPending;
            if (change.IsDuplicate && !orientation && !_forceNextSheet)
            {
                _duplicateFrames++;
                SetWorkingBytesUnsafe(0);
                return null;
            }

            _changedFrames++;
            stateVersion = _stateVersion;
            regions = orientation ? [] : _scheduler.TakeNext(_manualRegion);
            _orientationPending = false;
            _forceNextSheet = false;
        }

        ComposedAttentionSheet? composed = null;
        try
        {
            composed = AttentionSheetComposer.Compose(
                pixels,
                orientation ? AttentionSheetKind.Orientation : AttentionSheetKind.Regional,
                regions);
            var conservativePeak = checked(
                (long)pixels.ByteLength + composed.Canvas.ByteLength * 4L);
            if (conservativePeak > CaptureWorkerMetrics.VisualWorkingBudgetBytes)
            {
                throw new InvalidOperationException("Visual composition would exceed its hard working bound.");
            }

            SetWorkingBytes(conservativePeak);
            var encoded = PngEncoder.Encode(composed.Canvas);
            var target = new CaptureTargetIdentity(
                authorization.WindowId,
                authorization.ProcessId,
                authorization.ExecutableFileName,
                authorization.ExecutablePathFingerprint);
            var metadata = new AttentionSheetMetadata
            {
                TargetSessionId = authorization.TargetSessionId,
                Generation = authorization.Generation,
                Target = target,
                SourceSequenceNumber = frame.SequenceNumber,
                SourceTimestamp = frame.Timestamp,
                SourceWidth = frame.Width,
                SourceHeight = frame.Height,
                SheetWidth = composed.Canvas.Width,
                SheetHeight = composed.Canvas.Height,
                EncodedByteLength = encoded.Length,
                Kind = orientation ? AttentionSheetKind.Orientation : AttentionSheetKind.Regional,
                ChangeScore = change.Score,
                Regions = composed.Regions,
            };
            if (!metadata.IsProtocolSafe())
            {
                CryptographicOperations.ZeroMemory(encoded);
                throw new InvalidOperationException("Composed attention metadata failed its strict contract.");
            }

            lock (_gate)
            {
                if (_disposed || stateVersion != _stateVersion)
                {
                    _droppedSheets++;
                    CryptographicOperations.ZeroMemory(encoded);
                    return null;
                }

                _producedSheets++;
                if (orientation)
                {
                    _orientationSheets++;
                }

                _currentSheets++;
                _currentSheetBytes += encoded.Length;
                _maximumSheets = Math.Max(_maximumSheets, _currentSheets);
                _maximumSheetBytes = Math.Max(_maximumSheetBytes, _currentSheetBytes);
            }

            return new OwnedWorkerAttentionSheet(metadata, encoded, ReleaseSheet);
        }
        catch
        {
            lock (_gate)
            {
                if (!_disposed && stateVersion == _stateVersion)
                {
                    _orientationPending |= orientation;
                    _forceNextSheet |= !orientation;
                    _droppedSheets++;
                }
            }

            throw;
        }
        finally
        {
            composed?.Dispose();
            SetWorkingBytes(0);
        }
    }

    internal void SetManualRegion(NormalizedRegion? region)
    {
        region?.Validate(nameof(region));
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _manualRegion = region;
            _forceNextSheet = true;
            _scheduler.Reset();
            _stateVersion = checked(_stateVersion + 1);
        }
    }

    internal void Reset(bool clearManualRegion = true)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _changeDetector.Reset();
            _scheduler.Reset();
            if (clearManualRegion)
            {
                _manualRegion = null;
            }
            _orientationPending = true;
            _forceNextSheet = false;
            _lastChangeScore = 0;
            _stateVersion = checked(_stateVersion + 1);
        }
    }

    internal void RecordTransportDrop()
    {
        lock (_gate)
        {
            _droppedSheets++;
        }
    }

    internal VisualObservationMetrics Snapshot()
    {
        lock (_gate)
        {
            return new VisualObservationMetrics(
                _changedFrames,
                _duplicateFrames,
                _producedSheets,
                _orientationSheets,
                _droppedSheets,
                _currentSheets,
                _maximumSheets,
                _currentSheetBytes,
                _maximumSheetBytes,
                _currentWorkingBytes,
                _maximumWorkingBytes,
                _lastChangeScore);
        }
    }

    private void ReleaseSheet(int bytes)
    {
        lock (_gate)
        {
            _currentSheets--;
            _currentSheetBytes -= bytes;
            if (_currentSheets < 0 || _currentSheetBytes < 0)
            {
                throw new InvalidOperationException("Attention-sheet ownership accounting underflowed.");
            }
        }
    }

    private void SetWorkingBytes(long bytes)
    {
        lock (_gate)
        {
            SetWorkingBytesUnsafe(bytes);
        }
    }

    private void SetWorkingBytesUnsafe(long bytes)
    {
        if (bytes is < 0 or > CaptureWorkerMetrics.VisualWorkingBudgetBytes)
        {
            throw new InvalidOperationException("Visual working bytes exceeded their hard bound.");
        }

        _currentWorkingBytes = bytes;
        _maximumWorkingBytes = Math.Max(_maximumWorkingBytes, bytes);
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
            _changeDetector.Dispose();
            _scheduler.Reset();
            _manualRegion = null;
            _orientationPending = false;
            _forceNextSheet = false;
            _currentWorkingBytes = 0;
            _stateVersion = checked(_stateVersion + 1);
        }
    }
}

internal sealed class OwnedWorkerAttentionSheet : IDisposable
{
    private byte[]? _encodedImage;
    private Action<int>? _release;

    internal OwnedWorkerAttentionSheet(
        AttentionSheetMetadata metadata,
        byte[] encodedImage,
        Action<int> release)
    {
        Metadata = metadata;
        _encodedImage = encodedImage;
        _release = release;
        PayloadSha256 = Convert.ToHexString(SHA256.HashData(encodedImage));
    }

    internal AttentionSheetMetadata Metadata { get; }

    internal string PayloadSha256 { get; }

    internal ReadOnlyMemory<byte> EncodedImage =>
        Volatile.Read(ref _encodedImage) is { } image
            ? image
            : throw new ObjectDisposedException(nameof(OwnedWorkerAttentionSheet));

    public void Dispose()
    {
        var image = Interlocked.Exchange(ref _encodedImage, null);
        var release = Interlocked.Exchange(ref _release, null);
        if (image is null)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(image);
        release?.Invoke(image.Length);
    }
}

internal readonly record struct VisualObservationMetrics(
    long ChangedFrames,
    long DuplicateFrames,
    long ProducedSheets,
    long OrientationSheets,
    long DroppedSheets,
    int CurrentSheets,
    int MaximumSheets,
    long CurrentSheetBytes,
    long MaximumSheetBytes,
    long CurrentWorkingBytes,
    long MaximumWorkingBytes,
    double LastChangeScore);
