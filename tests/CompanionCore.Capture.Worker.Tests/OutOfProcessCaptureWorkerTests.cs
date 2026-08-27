using System.Collections.Concurrent;
using System.Diagnostics;
using CompanionCore.Capture.Client;
using CompanionCore.Capture.Contracts;
using CompanionCore.Runtime;

namespace CompanionCore.Capture.Worker.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OutOfProcessCaptureWorkerCollection
{
    public const string Name = "Out-of-process capture worker";
}

[Collection(OutOfProcessCaptureWorkerCollection.Name)]
public sealed class OutOfProcessCaptureWorkerTests
{
    private const int RestartWarmupCount = 12;
    private const int MeasuredRestartCount = 12;
    private const int MaximumParentHandleDrift = 2;

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
        using var process = Process.GetProcessById(processId);
        _ = GetWorkerProcessIdentity(process);
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
        await AssertProcessExitedAsync(process);
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
        var processIdentities = new List<WorkerProcessIdentity>();
        using (var initialProcess = Process.GetProcessById(worker.WorkerProcessId))
        {
            processIdentities.Add(GetWorkerProcessIdentity(initialProcess));
        }

        var childHandleCounts = new List<int>();

        for (var attempt = 0; attempt < RestartWarmupCount; attempt++)
        {
            using var oldProcess = Process.GetProcessById(worker.WorkerProcessId);
            var oldIdentity = GetWorkerProcessIdentity(oldProcess);
            await worker.RestartAsync(grant, CancellationToken.None);
            await AssertProcessExitedAsync(oldProcess);
            Assert.True(worker.WorkerProcessId > 0);
            using var currentProcess = Process.GetProcessById(worker.WorkerProcessId);
            var currentIdentity = GetWorkerProcessIdentity(currentProcess);
            Assert.NotEqual(oldIdentity, currentIdentity);
            processIdentities.Add(currentIdentity);
        }

        var parentHandleBaseline = GetParentHandleCount();
        for (var attempt = 0; attempt < MeasuredRestartCount; attempt++)
        {
            using var oldProcess = Process.GetProcessById(worker.WorkerProcessId);
            var oldIdentity = GetWorkerProcessIdentity(oldProcess);
            await worker.RestartAsync(grant, CancellationToken.None);
            await AssertProcessExitedAsync(oldProcess);
            Assert.True(worker.WorkerProcessId > 0);
            using var currentProcess = Process.GetProcessById(worker.WorkerProcessId);
            var currentIdentity = GetWorkerProcessIdentity(currentProcess);
            Assert.NotEqual(oldIdentity, currentIdentity);
            processIdentities.Add(currentIdentity);
            var metrics = await worker.GetMetricsAsync(CancellationToken.None);
            Assert.Equal(RestartWarmupCount + attempt + 1, metrics.RestartCount);
            Assert.True(metrics.MaximumObservedSourceFrames <= CaptureWorkerMetrics.MaximumSourceFrames);
            Assert.True(metrics.MaximumObservedAccountedBytes <= CaptureWorkerMetrics.ScreenshotBudgetBytes);
            Assert.True(metrics.NativeHandleCount > 0);
            Assert.True(metrics.WorkingSetBytes > 0);
            Assert.True(metrics.PrivateMemoryBytes > 0);
            childHandleCounts.Add(metrics.NativeHandleCount);
        }

        var settledParentHandleCount = await WaitForParentHandlesAsync(
            parentHandleBaseline + MaximumParentHandleDrift,
            TimeSpan.FromSeconds(10));
        using var finalProcess = Process.GetProcessById(worker.WorkerProcessId);
        _ = GetWorkerProcessIdentity(finalProcess);
        await worker.StopAndClearAsync(CancellationToken.None);
        await AssertProcessExitedAsync(finalProcess);
        Assert.Equal(processIdentities.Count, processIdentities.Distinct().Count());
        Assert.Equal(0, worker.WorkerProcessId);
        Assert.Equal(runtimeConstructionsBefore, CompanionRuntime.ConstructionCount);
        Assert.False(IsStrictlyIncreasing(childHandleCounts));
        Assert.True(
            settledParentHandleCount <= parentHandleBaseline + MaximumParentHandleDrift,
            $"Parent handles did not settle within the explicit drift ceiling. " +
            $"Baseline: {parentHandleBaseline}; settled: {settledParentHandleCount}; " +
            $"ceiling: {MaximumParentHandleDrift}.");
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

