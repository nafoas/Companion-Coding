using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class CaptureSignalTrackerTests
{
    [Fact]
    public void VirtualTime_NoSignalStartsAtExactThresholdAndIsNotRepeated()
    {
        var tracker = new CaptureSignalTracker(TimeSpan.FromSeconds(2));
        var start = CaptureWorkerTestSupport.FixedTime;
        tracker.Reset(start);

        Assert.Null(tracker.Evaluate(start.AddMilliseconds(1999), isMinimized: false));
        var transition = tracker.Evaluate(start.AddSeconds(2), isMinimized: false);
        Assert.NotNull(transition);
        Assert.Equal(CaptureWorkerStatus.NoSignal, transition.Status);
        Assert.Equal(CaptureWorkerStatusReason.FrameArrivalStalled, transition.Reason);
        Assert.True(transition.ClearRetainedFrames);
        Assert.True(transition.IsStall);
        Assert.Null(tracker.Evaluate(start.AddSeconds(20), isMinimized: false));
    }

    [Fact]
    public void FrameArrival_RecoversRunningAndStartsFreshVirtualTimeWindow()
    {
        var tracker = new CaptureSignalTracker(TimeSpan.FromSeconds(2));
        var start = CaptureWorkerTestSupport.FixedTime;
        tracker.Reset(start);
        _ = tracker.Evaluate(start.AddSeconds(2), isMinimized: false);

        tracker.MarkFrame(start.AddSeconds(3));

        Assert.Equal(CaptureWorkerStatus.Running, tracker.Status);
        Assert.Null(tracker.Evaluate(start.AddMilliseconds(4999), isMinimized: false));
        Assert.Equal(
            CaptureWorkerStatus.NoSignal,
            tracker.Evaluate(start.AddSeconds(5), isMinimized: false)?.Status);
    }

    [Fact]
    public void Minimized_IsDistinctFromNoArrivalAndRestoreWaitsForFreshFrame()
    {
        var tracker = new CaptureSignalTracker(TimeSpan.FromSeconds(2));
        var start = CaptureWorkerTestSupport.FixedTime;
        tracker.Reset(start);

        var minimized = tracker.Evaluate(start.AddMilliseconds(10), isMinimized: true);
        Assert.NotNull(minimized);
        Assert.Equal(CaptureWorkerStatus.PausedMinimized, minimized.Status);
        Assert.Equal(CaptureWorkerStatusReason.Minimized, minimized.Reason);
        Assert.False(minimized.IsStall);
        Assert.Null(tracker.Evaluate(start.AddMilliseconds(20), isMinimized: true));
        Assert.Null(tracker.Evaluate(start.AddSeconds(1), isMinimized: false));
        Assert.Equal(
            CaptureWorkerStatus.NoSignal,
            tracker.Evaluate(start.AddSeconds(2), isMinimized: false)?.Status);

        tracker.MarkFrame(start.AddSeconds(3));
        Assert.Equal(CaptureWorkerStatus.Running, tracker.Status);
    }
}
