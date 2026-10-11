using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

/// <summary>
/// Full-resolution keepsake photographs (deferred WIRE-01 D2): one requested frame of the
/// authorized target becomes a native-resolution photograph sheet, within every existing
/// sheet bound, without disturbing orientation or regional attention.
/// </summary>
public sealed class PhotographSheetTests
{
    [Fact]
    public async Task ARequestedPhotograph_IsTheNextFrameWhole_AtNativeResolution_EvenWhenDuplicate()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        using (var firstFrame = CreateFrame(1, pattern: 10))
        using (var orientation = await pipeline.ProcessAsync(firstFrame, authorization, CancellationToken.None))
        {
            Assert.Equal(AttentionSheetKind.Orientation, orientation!.Metadata.Kind);
        }

        pipeline.RequestPhotograph();
        Assert.True(pipeline.PhotographPending);
        using (var duplicateFrame = CreateFrame(2, pattern: 10))
        using (var photograph = await pipeline.ProcessAsync(duplicateFrame, authorization, CancellationToken.None))
        {
            Assert.NotNull(photograph);
            var metadata = photograph.Metadata;
            Assert.Equal(AttentionSheetKind.Photograph, metadata.Kind);
            Assert.True(metadata.IsProtocolSafe());
            Assert.Equal((320, 180, 320, 180), (metadata.SourceWidth, metadata.SourceHeight, metadata.SheetWidth, metadata.SheetHeight));
            Assert.Equal(2, metadata.SourceSequenceNumber);
            var region = Assert.Single(metadata.Regions);
            Assert.Equal(AttentionRegionKind.FullContext, region.Kind);
            Assert.Equal(new PixelRect(0, 0, 320, 180), region.SheetPixels);

            // Every pixel is the source pixel itself (BGRA source, RGBA PNG).
            var decoded = PngTestDecoder.Decode(photograph.EncodedImage.Span);
            Assert.Equal((320, 180), (decoded.Width, decoded.Height));
            foreach (var (x, y) in new[] { (0, 0), (319, 0), (17, 93), (319, 179) })
            {
                var pixel = decoded.Pixel(x, y);
                Assert.Equal(unchecked((byte)(10 * 7 + x + y)), pixel[0]);
                Assert.Equal(unchecked((byte)(10 * 3 + y)), pixel[1]);
                Assert.Equal(unchecked((byte)(10 + x)), pixel[2]);
                Assert.Equal(255, pixel[3]);
            }
        }

        Assert.False(pipeline.PhotographPending);
        Assert.Equal(1, pipeline.PhotographSheets);

        // The photograph consumed no visual change: the next identical frame is still a duplicate.
        using (var afterFrame = CreateFrame(3, pattern: 10))
        {
            Assert.Null(await pipeline.ProcessAsync(afterFrame, authorization, CancellationToken.None));
        }

