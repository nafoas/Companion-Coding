using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

/// <summary>What a recognizer read: counts and a digest of the normalized words, never the words.</summary>
internal sealed record TextReading(int Lines, int Characters, byte[] Digest);

/// <summary>Local text recognition over a BGRA crop (VIS-03).</summary>
internal interface ITextRecognizer : IDisposable
{
    bool IsAvailable { get; }

    /// <summary>The largest crop edge the engine accepts.</summary>
    int MaximumImageDimension { get; }

    ValueTask<TextReading?> ReadAsync(OwnedBgra32Buffer crop, CancellationToken cancellationToken);

    /// <summary>
    /// Counts the lines and characters of recognized text and digests its normalized form
    /// (lowercase, single spaces). The words themselves go no further than this call.
    /// </summary>
    internal static TextReading Summarize(IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();
        var count = 0;
        foreach (var line in lines)
        {
            var normalized = string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
            if (normalized.Length == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(normalized);
            count++;
        }

        var characters = builder.Length;
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        builder.Clear();
        try
        {
            return new TextReading(
                Math.Min(count, TextSniffSignal.MaximumLines),
                Math.Min(characters, TextSniffSignal.MaximumCharacters),
                SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}

/// <summary>No text recognition (not Windows, or no OCR language installed).</summary>
internal sealed class UnavailableTextRecognizer : ITextRecognizer
{
    internal static UnavailableTextRecognizer Instance { get; } = new();

    public bool IsAvailable => false;

    public int MaximumImageDimension => 0;

    public ValueTask<TextReading?> ReadAsync(OwnedBgra32Buffer crop, CancellationToken cancellationToken) =>
        ValueTask.FromResult<TextReading?>(null);

    public void Dispose()
    {
    }
}

/// <summary>Windows' built-in OCR (<c>Windows.Media.Ocr</c>): part of Windows, nothing downloaded.</summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal sealed class WindowsOcrTextRecognizer : ITextRecognizer
{
    private readonly Windows.Media.Ocr.OcrEngine _engine;

    private WindowsOcrTextRecognizer(Windows.Media.Ocr.OcrEngine engine) => _engine = engine;

    public bool IsAvailable => true;

    public int MaximumImageDimension => (int)Windows.Media.Ocr.OcrEngine.MaxImageDimension;

    /// <summary>The engine for the user's languages, or the unavailable recognizer when there is none.</summary>
    internal static ITextRecognizer Create()
    {
        try
        {
            return Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages() is { } engine
                ? new WindowsOcrTextRecognizer(engine)
                : UnavailableTextRecognizer.Instance;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return UnavailableTextRecognizer.Instance;
        }
    }

    public async ValueTask<TextReading?> ReadAsync(OwnedBgra32Buffer crop, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var bitmap = new Windows.Graphics.Imaging.SoftwareBitmap(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
            crop.Width,
            crop.Height,
            Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
        CopyInto(bitmap, crop);
        var result = await _engine.RecognizeAsync(bitmap).AsTask(cancellationToken).ConfigureAwait(false);
        return ITextRecognizer.Summarize(result.Lines.Select(line => line.Text).ToArray());
    }

    private static unsafe void CopyInto(Windows.Graphics.Imaging.SoftwareBitmap bitmap, OwnedBgra32Buffer crop)
    {
        using var buffer = bitmap.LockBuffer(Windows.Graphics.Imaging.BitmapBufferAccessMode.Write);
        using var reference = buffer.CreateReference();
        var access = WinRT.CastExtensions.As<IMemoryBufferByteAccess>(reference);
        access.GetBuffer(out var data, out var capacity);
        var plane = buffer.GetPlaneDescription(0);
        var source = crop.ReadOnlySpan;
        for (var y = 0; y < crop.Height; y++)
        {
            var row = source.Slice(y * crop.Stride, crop.Width * 4);
            var target = new Span<byte>(data + plane.StartIndex + (y * plane.Stride), crop.Width * 4);
            if (plane.StartIndex + (y * plane.Stride) + (crop.Width * 4) > capacity)
            {
                throw new InvalidOperationException("The text recognizer buffer is smaller than its crop.");
            }

            row.CopyTo(target);
        }
    }

    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// The Text Sniff (VIS-03). It reads text-likely regions (the dialogue band and the upper
/// corners) with the local recognizer and reports whether the words changed since that
/// region was last read. Only counts and a RAM-only digest are kept; the words never leave.
/// <list type="bullet">
/// <item>A region is read when a grid cell over it moved at least <see cref="TextCellChange"/>
/// (small enough for a subtitle swap the pixel Sniff calls a duplicate), or when it has not
/// been read for <see cref="RefreshInterval"/>.</item>
/// <item>Each region is read at most once per <see cref="RegionSpacing"/>, one read at a time,
/// off the frame path: the frame only lends a crop, so the Sniff and sheets never wait on a
/// read (<see cref="TryStart"/>).</item>
/// <item>Only a change is reported: words appeared, changed, or vanished.</item>
/// </list>
/// Time comes from the frames' own timestamps.
/// </summary>
internal sealed class TextSniffer : IDisposable
{
    internal static readonly TimeSpan RegionSpacing = TimeSpan.FromMilliseconds(500);

    internal static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    internal const double TextCellChange = 0.02;

    internal static readonly IReadOnlyList<VisualRegion> TextLikelyRegions =
        VisualRegionLayout.DefaultFocusRegions
            .Where(region => region.Kind is AttentionRegionKind.LowerDialogueInventory
                or AttentionRegionKind.UpperLeft
                or AttentionRegionKind.UpperRight)
            .ToArray();

    private readonly object _gate = new();
    private readonly ITextRecognizer _recognizer;
    private readonly Dictionary<AttentionRegionKind, RegionState> _regions = [];
    private long _reads;
    private long _changes;
    private long _lastMicroseconds;
    private long _maximumMicroseconds;
    private CancellationTokenSource _lifetime = new();
    private Task _inFlight = Task.CompletedTask;
    private bool _disposed;

    internal TextSniffer(ITextRecognizer recognizer)
    {
        _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
    }

    internal bool IsAvailable => _recognizer.IsAvailable;

    internal (long Reads, long Changes, long LastMicroseconds, long MaximumMicroseconds) Snapshot()
    {
        lock (_gate)
        {
            return (_reads, _changes, _lastMicroseconds, _maximumMicroseconds);
        }
    }

    /// <summary>
    /// Starts reading the due region of this frame in the background, when no read is in
    /// flight. The crop is copied now (the frame's pixels are not kept); the read reports a
    /// change through <paramref name="onText"/>. Returns whether a read started.
    /// </summary>
    internal bool TryStart(
        OwnedBgra32Buffer pixels,
        ReadOnlySpan<double> cellChanges,
        TextSniffSource source,
        Action<TextSniffSignal> onText)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onText);
        CancellationToken cancellationToken;
        lock (_gate)
        {
            if (_disposed || !_inFlight.IsCompleted)
            {
                return false;
            }

            cancellationToken = _lifetime.Token;
        }

        if (TakeDue(source.Timestamp, cellChanges) is not { } region)
        {
            return false;
        }

        var crop = Crop(pixels, region.Bounds, _recognizer.MaximumImageDimension);
        lock (_gate)
        {
            if (_disposed)
            {
                crop.Dispose();
                return false;
            }

            _inFlight = Task.Run(
                async () =>
                {
                    using (crop)
                    {
                        if (await ReadCropAsync(crop, region, source, cancellationToken).ConfigureAwait(false) is { } signal
                            && !cancellationToken.IsCancellationRequested)
                        {
                            onText(signal);
                        }
                    }
                },
                CancellationToken.None);
        }

        return true;
    }

    /// <summary>Waits for the read in flight, if any (shutdown and tests).</summary>
    internal async Task DrainAsync()
    {
        Task inFlight;
        lock (_gate)
        {
            inFlight = _inFlight;
        }

        try
        {
            await inFlight.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A failed read reports nothing; the next frame may try again.
        }
    }

    /// <summary>
    /// Picks the one region to read for a frame at <paramref name="at"/> and reserves it,
    /// or returns null. A moved region (the most moved first) beats a stale one (the
    /// longest unread first); a region inside its spacing is never picked.
    /// </summary>
    internal VisualRegion? TakeDue(DateTimeOffset at, ReadOnlySpan<double> cellChanges)
    {
        if (!_recognizer.IsAvailable || cellChanges.Length != SniffSignal.GridColumns * SniffSignal.GridRows)
        {
            return null;
        }

        lock (_gate)
        {
            VisualRegion? moved = null;
            var movedChange = 0d;
            VisualRegion? stale = null;
            DateTimeOffset? staleSince = null;
            foreach (var region in TextLikelyRegions)
            {
                _regions.TryGetValue(region.Kind, out var state);
                var last = state?.ReadAt;
                if (last is { } previous && at >= previous && at - previous < RegionSpacing)
                {
                    continue;
                }

                var change = MaximumCellChange(region.Bounds, cellChanges);
                if (change >= TextCellChange && change > movedChange)
                {
                    moved = region;
                    movedChange = change;
                }

                if (last is null || at - last.Value >= RefreshInterval || at < last.Value)
                {
                    if (stale is null || (staleSince is { } since && (last is null || last < since)))
                    {
                        stale = region;
                        staleSince = last;
                    }
                }
            }

            if ((moved ?? stale) is not { } chosen)
            {
                return null;
            }

            _regions[chosen.Kind] = (_regions.GetValueOrDefault(chosen.Kind) ?? new RegionState()) with { ReadAt = at };
            return chosen;
        }
    }

    /// <summary>Reads <paramref name="region"/> of the frame; a signal only when its words changed.</summary>
    internal async ValueTask<TextSniffSignal?> ReadAsync(
        OwnedBgra32Buffer pixels,
        VisualRegion region,
        TextSniffSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        using var crop = Crop(pixels, region.Bounds, _recognizer.MaximumImageDimension);
        return await ReadCropAsync(crop, region, source, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TextSniffSignal?> ReadCropAsync(
        OwnedBgra32Buffer crop,
        VisualRegion region,
        TextSniffSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var started = Stopwatch.GetTimestamp();
        TextReading? reading;
        try
        {
            reading = await _recognizer.ReadAsync(crop, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }

        if (reading is null)
        {
            return null;
        }

        var micros = Math.Clamp((long)Stopwatch.GetElapsedTime(started).TotalMicroseconds, 0, SniffSignal.MaximumComputeMicroseconds);
        bool changed;
        lock (_gate)
        {
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                CryptographicOperations.ZeroMemory(reading.Digest);
                return null;
            }

            var state = _regions.GetValueOrDefault(region.Kind) ?? new RegionState();
            changed = state.Digest is { } previous
                ? !CryptographicOperations.FixedTimeEquals(previous, reading.Digest)
                : reading.Lines > 0;
            if (state.Digest is { } old)
            {
                CryptographicOperations.ZeroMemory(old);
            }

            _regions[region.Kind] = state with { Digest = reading.Digest };
            _reads++;
            _changes += changed ? 1 : 0;
            _lastMicroseconds = micros;
            _maximumMicroseconds = Math.Max(_maximumMicroseconds, micros);
        }

        if (!changed)
        {
            return null;
        }

        return new TextSniffSignal
        {
            TargetSessionId = source.Authorization.TargetSessionId,
            Generation = source.Authorization.Generation,
            Target = new CaptureTargetIdentity(
                source.Authorization.WindowId,
                source.Authorization.ProcessId,
                source.Authorization.ExecutableFileName,
                source.Authorization.ExecutablePathFingerprint),
            SourceSequenceNumber = source.SequenceNumber,
            SourceTimestamp = source.Timestamp,
            SourceWidth = source.Width,
            SourceHeight = source.Height,
            SourceAccountedBytes = source.AccountedBytes,
            RegionKind = region.Kind,
            Region = region.Bounds,
            Lines = reading.Lines,
            Characters = reading.Lines == 0 ? 0 : reading.Characters,
            TextChanged = true,
            ComputeMicroseconds = micros,
        };
    }

    private static double MaximumCellChange(NormalizedRegion bounds, ReadOnlySpan<double> cellChanges)
    {
        var maximum = 0d;
        for (var cell = 0; cell < cellChanges.Length; cell++)
        {
            var column = cell % SniffSignal.GridColumns;
            var row = cell / SniffSignal.GridColumns;
            var left = column / (double)SniffSignal.GridColumns;
            var top = row / (double)SniffSignal.GridRows;
            var overlaps = left < bounds.X + bounds.Width
                && left + (1d / SniffSignal.GridColumns) > bounds.X
                && top < bounds.Y + bounds.Height
                && top + (1d / SniffSignal.GridRows) > bounds.Y;
            if (overlaps && double.IsFinite(cellChanges[cell]))
            {
                maximum = Math.Max(maximum, cellChanges[cell]);
            }
        }

        return maximum;
    }

    /// <summary>Copies the region out of the frame, halving until it fits the engine.</summary>
    internal static OwnedBgra32Buffer Crop(OwnedBgra32Buffer pixels, NormalizedRegion bounds, int maximumDimension)
    {
        var rect = VisualRegionLayout.MapToPixels(bounds, pixels.Width, pixels.Height);
        var crop = OwnedBgra32Buffer.Allocate(rect.Width, rect.Height);
        var source = pixels.ReadOnlySpan;
        var target = crop.Span;
        for (var y = 0; y < rect.Height; y++)
        {
            source.Slice(((rect.Y + y) * pixels.Stride) + (rect.X * 4), rect.Width * 4)
                .CopyTo(target.Slice(y * crop.Stride, rect.Width * 4));
        }

        while (maximumDimension > 0 && Math.Max(crop.Width, crop.Height) > maximumDimension && crop.Width >= 2 && crop.Height >= 2)
        {
            var halved = VisualObservationPipeline.Halve(crop);
            crop.Dispose();
            crop = halved;
        }

        return crop;
    }

    /// <summary>Forgets every region (a new grant or target): the read in flight reports nothing.</summary>
    internal void Reset()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                // The old source is only cancelled: a read in flight may still hold its token.
                _lifetime.Cancel();
                _lifetime = new CancellationTokenSource();
            }

            ClearRegions();
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
            _lifetime.Cancel();
            ClearRegions();
        }

        DrainAsync().GetAwaiter().GetResult();
        _lifetime.Dispose();
        _recognizer.Dispose();
    }

    private void ClearRegions()
    {
        foreach (var state in _regions.Values)
        {
            if (state.Digest is { } digest)
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }

        _regions.Clear();
    }

    private sealed record RegionState
    {
        public DateTimeOffset ReadAt { get; init; }

        public byte[]? Digest { get; init; }
    }
}

/// <summary>The authorized frame a Text Sniff reads, as signal metadata.</summary>
internal sealed record TextSniffSource(
    CaptureIpcAuthorization Authorization,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    int Width,
    int Height,
    long AccountedBytes);
