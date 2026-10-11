using System.Security.Cryptography;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed class FrameChangeDetector : IDisposable
{
    internal const int MaximumSignatureWidth = 64;
    internal const int MaximumSignatureHeight = 36;
    internal const double NearDuplicateThreshold = 0.006;

    private byte[]? _baseline;
    private int _signatureWidth;
    private int _signatureHeight;
    private int _sourceWidth;
    private int _sourceHeight;

    internal FrameChangeResult Evaluate(OwnedBgra32Buffer pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var signatureWidth = Math.Min(MaximumSignatureWidth, pixels.Width);
        var signatureHeight = Math.Min(MaximumSignatureHeight, pixels.Height);
        var signature = GC.AllocateUninitializedArray<byte>(
            checked(signatureWidth * signatureHeight));
        BuildSignature(pixels, signature, signatureWidth, signatureHeight);

        if (_baseline is null
            || _sourceWidth != pixels.Width
            || _sourceHeight != pixels.Height
            || _signatureWidth != signatureWidth
            || _signatureHeight != signatureHeight)
        {
            ReplaceBaseline(signature, pixels.Width, pixels.Height, signatureWidth, signatureHeight);
            return new FrameChangeResult(1, IsDuplicate: false, GeometryChanged: true);
        }

        long difference = 0;
        Span<long> cellDifference = stackalloc long[SniffSignal.GridColumns * SniffSignal.GridRows];
        Span<int> cellSamples = stackalloc int[SniffSignal.GridColumns * SniffSignal.GridRows];
        cellDifference.Clear();
        cellSamples.Clear();
        for (var y = 0; y < signatureHeight; y++)
        {
            var row = Math.Min(SniffSignal.GridRows - 1, (y * SniffSignal.GridRows) / signatureHeight);
            for (var x = 0; x < signatureWidth; x++)
            {
                var index = (y * signatureWidth) + x;
                var delta = Math.Abs(signature[index] - _baseline[index]);
                difference += delta;
                var cell = (row * SniffSignal.GridColumns)
                    + Math.Min(SniffSignal.GridColumns - 1, (x * SniffSignal.GridColumns) / signatureWidth);
                cellDifference[cell] += delta;
                cellSamples[cell]++;
            }
        }

        var score = difference / (signature.Length * 255d);
        var grid = SniffGrid.From(score, cellDifference, cellSamples);

        // A frame is a duplicate only when the whole frame is still and no single cell
        // moved noticeably: a small dialogue icon matters even when the screen barely changed.
        if (score <= NearDuplicateThreshold && grid.HotChange < SniffGrid.CellChanged)
        {
            CryptographicOperations.ZeroMemory(signature);
            return new FrameChangeResult(score, IsDuplicate: true, GeometryChanged: false);
        }

        ReplaceBaseline(signature, pixels.Width, pixels.Height, signatureWidth, signatureHeight);
        return new FrameChangeResult(score, IsDuplicate: false, GeometryChanged: false)
        {
            Grid = grid,
        };
    }

    private static void BuildSignature(
        OwnedBgra32Buffer pixels,
        Span<byte> signature,
        int signatureWidth,
        int signatureHeight)
    {
        var source = pixels.ReadOnlySpan;
        for (var y = 0; y < signatureHeight; y++)
        {
            var sourceY = Math.Min(
                pixels.Height - 1,
                (int)(((y + 0.5) * pixels.Height) / signatureHeight));
            for (var x = 0; x < signatureWidth; x++)
            {
                var sourceX = Math.Min(
                    pixels.Width - 1,
                    (int)(((x + 0.5) * pixels.Width) / signatureWidth));
                var offset = checked((sourceY * pixels.Stride) + (sourceX * 4));
                var blue = source[offset];
                var green = source[offset + 1];
                var red = source[offset + 2];
                signature[(y * signatureWidth) + x] =
                    (byte)((red * 77 + green * 150 + blue * 29) >> 8);
            }
        }
    }

    private void ReplaceBaseline(
        byte[] signature,
        int sourceWidth,
        int sourceHeight,
        int signatureWidth,
        int signatureHeight)
    {
        var previous = Interlocked.Exchange(ref _baseline, signature);
        if (previous is not null)
        {
            CryptographicOperations.ZeroMemory(previous);
        }

        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _signatureWidth = signatureWidth;
        _signatureHeight = signatureHeight;
    }

    internal void Reset()
    {
        var baseline = Interlocked.Exchange(ref _baseline, null);
        if (baseline is not null)
        {
            CryptographicOperations.ZeroMemory(baseline);
        }

        _sourceWidth = 0;
        _sourceHeight = 0;
        _signatureWidth = 0;
        _signatureHeight = 0;
    }

    public void Dispose() => Reset();
}

