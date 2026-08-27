using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class FrameChangeDetectorTests
{
    [Fact]
    public void ExactAndNearDuplicatesAreRejectedButAccumulatedChangeEscapesBaseline()
    {
        using var detector = new FrameChangeDetector();
        using var first = Solid(64, 36, 40);
        using var exact = Solid(64, 36, 40);
        using var near = Solid(64, 36, 41);
        using var accumulated = Solid(64, 36, 42);

        var initial = detector.Evaluate(first);
        var duplicate = detector.Evaluate(exact);
        var nearDuplicate = detector.Evaluate(near);
        var changed = detector.Evaluate(accumulated);

        Assert.True(initial.GeometryChanged);
        Assert.False(initial.IsDuplicate);
        Assert.Equal(1, initial.Score);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(0, duplicate.Score);
        Assert.True(nearDuplicate.IsDuplicate);
        Assert.InRange(nearDuplicate.Score, 0, FrameChangeDetector.NearDuplicateThreshold);
        Assert.False(changed.IsDuplicate);
        Assert.True(changed.Score > FrameChangeDetector.NearDuplicateThreshold);
    }

    [Fact]
    public void GeometryChangeAndResetNeverCompareIncompatibleSignatures()
    {
        using var detector = new FrameChangeDetector();
        using var first = Solid(64, 36, 20);
        using var resized = Solid(80, 45, 20);
        using var afterReset = Solid(80, 45, 20);

        _ = detector.Evaluate(first);
        var geometry = detector.Evaluate(resized);
        detector.Reset();
        var reset = detector.Evaluate(afterReset);

        Assert.True(geometry.GeometryChanged);
        Assert.False(geometry.IsDuplicate);
        Assert.True(reset.GeometryChanged);
        Assert.False(reset.IsDuplicate);
    }

    private static OwnedBgra32Buffer Solid(int width, int height, byte value)
    {
        var pixels = OwnedBgra32Buffer.Allocate(width, height);
        for (var offset = 0; offset < pixels.ByteLength; offset += 4)
        {
            pixels.Span[offset] = value;
            pixels.Span[offset + 1] = value;
            pixels.Span[offset + 2] = value;
            pixels.Span[offset + 3] = 255;
        }

        return pixels;
    }
}