    [Fact]
    public async Task SyntheticWorker_ProducesBoundedOrientationAndManualRegionalSheets()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = CreateWorker();
        var grant = CaptureWorkerTestSupport.CreateGrant();
        var publishedFrames = new ConcurrentDictionary<long, byte>();
        var attentionBeforeSourceFrame = 0;
        var firstAttentionSignal = new TaskCompletionSource<AttentionSheetMetadata>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        worker.FrameProduced += (_, frame) => publishedFrames.TryAdd(frame.SequenceNumber, 0);
        worker.AttentionSheetProduced += (_, metadata) =>
        {
            if (!publishedFrames.ContainsKey(metadata.SourceSequenceNumber))
            {
                Interlocked.Exchange(ref attentionBeforeSourceFrame, 1);
            }

            firstAttentionSignal.TrySetResult(metadata);
        };
        await worker.StartAsync(grant, CancellationToken.None);
        await firstAttentionSignal.Task.WaitAsync(TimeSpan.FromSeconds(10));
        AttentionSheet? orientation = null;
        await CaptureWorkerTestSupport.WaitUntilAsync(
            () => (orientation = worker.TakeLatestAttentionSheet()) is not null,
            TimeSpan.FromSeconds(10));
        using (orientation)
        {
            Assert.Equal(AttentionSheetKind.Orientation, orientation!.Metadata.Kind);
            Assert.True(orientation.Metadata.Matches(grant));
            Assert.Equal(0, Volatile.Read(ref attentionBeforeSourceFrame));
            await CaptureWorkerTestSupport.WaitUntilAsync(
                () => publishedFrames.ContainsKey(orientation.Metadata.SourceSequenceNumber),
                TimeSpan.FromSeconds(10));
            var decoded = PngTestDecoder.Decode(orientation.EncodedImage.Span);
            Assert.Equal(orientation.Metadata.SheetWidth, decoded.Width);
            Assert.Equal(orientation.Metadata.SheetHeight, decoded.Height);
        }

        var manual = new NormalizedRegion(0.2, 0.2, 0.4, 0.5);
        await worker.SetManualRegionAsync(grant, manual, CancellationToken.None);
        AttentionSheet? regional = null;
        await CaptureWorkerTestSupport.WaitUntilAsync(
            () => (regional = worker.TakeLatestAttentionSheet()) is not null
                && regional.Metadata.Kind == AttentionSheetKind.Regional
                && regional.Metadata.Regions.Any(
                    region => region.Kind == AttentionRegionKind.ManualFocus),
            TimeSpan.FromSeconds(10));
        using (regional)
        {
            Assert.NotNull(regional);
            Assert.Contains(
                regional!.Metadata.Regions,
                region => region.Kind == AttentionRegionKind.ManualFocus
                    && region.NormalizedSource == manual);
        }

        var metrics = await worker.GetMetricsAsync(CancellationToken.None);
        Assert.True(metrics.ProducedAttentionSheets >= 2);
        Assert.Equal(1, metrics.ProducedOrientationSheets);
        Assert.InRange(
            metrics.MaximumObservedAttentionSheets,
            1,
            AttentionSheet.MaximumRetainedSheets);
        Assert.InRange(
            metrics.MaximumObservedVisualWorkingBytes,
            1,
            CaptureWorkerMetrics.VisualWorkingBudgetBytes);

        await worker.StopAndClearAsync(CancellationToken.None);
        Assert.Null(worker.TakeLatestAttentionSheet());
    }

    [Fact]
    public async Task Restart_EmitsFreshOrientationAndRetainsOnlyFreshEpochSheets()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = CreateWorker();
        var grant = CaptureWorkerTestSupport.CreateGrant();
        var orientations = new ConcurrentQueue<AttentionSheetMetadata>();
        worker.AttentionSheetProduced += (_, metadata) =>
        {
            if (metadata.Kind == AttentionSheetKind.Orientation)
            {
                orientations.Enqueue(metadata);
            }
        };

        await worker.StartAsync(grant, CancellationToken.None);
        await CaptureWorkerTestSupport.WaitUntilAsync(
            () => orientations.Count >= 1,
            TimeSpan.FromSeconds(10));

        await worker.RestartAsync(grant, CancellationToken.None);
        await CaptureWorkerTestSupport.WaitUntilAsync(
            () => orientations.Count >= 2,
            TimeSpan.FromSeconds(10));
        var freshOrientation = orientations.Last();
        Assert.Equal(AttentionSheetKind.Orientation, freshOrientation.Kind);
        Assert.True(freshOrientation.Matches(grant));

        AttentionSheet? latest = null;
        await CaptureWorkerTestSupport.WaitUntilAsync(
            () => (latest = worker.TakeLatestAttentionSheet()) is not null,
            TimeSpan.FromSeconds(10));
        using (latest)
        {
            Assert.True(latest!.Metadata.Matches(grant));
            Assert.True(latest.Metadata.SourceTimestamp >= freshOrientation.SourceTimestamp);
        }

        var metrics = await worker.GetMetricsAsync(CancellationToken.None);
        Assert.Equal(1, metrics.ProducedOrientationSheets);

        await worker.StopAndClearAsync(CancellationToken.None);
        Assert.Null(worker.TakeLatestAttentionSheet());
    }

    private static OutOfProcessCaptureWorker CreateWorker() =>
        new(CaptureWorkerLaunchOptions.ForPrivateSafeSyntheticTests());

    private static async Task AssertProcessExitedAsync(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!process.HasExited)
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.True(process.HasExited);
    }

    private static async Task<int> WaitForParentHandlesAsync(
        int maximum,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var current = GetParentHandleCount();
        while (current > maximum && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(25);
            current = GetParentHandleCount();
        }

        return current;
    }

    private static int GetParentHandleCount()
    {
        using var parent = Process.GetCurrentProcess();
        return parent.HandleCount;
    }

    private static WorkerProcessIdentity GetWorkerProcessIdentity(Process process) =>
        new(process.Id, process.StartTime.ToUniversalTime());

    private static bool IsStrictlyIncreasing(IReadOnlyList<int> values) =>
        values.Count > 1
        && values.Zip(values.Skip(1), (left, right) => right > left).All(increased => increased);

    private readonly record struct WorkerProcessIdentity(int ProcessId, DateTime StartTimeUtc);
}
