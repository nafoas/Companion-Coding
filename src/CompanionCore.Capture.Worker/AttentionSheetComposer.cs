using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal static class AttentionSheetComposer
{
    private const int Padding = 8;
    private const int Gap = 8;
    private const int HeaderHeight = 24;

    internal static ComposedAttentionSheet Compose(
        OwnedBgra32Buffer source,
        AttentionSheetKind kind,
        IReadOnlyList<VisualRegion> focusRegions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(focusRegions);
        if (kind == AttentionSheetKind.Orientation && focusRegions.Count != 0
            || kind == AttentionSheetKind.Regional && focusRegions.Count is < 1 or > 2)
        {
            throw new ArgumentException("The requested attention-sheet layout is invalid.");
        }

        var fullSource = new PixelRect(0, 0, source.Width, source.Height);
        var fullSize = kind == AttentionSheetKind.Orientation
            ? Fit(source.Width, source.Height, 1280, 720)
            : Fit(source.Width, source.Height, 768, 432);
        var focusSizes = focusRegions
            .Select(region =>
            {
                var pixels = VisualRegionLayout.MapToPixels(region.Bounds, source.Width, source.Height);
                return (Region: region, Source: pixels, Size: Fit(pixels.Width, pixels.Height, 384, 216));
            })
            .ToArray();

        var focusRowWidth = focusSizes.Length == 0
            ? 0
            : focusSizes.Sum(item => item.Size.Width) + (Gap * (focusSizes.Length - 1));
        var focusRowHeight = focusSizes.Length == 0
            ? 0
            : focusSizes.Max(item => item.Size.Height + HeaderHeight);
        var contentWidth = Math.Max(fullSize.Width, focusRowWidth);
        var width = checked(contentWidth + Padding * 2);
        var height = checked(
            Padding
            + HeaderHeight
            + fullSize.Height
            + (focusSizes.Length == 0 ? 0 : Gap + focusRowHeight)
            + Padding);
        var canvas = OwnedBgra32Buffer.Allocate(width, height);
        canvas.Span.Fill(0x18);
        SetOpaqueAlpha(canvas);

        var metadata = new List<AttentionSheetRegionMetadata>(1 + focusSizes.Length);
        var fullX = Padding + ((contentWidth - fullSize.Width) / 2);
        DrawLabel(canvas, fullX, Padding, fullSize.Width, AttentionRegionKind.FullContext);
        var fullDestination = new PixelRect(
            fullX,
            Padding + HeaderHeight,
            fullSize.Width,
            fullSize.Height);
        ScaleRegion(source, fullSource, canvas, fullDestination);
        metadata.Add(new AttentionSheetRegionMetadata
        {
            Kind = AttentionRegionKind.FullContext,
            NormalizedSource = VisualRegionLayout.FullContext,
            SourcePixels = fullSource,
            SheetPixels = fullDestination,
        });

        if (focusSizes.Length > 0)
        {
            var x = Padding + ((contentWidth - focusRowWidth) / 2);
            var y = fullDestination.Y + fullDestination.Height + Gap;
            foreach (var item in focusSizes)
            {
                DrawLabel(canvas, x, y, item.Size.Width, item.Region.Kind);
                var destination = new PixelRect(
                    x,
                    y + HeaderHeight,
                    item.Size.Width,
                    item.Size.Height);
                ScaleRegion(source, item.Source, canvas, destination);
                metadata.Add(new AttentionSheetRegionMetadata
                {
                    Kind = item.Region.Kind,
                    NormalizedSource = item.Region.Bounds,
                    SourcePixels = item.Source,
                    SheetPixels = destination,
                });
                x += item.Size.Width + Gap;
            }
        }

        return new ComposedAttentionSheet(canvas, metadata.ToArray());
    }

    private static (int Width, int Height) Fit(
        int width,
        int height,
        int maximumWidth,
        int maximumHeight)
    {
        var scale = Math.Min(
            1d,
            Math.Min(maximumWidth / (double)width, maximumHeight / (double)height));
        return (
            Math.Max(1, (int)Math.Round(width * scale, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(height * scale, MidpointRounding.AwayFromZero)));
    }

    private static void ScaleRegion(
        OwnedBgra32Buffer source,
        PixelRect sourceRect,
        OwnedBgra32Buffer destination,
        PixelRect destinationRect)
    {
        var sourcePixels = source.ReadOnlySpan;
        var destinationPixels = destination.Span;
        for (var y = 0; y < destinationRect.Height; y++)
        {
            var sourceY = sourceRect.Y
                + Math.Min(sourceRect.Height - 1, (int)(((y + 0.5) * sourceRect.Height) / destinationRect.Height));
            for (var x = 0; x < destinationRect.Width; x++)
            {
                var sourceX = sourceRect.X
                    + Math.Min(sourceRect.Width - 1, (int)(((x + 0.5) * sourceRect.Width) / destinationRect.Width));
                var sourceOffset = checked(sourceY * source.Stride + sourceX * 4);
                var destinationOffset = checked(
                    (destinationRect.Y + y) * destination.Stride
                    + (destinationRect.X + x) * 4);
                sourcePixels.Slice(sourceOffset, 4).CopyTo(destinationPixels.Slice(destinationOffset, 4));
            }
        }
    }

    private static void DrawLabel(
        OwnedBgra32Buffer canvas,
        int x,
        int y,
        int width,
        AttentionRegionKind kind)
    {
        FillRect(canvas, new PixelRect(x, y, width, HeaderHeight), 0x20, 0x20, 0x20, 0xff);
        BitmapLabelFont.Draw(
            canvas,
            VisualRegionLayout.GetVisibleLabel(kind),
            x + 5,
            y + 5,
            maximumWidth: Math.Max(0, width - 10));
    }

    private static void FillRect(
        OwnedBgra32Buffer canvas,
        PixelRect rect,
        byte blue,
        byte green,
        byte red,
        byte alpha)
    {
        var pixels = canvas.Span;
        for (var y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            for (var x = rect.X; x < rect.X + rect.Width; x++)
            {
                var offset = checked(y * canvas.Stride + x * 4);
                pixels[offset] = blue;
                pixels[offset + 1] = green;
                pixels[offset + 2] = red;
                pixels[offset + 3] = alpha;
            }
        }
    }

    private static void SetOpaqueAlpha(OwnedBgra32Buffer canvas)
    {
        var pixels = canvas.Span;
        for (var offset = 3; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = 255;
        }
    }
}

internal sealed class ComposedAttentionSheet : IDisposable
{
    internal ComposedAttentionSheet(
        OwnedBgra32Buffer canvas,
        AttentionSheetRegionMetadata[] regions)
    {
        Canvas = canvas;
        Regions = regions;
    }

    internal OwnedBgra32Buffer Canvas { get; }

    internal AttentionSheetRegionMetadata[] Regions { get; }

    public void Dispose() => Canvas.Dispose();
}

internal static class BitmapLabelFont
{
    private static readonly IReadOnlyDictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['A'] = ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['B'] = ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
        ['C'] = ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
        ['D'] = ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
        ['E'] = ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
        ['F'] = ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
        ['G'] = ["01111", "10000", "10000", "10111", "10001", "10001", "01111"],
        ['H'] = ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['I'] = ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
        ['J'] = ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
        ['K'] = ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
        ['L'] = ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
        ['M'] = ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
        ['N'] = ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
        ['O'] = ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
        ['P'] = ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
        ['Q'] = ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
        ['R'] = ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
        ['S'] = ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
        ['T'] = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
        ['U'] = ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
        ['V'] = ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
        ['W'] = ["10001", "10001", "10001", "10101", "10101", "11011", "10001"],
        ['X'] = ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
        ['Y'] = ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
        ['Z'] = ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
        ['/'] = ["00001", "00010", "00010", "00100", "01000", "01000", "10000"],
    };

    internal static void Draw(
        OwnedBgra32Buffer canvas,
        string text,
        int x,
        int y,
        int maximumWidth)
    {
        const int glyphWidth = 5;
        const int spacing = 1;
        var cursor = x;
        foreach (var character in text)
        {
            if (cursor + glyphWidth > x + maximumWidth)
            {
                return;
            }

            if (character != ' ' && Glyphs.TryGetValue(character, out var glyph))
            {
                for (var row = 0; row < glyph.Length; row++)
                {
                    for (var column = 0; column < glyphWidth; column++)
                    {
                        if (glyph[row][column] != '1')
                        {
                            continue;
                        }

                        var offset = checked((y + row) * canvas.Stride + (cursor + column) * 4);
                        canvas.Span[offset] = 0xff;
                        canvas.Span[offset + 1] = 0xff;
                        canvas.Span[offset + 2] = 0xff;
                        canvas.Span[offset + 3] = 0xff;
                    }
                }
            }

            cursor += glyphWidth + spacing;
        }
    }
}
