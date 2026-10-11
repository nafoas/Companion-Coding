using System.Text.Json;
using CompanionCore.Capture.Client;
using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;
using static CompanionCore.Capture.Worker.Tests.SniffTests;

namespace CompanionCore.Capture.Worker.Tests;

/// <summary>
/// VIS-03 — the Text Sniff: text-likely regions are read with the local recognizer, off
/// the frame path, and only "the words changed" leaves the reader — never the words.
/// </summary>
public sealed class TextSniffTests
{
    private static readonly NormalizedRegion DialogueBox = new(0.30, 0.80, 0.40, 0.15);

    /// <summary>One dialogue-band grid cell (row 5, column 3).</summary>
    private static readonly NormalizedRegion DialogueCell = new(3d / SniffSignal.GridColumns, 5d / SniffSignal.GridRows, 1d / SniffSignal.GridColumns, 1d / SniffSignal.GridRows);

    private static readonly int DialogueCellIndex = (5 * SniffSignal.GridColumns) + 3;

    private static readonly int UpperLeftCellIndex = 0;

    private static readonly int CenterCellIndex = (2 * SniffSignal.GridColumns) + 3;

    [Fact]
    public async Task ADialogueChange_IsRead_AndNewWordsAreReportedAsCountsOnly()
    {
        var recognizer = new ScriptedRecognizer { Words = ["Hello there, Prince!"] };
        using var pipeline = new VisualObservationPipeline(recognizer);
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var texts = new List<TextSniffSignal>();

        using (var first = Frame(1, 0, patch: null))
        using (await pipeline.ProcessAsync(first, authorization, null, texts.Add, CancellationToken.None))
        {
        }

        // A first look has nothing to compare against: nothing is read yet.
        await pipeline.Text.DrainAsync();
        Assert.Equal(0, recognizer.Reads);

        using (var second = Frame(2, 100, patch: DialogueBox))
        using (await pipeline.ProcessAsync(second, authorization, null, texts.Add, CancellationToken.None))
        {
        }

        await pipeline.Text.DrainAsync();
        var text = Assert.Single(texts);
        Assert.True(text.IsProtocolSafe());
        Assert.Equal(AttentionRegionKind.LowerDialogueInventory, text.RegionKind);
        Assert.Equal((1, "hello there, prince!".Length), (text.Lines, text.Characters));
        Assert.True(text.NewText);
        Assert.Equal((authorization.TargetSessionId, authorization.Generation, 2L), (text.TargetSessionId, text.Generation, text.SourceSequenceNumber));
        Assert.Equal((320, 180, 320L * 180 * 4), (text.SourceWidth, text.SourceHeight, text.SourceAccountedBytes));

        // The recognizer saw the whole dialogue band, cropped from the frame.
        var crop = Assert.Single(recognizer.Crops);
        var expected = VisualRegionLayout.MapToPixels(TextSniffer.TextLikelyRegions.First(region => region.Kind == AttentionRegionKind.LowerDialogueInventory).Bounds, 320, 180);
        Assert.Equal((expected.Width, expected.Height), crop);
    }

    [Fact]
    public async Task Changed_FollowsTheWords_NotThePixels()
    {
        var recognizer = new ScriptedRecognizer { Words = ["Press E to talk"] };
        using var sniffer = new TextSniffer(recognizer);
        var texts = new List<TextSniffSignal>();
        using var pixels = OwnedBgra32Buffer.Allocate(320, 180);
        var moved = Changes((DialogueCellIndex, 0.3));

        async Task ReadAt(int milliseconds)
        {
            Assert.True(sniffer.TryStart(pixels, moved, Source(milliseconds), texts.Add));
            await sniffer.DrainAsync();
        }

        await ReadAt(0);
        Assert.Single(texts);

        // The pixels moved again but the words are the same (even re-spaced and re-cased): no signal.
        recognizer.Words = ["press  E to   TALK"];
        await ReadAt(600);
        Assert.Single(texts);
        Assert.Equal(2, recognizer.Reads);

        // Different words: a change.
        recognizer.Words = ["The door is locked."];
        await ReadAt(1200);
        Assert.Equal(2, texts.Count);
        Assert.True(texts[^1].NewText);

        // The words vanished: a change, but not new text.
        recognizer.Words = [];
        await ReadAt(1800);
        Assert.Equal(3, texts.Count);
        Assert.Equal((0, 0, true, false), (texts[^1].Lines, texts[^1].Characters, texts[^1].TextChanged, texts[^1].NewText));

        // Still nothing: no signal.
        await ReadAt(2400);
        Assert.Equal(3, texts.Count);
        var (reads, changes, last, maximum) = sniffer.Snapshot();
        Assert.Equal((5L, 3L), (reads, changes));
        Assert.True(maximum >= last);
    }

