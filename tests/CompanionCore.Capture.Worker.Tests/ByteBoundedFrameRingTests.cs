using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

/// <summary>The RAM-only frame ring never exceeds its byte or frame bound, evicting oldest first.</summary>
public sealed class ByteBoundedFrameRingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class Resource : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private static CaptureSourceFrame Frame(long bytes, int second = 0) =>
        new(T0.AddSeconds(second), 4, 4, bytes, new Resource());

    [Fact]
    public void AFrameLargerThanTheWholeBound_IsRefusedAndReturnedForDisposal()
    {
        var ring = new ByteBoundedFrameRing(maximumBytes: 100, maximumFrames: 4);
        var huge = Frame(101);

        var evicted = ring.Add(huge);

        Assert.Same(huge, Assert.Single(evicted));
        Assert.Equal(0, ring.Count);
        Assert.Equal(0, ring.Bytes);
    }

    [Fact]
    public void TheFrameBound_EvictsTheOldestFirst()
    {
        var ring = new ByteBoundedFrameRing(maximumBytes: 1000, maximumFrames: 2);
        var first = Frame(10, 0);
        var second = Frame(10, 1);
        var third = Frame(10, 2);

        Assert.Empty(ring.Add(first));
        Assert.Empty(ring.Add(second));
        var evicted = ring.Add(third);

        Assert.Same(first, Assert.Single(evicted));
        Assert.Equal(2, ring.Count);
        Assert.Equal(20, ring.Bytes);
        Assert.Same(second, ring.RemoveOldest());
    }

    [Fact]
    public void TheByteBound_EvictsUntilTheNewFrameFits()
    {
        var ring = new ByteBoundedFrameRing(maximumBytes: 100, maximumFrames: 10);
        var a = Frame(40, 0);
        var b = Frame(40, 1);
        var c = Frame(30, 2);
        ring.Add(a);
        ring.Add(b);

        var evicted = ring.Add(c);

        Assert.Same(a, Assert.Single(evicted));
        Assert.Equal(70, ring.Bytes);
        Assert.True(ring.Bytes <= ring.MaximumBytes);

        var exact = Frame(100, 3);
        Assert.Equal([b, c], ring.Add(exact));
        Assert.Equal(100, ring.Bytes);
        Assert.Equal(1, ring.Count);
    }

    [Fact]
    public void DrainAndLifetime_AreExact()
    {
        var ring = new ByteBoundedFrameRing(maximumBytes: 100, maximumFrames: 10);
        Assert.Equal(TimeSpan.Zero, ring.OldestLifetime(T0));
        Assert.Null(ring.RemoveOldest());
        ring.Add(Frame(10, 0));
        ring.Add(Frame(10, 5));

        Assert.Equal(TimeSpan.FromSeconds(7), ring.OldestLifetime(T0.AddSeconds(7)));
        Assert.Equal(TimeSpan.Zero, ring.OldestLifetime(T0.AddSeconds(-1)));
        Assert.Equal(2, ring.Drain().Count);
        Assert.Equal((0, 0L), (ring.Count, ring.Bytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ByteBoundedFrameRing(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ByteBoundedFrameRing(1, 0));
    }
}
