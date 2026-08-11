using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class CaptureWorkerEngineTests
{
    [Fact]
    public async Task NoSignal_ClearsRetainedFramesAndPublishesOnlyNewWorkAfterRecovery()
    {
        await using var source = new ControllableCaptureSource();
        await using var engine = new CaptureWorkerEngine(source);
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var frames = new List<CaptureEngineFrame>();
        engine.FrameProduced += (_, frame) => frames.Add(frame);
        await engine.StartAsync(authorization, CancellationToken.None);
        var firstResource = new TrackingResource();
        source.Emit(CreateFrame(firstResource));
        await CaptureWorkerTestSupport.WaitUntilAsync(() => frames.Count == 1);

        source.Report(new CaptureSourceStatusChanged(
            CaptureWorkerStatus.NoSignal,
            CaptureWorkerStatusReason.FrameArrivalStalled,
            ClearRetainedFrames: true,
            IsStall: true));

        Assert.Equal(CaptureWorkerStatus.NoSignal, engine.Status);
        Assert.Equal(1, firstResource.DisposeCount);
        Assert.Equal(0, engine.GetMetrics().CurrentSourceFrames);
        var secondResource = new TrackingResource();
        source.Emit(CreateFrame(secondResource));
        await CaptureWorkerTestSupport.WaitUntilAsync(() => frames.Count == 2);

        Assert.Equal(CaptureWorkerStatus.Running, engine.Status);
        Assert.Equal([1L, 2L], frames.Select(frame => frame.SequenceNumber));
        Assert.All(frames, frame => Assert.Equal(authorization, frame.Authorization));
        await engine.StopAndClearAsync(CancellationToken.None);
        Assert.Equal(1, secondResource.DisposeCount);
    }

    [Fact]
    public async Task Resize_ClearsIncompatibleFramesAndContinuesOnSameAuthorization()
    {
        await using var source = new ControllableCaptureSource();
        await using var engine = new CaptureWorkerEngine(source);
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var produced = 0;
        engine.FrameProduced += (_, _) => Interlocked.Increment(ref produced);
        await engine.StartAsync(authorization, CancellationToken.None);
        var oldResource = new TrackingResource();
        source.Emit(CreateFrame(oldResource));
        await CaptureWorkerTestSupport.WaitUntilAsync(() => Volatile.Read(ref produced) == 1);

        source.Report(new CaptureSourceStatusChanged(
            CaptureWorkerStatus.Running,
            CaptureWorkerStatusReason.None,
            ClearRetainedFrames: true,
            IsResize: true));

        Assert.Equal(1, oldResource.DisposeCount);
        Assert.Equal(1, engine.GetMetrics().ResizeCount);
        Assert.Equal(0, engine.GetMetrics().CurrentSourceFrames);
        var resizedResource = new TrackingResource();
        source.Emit(CreateFrame(resizedResource, width: 48, height: 40));
        await CaptureWorkerTestSupport.WaitUntilAsync(() => Volatile.Read(ref produced) == 2);
        Assert.Equal(CaptureWorkerStatus.Running, engine.Status);

        await engine.StopAndClearAsync(CancellationToken.None);
        Assert.Equal(1, resizedResource.DisposeCount);
    }

    [Fact]
    public async Task TerminalFault_RevokesGenerationClearsOwnedFramesAndRejectsLateFrame()
    {
        await using var source = new ControllableCaptureSource();
        await using var engine = new CaptureWorkerEngine(source);
        var produced = 0;
        engine.FrameProduced += (_, _) => Interlocked.Increment(ref produced);
        await engine.StartAsync(
            CaptureWorkerTestSupport.CreateAuthorization(),
            CancellationToken.None);
        var retained = new TrackingResource();
        source.Emit(CreateFrame(retained));
        await CaptureWorkerTestSupport.WaitUntilAsync(() => Volatile.Read(ref produced) == 1);

        source.Report(new CaptureSourceStatusChanged(
            CaptureWorkerStatus.Faulted,
            CaptureWorkerStatusReason.TargetClosed,
            ClearRetainedFrames: true,
            IsFault: true));
        var late = new TrackingResource();
        source.Emit(CreateFrame(late));

        Assert.Equal(CaptureWorkerStatus.Faulted, engine.Status);
        Assert.Equal(1, retained.DisposeCount);
        Assert.Equal(1, late.DisposeCount);
        Assert.Equal(1, Volatile.Read(ref produced));
        Assert.Equal(0, engine.GetMetrics().CurrentSourceFrames);
        Assert.Equal(1, engine.GetMetrics().FaultCount);
    }

    [Fact]
    public async Task CancelledStart_StopsSourceClearsPipelineAndEndsStopped()
    {
        await using var source = new ControllableCaptureSource
        {
            StartHandler = static async (_, cancellationToken) =>
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
        };
        await using var engine = new CaptureWorkerEngine(source);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.StartAsync(
                CaptureWorkerTestSupport.CreateAuthorization(),
                cancellation.Token));

        Assert.Equal(CaptureWorkerStatus.Stopped, engine.Status);
        Assert.Equal(1, source.StopCount);
        Assert.Equal(0, engine.GetMetrics().CurrentSourceFrames);
    }

    private static CaptureSourceFrame CreateFrame(
        IDisposable resource,
        int width = 32,
        int height = 32) =>
        new(
            CaptureWorkerTestSupport.FixedTime,
            width,
            height,
            checked((long)width * height * 4),
            resource);
}