        var metrics = pipeline.Snapshot();
        Assert.Equal(1, metrics.OrientationSheets);
        Assert.Equal(2, metrics.ProducedSheets);
        Assert.Equal(0, metrics.CurrentSheets);
        Assert.Equal(0, metrics.CurrentSheetBytes);
    }

    [Fact]
    public async Task APendingOrientation_StillFollowsAPhotographTakenFromTheFirstFrame()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        pipeline.RequestPhotograph();

        using (var firstFrame = CreateFrame(1, pattern: 10))
        using (var photograph = await pipeline.ProcessAsync(firstFrame, authorization, CancellationToken.None))
        {
            Assert.Equal(AttentionSheetKind.Photograph, photograph!.Metadata.Kind);
        }

        using var secondFrame = CreateFrame(2, pattern: 10);
        using var orientation = await pipeline.ProcessAsync(secondFrame, authorization, CancellationToken.None);
        Assert.Equal(AttentionSheetKind.Orientation, orientation!.Metadata.Kind);
    }

    [Fact]
    public async Task AResetOrDisposal_DisarmsARequestedPhotograph()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        pipeline.RequestPhotograph();
        pipeline.Reset();
        Assert.False(pipeline.PhotographPending);

        using (var frame = CreateFrame(1, pattern: 10))
        using (var sheet = await pipeline.ProcessAsync(frame, authorization, CancellationToken.None))
        {
            Assert.Equal(AttentionSheetKind.Orientation, sheet!.Metadata.Kind);
        }

        pipeline.RequestPhotograph();
        pipeline.Dispose();
        Assert.False(pipeline.PhotographPending);
        Assert.Throws<ObjectDisposedException>(pipeline.RequestPhotograph);
    }

    [Fact]
    public async Task APhotographThatCannotFitTheSheetBound_IsHalved_UntilItFits()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        pipeline.RequestPhotograph();

        // Incompressible noise: 2048×1536 encodes to about 12 MiB, past the 8 MiB sheet bound.
        using var frame = new CaptureSourceFrame(CaptureWorkerTestSupport.FixedTime, 2048, 1536, 2048L * 1536 * 4, new NoisePixelResource(2048, 1536));
        frame.AssignSequence(1);
        using var photograph = await pipeline.ProcessAsync(frame, authorization, CancellationToken.None);

        Assert.NotNull(photograph);
        var metadata = photograph.Metadata;
        Assert.Equal(AttentionSheetKind.Photograph, metadata.Kind);
        Assert.Equal((2048, 1536), (metadata.SourceWidth, metadata.SourceHeight));
        Assert.Equal((1024, 768), (metadata.SheetWidth, metadata.SheetHeight));
        Assert.True(metadata.EncodedByteLength <= AttentionSheet.MaximumEncodedBytes);
        Assert.True(metadata.IsProtocolSafe());
        Assert.InRange(pipeline.Snapshot().MaximumWorkingBytes, 1, CaptureWorkerMetrics.VisualWorkingBudgetBytes);
    }

    [Fact]
    public async Task APhotographConsumesNoVisualChange_SoTheNextChangedFrameIsStillRegional()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        using (var firstFrame = CreateFrame(1, pattern: 10))
        using (await pipeline.ProcessAsync(firstFrame, authorization, CancellationToken.None))
        {
        }

        pipeline.RequestPhotograph();
        using (var photoFrame = CreateFrame(2, pattern: 80))
        using (var photograph = await pipeline.ProcessAsync(photoFrame, authorization, CancellationToken.None))
        {
            Assert.Equal(AttentionSheetKind.Photograph, photograph!.Metadata.Kind);
        }

        // The same picture again is still a change against what attention last saw.
        using var nextFrame = CreateFrame(3, pattern: 80);
        using var regional = await pipeline.ProcessAsync(nextFrame, authorization, CancellationToken.None);
        Assert.Equal(AttentionSheetKind.Regional, regional!.Metadata.Kind);
    }

    [Fact]
    public async Task APhotographWiderThanTheCamerasSourceEdge_IsHalvedEvenWhenItWouldEncode()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        pipeline.RequestPhotograph();
        using var frame = CreateFrame(1, pattern: 10, width: 8400, height: 60);

        using var photograph = await pipeline.ProcessAsync(frame, authorization, CancellationToken.None);

        Assert.Equal((8400, 60), (photograph!.Metadata.SourceWidth, photograph.Metadata.SourceHeight));
        Assert.Equal((4200, 30), (photograph.Metadata.SheetWidth, photograph.Metadata.SheetHeight));
    }

    [Fact]
    public async Task APhotographThatCannotBeProduced_StaysArmedForTheNextFrame()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        pipeline.RequestPhotograph();

        // One pixel wide and taller than the edge bound: it can never be halved to fit.
        using (var impossible = new CaptureSourceFrame(CaptureWorkerTestSupport.FixedTime, 1, 9000, 9000L * 4, new NoisePixelResource(1, 9000)))
        {
            impossible.AssignSequence(1);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await pipeline.ProcessAsync(impossible, authorization, CancellationToken.None));
        }

        Assert.True(pipeline.PhotographPending);
        Assert.Equal(0, pipeline.Snapshot().CurrentWorkingBytes);
        using var frame = CreateFrame(2, pattern: 10);
        using var photograph = await pipeline.ProcessAsync(frame, authorization, CancellationToken.None);
        Assert.Equal(AttentionSheetKind.Photograph, photograph!.Metadata.Kind);
    }

    [Fact]
    public void Halving_AveragesEachTwoByTwoBlock_AndDropsAnOddEdge()
    {
        var pixels = new byte[3 * 2 * 4];
        void Set(int x, int y, byte value) => pixels.AsSpan(((y * 3) + x) * 4, 4).Fill(value);
        Set(0, 0, 10);
        Set(1, 0, 20);
        Set(0, 1, 30);
        Set(1, 1, 43);
        Set(2, 0, 255);
        Set(2, 1, 255);
        using var source = new OwnedBgra32Buffer(3, 2, pixels);

        using var halved = VisualObservationPipeline.Halve(source);

        Assert.Equal((1, 1), (halved.Width, halved.Height));
        // (10 + 20 + 30 + 43) / 4 = 25.75, rounded to the nearest: 26.
        Assert.Equal(new byte[] { 26, 26, 26, 26 }, halved.ReadOnlySpan.ToArray());
    }

    [Fact]
    public void TheEncoder_ReportsAnImageThatCannotFit_InsteadOfThrowing()
    {
        var noise = new byte[2048 * 1536 * 4];
        new Random(7).NextBytes(noise);
        using var canvas = new OwnedBgra32Buffer(2048, 1536, noise);

        Assert.Null(PngEncoder.TryEncode(canvas));
        Assert.Throws<InvalidOperationException>(() => PngEncoder.Encode(canvas));
    }

    [Fact]
    public async Task RequestPhotograph_RequiresTheActiveGrantOfARunningWorker_AndProducesOnePhotograph()
    {
        await using var source = new ControllableCaptureSource();
        await using var engine = new CaptureWorkerEngine(source);
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var kinds = new List<AttentionSheetKind>();
        engine.AttentionSheetProduced += (_, sheet) =>
        {
            lock (kinds)
            {
                kinds.Add(sheet.Metadata.Kind);
            }

            sheet.Dispose();
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RequestPhotographAsync(authorization, CancellationToken.None));

        await engine.StartAsync(authorization, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RequestPhotographAsync(CaptureWorkerTestSupport.CreateAuthorization(generation: 8), CancellationToken.None));
        Assert.False(engine.PhotographPending);

        await engine.RequestPhotographAsync(authorization, CancellationToken.None);
        Assert.True(engine.PhotographPending);
        source.Emit(CaptureWorkerTestSupport.CreatePixelFrame(320, 180, 10));
        await CaptureWorkerTestSupport.WaitUntilAsync(() => engine.PhotographSheets == 1);
        await CaptureWorkerTestSupport.WaitUntilAsync(() => { lock (kinds) { return kinds.Count == 1; } });
        Assert.Equal([AttentionSheetKind.Photograph], kinds);

        // Stopping disarms any request; a later grant must ask again.
        await engine.RequestPhotographAsync(authorization, CancellationToken.None);
        await engine.StopAndClearAsync(CancellationToken.None);
        Assert.False(engine.PhotographPending);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RequestPhotographAsync(authorization, CancellationToken.None));
    }

    [Fact]
    public void RequestPhotographCommand_RequiresAuthorizationAndNoOtherFields()
    {
        var valid = new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.RequestPhotograph,
            CorrelationId = Guid.NewGuid(),
            ControlSequence = 1,
            Authorization = CaptureWorkerTestSupport.CreateAuthorization(),
        };
        WorkerIpcHost.ValidateCommandShape(valid);

        foreach (var malformed in new[]
                 {
                     valid with { Authorization = null },
                     valid with { ManualRegion = new NormalizedRegion(0.1, 0.1, 0.2, 0.2) },
                     valid with { PayloadLength = 8 },
                     valid with { SequenceNumber = 5 },
                 })
        {
            var exception = Assert.Throws<CaptureProtocolException>(() => WorkerIpcHost.ValidateCommandShape(malformed));
            Assert.Equal(CaptureWorkerErrorCode.MalformedMessage, exception.ErrorCode);
        }
    }

    [Fact]
    public void PhotographMetadata_IsStrict()
    {
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var valid = new AttentionSheetMetadata
        {
            TargetSessionId = authorization.TargetSessionId,
            Generation = authorization.Generation,
            Target = new CaptureTargetIdentity(authorization.WindowId, authorization.ProcessId, authorization.ExecutableFileName, authorization.ExecutablePathFingerprint),
            SourceSequenceNumber = 1,
            SourceTimestamp = CaptureWorkerTestSupport.FixedTime,
            SourceWidth = 1920,
            SourceHeight = 1080,
            SheetWidth = 1920,
            SheetHeight = 1080,
            EncodedByteLength = 1024,
            Kind = AttentionSheetKind.Photograph,
            Regions = [Full(1920, 1080, 1920, 1080)],
        };
        Assert.True(valid.IsProtocolSafe());
        Assert.True((valid with { SheetWidth = 960, SheetHeight = 540, Regions = [Full(1920, 1080, 960, 540)] }).IsProtocolSafe());

        foreach (var malformed in new[]
                 {
                     // A photograph is never larger than its source.
                     valid with { SheetWidth = 2000, Regions = [Full(1920, 1080, 2000, 1080)] },
                     valid with { SheetHeight = 1200, Regions = [Full(1920, 1080, 1920, 1200)] },

                     // It is one whole-sheet full-context region.
                     valid with { Regions = [Full(1920, 1080, 1000, 1080)] },
                     valid with { Regions = [Full(1920, 1080, 1920, 1080), Center(1920, 1080)] },

                     // It never exceeds the keepsake camera's source edge.
                     valid with { SourceWidth = 8200, SourceHeight = 1000, SheetWidth = 8200, SheetHeight = 1000, Regions = [Full(8200, 1000, 8200, 1000)] },
                 })
        {
            Assert.False(malformed.IsProtocolSafe());
        }
    }

    private static AttentionSheetRegionMetadata Full(int sourceWidth, int sourceHeight, int sheetWidth, int sheetHeight) => new()
    {
        Kind = AttentionRegionKind.FullContext,
        NormalizedSource = new NormalizedRegion(0, 0, 1, 1),
        SourcePixels = new PixelRect(0, 0, sourceWidth, sourceHeight),
        SheetPixels = new PixelRect(0, 0, sheetWidth, sheetHeight),
    };

    private static AttentionSheetRegionMetadata Center(int sourceWidth, int sourceHeight) => new()
    {
        Kind = AttentionRegionKind.CenterEnvironment,
        NormalizedSource = new NormalizedRegion(0.25, 0.25, 0.5, 0.5),
        SourcePixels = new PixelRect(sourceWidth / 4, sourceHeight / 4, sourceWidth / 2, sourceHeight / 2),
        SheetPixels = new PixelRect(0, 0, 10, 10),
    };

    private static CaptureSourceFrame CreateFrame(long sequence, int pattern, int width = 320, int height = 180)
    {
        var frame = CaptureWorkerTestSupport.CreatePixelFrame(width, height, pattern);
        frame.AssignSequence(sequence);
        return frame;
    }

    private sealed class NoisePixelResource(int width, int height) : ICapturePixelSource
    {
        public ValueTask<OwnedBgra32Buffer> CopyPixelsAsync(CancellationToken cancellationToken)
        {
            var pixels = new byte[checked(width * height * 4)];
            new Random(11).NextBytes(pixels);
            return ValueTask.FromResult(new OwnedBgra32Buffer(width, height, pixels));
        }

        public void Dispose()
        {
        }
    }
}
