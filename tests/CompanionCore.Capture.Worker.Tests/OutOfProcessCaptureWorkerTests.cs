using System.Diagnostics;
using CompanionCore.Capture.Client;
using CompanionCore.Capture.Contracts;
using CompanionCore.Runtime;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class OutOfProcessCaptureWorkerTests
{
    [Fact]
    public async Task CancelledStart_NeverLaunchesAProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = CreateWorker();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            worker.StartAsync(CaptureWorkerTestSupport.CreateGrant(), cancellation.Token));

        Assert.Equal(0, worker.WorkerProcessId);
        Assert.Equal(CaptureWorkerStatus.Stopped, worker.Status);
    }

    [Fact]
    public async Task SyntheticWorker_RoundTripsExactMetadataAndStopKillsOwnedChild()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = CreateWorker();
        var grant = CaptureWorkerTestSupport.CreateGrant();
        var frameReady = new TaskCompletionSource<CaptureFrameMetadata>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        worker.FrameProduced += (_, frame) => frameReady.TrySetResult(frame);

        await worker.StartAsync(grant, CancellationToken.None);
        var processId = worker.WorkerProcessId;
        var frame = await frameReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var metrics = await worker.GetMetricsAsync(CancellationToken.None);

        Assert.True(processId > 0);
        Assert.Equal(grant.TargetSessionId, frame.TargetSessionId);
        Assert.Equal(grant.Generation, frame.Generation);
        Assert.Equal(grant.Target, frame.Target);
        Assert.InRange(metrics.CurrentSourceFrames, 0, CaptureWorkerMetrics.MaximumSourceFrames);
        Assert.InRange(metrics.CurrentAccountedBytes, 0, CaptureWorkerMetrics.ScreenshotBudgetBytes);

        await worker.StopAndClearAsync(CancellationToken.None);
        Assert.Equal(0, worker.WorkerProcessId);
        await AssertProcessExitedAsync(processId);
    }

    [Fact]
    public async Task RepeatedRestarts_UseFreshChildrenKeepBoundsAndLeaveNoChildAlive()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = CreateWorker();
        var grant = CaptureWorkerTestSupport.CreateGrant();
        var runtimeConstructionsBefore = CompanionRuntime.ConstructionCount;
        await worker.StartAsync(grant, CancellationToken.None);
        var processIds = new List<int> { worker.WorkerProcessId };
        var childHandleCounts = new List<int>();
        var parentHandleCounts = new List<int>();

        for (var attempt = 0; attempt < 12; attempt++)
        {
            var oldProcessId = worker.WorkerProcessId;
            await worker.RestartAsync(grant, CancellationToken.None);
            await AssertProcessExitedAsync(oldProcessId);
            Assert.True(worker.WorkerProcessId > 0);
            Assert.NotEqual(oldProcessId, worker.WorkerProcessId);
            processIds.Add(worker.WorkerProcessId);
            var metrics = await worker.GetMetricsAsync(CancellationToken.None);
            Assert.Equal(attempt + 1, metrics.RestartCount);
            Assert.True(metrics.MaximumObservedSourceFrames <= CaptureWorkerMetrics.MaximumSourceFrames);
            Assert.True(metrics.MaximumObservedAccountedBytes <= CaptureWorkerMetrics.ScreenshotBudgetBytes);
            Assert.True(metrics.NativeHandleCount > 0);
            Assert.True(metrics.WorkingSetBytes > 0);
            Assert.True(metrics.PrivateMemoryBytes > 0);
            childHandleCounts.Add(metrics.NativeHandleCount);
            using var parent = Process.GetCurrentProcess();
            parentHandleCounts.Add(parent.HandleCount);
        }

        var finalProcessId = worker.WorkerProcessId;
        await worker.StopAndClearAsync(CancellationToken.None);
        await AssertProcessExitedAsync(finalProcessId);
        Assert.Equal(processIds.Count, processIds.Distinct().Count());
        Assert.Equal(0, worker.WorkerProcessId);
        Assert.Equal(runtimeConstructionsBefore, CompanionRuntime.ConstructionCount);
        Assert.False(IsStrictlyIncreasing(childHandleCounts));
        Assert.False(IsStrictlyIncreasing(parentHandleCounts));
    }

    [Fact]
    public async Task UnexpectedCrash_FailsClosedAndRestartCreatesFreshWorker()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = CreateWorker();
        var grant = CaptureWorkerTestSupport.CreateGrant();
        await worker.StartAsync(grant, CancellationToken.None);
        var crashedProcessId = worker.WorkerProcessId;
        using (var process = Process.GetProcessById(crashedProcessId))
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }

        await CaptureWorkerTestSupport.WaitUntilAsync(
            () => worker.Status == CaptureWorkerStatus.Faulted,
            TimeSpan.FromSeconds(10));
        await worker.RestartAsync(grant, CancellationToken.None);

        Assert.Equal(CaptureWorkerStatus.Running, worker.Status);
        Assert.True(worker.WorkerProcessId > 0);
        Assert.NotEqual(crashedProcessId, worker.WorkerProcessId);
        await worker.StopAndClearAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BlockingFrameObserver_CannotBlockProtocolResponsesOrWorkerStop()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = CreateWorker();
        using var releaseObserver = new ManualResetEventSlim();
        var observerEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        worker.FrameProduced += (_, _) =>
        {
            observerEntered.TrySetResult();
            releaseObserver.Wait(TimeSpan.FromSeconds(10));
        };
        await worker.StartAsync(CaptureWorkerTestSupport.CreateGrant(), CancellationToken.None);
        await observerEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var metrics = await worker.GetMetricsAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(3));
        var stop = worker.StopAndClearAsync(CancellationToken.None);
        var result = await stop.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(metrics.WorkerProcessId > 0);
        Assert.True(result.ClearedMetadataCount >= 0);
        Assert.Equal(0, worker.WorkerProcessId);
        releaseObserver.Set();
    }

    private static OutOfProcessCaptureWorker CreateWorker() =>
        new(CaptureWorkerLaunchOptions.ForPrivateSafeSyntheticTests());

    private static async Task AssertProcessExitedAsync(int processId)
    {
        await CaptureWorkerTestSupport.WaitUntilAsync(
            () =>
            {
                try
                {
                    using var process = Process.GetProcessById(processId);
                    return process.HasExited;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            },
            TimeSpan.FromSeconds(10));
    }

    private static bool IsStrictlyIncreasing(IReadOnlyList<int> values) =>
        values.Count > 1
        && values.Zip(values.Skip(1), (left, right) => right > left).All(increased => increased);
}
