using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class AttentionSheetComposerTests
{
    [Fact]
    public void Orientation_IsLosslessInspectablyLabeledAndBounded()
    {
        using var source = Pattern(1600, 900, 17);
        using var composed = AttentionSheetComposer.Compose(
            source,
            AttentionSheetKind.Orientation,
            []);
        var encoded = PngEncoder.Encode(composed.Canvas);
        try
        {
            var decoded = PngTestDecoder.Decode(encoded);
            Assert.Equal(composed.Canvas.Width, decoded.Width);
            Assert.Equal(composed.Canvas.Height, decoded.Height);
            Assert.Single(composed.Regions);
            Assert.Equal(AttentionRegionKind.FullContext, composed.Regions[0].Kind);
            Assert.InRange(encoded.Length, 1, AttentionSheet.MaximumEncodedBytes);
            Assert.True(HeaderContainsWhiteLabel(decoded, composed.Regions[0].SheetPixels));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(encoded);
        }
    }

    [Fact]
    public void RegionalSheet_UsesOneSourceMomentAndCarriesVisibleAndStructuredLabels()
    {
        const int pattern = 23;
        using var source = Pattern(320, 180, pattern);
        var focus = new[]
        {
            new VisualRegion(AttentionRegionKind.ManualFocus, new NormalizedRegion(0.1, 0.2, 0.3, 0.4)),
            new VisualRegion(AttentionRegionKind.UpperRight, new NormalizedRegion(0.65, 0, 0.35, 0.32)),
        };
        using var composed = AttentionSheetComposer.Compose(
            source,
            AttentionSheetKind.Regional,
            focus);
        var encoded = PngEncoder.Encode(composed.Canvas);
        try
        {
            var decoded = PngTestDecoder.Decode(encoded);
            Assert.Equal(
                new[]
                {
                    AttentionRegionKind.FullContext,
                    AttentionRegionKind.ManualFocus,
                    AttentionRegionKind.UpperRight,
                },
                composed.Regions.Select(region => region.Kind));
            foreach (var region in composed.Regions)
            {
                Assert.True(HeaderContainsWhiteLabel(decoded, region.SheetPixels));
                AssertCenterMatchesSourceMoment(decoded, region, pattern);
            }
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(encoded);
        }
    }

    [Fact]
    public void SyntheticInspectionArtifact_IsProducedForThePawGate()
    {
        using var source = Pattern(640, 360, 31);
        using var composed = AttentionSheetComposer.Compose(
            source,
            AttentionSheetKind.Regional,
            [
                new VisualRegion(
                    AttentionRegionKind.CenterEnvironment,
                    VisualRegionLayout.DefaultFocusRegions[0].Bounds),
                new VisualRegion(
                    AttentionRegionKind.LowerDialogueInventory,
                    VisualRegionLayout.DefaultFocusRegions[1].Bounds),
            ]);
        var encoded = PngEncoder.Encode(composed.Canvas);
        try
        {
            _ = PngTestDecoder.Decode(encoded);
            var outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestResults");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllBytes(
                Path.Combine(outputDirectory, "task06-attention-sheet.png"),
                encoded);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(encoded);
        }
    }

    private static OwnedBgra32Buffer Pattern(int width, int height, int pattern)
    {
        var pixels = OwnedBgra32Buffer.Allocate(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * pixels.Stride) + (x * 4);
                pixels.Span[offset] = unchecked((byte)(pattern + x));
                pixels.Span[offset + 1] = unchecked((byte)(pattern * 3 + y));
                pixels.Span[offset + 2] = unchecked((byte)(pattern * 7 + x + y));
                pixels.Span[offset + 3] = 255;
            }
        }

        return pixels;
    }

    private static bool HeaderContainsWhiteLabel(DecodedPng decoded, PixelRect imageBounds)
    {
        var top = Math.Max(0, imageBounds.Y - 24);
        for (var y = top; y < imageBounds.Y; y++)
        {
            for (var x = imageBounds.X; x < imageBounds.X + imageBounds.Width; x++)
            {
                var pixel = decoded.Pixel(x, y);
                if (pixel[0] == 255 && pixel[1] == 255 && pixel[2] == 255 && pixel[3] == 255)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AssertCenterMatchesSourceMoment(
        DecodedPng decoded,
        AttentionSheetRegionMetadata region,
        int pattern)
    {
        var destinationX = region.SheetPixels.Width / 2;
        var destinationY = region.SheetPixels.Height / 2;
        var sourceX = region.SourcePixels.X
            + Math.Min(
                region.SourcePixels.Width - 1,
                (int)(((destinationX + 0.5) * region.SourcePixels.Width) / region.SheetPixels.Width));
        var sourceY = region.SourcePixels.Y
            + Math.Min(
                region.SourcePixels.Height - 1,
                (int)(((destinationY + 0.5) * region.SourcePixels.Height) / region.SheetPixels.Height));
        var actual = decoded.Pixel(
            region.SheetPixels.X + destinationX,
            region.SheetPixels.Y + destinationY);
        Assert.Equal(unchecked((byte)(pattern * 7 + sourceX + sourceY)), actual[0]);
        Assert.Equal(unchecked((byte)(pattern * 3 + sourceY)), actual[1]);
        Assert.Equal(unchecked((byte)(pattern + sourceX)), actual[2]);
        Assert.Equal(255, actual[3]);
    }
}
