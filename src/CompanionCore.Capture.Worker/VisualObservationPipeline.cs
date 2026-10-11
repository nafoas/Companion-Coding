using System.Security.Cryptography;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed class VisualObservationPipeline : IDisposable
{
    private const long CompositionReserveBytes = 16L * 1024 * 1024;
    private const long MaximumPixelCopyBytes =
        CaptureWorkerMetrics.VisualWorkingBudgetBytes / 2;

    private readonly object _gate = new();
    private readonly FrameChangeDetector _changeDetector = new();
    private readonly StaggeredRegionScheduler _scheduler = new();
    private NormalizedRegion? _manualRegion;
    private bool _orientationPending = true;
    private bool _photographPending;
    private bool _forceNextSheet;
    private long _stateVersion = 1;
    private long _changedFrames;
    private long _duplicateFrames;
    private long _producedSheets;
    private long _orientationSheets;
    private long _photographSheets;
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

        if (frame.AccountedBytes > MaximumPixelCopyBytes)
        {
            lock (_gate)
            {
                _droppedSheets++;
            }

            return null;
        }

        OwnedBgra32Buffer? pixels = null;
        ComposedAttentionSheet? composed = null;
        long stateVersion = 0;
        var orientation = false;
        var photograph = false;
        var stateCaptured = false;
        try
        {
            // WGC readback briefly owns a SoftwareBitmap plus the tightly packed copy.
            // Account both conservatively before the asynchronous copy begins.
            SetWorkingBytes(checked(frame.AccountedBytes * 2));
            pixels = await frame.CopyPixelsAsync(cancellationToken).ConfigureAwait(false);
            if (pixels is null
                || pixels.Width != frame.Width
                || pixels.Height != frame.Height
                || pixels.ByteLength > MaximumPixelCopyBytes)
            {
                throw new InvalidOperationException("Source pixel geometry exceeds its visual contract.");
            }

            SetWorkingBytes(pixels.ByteLength);
            IReadOnlyList<VisualRegion> regions = [];
            FrameChangeResult change = default;
            lock (_gate)
            {
                if (_currentSheets >= AttentionSheet.MaximumRetainedSheets)
                {
                    _droppedSheets++;
                    return null;
                }

                if (_photographPending)
                {
                    // A requested photograph takes this frame whole; it consumes no visual
                    // change, so orientation and regional state are left untouched.
                    _photographPending = false;
                    photograph = true;
                    stateVersion = _stateVersion;
                    stateCaptured = true;
                }
                else
                {
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
                        return null;
                    }

                    _changedFrames++;
                    stateVersion = _stateVersion;
                    stateCaptured = true;
                    regions = orientation ? [] : _scheduler.TakeNext(_manualRegion);
                    _orientationPending = false;
                    _forceNextSheet = false;
                }
            }

            if (photograph)
            {
                return ProducePhotograph(frame, authorization, pixels, stateVersion);
            }

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
                if (!_disposed && stateCaptured && stateVersion == _stateVersion)
                {
                    _orientationPending |= orientation;
                    _forceNextSheet |= !orientation && !photograph;
                    _photographPending |= photograph;
                }

                _droppedSheets++;
            }

            throw;
        }
        finally
        {
            composed?.Dispose();
            pixels?.Dispose();
            SetWorkingBytes(0);
        }
    }

    /// <summary>
    /// Encodes the whole source frame at native resolution. When the image is larger than
    /// the photograph edge or its encoding exceeds the sheet bound, it is halved (2×2
    /// average) and tried again; by 1280 pixels every image fits, so this always ends.
    /// </summary>
    private OwnedWorkerAttentionSheet? ProducePhotograph(
        CaptureSourceFrame frame,
        CaptureIpcAuthorization authorization,
        OwnedBgra32Buffer pixels,
        long stateVersion)
    {
        var canvas = pixels;
        byte[]? encoded = null;
        try
        {
            while (true)
            {
                var conservativePeak = checked(
                    (long)pixels.ByteLength
                    + (ReferenceEquals(canvas, pixels) ? 0 : canvas.ByteLength)
                    + (2L * AttentionSheet.MaximumEncodedBytes));
                if (conservativePeak > CaptureWorkerMetrics.VisualWorkingBudgetBytes)
                {
                    throw new InvalidOperationException("The photograph would exceed its hard working bound.");
                }

                SetWorkingBytes(conservativePeak);
                if (Math.Max(canvas.Width, canvas.Height) <= AttentionSheet.MaximumPhotographEdge
                    && PngEncoder.TryEncode(canvas) is { } fitted)
                {
                    encoded = fitted;
                    break;
                }

                if (canvas.Width < 2 || canvas.Height < 2)
                {
                    throw new InvalidOperationException("The photograph cannot fit its bounds.");
                }

                var halved = Halve(canvas);
                if (!ReferenceEquals(canvas, pixels))
                {
                    canvas.Dispose();
                }

                canvas = halved;
            }

            var metadata = new AttentionSheetMetadata
            {
                TargetSessionId = authorization.TargetSessionId,
                Generation = authorization.Generation,
                Target = new CaptureTargetIdentity(
                    authorization.WindowId,
                    authorization.ProcessId,
                    authorization.ExecutableFileName,
                    authorization.ExecutablePathFingerprint),
                SourceSequenceNumber = frame.SequenceNumber,
                SourceTimestamp = frame.Timestamp,
                SourceWidth = frame.Width,
                SourceHeight = frame.Height,
                SheetWidth = canvas.Width,
                SheetHeight = canvas.Height,
                EncodedByteLength = encoded.Length,
                Kind = AttentionSheetKind.Photograph,
                ChangeScore = 0,
                Regions =
                [
                    new AttentionSheetRegionMetadata
                    {
                        Kind = AttentionRegionKind.FullContext,
                        NormalizedSource = VisualRegionLayout.FullContext,
                        SourcePixels = new PixelRect(0, 0, frame.Width, frame.Height),
                        SheetPixels = new PixelRect(0, 0, canvas.Width, canvas.Height),
                    },
                ],
            };
            if (!metadata.IsProtocolSafe())
            {
                throw new InvalidOperationException("Photograph metadata failed its strict contract.");
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
                _photographSheets++;
                _currentSheets++;
                _currentSheetBytes += encoded.Length;
                _maximumSheets = Math.Max(_maximumSheets, _currentSheets);
                _maximumSheetBytes = Math.Max(_maximumSheetBytes, _currentSheetBytes);
            }

            var sheet = new OwnedWorkerAttentionSheet(metadata, encoded, ReleaseSheet);
            encoded = null;
            return sheet;
        }
        finally
        {
            if (encoded is not null)
            {
                CryptographicOperations.ZeroMemory(encoded);
            }

            if (!ReferenceEquals(canvas, pixels))
            {
                canvas.Dispose();
            }
        }
    }

    /// <summary>2×2 box average; an odd last row or column is dropped.</summary>
    internal static OwnedBgra32Buffer Halve(OwnedBgra32Buffer source)
    {
        var width = source.Width / 2;
        var height = source.Height / 2;
        var target = OwnedBgra32Buffer.Allocate(width, height);
        var input = source.ReadOnlySpan;
        var output = target.Span;
        for (var y = 0; y < height; y++)
        {
            var top = (2 * y) * source.Stride;
            var bottom = top + source.Stride;
            var row = y * target.Stride;
            for (var x = 0; x < width; x++)
            {
                var left = 8 * x;
                for (var channel = 0; channel < 4; channel++)
                {
                    var sum = input[top + left + channel] + input[top + left + 4 + channel]
                        + input[bottom + left + channel] + input[bottom + left + 4 + channel];
                    output[row + (4 * x) + channel] = (byte)((sum + 2) / 4);
                }
            }
        }

        return target;
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

    internal void RequestOrientation()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _orientationPending = true;
        }
    }

    /// <summary>
    /// Arms one photograph for the active grant: the next usable frame, even a duplicate,
    /// becomes a <see cref="AttentionSheetKind.Photograph"/> sheet. Any reset disarms it.
    /// </summary>
    internal void RequestPhotograph()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _photographPending = true;
        }
    }

    internal bool PhotographPending
    {
        get
        {
            lock (_gate)
            {
                return _photographPending;
            }
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
            _photographPending = false;
            _forceNextSheet = false;
            _lastChangeScore = 0;
            _stateVersion = checked(_stateVersion + 1);
        }
    }

    internal long PhotographSheets
    {
        get
        {
            lock (_gate)
            {
                return _photographSheets;
            }
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
            _photographPending = false;
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
        try
        {
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(encodedImage));
        }
        catch
        {
            _encodedImage = null;
            _release = null;
            CryptographicOperations.ZeroMemory(encodedImage);
            release(encodedImage.Length);
            throw;
        }
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