    [Fact]
    public void Regions_AreReadWhenTheyMove_AtMostOncePerSpacing_AndRefreshedWhenQuiet()
    {
        using var sniffer = new TextSniffer(new ScriptedRecognizer());
        var dialogue = Changes((DialogueCellIndex, 0.04));
        var quiet = Changes();
        var t0 = CaptureWorkerTestSupport.FixedTime;

        // The moved region first; then never-read regions fill in, one per frame.
        Assert.Equal(AttentionRegionKind.LowerDialogueInventory, sniffer.TakeDue(t0, dialogue)?.Kind);
        Assert.Equal(AttentionRegionKind.UpperLeft, sniffer.TakeDue(t0.AddMilliseconds(100), dialogue)?.Kind);
        Assert.Equal(AttentionRegionKind.UpperRight, sniffer.TakeDue(t0.AddMilliseconds(200), dialogue)?.Kind);

        // Every region is inside its spacing.
        Assert.Null(sniffer.TakeDue(t0.AddMilliseconds(300), dialogue));
        Assert.Null(sniffer.TakeDue(t0.AddMilliseconds(499), dialogue));

        // The dialogue band moved again after its spacing.
        Assert.Equal(AttentionRegionKind.LowerDialogueInventory, sniffer.TakeDue(t0.AddMilliseconds(500), dialogue)?.Kind);

        // A change below the text threshold, or one elsewhere, reads nothing until a refresh is due.
        Assert.Null(sniffer.TakeDue(t0.AddMilliseconds(1100), Changes((DialogueCellIndex, TextSniffer.TextCellChange / 2))));
        Assert.Null(sniffer.TakeDue(t0.AddMilliseconds(1200), Changes((CenterCellIndex, 0.9))));
        Assert.Null(sniffer.TakeDue(t0.AddMilliseconds(2000), quiet));

        // Quiet regions are refreshed after two seconds, the longest unread first.
        Assert.Equal(AttentionRegionKind.UpperLeft, sniffer.TakeDue(t0.AddMilliseconds(2100), quiet)?.Kind);
        Assert.Equal(AttentionRegionKind.UpperRight, sniffer.TakeDue(t0.AddMilliseconds(2200), quiet)?.Kind);
        Assert.Equal(AttentionRegionKind.LowerDialogueInventory, sniffer.TakeDue(t0.AddMilliseconds(2500), quiet)?.Kind);

        // The most-moved region wins over a less-moved one.
        Assert.Equal(AttentionRegionKind.UpperLeft, sniffer.TakeDue(
            t0.AddMilliseconds(3100),
            Changes((DialogueCellIndex, 0.05), (UpperLeftCellIndex, 0.2)))?.Kind);
    }

    [Fact]
    public async Task ASubtitleSwapTooSmallToSniff_IsStillRead()
    {
        var recognizer = new ScriptedRecognizer { Words = ["Line one"] };
        using var pipeline = new VisualObservationPipeline(recognizer);
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var sniffs = new List<SniffSignal>();
        var texts = new List<TextSniffSignal>();
        using (var first = Frame(1, 0, patch: DialogueCell, patchShade: 40))
        using (await pipeline.ProcessAsync(first, authorization, sniffs.Add, texts.Add, CancellationToken.None))
        {
        }

        await pipeline.Text.DrainAsync();

        // One dialogue cell shifts by a few shades: a near-duplicate frame, so no Sniff and no sheet …
        using (var swap = Frame(2, 100, patch: DialogueCell, patchShade: 50))
        using (var sheet = await pipeline.ProcessAsync(swap, authorization, sniffs.Add, texts.Add, CancellationToken.None))
        {
            Assert.Null(sheet);
        }

        await pipeline.Text.DrainAsync();
        Assert.Empty(sniffs);
        Assert.Equal(1, pipeline.Snapshot().DuplicateFrames);

        // … but the dialogue band is read, and the new line is noticed.
        var text = Assert.Single(texts);
        Assert.Equal(AttentionRegionKind.LowerDialogueInventory, text.RegionKind);
        Assert.True(text.NewText);
    }

