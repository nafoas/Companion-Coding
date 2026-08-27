using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class VisualObservationPipelineTests
{
    [Fact]
    public async Task FirstFrameOrientsDuplicatesDropAndChangedFramesStagger()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();

        using var firstFrame = CreateFrame(1, pattern: 10);
        using var orientation = await pipeline.ProcessAsync(
            firstFrame,
            authorization,
            CancellationToken.None);
        Assert.NotNull(orientation);
        Assert.Equal(AttentionSheetKind.Orientation, orientation.Metadata.Kind);
        Assert.Single(orientation.Metadata.Regions);

        using var duplicateFrame = CreateFrame(2, pattern: 10);
        var duplicate = await pipeline.ProcessAsync(
            duplicateFrame,
            authorization,
            CancellationToken.None);
        Assert.Null(duplicate);

        orientation.Dispose();
        using var changedFrame = CreateFrame(3, pattern: 80);
        using var regional = await pipeline.ProcessAsync(
            changedFrame,
            authorization,
            CancellationToken.None);
        Assert.NotNull(regional);
        Assert.Equal(AttentionSheetKind.Regional, regional.Metadata.Kind);
        Assert.Equal(
            new[]
            {
                AttentionRegionKind.FullContext,
                AttentionRegionKind.CenterEnvironment,
                AttentionRegionKind.LowerDialogueInventory,
            },
            regional.Metadata.Regions.Select(region => region.Kind));

        var metrics = pipeline.Snapshot();
        Assert.Equal(2, metrics.ChangedFrames);
        Assert.Equal(1, metrics.DuplicateFrames);
        Assert.Equal(2, metrics.ProducedSheets);
        Assert.Equal(1, metrics.OrientationSheets);
        Assert.InRange(metrics.MaximumSheets, 1, AttentionSheet.MaximumRetainedSheets);
    }

    [Fact]
    public async Task ManualOverrideForcesNextSheetAndSurvivesGeometryRemap()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        using (var initialFrame = CreateFrame(1, 10))
        using (var initial = await pipeline.ProcessAsync(
                   initialFrame,
                   authorization,
                   CancellationToken.None))
        {
            Assert.NotNull(initial);
        }

        var manual = new NormalizedRegion(0.2, 0.25, 0.3, 0.35);
        pipeline.SetManualRegion(manual);
        using (var sameFrame = CreateFrame(2, 10))
        using (var forced = await pipeline.ProcessAsync(
                   sameFrame,
                   authorization,
                   CancellationToken.None))
        {
            Assert.NotNull(forced);
            var manualMetadata = Assert.Single(
                forced.Metadata.Regions,
                region => region.Kind == AttentionRegionKind.ManualFocus);
            Assert.Equal(manual, manualMetadata.NormalizedSource);
            Assert.Equal(
                VisualRegionLayout.MapToPixels(manual, 320, 180),
                manualMetadata.SourcePixels);
        }

        pipeline.Reset(clearManualRegion: false);
        using (var resizedFrame = CreateFrame(3, 90, width: 640, height: 360))
        using (var resizedOrientation = await pipeline.ProcessAsync(
                   resizedFrame,
                   authorization,
                   CancellationToken.None))
        {
            Assert.NotNull(resizedOrientation);
            Assert.Equal(AttentionSheetKind.Orientation, resizedOrientation.Metadata.Kind);
        }

        using var changedAfterResize = CreateFrame(4, 160, width: 640, height: 360);
        using var remapped = await pipeline.ProcessAsync(
            changedAfterResize,
            authorization,
            CancellationToken.None);
        Assert.NotNull(remapped);
        var remappedManual = Assert.Single(
            remapped.Metadata.Regions,
            region => region.Kind == AttentionRegionKind.ManualFocus);
        Assert.Equal(VisualRegionLayout.MapToPixels(manual, 640, 360), remappedManual.SourcePixels);
    }

    [Fact]
    public async Task AcceleratedSixHourVisualSoakReturnsToZeroOwnership()
    {
        const int simulatedFrames = 6 * 60 * 60 * 10;
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        for (var index = 0; index < simulatedFrames; index++)
        {
            using var frame = CreateFrame(
                sequence: index + 1,
                pattern: index / 1000,
                width: 8,
                height: 8,
                timestamp: CaptureWorkerTestSupport.FixedTime.AddMilliseconds(index * 100L));
            using var sheet = await pipeline.ProcessAsync(
                frame,
                authorization,
                CancellationToken.None);
        }

        var metrics = pipeline.Snapshot();
        Assert.Equal(0, metrics.CurrentSheets);
        Assert.Equal(0, metrics.CurrentSheetBytes);
        Assert.Equal(0, metrics.CurrentWorkingBytes);
        Assert.InRange(metrics.MaximumSheets, 1, AttentionSheet.MaximumRetainedSheets);
        Assert.InRange(
            metrics.MaximumSheetBytes,
            1,
            AttentionSheet.MaximumEncodedBytes * AttentionSheet.MaximumRetainedSheets);
        Assert.InRange(
            metrics.MaximumWorkingBytes,
            1,
            CaptureWorkerMetrics.VisualWorkingBudgetBytes);
        Assert.True(metrics.DuplicateFrames > 200_000);
    }

    [Fact]
    public async Task OversizedReadbackIsRejectedBeforePixelCopy()
    {
        using var pipeline = new VisualObservationPipeline();
        var ownership = new PixelOwnershipCounter();
        using var frame = new CaptureSourceFrame(
            CaptureWorkerTestSupport.FixedTime,
            width: 1,
            height: 1,
            accountedBytes: (CaptureWorkerMetrics.VisualWorkingBudgetBytes / 2) + 1,
            new TestPixelResource(1, 1, pattern: 10, counter: ownership));
        frame.AssignSequence(1);

        var sheet = await pipeline.ProcessAsync(
            frame,
            CaptureWorkerTestSupport.CreateAuthorization(),
            CancellationToken.None);

        Assert.Null(sheet);
        Assert.Equal(0, ownership.CopiesCreated);
        var metrics = pipeline.Snapshot();
        Assert.Equal(1, metrics.DroppedSheets);
        Assert.Equal(0, metrics.CurrentWorkingBytes);
    }

    [Fact]
    public async Task CancelledReadbackRestoresWorkingOwnershipToZero()
    {
        using var pipeline = new VisualObservationPipeline();
        using var frame = CreateFrame(1, pattern: 10);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await pipeline.ProcessAsync(
                frame,
                CaptureWorkerTestSupport.CreateAuthorization(),
                cancellation.Token));

        var metrics = pipeline.Snapshot();
        Assert.Equal(0, metrics.CurrentWorkingBytes);
        Assert.Equal(1, metrics.DroppedSheets);
    }

    private static CaptureSourceFrame CreateFrame(
        long sequence,
        int pattern,
        int width = 320,
        int height = 180,
        DateTimeOffset? timestamp = null)
    {
        var frame = CaptureWorkerTestSupport.CreatePixelFrame(
            width,
            height,
            pattern,
            timestamp);
        frame.AssignSequence(sequence);
        return frame;
    }
}
