using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Fake;

namespace CompanionCore.Capture.Tests;

public sealed class FakeCaptureWorkerTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_TransitionsThroughStartingToRunning_AndProducesExactDeterministicFrame()
    {
        var clock = new ManualClock(FixedTime);
        using var worker = new FakeCaptureWorker(clock);
        var statuses = new List<CaptureWorkerStatusChanged>();
        CaptureFrameMetadata? frame = null;
        worker.StatusChanged += (_, e) => statuses.Add(e);
        worker.FrameProduced += (_, e) => frame = e;

        await worker.StartAsync(CancellationToken.None);

        Assert.Equal(CaptureWorkerStatus.Running, worker.Status);
        Assert.Equal(
            [CaptureWorkerStatus.Starting, CaptureWorkerStatus.Running],
            statuses.Select(s => s.Status));
        Assert.All(statuses, s => Assert.Equal(FixedTime, s.Timestamp));
        // Exact, not "not null": with a fixed clock two identical scripted runs must
        // produce byte-identical metadata.
        Assert.Equal(new CaptureFrameMetadata(1, FixedTime, 1, 1), frame);
    }

    [Fact]
    public async Task StartAsync_TwoIdenticalRuns_ProduceIdenticalFrames()
    {
        // Determinism, proven directly: two independently constructed workers with the
        // same fixed clock produce the exact same first frame.
        var frameA = await CaptureFirstFrame(new ManualClock(FixedTime));
        var frameB = await CaptureFirstFrame(new ManualClock(FixedTime));

        Assert.Equal(frameA, frameB);

        static async Task<CaptureFrameMetadata> CaptureFirstFrame(ISystemClock clock)
        {
            using var worker = new FakeCaptureWorker(clock);
            CaptureFrameMetadata? frame = null;
            worker.FrameProduced += (_, e) => frame = e;
            await worker.StartAsync(CancellationToken.None);
            return frame!;
        }
    }

    [Fact]
    public async Task StopAsync_SetsStatusToStopped()
    {
        using var worker = new FakeCaptureWorker(new ManualClock(FixedTime));
        await worker.StartAsync(CancellationToken.None);

        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(CaptureWorkerStatus.Stopped, worker.Status);
    }

    [Fact]
    public async Task StopAsync_AlreadyCancelledToken_ThrowsWithoutChangingStatus()
    {
        using var worker = new FakeCaptureWorker(new ManualClock(FixedTime));
        await worker.StartAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => worker.StopAsync(cts.Token));

        // Status must be unchanged by the cancelled attempt — still Running, not Stopped.
        Assert.Equal(CaptureWorkerStatus.Running, worker.Status);
    }

    [Fact]
    public async Task RestartAsync_GoesThroughStoppedRestartingRunning_AndSequenceKeepsIncreasing()
    {
        var clock = new ManualClock(FixedTime);
        using var worker = new FakeCaptureWorker(clock);
        await worker.StartAsync(CancellationToken.None);

        var statuses = new List<CaptureWorkerStatus>();
        var frames = new List<CaptureFrameMetadata>();
        worker.StatusChanged += (_, e) => statuses.Add(e.Status);
        worker.FrameProduced += (_, e) => frames.Add(e);
        clock.UtcNow = FixedTime.AddSeconds(1);

        await worker.RestartAsync(CancellationToken.None);

        Assert.Equal(
            [CaptureWorkerStatus.Stopped, CaptureWorkerStatus.Restarting, CaptureWorkerStatus.Starting, CaptureWorkerStatus.Running],
            statuses);
        Assert.Equal(CaptureWorkerStatus.Running, worker.Status);
        var newFrame = Assert.Single(frames);
        Assert.Equal(new CaptureFrameMetadata(2, FixedTime.AddSeconds(1), 1, 1), newFrame);
    }

    [Fact]
    public async Task RestartAsync_AlreadyCancelledToken_ThrowsAndLeavesWorkerRunning()
    {
        using var worker = new FakeCaptureWorker(new ManualClock(FixedTime));
        await worker.StartAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => worker.RestartAsync(cts.Token));

        // The cancelled Stop step inside Restart must not have mutated status — the
        // worker is left exactly as it was before the restart attempt, not half-restarted.
        Assert.Equal(CaptureWorkerStatus.Running, worker.Status);
    }

    [Fact]
    public async Task StartAsync_AlreadyCancelledToken_ThrowsWithoutChangingStatus()
    {
        using var worker = new FakeCaptureWorker(new ManualClock(FixedTime));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => worker.StartAsync(cts.Token));

        Assert.Equal(CaptureWorkerStatus.Stopped, worker.Status);
    }

    [Fact]
    public void Dispose_SetsStatusToStopped()
    {
        var worker = new FakeCaptureWorker();

        worker.Dispose();

        Assert.Equal(CaptureWorkerStatus.Stopped, worker.Status);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var worker = new FakeCaptureWorker();

        worker.Dispose();
        var exception = Record.Exception(worker.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public async Task Dispose_AfterStart_LeavesNoPendingWorkAndSubsequentCallsThrow()
    {
        var worker = new FakeCaptureWorker(new ManualClock(FixedTime));
        await worker.StartAsync(CancellationToken.None);

        worker.Dispose();

        Assert.Equal(CaptureWorkerStatus.Stopped, worker.Status);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.StartAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.StopAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.RestartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AfterDispose_StartAsync_Throws()
    {
        var worker = new FakeCaptureWorker();
        worker.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.StartAsync(CancellationToken.None));
    }

    private sealed class ManualClock(DateTimeOffset initial) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }
}