    [Fact]
    public async Task OneReadAtATime_OffTheFramePath_AndAResetSilencesTheReadInFlight()
    {
        var recognizer = new ScriptedRecognizer { Words = ["Secret words"], Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var sniffer = new TextSniffer(recognizer);
        using var pixels = OwnedBgra32Buffer.Allocate(320, 180);
        var texts = new List<TextSniffSignal>();

        // The frame only lends a crop: TryStart returns while the read waits.
        Assert.True(sniffer.TryStart(pixels, Changes((DialogueCellIndex, 0.3)), Source(0), texts.Add));
        Assert.False(sniffer.TryStart(pixels, Changes((UpperLeftCellIndex, 0.3)), Source(100), texts.Add));

        sniffer.Reset();
        recognizer.Gate.SetResult();
        await sniffer.DrainAsync();
        Assert.Empty(texts);
        Assert.Equal((0L, 0L), (sniffer.Snapshot().Reads, sniffer.Snapshot().Changes));

        // After the reset the same words are new again.
        recognizer.Gate = null;
        Assert.True(sniffer.TryStart(pixels, Changes((DialogueCellIndex, 0.3)), Source(200), texts.Add));
        await sniffer.DrainAsync();
        Assert.Single(texts);
    }

    [Fact]
    public async Task AFailingRecognizer_ReportsNothing_AndTheNextFrameCarriesOn()
    {
        var recognizer = new ScriptedRecognizer { Words = ["ok"], Throw = true };
        using var sniffer = new TextSniffer(recognizer);
        using var pixels = OwnedBgra32Buffer.Allocate(320, 180);
        var texts = new List<TextSniffSignal>();
        Assert.True(sniffer.TryStart(pixels, Changes((DialogueCellIndex, 0.3)), Source(0), texts.Add));
        await sniffer.DrainAsync();
        Assert.Empty(texts);

        recognizer.Throw = false;
        Assert.True(sniffer.TryStart(pixels, Changes((DialogueCellIndex, 0.3)), Source(600), texts.Add));
        await sniffer.DrainAsync();
        Assert.Single(texts);
    }

    [Fact]
    public async Task NoRecognizer_ReadsNothing()
    {
        using var pipeline = new VisualObservationPipeline();
        var texts = new List<TextSniffSignal>();
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        for (var index = 0; index < 3; index++)
        {
            using var frame = Frame(index + 1, index * 600, patch: index % 2 == 0 ? null : DialogueBox);
            using (await pipeline.ProcessAsync(frame, authorization, null, texts.Add, CancellationToken.None))
            {
            }
        }

        await pipeline.Text.DrainAsync();
        Assert.Empty(texts);
        Assert.False(pipeline.Text.IsAvailable);
        Assert.Null(pipeline.Text.TakeDue(CaptureWorkerTestSupport.FixedTime, Changes((DialogueCellIndex, 0.5))));
    }

    [Fact]
    public async Task NoRecognizedWord_IsReachableFromAnySignalOrMetric()
    {
        var recognizer = new ScriptedRecognizer { Words = ["Zanzibar quokka"] };
        await using var source = new ControllableCaptureSource();
        await using var engine = new CaptureWorkerEngine(source, textRecognizer: recognizer);
        var texts = new List<TextSniffSignal>();
        engine.TextSniffProduced += (_, text) =>
        {
            lock (texts)
            {
                texts.Add(text);
            }
        };
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        await engine.StartAsync(authorization, CancellationToken.None);
        source.Emit(PatchFrame(320, 180, CaptureWorkerTestSupport.FixedTime, null));
        source.Emit(PatchFrame(320, 180, CaptureWorkerTestSupport.FixedTime.AddMilliseconds(300), DialogueBox));
        await CaptureWorkerTestSupport.WaitUntilAsync(() =>
        {
            lock (texts)
            {
                return texts.Count == 1;
            }
        });

        var metrics = engine.GetMetrics();
        Assert.True(metrics.IsProtocolSafe());
        Assert.True(metrics.TextRecognitionAvailable);
        Assert.Equal((1L, 1L), (metrics.TextReads, metrics.TextChanges));
        Assert.True(metrics.MaximumTextReadMicroseconds >= metrics.LastTextReadMicroseconds);

        // The words exist only inside the recognizer: not in the signal, its wire form, or the metrics.
        using var stream = new MemoryStream();
        await CaptureIpcProtocol.WriteAsync(stream, new CaptureIpcMessage { Kind = CaptureIpcMessageKind.TextSniffProduced, TextSniff = texts[0] }, CancellationToken.None);
        var wire = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        var everything = wire + JsonSerializer.Serialize(metrics) + JsonSerializer.Serialize(texts[0]);
        Assert.DoesNotContain("zanzibar", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quokka", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(typeof(TextSniffSignal).GetProperties(), property => property.PropertyType == typeof(string));
        await engine.StopAndClearAsync(CancellationToken.None);
    }

    [Fact]
    public void TextSniffSignals_AreStrictlyBounded()
    {
        var good = Valid();
        Assert.True(good.IsProtocolSafe());
        Assert.False((good with { Lines = TextSniffSignal.MaximumLines + 1 }).IsProtocolSafe());
        Assert.False((good with { Characters = TextSniffSignal.MaximumCharacters + 1 }).IsProtocolSafe());
        Assert.False((good with { Lines = -1 }).IsProtocolSafe());
        Assert.False((good with { Lines = 0, Characters = 5 }).IsProtocolSafe());
        Assert.True((good with { Lines = 0, Characters = 0 }).IsProtocolSafe());
        Assert.False((good with { RegionKind = AttentionRegionKind.FullContext }).IsProtocolSafe());
        Assert.False((good with { RegionKind = AttentionRegionKind.ManualFocus }).IsProtocolSafe());
        Assert.False((good with { RegionKind = (AttentionRegionKind)99 }).IsProtocolSafe());
        Assert.False((good with { Region = new NormalizedRegion(0.9, 0.9, 0.2, 0.2) }).IsProtocolSafe());
        Assert.False((good with { ComputeMicroseconds = SniffSignal.MaximumComputeMicroseconds + 1 }).IsProtocolSafe());
        Assert.False((good with { SourceAccountedBytes = CaptureWorkerMetrics.ScreenshotBudgetBytes + 1 }).IsProtocolSafe());
        Assert.False((good with { Target = null }).IsProtocolSafe());
        Assert.False((good with { SourceSequenceNumber = 0 }).IsProtocolSafe());
        Assert.False((good with { TargetSessionId = Guid.Empty }).IsProtocolSafe());
        Assert.True(good.Matches(CaptureWorkerTestSupport.CreateGrant()));
        Assert.False(good.Matches(CaptureWorkerTestSupport.CreateGrant(generation: 8)));
    }

    [Fact]
    public async Task TextRecognitionMetrics_AreBounded()
    {
        await using var source = new ControllableCaptureSource();
        await using var engine = new CaptureWorkerEngine(source);
        Assert.False(engine.GetMetrics().TextRecognitionAvailable);
        var good = engine.GetMetrics() with { TextReads = 4, TextChanges = 2, LastTextReadMicroseconds = 10, MaximumTextReadMicroseconds = 20 };
        Assert.True(good.IsProtocolSafe());
        Assert.False((good with { TextChanges = 5 }).IsProtocolSafe());
        Assert.False((good with { TextReads = -1 }).IsProtocolSafe());
        Assert.False((good with { LastTextReadMicroseconds = 30 }).IsProtocolSafe());
        Assert.False((good with { MaximumTextReadMicroseconds = SniffSignal.MaximumComputeMicroseconds + 1 }).IsProtocolSafe());
    }

    [Fact]
    public async Task TextSniffMessages_RoundTrip_AndAreShapeChecked()
    {
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var text = Valid();
        var notification = new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.TextSniffProduced,
            Authorization = authorization,
            SequenceNumber = text.SourceSequenceNumber,
            TextSniff = text,
        };
        using var stream = new MemoryStream();
        await CaptureIpcProtocol.WriteAsync(stream, notification, CancellationToken.None);
        stream.Position = 0;
        var read = await CaptureIpcProtocol.ReadAsync(stream, CancellationToken.None);
        Assert.Equal(text, read.TextSniff);
        OutOfProcessCaptureWorker.ValidateTextSniffNotificationShape(read);

        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateTextSniffNotificationShape(read with { SequenceNumber = 99 }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateTextSniffNotificationShape(read with { TextSniff = text with { Lines = 999 } }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateTextSniffNotificationShape(read with { Sniff = SniffTests.Valid() }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateTextSniffNotificationShape(read with { Cadence = CaptureCadence.Noticing }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateTextSniffNotificationShape(read with { Authorization = authorization with { Generation = 8 } }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateTextSniffNotificationShape(read with { TextSniff = null }));

        // A sniff notification may not smuggle a text sniff, nor a command carry one.
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSniffNotificationShape(new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.SniffProduced,
            Authorization = authorization,
            SequenceNumber = SniffTests.Valid().SourceSequenceNumber,
            Sniff = SniffTests.Valid(),
            TextSniff = text,
        }));
        Assert.Throws<CaptureProtocolException>(() => WorkerIpcHost.ValidateCommandShape(new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.GetMetrics,
            CorrelationId = Guid.NewGuid(),
            ControlSequence = 1,
            TextSniff = text,
        }));
    }

    [Fact]
    public async Task WindowsTextRecognition_IsAvailableAndReadsABlankCropAsNothing_OrReportsUnavailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var recognizer = WindowsOcrTextRecognizer.Create();
        if (!recognizer.IsAvailable)
        {
            // No OCR language installed: the Text Sniff stays quiet and nothing else changes.
            Assert.IsType<UnavailableTextRecognizer>(recognizer);
            return;
        }

        Assert.True(recognizer.MaximumImageDimension > 0);
        using var blank = OwnedBgra32Buffer.Allocate(1728, 346);
        blank.Span.Fill(255);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var reading = await recognizer.ReadAsync(blank, CancellationToken.None);
        started.Stop();
        Assert.NotNull(reading);
        Assert.Equal((0, 0), (reading.Lines, reading.Characters));
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(5), $"A blank read took {started.Elapsed.TotalMilliseconds:F0} ms.");
        Console.WriteLine($"Windows OCR blank 1728x346 read: {started.Elapsed.TotalMilliseconds:F1} ms");
    }

