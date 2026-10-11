using System.Diagnostics;
using CompanionCore.Capture.Client;
using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

/// <summary>
/// VIS-01 — the Sniff: every changed frame is read locally, before any sheet work, into a
/// pixel-free signal saying where the change is, how spread out it is, and whether the
/// whole scene cut. Sheets follow the attention-set spacing unless a sniff triggers.
/// </summary>
public sealed class SniffTests
{
    private static readonly NormalizedRegion DialogueBox = new(0.30, 0.80, 0.40, 0.15);

    [Fact]
    public async Task AChangeInTheDialogueBand_IsSniffedThere_AndTriggersAnAimedSheet()
    {
        using var pipeline = new VisualObservationPipeline();
        pipeline.SetSheetSpacing(TimeSpan.FromSeconds(1));
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var sniffs = new List<SniffSignal>();

        using (var first = Frame(1, 0, patch: null))
        using (var orientation = await pipeline.ProcessAsync(first, authorization, sniffs.Add, CancellationToken.None))
        {
            Assert.Equal(AttentionSheetKind.Orientation, orientation!.Metadata.Kind);
        }

        // The orientation frame is never sniffed: a first look is not a change.
        Assert.Empty(sniffs);

        using (var second = Frame(2, 100, patch: DialogueBox))
        using (var sheet = await pipeline.ProcessAsync(second, authorization, sniffs.Add, CancellationToken.None))
        {
            var sniff = Assert.Single(sniffs);
            Assert.True(sniff.IsProtocolSafe());
            Assert.Equal(AttentionRegionKind.LowerDialogueInventory, sniff.HotRegionKind);
            Assert.True(sniff.HotRegion.Y >= 0.66, $"hot region {sniff.HotRegion}");
            Assert.True(sniff.HotChange > sniff.Change);
            Assert.True(sniff.IsTrigger);
            Assert.False(sniff.SceneCut);
            Assert.InRange(sniff.Motion, 0.01, 0.3);
            Assert.Equal(2, sniff.SourceSequenceNumber);
            Assert.Equal((320, 180, 320L * 180 * 4), (sniff.SourceWidth, sniff.SourceHeight, sniff.SourceAccountedBytes));

            // Inside the sheet spacing, the trigger still composes a sheet at once, aimed at the change.
            Assert.NotNull(sheet);
            Assert.Equal(AttentionSheetKind.Regional, sheet.Metadata.Kind);
            // The hot cells (columns 2–5, rows 4–5) padded by half a cell — not the default layout box.
            var aimed = Assert.Single(sheet.Metadata.Regions, region => region.Kind == AttentionRegionKind.LowerDialogueInventory);
            Assert.Equal(1.5 / SniffSignal.GridColumns, aimed.NormalizedSource.X, 6);
            Assert.Equal(5d / SniffSignal.GridColumns, aimed.NormalizedSource.Width, 6);
            Assert.Equal(3.5 / SniffSignal.GridRows, aimed.NormalizedSource.Y, 6);
            Assert.True(sheet.Metadata.IsProtocolSafe());
        }
    }

    [Fact]
    public async Task AWholeScreenChange_IsASceneCut()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var sniffs = new List<SniffSignal>();
        using (var first = Frame(1, 0, patch: null))
        using (await pipeline.ProcessAsync(first, authorization, sniffs.Add, CancellationToken.None))
        {
        }

        using (var cut = Frame(2, 100, patch: null, baseShade: 220))
        using (await pipeline.ProcessAsync(cut, authorization, sniffs.Add, CancellationToken.None))
        {
        }

