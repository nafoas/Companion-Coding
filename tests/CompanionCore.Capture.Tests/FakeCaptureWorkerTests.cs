using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Fake;

namespace CompanionCore.Capture.Tests;

public sealed class FakeCaptureWorkerTests
{
    [Fact]
    public async Task StartAsync_TransitionsThroughStartingToRunning_AndProducesOneFrame()
    {
        using var worker = new FakeCaptureWorker();
        var statuses = new List<CaptureWorkerStatus>();
        CaptureFrameMetadata? frame = null;
        worker.StatusChanged += (_, e) => statuses.Add(e.Status);
        worker.FrameProduced += (_, e) => frame = e;

        await worker.StartAsync(CancellationToken.None);

        Assert.Equal(CaptureWorkerStatus.Running, worker.Status);
        Assert.Equal([CaptureWorkerStatus.Starting, CaptureWorkerStatus.Running], statuses);
        Assert.NotNull(frame);
        Assert.Equal(1, frame!.SequenceNumber);
        // Synthetic, not a real capture: fixed 1x1 dimensions, never anything larger.
        Assert.Equal(1, frame.Width);
        Assert.Equal(1, frame.Height);
    }

    [Fact]
    public async Task StopAsync_SetsStatusToStopped()
    {
        using var worker = new FakeCaptureWorker();
        await worker.StartAsync(CancellationToken.None);

        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(CaptureWorkerStatus.Stopped, worker.Status);
    }

    [Fact]
    public async Task RestartAsync_GoesThroughStoppedRestartingRunning_AndSequenceKeepsIncreasing()
    {
        using var worker = new FakeCaptureWorker();
        await worker.StartAsync(CancellationToken.None);

        var statuses = new List<CaptureWorkerStatus>();
        var frames = new List<CaptureFrameMetadata>();
        worker.StatusChanged += (_, e) => statuses.Add(e.Status);
        worker.FrameProduced += (_, e) => frames.Add(e);

        await worker.RestartAsync(CancellationToken.None);

        Assert.Equal(
            [CaptureWorkerStatus.Stopped, CaptureWorkerStatus.Restarting, CaptureWorkerStatus.Starting, CaptureWorkerStatus.Running],
            statuses);
        Assert.Equal(CaptureWorkerStatus.Running, worker.Status);
        var newFrame = Assert.Single(frames);
        Assert.Equal(2, newFrame.SequenceNumber);
    }

    [Fact]
    public async Task StartAsync_AlreadyCancelledToken_ThrowsWithoutChangingStatus()
    {
        using var worker = new FakeCaptureWorker();
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
    public async Task AfterDispose_StartAsync_Throws()
    {
        var worker = new FakeCaptureWorker();
        worker.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.StartAsync(CancellationToken.None));
    }
}