internal readonly record struct FrameChangeResult(
    double Score,
    bool IsDuplicate,
    bool GeometryChanged)
{
    /// <summary>The per-cell analysis; present only for an ordinary changed frame.</summary>
    internal SniffGrid? Grid { get; init; }
}

/// <summary>
/// The Sniff's reading of one changed frame over the 8×6 cell grid (VIS-01): where the
/// change is, how spread out it is, and whether the whole scene cut.
/// </summary>
internal sealed record SniffGrid(
    double Change,
    double HotChange,
    double Motion,
    bool SceneCut,
    int HotCell,
    NormalizedRegion HotRegion)
{
    /// <summary>A cell whose mean change reaches this counts as moving.</summary>
    internal const double CellChanged = 0.06;

    internal static SniffGrid From(double score, ReadOnlySpan<long> cellDifference, ReadOnlySpan<int> cellSamples)
    {
        var hotCell = 0;
        var hotChange = 0d;
        var changedCells = 0;
        var populatedCells = 0;
        int minColumn = SniffSignal.GridColumns, minRow = SniffSignal.GridRows, maxColumn = -1, maxRow = -1;
        for (var cell = 0; cell < cellDifference.Length; cell++)
        {
            if (cellSamples[cell] == 0)
            {
                continue;
            }

            populatedCells++;
            var change = cellDifference[cell] / (cellSamples[cell] * 255d);
            if (change > hotChange)
            {
                hotChange = change;
                hotCell = cell;
            }

            if (change >= CellChanged)
            {
                changedCells++;
                var column = cell % SniffSignal.GridColumns;
                var row = cell / SniffSignal.GridColumns;
                minColumn = Math.Min(minColumn, column);
                maxColumn = Math.Max(maxColumn, column);
                minRow = Math.Min(minRow, row);
                maxRow = Math.Max(maxRow, row);
            }
        }

        if (changedCells == 0)
        {
            minColumn = maxColumn = hotCell % SniffSignal.GridColumns;
            minRow = maxRow = hotCell / SniffSignal.GridColumns;
        }

        var motion = populatedCells == 0 ? 0 : changedCells / (double)populatedCells;
        var region = new NormalizedRegion(
            minColumn / (double)SniffSignal.GridColumns,
            minRow / (double)SniffSignal.GridRows,
            (maxColumn - minColumn + 1) / (double)SniffSignal.GridColumns,
            (maxRow - minRow + 1) / (double)SniffSignal.GridRows);
        return new SniffGrid(
            Math.Clamp(score, 0, 1),
            Math.Clamp(Math.Max(hotChange, score), 0, 1),
            Math.Clamp(motion, 0, 1),
            score >= SniffSignal.SceneCutChange && motion >= SniffSignal.SceneCutMotion,
            hotCell,
            Clamp(region));
    }

    /// <summary>Guards floating-point edges so the region is always valid.</summary>
    private static NormalizedRegion Clamp(NormalizedRegion region)
    {
        var x = Math.Clamp(region.X, 0, 1);
        var y = Math.Clamp(region.Y, 0, 1);
        return new NormalizedRegion(x, y, Math.Clamp(region.Width, 1e-6, 1 - x), Math.Clamp(region.Height, 1e-6, 1 - y));
    }
}
