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
    private const int RestartEvidenceCount = 24;

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
        var processIdentity = GetWorkerProcessIdentity(processId);
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
        Assert.False(IsProcessIdentityAlive(processIdentity));
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
        var processIdentities = new List<WorkerProcessIdentity>
        {
            GetWorkerProcessIdentity(worker.WorkerProcessId),
        };
        var ownedResources = worker.ObserveOwnedResources();
        Assert.True(ownedResources.HasExpectedOpenHandles);
        var childHandleCounts = new List<int>();

        for (var attempt = 0; attempt < RestartEvidenceCount; attempt++)
        {
            var oldIdentity = GetWorkerProcessIdentity(worker.WorkerProcessId);
            var oldResources = ownedResources;
            await worker.RestartAsync(grant, CancellationToken.None);
            Assert.False(IsProcessIdentityAlive(oldIdentity));
            Assert.True(oldResources.AllObservedHandlesClosed);
            Assert.True(worker.WorkerProcessId > 0);
            var currentIdentity = GetWorkerProcessIdentity(worker.WorkerProcessId);
            Assert.NotEqual(oldIdentity, currentIdentity);
            processIdentities.Add(currentIdentity);
            ownedResources = worker.ObserveOwnedResources();
            Assert.True(ownedResources.HasExpectedOpenHandles);
            var metrics = await worker.GetMetricsAsync(CancellationToken.None);
            Assert.Equal(attempt + 1, metrics.RestartCount);
            Assert.True(metrics.MaximumObservedSourceFrames <= CaptureWorkerMetrics.MaximumSourceFrames);
            Assert.True(metrics.MaximumObservedAccountedBytes <= CaptureWorkerMetrics.ScreenshotBudgetBytes);
            Assert.True(metrics.NativeHandleCount > 0);
            Assert.True(metrics.WorkingSetBytes > 0);
            Assert.True(metrics.PrivateMemoryBytes > 0);
            childHandleCounts.Add(metrics.NativeHandleCount);
        }

        var finalIdentity = GetWorkerProcessIdentity(worker.WorkerProcessId);
        var finalResources = ownedResources;
        await worker.StopAndClearAsync(CancellationToken.None);
        Assert.False(IsProcessIdentityAlive(finalIdentity));
        Assert.True(finalResources.AllObservedHandlesClosed);
        Assert.True(worker.ObserveOwnedResources().IsEmpty);
        Assert.Equal(processIdentities.Count, processIdentities.Distinct().Count());
        Assert.Equal(0, worker.WorkerProcessId);
        Assert.Equal(runtimeConstructionsBefore, CompanionRuntime.ConstructionCount);
        Assert.False(IsStrictlyIncreasing(childHandleCounts));
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
        var stopped = new TaskCompletionSource<CaptureWorkerStatusChanged>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var framesDispatchedAfterStop = 0;
        worker.FrameProduced += (_, _) =>
        {
            if (stopped.Task.IsCompleted)
            {
                Interlocked.Increment(ref framesDispatchedAfterStop);
                return;
            }

            observerEntered.TrySetResult();
            releaseObserver.Wait(TimeSpan.FromSeconds(10));
        };
        worker.StatusChanged += (_, change) =>
        {
            if (change.Status == CaptureWorkerStatus.Stopped)
            {
                stopped.TrySetResult(change);
            }
        };
        await worker.StartAsync(CaptureWorkerTestSupport.CreateGrant(), CancellationToken.None);
        await observerEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            var metrics = await worker.GetMetricsAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(3));
            var stop = worker.StopAndClearAsync(CancellationToken.None);
            var result = await stop.WaitAsync(TimeSpan.FromSeconds(5));
            var stoppedChange = await stopped.Task.WaitAsync(TimeSpan.FromSeconds(3));

            Assert.True(metrics.WorkerProcessId > 0);
            Assert.True(result.ClearedMetadataCount >= 0);
            Assert.Equal(CaptureWorkerStatus.Stopped, stoppedChange.Status);
            Assert.Equal(0, worker.WorkerProcessId);
        }
        finally
        {
            releaseObserver.Set();
        }

        // Frames queued behind the blocked observer predate the Stopped fence that was
        // already delivered. Releasing the observer must not dispatch any of them.
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.Equal(0, Volatile.Read(ref framesDispatchedAfterStop));
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
        var foreignAttention = 0;
        var firstAttentionSignal = new TaskCompletionSource<AttentionSheetMetadata>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        worker.FrameProduced += (_, frame) => publishedFrames.TryAdd(frame.SequenceNumber, 0);
        worker.AttentionSheetProduced += (_, metadata) =>
        {
            if (!publishedFrames.ContainsKey(metadata.SourceSequenceNumber))
            {
                Interlocked.Exchange(ref attentionBeforeSourceFrame, 1);
            }

            if (!metadata.Matches(grant))
            {
                Interlocked.Exchange(ref foreignAttention, 1);
            }

            firstAttentionSignal.TrySetResult(metadata);
        };
        await worker.StartAsync(grant, CancellationToken.None);
        var firstSignal = await firstAttentionSignal.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The worker emits exactly one orientation, then a regional sheet for each
        // changed frame. Client retention and notifications are newest-preserving,
        // so whether the retained sheet is still the orientation is timing, not
        // contract. Orientation production is proven exactly by the worker metric
        // below; its pixel layout is proven in-process and by the CI artifact.
        AttentionSheet? retained = null;
        await CaptureWorkerTestSupport.WaitUntilAsync(
            () => (retained = worker.TakeLatestAttentionSheet()) is not null,
            TimeSpan.FromSeconds(10));
        using (retained)
        {
            Assert.Contains(
                retained!.Metadata.Kind,
                new[] { AttentionSheetKind.Orientation, AttentionSheetKind.Regional });
            Assert.True(retained.Metadata.Matches(grant));
            Assert.True(retained.Metadata.SourceSequenceNumber >= firstSignal.SourceSequenceNumber);
            Assert.Equal(0, Volatile.Read(ref attentionBeforeSourceFrame));
            Assert.Equal(0, Volatile.Read(ref foreignAttention));
            Assert.True(publishedFrames.ContainsKey(firstSignal.SourceSequenceNumber));
            var decoded = PngTestDecoder.Decode(retained.EncodedImage.Span);
            Assert.Equal(retained.Metadata.SheetWidth, decoded.Width);
            Assert.Equal(retained.Metadata.SheetHeight, decoded.Height);
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

    private static WorkerProcessIdentity GetWorkerProcessIdentity(int processId)
    {
        using var process = Process.GetProcessById(processId);
        return new(process.Id, process.StartTime.ToUniversalTime());
    }

    private static bool IsProcessIdentityAlive(WorkerProcessIdentity identity)
    {
        try
        {
            using var process = Process.GetProcessById(identity.ProcessId);
            return !process.HasExited
                && process.StartTime.ToUniversalTime() == identity.StartTimeUtc;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsStrictlyIncreasing(IReadOnlyList<int> values) =>
        values.Count > 1
        && values.Zip(values.Skip(1), (left, right) => right > left).All(increased => increased);

    private readonly record struct WorkerProcessIdentity(int ProcessId, DateTime StartTimeUtc);
}