        var sniff = Assert.Single(sniffs);
        Assert.True(sniff.SceneCut);
        Assert.True(sniff.Motion >= SniffSignal.SceneCutMotion);
        Assert.True(sniff.IsTrigger);
    }

    [Fact]
    public async Task AStrongChangeOverHalfTheScreen_IsNotASceneCut()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var sniffs = new List<SniffSignal>();
        using (var first = Frame(1, 0, patch: null, baseShade: 20))
        using (await pipeline.ProcessAsync(first, authorization, sniffs.Add, CancellationToken.None))
        {
        }

        // The whole left half changes brightly: plenty of global change, but not the whole scene.
        using (var half = Frame(2, 100, patch: new NormalizedRegion(0, 0, 0.5, 1), baseShade: 20, patchShade: 250))
        using (await pipeline.ProcessAsync(half, authorization, sniffs.Add, CancellationToken.None))
        {
        }

        var sniff = Assert.Single(sniffs);
        Assert.True(sniff.Change >= SniffSignal.SceneCutChange, $"change {sniff.Change}");
        Assert.True(sniff.Motion < SniffSignal.SceneCutMotion);
        Assert.False(sniff.SceneCut);
        Assert.True(sniff.IsTrigger);
    }

    [Fact]
    public async Task AReArmedOrientation_IsStillSniffed()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var sniffs = new List<SniffSignal>();
        using (var first = Frame(1, 0, patch: null))
        using (await pipeline.ProcessAsync(first, authorization, sniffs.Add, CancellationToken.None))
        {
        }

        pipeline.RequestOrientation();
        using var changed = Frame(2, 100, patch: DialogueBox);
        using var orientation = await pipeline.ProcessAsync(changed, authorization, sniffs.Add, CancellationToken.None);
        Assert.Equal(AttentionSheetKind.Orientation, orientation!.Metadata.Kind);
        Assert.Equal(AttentionRegionKind.LowerDialogueInventory, Assert.Single(sniffs).HotRegionKind);
    }

    [Fact]
    public async Task AWeakChange_IsSniffedEveryFrame_ButSheetsFollowTheSpacing()
    {
        using var pipeline = new VisualObservationPipeline();
        pipeline.SetSheetSpacing(TimeSpan.FromSeconds(1));
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var sniffs = new List<SniffSignal>();
        using (var first = Frame(1, 0, patch: null, baseShade: 70))
        using (await pipeline.ProcessAsync(first, authorization, sniffs.Add, CancellationToken.None))
        {
        }

        // One grid cell flickers gently. On its own the whole-frame change is below the
        // near-duplicate threshold, but the cell moved, so every frame is sniffed.
        var cell = new NormalizedRegion(3d / SniffSignal.GridColumns, 2d / SniffSignal.GridRows, 1d / SniffSignal.GridColumns, 1d / SniffSignal.GridRows);
        var sheets = 0;
        for (var index = 0; index < 4; index++)
        {
            using var frame = Frame(index + 2, 100 * (index + 1), patch: cell, baseShade: 70, patchShade: index % 2 == 0 ? (byte)90 : (byte)70);
            using var sheet = await pipeline.ProcessAsync(frame, authorization, sniffs.Add, CancellationToken.None);
            sheets += sheet is null ? 0 : 1;
        }

        Assert.Equal(4, sniffs.Count);
        Assert.All(sniffs, sniff =>
        {
            Assert.False(sniff.IsTrigger);
            Assert.Equal((2 * SniffSignal.GridColumns) + 3, sniff.HotCell);
            Assert.True(sniff.Change <= FrameChangeDetector.NearDuplicateThreshold);
        });

        // At most one regional sheet lands inside one second of spacing.
        Assert.Equal(1, sheets);
        var metrics = pipeline.Snapshot();
        Assert.Equal(4, metrics.Sniffs);
        Assert.Equal(3, metrics.SpacedSheets);

        // Still inside that second, a trigger-level change breaks through the spacing.
        using var strong = Frame(6, 500, patch: DialogueBox, baseShade: 70);
        using var triggered = await pipeline.ProcessAsync(strong, authorization, sniffs.Add, CancellationToken.None);
        Assert.True(sniffs[^1].IsTrigger);
        Assert.NotNull(triggered);
        Assert.Equal(AttentionSheetKind.Regional, triggered.Metadata.Kind);
    }

    [Fact]
    public async Task DuplicateFrames_AreNeverSniffed()
    {
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var sniffs = new List<SniffSignal>();
        for (var sequence = 1; sequence <= 3; sequence++)
        {
            using var frame = Frame(sequence, sequence * 100, patch: null);
            using (await pipeline.ProcessAsync(frame, authorization, sniffs.Add, CancellationToken.None))
            {
            }
        }

        Assert.Empty(sniffs);
    }

    [Fact]
    public async Task AFourKFrame_IsSniffedAndSheeted_WithinItsComputeBudget()
    {
        // Before VIS-01 a 3840×2160 frame (33.2 MB) exceeded the 32 MB copy bound and was dropped.
        using var pipeline = new VisualObservationPipeline();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var sniffs = new List<SniffSignal>();
        using (var first = Frame(1, 0, patch: null, width: 3840, height: 2160))
        using (var orientation = await pipeline.ProcessAsync(first, authorization, sniffs.Add, CancellationToken.None))
        {
            Assert.NotNull(orientation);
        }

        var durations = new List<double>();
        for (var index = 0; index < 5; index++)
        {
            using var frame = Frame(index + 2, 100 * (index + 1), patch: DialogueBox, width: 3840, height: 2160, patchShade: (byte)(140 + (index % 2 * 40)));
            var stopwatch = Stopwatch.StartNew();
            SniffSignal? sniffed = null;
            using var sheet = await pipeline.ProcessAsync(frame, authorization, sniff =>
            {
                sniffed = sniff;
                durations.Add(stopwatch.Elapsed.TotalMilliseconds);
            }, CancellationToken.None);
            Assert.NotNull(sniffed);
            Assert.Equal(3840, sniffed.SourceWidth);
        }

        durations.Sort();
        var median = durations[durations.Count / 2];
        Assert.True(median <= 50, $"Median 4K sniff took {median:F1} ms.");
        Assert.Equal(5, durations.Count);
    }

    [Fact]
    public void SniffSignals_AreStrictlyBounded()
    {
        var good = Valid();
        Assert.True(good.IsProtocolSafe());
        Assert.False((good with { HotChange = 1.5 }).IsProtocolSafe());
        Assert.False((good with { Change = double.NaN }).IsProtocolSafe());
        Assert.False((good with { HotChange = 0.1, Change = 0.2 }).IsProtocolSafe());
        Assert.False((good with { HotCell = 48 }).IsProtocolSafe());
        Assert.False((good with { HotRegion = new NormalizedRegion(0.9, 0.9, 0.2, 0.2) }).IsProtocolSafe());
        Assert.False((good with { HotRegionKind = AttentionRegionKind.FullContext }).IsProtocolSafe());
        Assert.False((good with { SourceAccountedBytes = CaptureWorkerMetrics.ScreenshotBudgetBytes + 1 }).IsProtocolSafe());
        Assert.False((good with { ComputeMicroseconds = -1 }).IsProtocolSafe());
        Assert.False((good with { Target = null }).IsProtocolSafe());
        Assert.False((good with { SourceSequenceNumber = 0 }).IsProtocolSafe());
    }

    [Fact]
    public async Task SniffAndCadenceMessages_RoundTrip_AndAreShapeChecked()
    {
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var sniff = Valid();
        var notification = new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.SniffProduced,
            Authorization = authorization,
            SequenceNumber = sniff.SourceSequenceNumber,
            Sniff = sniff,
        };
        using var stream = new MemoryStream();
        await CaptureIpcProtocol.WriteAsync(stream, notification, CancellationToken.None);
        stream.Position = 0;
        var read = await CaptureIpcProtocol.ReadAsync(stream, CancellationToken.None);
        Assert.Equal(sniff, read.Sniff);
        OutOfProcessCaptureWorker.ValidateSniffNotificationShape(read);

        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSniffNotificationShape(read with { SequenceNumber = 99 }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSniffNotificationShape(read with { Sniff = sniff with { HotChange = 2 } }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSniffNotificationShape(read with { Cadence = CaptureCadence.Noticing }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSniffNotificationShape(read with { Authorization = authorization with { Generation = 8 } }));

        var command = new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.SetCadence,
            CorrelationId = Guid.NewGuid(),
            ControlSequence = 1,
            Authorization = authorization,
            Cadence = CaptureCadence.BnuyMode,
        };
        WorkerIpcHost.ValidateCommandShape(command);
        Assert.Throws<CaptureProtocolException>(() => WorkerIpcHost.ValidateCommandShape(command with { Cadence = null }));
        Assert.Throws<CaptureProtocolException>(() => WorkerIpcHost.ValidateCommandShape(command with { Authorization = null }));
        Assert.Throws<CaptureProtocolException>(() => WorkerIpcHost.ValidateCommandShape(command with
        {
            Cadence = new CaptureCadence { FrameSpacing = TimeSpan.FromMilliseconds(10), SheetSpacing = TimeSpan.FromSeconds(1) },
        }));
        Assert.Throws<CaptureProtocolException>(() => WorkerIpcHost.ValidateCommandShape(command with { Sniff = sniff }));
        Assert.Throws<CaptureProtocolException>(() => WorkerIpcHost.ValidateCommandShape(new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.GetMetrics,
            CorrelationId = Guid.NewGuid(),
            ControlSequence = 1,
            Cadence = CaptureCadence.Noticing,
        }));
    }

    [Fact]
    public void TheAttentionCadences_AreValid_AndFasterWithAttention()
    {
        foreach (var cadence in new[] { CaptureCadence.Noticing, CaptureCadence.Engaged, CaptureCadence.BnuyMode })
        {
            Assert.True(cadence.IsValid);
        }

        Assert.True(CaptureCadence.BnuyMode.FrameSpacing < CaptureCadence.Engaged.FrameSpacing);
        Assert.True(CaptureCadence.Engaged.FrameSpacing < CaptureCadence.Noticing.FrameSpacing);
        Assert.False(new CaptureCadence { FrameSpacing = TimeSpan.FromSeconds(1), SheetSpacing = TimeSpan.FromMilliseconds(500) }.IsValid);
    }

    [Fact]
    public async Task TheEngine_AppliesACadenceForTheActiveGrantOnly_AndResetsItOnStart()
    {
        await using var source = new ControllableCaptureSource();
        await using var engine = new CaptureWorkerEngine(source, minimumFrameSpacing: CaptureCadence.Noticing.FrameSpacing, sheetSpacing: CaptureCadence.Noticing.SheetSpacing);
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        await engine.StartAsync(authorization, CancellationToken.None);

        await engine.SetCadenceAsync(authorization, CaptureCadence.BnuyMode, CancellationToken.None);
        Assert.Equal(CaptureCadence.BnuyMode.FrameSpacing, engine.FrameSpacing);
        Assert.Equal(CaptureCadence.BnuyMode.SheetSpacing, engine.SheetSpacing);
        var metrics = engine.GetMetrics();
        Assert.Equal(CaptureCadence.BnuyMode.FrameSpacing, metrics.FrameSpacing);
        Assert.True(metrics.IsProtocolSafe());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.SetCadenceAsync(CaptureWorkerTestSupport.CreateAuthorization(generation: 8), CaptureCadence.Noticing, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            engine.SetCadenceAsync(authorization, new CaptureCadence(), CancellationToken.None));

        await engine.StopAndClearAsync(CancellationToken.None);
        await engine.StartAsync(authorization, CancellationToken.None);
        Assert.Equal(CaptureCadence.Noticing.FrameSpacing, engine.FrameSpacing);
        Assert.Equal(CaptureCadence.Noticing.SheetSpacing, engine.SheetSpacing);
        await engine.StopAndClearAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.SetCadenceAsync(authorization, CaptureCadence.Engaged, CancellationToken.None));
    }

    [Fact]
    public async Task TheEngine_RaisesSniffsForItsActiveGrant()
    {
        await using var source = new ControllableCaptureSource();
        await using var engine = new CaptureWorkerEngine(source);
        var sniffs = new List<SniffSignal>();
        engine.SniffProduced += (_, sniff) =>
        {
            lock (sniffs)
            {
                sniffs.Add(sniff);
            }
        };
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        await engine.StartAsync(authorization, CancellationToken.None);
        source.Emit(PatchFrame(320, 180, CaptureWorkerTestSupport.FixedTime, null));
        source.Emit(PatchFrame(320, 180, CaptureWorkerTestSupport.FixedTime.AddMilliseconds(300), DialogueBox));
        await CaptureWorkerTestSupport.WaitUntilAsync(() =>
        {
            lock (sniffs)
            {
                return sniffs.Count == 1;
            }
        });

        var sniff = sniffs[0];
        Assert.Equal(authorization.TargetSessionId, sniff.TargetSessionId);
        Assert.Equal(authorization.Generation, sniff.Generation);
        Assert.InRange(sniff.ComputeMicroseconds, 1, SniffSignal.MaximumComputeMicroseconds);
        var metrics = engine.GetMetrics();
        Assert.Equal(1, metrics.Sniffs);
        Assert.True(metrics.MaximumSniffMicroseconds >= metrics.LastSniffMicroseconds);
        await engine.StopAndClearAsync(CancellationToken.None);
    }

    internal static SniffSignal Valid() => new()
    {
        TargetSessionId = CaptureWorkerTestSupport.CreateAuthorization().TargetSessionId,
        Generation = CaptureWorkerTestSupport.CreateAuthorization().Generation,
        Target = CaptureWorkerTestSupport.CreateGrant().Target,
        SourceSequenceNumber = 4,
        SourceTimestamp = CaptureWorkerTestSupport.FixedTime,
        SourceWidth = 1920,
        SourceHeight = 1080,
        SourceAccountedBytes = 1920L * 1080 * 4,
        Change = 0.05,
        HotChange = 0.4,
        Motion = 0.1,
        HotRegion = new NormalizedRegion(0.25, 0.5, 0.25, 1d / 3),
        HotRegionKind = AttentionRegionKind.LowerDialogueInventory,
        HotCell = 34,
        ComputeMicroseconds = 1200,
    };

    private static CaptureSourceFrame Frame(
        long sequence,
        int milliseconds,
        NormalizedRegion? patch,
        byte baseShade = 40,
        byte patchShade = 230,
        int width = 320,
        int height = 180)
    {
        var frame = PatchFrame(width, height, CaptureWorkerTestSupport.FixedTime.AddMilliseconds(milliseconds), patch, baseShade, patchShade);
        frame.AssignSequence(sequence);
        frame.MarkArrived();
        return frame;
    }

    private static CaptureSourceFrame PatchFrame(
        int width,
        int height,
        DateTimeOffset timestamp,
        NormalizedRegion? patch,
        byte baseShade = 40,
        byte patchShade = 230) =>
        new(timestamp, width, height, (long)width * height * 4, new PatchPixelResource(width, height, baseShade, patch, patchShade));

    /// <summary>A uniform frame with one optional rectangle of a different shade.</summary>
    private sealed class PatchPixelResource(int width, int height, byte baseShade, NormalizedRegion? patch, byte patchShade) : ICapturePixelSource
    {
        public ValueTask<OwnedBgra32Buffer> CopyPixelsAsync(CancellationToken cancellationToken)
        {
            // Pooled like the real readback, so repeated 4K frames do not churn the collector.
            var buffer = OwnedBgra32Buffer.Allocate(width, height);
            var pixels = buffer.Span;
            pixels.Fill(baseShade);
            if (patch is { } region)
            {
                var left = (int)(region.X * width);
                var top = (int)(region.Y * height);
                var right = (int)((region.X + region.Width) * width);
                var bottom = (int)((region.Y + region.Height) * height);
                for (var y = top; y < bottom; y++)
                {
                    pixels.Slice((y * width + left) * 4, (right - left) * 4).Fill(patchShade);
                }
            }

            return ValueTask.FromResult(buffer);
        }

        public void Dispose()
        {
        }
    }
}