    internal static TextSniffSignal Valid() => new()
    {
        TargetSessionId = CaptureWorkerTestSupport.CreateAuthorization().TargetSessionId,
        Generation = CaptureWorkerTestSupport.CreateAuthorization().Generation,
        Target = CaptureWorkerTestSupport.CreateGrant().Target,
        SourceSequenceNumber = 4,
        SourceTimestamp = CaptureWorkerTestSupport.FixedTime,
        SourceWidth = 1920,
        SourceHeight = 1080,
        SourceAccountedBytes = 1920L * 1080 * 4,
        RegionKind = AttentionRegionKind.LowerDialogueInventory,
        Region = new NormalizedRegion(0.05, 0.65, 0.90, 0.32),
        Lines = 2,
        Characters = 40,
        TextChanged = true,
        ComputeMicroseconds = 25_000,
    };

    private static TextSniffSource Source(int milliseconds) => new(
        CaptureWorkerTestSupport.CreateAuthorization(),
        milliseconds + 1,
        CaptureWorkerTestSupport.FixedTime.AddMilliseconds(milliseconds),
        320,
        180,
        320L * 180 * 4);

    private static double[] Changes(params (int Cell, double Change)[] cells)
    {
        var changes = new double[SniffSignal.GridColumns * SniffSignal.GridRows];
        foreach (var (cell, change) in cells)
        {
            changes[cell] = change;
        }

        return changes;
    }

    /// <summary>Reads whatever words it is told to, after an optional gate.</summary>
    private sealed class ScriptedRecognizer : ITextRecognizer
    {
        private int _reads;

        public string[] Words { get; set; } = [];

        public TaskCompletionSource? Gate { get; set; }

        public bool Throw { get; set; }

        public List<(int Width, int Height)> Crops { get; } = [];

        public int Reads => Volatile.Read(ref _reads);

        public bool IsAvailable => true;

        public int MaximumImageDimension => 10_000;

        public async ValueTask<TextReading?> ReadAsync(OwnedBgra32Buffer crop, CancellationToken cancellationToken)
        {
            lock (Crops)
            {
                Crops.Add((crop.Width, crop.Height));
            }

            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            Interlocked.Increment(ref _reads);
            if (Throw)
            {
                throw new InvalidOperationException("synthetic recognizer failure");
            }

            return ITextRecognizer.Summarize(Words);
        }

        public void Dispose()
        {
        }
    }
}
