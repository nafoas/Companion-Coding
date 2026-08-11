using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class CaptureFramePipelineTests
{
    [Fact]
    public async Task OversizedFrame_IsRejectedAndDisposedExactlyOnce()
    {
        await using var pipeline = new CaptureFramePipeline();
        pipeline.Resume();
        var resource = new TrackingResource();
        var frame = CreateFrame(
            resource,
            CaptureWorkerMetrics.ScreenshotBudgetBytes + 1);

        var accepted = pipeline.TryOffer(frame);

        var metrics = CaptureWorkerTestSupport.Snapshot(pipeline);
        Assert.False(accepted);
        Assert.Equal(1, resource.DisposeCount);
        Assert.Equal(1, metrics.DroppedFrames);
        Assert.Equal(1, metrics.DisposedFrames);
        Assert.Equal(0, metrics.CurrentSourceFrames);
        Assert.Equal(0, metrics.CurrentAccountedBytes);
    }

    [Fact]
    public async Task ProducerPressure_KeepsNewestThreeAndNeverExceedsQueueBound()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pipeline = new CaptureFramePipeline(
            processor: async (_, cancellationToken) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            });
        pipeline.Resume();
        var resources = Enumerable.Range(0, 4)
            .Select(_ => new TrackingResource())
            .ToArray();

        Assert.True(pipeline.TryOffer(CreateFrame(resources[0], 1024)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(pipeline.TryOffer(CreateFrame(resources[1], 1024)));
        Assert.True(pipeline.TryOffer(CreateFrame(resources[2], 1024)));
        Assert.True(pipeline.TryOffer(CreateFrame(resources[3], 1024)));

        var pressured = CaptureWorkerTestSupport.Snapshot(pipeline);
        Assert.Equal(CaptureFramePipeline.ProcessingQueueCapacity, pressured.QueueDepth);
        Assert.Equal(3, pressured.CurrentSourceFrames);
        Assert.Equal(3, pressured.MaximumObservedSourceFrames);
        Assert.Equal(1, pressured.DroppedFrames);
        Assert.Equal(1, resources[1].DisposeCount);

        release.TrySetResult();
        await CaptureWorkerTestSupport.WaitUntilAsync(
            () => CaptureWorkerTestSupport.Snapshot(pipeline).RingFrameCount == 3);
        var cleared = await pipeline.ClearAsync(CancellationToken.None);

        Assert.Equal(3, cleared.ClearedMetadataCount);
        Assert.All(resources, resource => Assert.Equal(1, resource.DisposeCount));
        Assert.Equal(0, CaptureWorkerTestSupport.Snapshot(pipeline).CurrentSourceFrames);
    }

    [Fact]
    public async Task BytePressure_EvictsOldestAndStaysBelowSixtyFourMiB()
    {
        await using var pipeline = new CaptureFramePipeline();
        pipeline.Resume();
        var resources = Enumerable.Range(0, 10)
            .Select(_ => new TrackingResource())
            .ToArray();
        var published = 0;
        pipeline.FrameReady += (_, _) => Interlocked.Increment(ref published);
        const long frameBytes = 24L * 1024 * 1024;

        for (var index = 0; index < resources.Length; index++)
        {
            Assert.True(pipeline.TryOffer(CreateFrame(resources[index], frameBytes)));
            var expected = index + 1;
            await CaptureWorkerTestSupport.WaitUntilAsync(
                () => Volatile.Read(ref published) >= expected);

            var snapshot = CaptureWorkerTestSupport.Snapshot(pipeline);
            Assert.InRange(snapshot.CurrentAccountedBytes, 0, CaptureWorkerMetrics.ScreenshotBudgetBytes);
            Assert.InRange(snapshot.CurrentSourceFrames, 0, CaptureWorkerMetrics.MaximumSourceFrames);
        }

        var final = CaptureWorkerTestSupport.Snapshot(pipeline);
        Assert.Equal(2, final.RingFrameCount);
        Assert.Equal(48L * 1024 * 1024, final.RingBytes);
        Assert.True(final.MaximumObservedAccountedBytes <= CaptureWorkerMetrics.ScreenshotBudgetBytes);
        Assert.True(final.MaximumObservedSourceFrames <= CaptureWorkerMetrics.MaximumSourceFrames);

        await pipeline.ClearAsync(CancellationToken.None);
        Assert.All(resources, resource => Assert.Equal(1, resource.DisposeCount));
    }

    [Fact]
    public async Task Clear_WaitsForOwnedProcessingFrameAndCountsEveryDisposedByte()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pipeline = new CaptureFramePipeline(
            processor: async (_, cancellationToken) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            });
        pipeline.Resume();
        var resources = Enumerable.Range(0, 3)
            .Select(_ => new TrackingResource())
            .ToArray();
        Assert.True(pipeline.TryOffer(CreateFrame(resources[0], 2048)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(pipeline.TryOffer(CreateFrame(resources[1], 2048)));
        Assert.True(pipeline.TryOffer(CreateFrame(resources[2], 2048)));
        var clear = pipeline.ClearAsync(CancellationToken.None);
        Assert.False(clear.IsCompleted);

        release.TrySetResult();
        var result = await clear;

        Assert.Equal(3, result.ClearedMetadataCount);
        Assert.Equal(3 * 2048, result.ClearedBytes);
        Assert.All(resources, resource => Assert.Equal(1, resource.DisposeCount));
    }

    [Fact]
    public async Task OldestFrameLifetime_UsesInjectedClockAndResetsOnClear()
    {
        var clock = new ManualClock(CaptureWorkerTestSupport.FixedTime);
        await using var pipeline = new CaptureFramePipeline(clock);
        pipeline.Resume();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pipeline.FrameReady += (_, _) => ready.TrySetResult();
        var resource = new TrackingResource();
        Assert.True(pipeline.TryOffer(CreateFrame(resource, 512, clock.UtcNow)));
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        clock.UtcNow = clock.UtcNow.AddSeconds(9);
        Assert.Equal(
            TimeSpan.FromSeconds(9),
            CaptureWorkerTestSupport.Snapshot(pipeline).OldestFrameLifetime);

        await pipeline.ClearAsync(CancellationToken.None);
        Assert.Equal(TimeSpan.Zero, CaptureWorkerTestSupport.Snapshot(pipeline).OldestFrameLifetime);
        Assert.Equal(1, resource.DisposeCount);
    }

    [Fact]
    public async Task Clear_ContinuesAfterOneResourceReleaseThrows()
    {
        await using var pipeline = new CaptureFramePipeline();
        pipeline.Resume();
        var published = 0;
        pipeline.FrameReady += (_, _) => Interlocked.Increment(ref published);
        var throwing = new ThrowingResource();
        var healthy = new TrackingResource();
        Assert.True(pipeline.TryOffer(CreateFrame(throwing, 512)));
        await CaptureWorkerTestSupport.WaitUntilAsync(() => Volatile.Read(ref published) == 1);
        Assert.True(pipeline.TryOffer(CreateFrame(healthy, 512)));
        await CaptureWorkerTestSupport.WaitUntilAsync(() => Volatile.Read(ref published) == 2);

        var cleared = await pipeline.ClearAsync(CancellationToken.None);

        Assert.Equal(2, cleared.ClearedMetadataCount);
        Assert.Equal(1, throwing.DisposeCount);
        Assert.Equal(1, healthy.DisposeCount);
        Assert.Equal(0, CaptureWorkerTestSupport.Snapshot(pipeline).CurrentSourceFrames);
    }

    [Fact]
    public async Task AcceleratedSixHourSoak_HasConstantBoundsAndZeroOutstandingOwnership()
    {
        const int simulatedFrames = 6 * 60 * 60 * 10;
        var counter = new SharedDisposalCounter();
        await using var pipeline = new CaptureFramePipeline();
        pipeline.Resume();

        for (var index = 0; index < simulatedFrames; index++)
        {
            _ = pipeline.TryOffer(new CaptureSourceFrame(
                CaptureWorkerTestSupport.FixedTime.AddMilliseconds(index * 100L),
                width: 32,
                height: 32,
                accountedBytes: 4096,
                new CountedResource(counter)));
        }

        await pipeline.ClearAsync(CancellationToken.None);
        var metrics = CaptureWorkerTestSupport.Snapshot(pipeline);

        Assert.Equal(simulatedFrames, Interlocked.Read(ref counter.Created));
        Assert.Equal(simulatedFrames, Interlocked.Read(ref counter.Disposed));
        Assert.Equal(0, metrics.CurrentSourceFrames);
        Assert.Equal(0, metrics.CurrentAccountedBytes);
        Assert.Equal(0, metrics.QueueDepth);
        Assert.Equal(0, metrics.RingFrameCount);
        Assert.True(metrics.MaximumObservedSourceFrames <= CaptureWorkerMetrics.MaximumSourceFrames);
        Assert.True(metrics.MaximumObservedAccountedBytes <= CaptureWorkerMetrics.ScreenshotBudgetBytes);
        Assert.Equal(CaptureFramePipeline.ProcessingQueueCapacity, metrics.QueueCapacity);
    }

    private static CaptureSourceFrame CreateFrame(
        IDisposable resource,
        long bytes,
        DateTimeOffset? timestamp = null) =>
        new(
            timestamp ?? CaptureWorkerTestSupport.FixedTime,
            width: 32,
            height: 32,
            accountedBytes: bytes,
            resource);
}