public sealed class PixelBufferPoolTests
{
    [Fact]
    public void APooledBuffer_IsZeroedBeforeItIsReused()
    {
        // An odd geometry no other test uses, so the pool's reuse is observable here.
        const int width = 97;
        const int height = 13;
        var first = OwnedBgra32Buffer.Allocate(width, height);
        first.Span.Fill(0xAB);
        first.Dispose();

        // Whether or not the same array comes back, no earlier pixel survives.
        using var second = OwnedBgra32Buffer.Allocate(width, height);
        Assert.All(second.Span.ToArray(), value => Assert.Equal(0, value));
        Assert.InRange(PixelBufferPool.RetainedBytes, 0, PixelBufferPool.MaximumRetainedBytes);
    }

    [Fact]
    public void ThePool_KeepsAtMostAFewArraysPerLength()
    {
        const int length = 4 * 31 * 7;
        var arrays = Enumerable.Range(0, 10).Select(_ => new byte[length]).ToArray();
        foreach (var array in arrays)
        {
            PixelBufferPool.Return(array);
        }

        var reused = Enumerable.Range(0, 10).Select(_ => PixelBufferPool.Rent(length)).ToArray();
        Assert.InRange(reused.Count(rented => arrays.Contains(rented)), 1, PixelBufferPool.MaximumPerLength);
    }
}
