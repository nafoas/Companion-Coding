using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CompanionCore.Capture.Client;
using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;
using static CompanionCore.Capture.Worker.Tests.SniffTests;

namespace CompanionCore.Capture.Worker.Tests;

/// <summary>
/// VIS-04 — the Scene Sniff: the bundled local vision model reads the whole frame off the
/// frame path, and only "how far the scene moved" and a fixed-vocabulary label leave it.
/// </summary>
public sealed class SceneSniffTests
{
    private static readonly NormalizedRegion DialogueBox = new(0.30, 0.80, 0.40, 0.15);

    private static readonly int Dimensions = SceneLabelTable.Default.Dimensions;

    [Fact]
    public void Preprocessing_MatchesTheReferenceImplementation()
    {
        var reference = Reference.Load();
        using var pixels = Pattern(reference.Width, reference.Height);

        var tensor = ScenePreprocessor.ToTensor(pixels);

        Assert.Equal(ScenePreprocessor.TensorLength, tensor.Length);
        foreach (var (index, expected) in reference.Probes)
        {
            Assert.Equal(expected, tensor[index], 5);
        }

        Assert.Equal(reference.TensorSum, tensor.Sum(value => (double)value), 1);
        Assert.All(tensor, value => Assert.InRange(value, -1f, 1f));
    }

    [Fact]
    public void TheLabelTable_MatchesTheVocabularyAndThePinnedModel()
    {
        var table = SceneLabelTable.Default;
        Assert.Equal(SceneVocabulary.Keys.Count, table.Count);
        Assert.Equal(768, table.Dimensions);
        Assert.Equal(Reference.Load().VisionModelSha256, table.VisionModelSha256);
        Assert.Equal(4.764997, table.LogitScale, 6);
        Assert.Equal(-12.932437, table.LogitBias, 6);
        Assert.Equal(SceneVocabulary.Keys.Count, SceneVocabulary.Keys.Distinct().Count());
    }

    [Fact]
    public void Scoring_FindsTheClosestLabel_AndIsUnsureBelowTheFloor()
    {
        var table = SceneLabelTable.Default;
        var combat = SceneVocabulary.Keys.ToList().IndexOf("combat");
        var (label, confidence) = table.Score(LabelEmbedding(combat), SceneSniffer.MinimumLabelConfidence);
        Assert.Equal(combat, label);
        Assert.True(confidence > 0.99);

        // The synthetic pattern resembles no scene: unsure, with no confidence.
        Assert.Equal((SceneVocabulary.Unsure, 0d), table.Score(Reference.Load().Embedding, SceneSniffer.MinimumLabelConfidence));
    }

    [Fact]
    public void Reads_AreDueOnRefresh_OrSoonAfterATrigger()
    {
        using var sniffer = new SceneSniffer(new ScriptedEmbedder());
        var t0 = CaptureWorkerTestSupport.FixedTime;

        Assert.True(sniffer.TakeDue(t0, trigger: false));
        Assert.False(sniffer.TakeDue(t0.AddMilliseconds(200), trigger: true));
        Assert.True(sniffer.TakeDue(t0.AddMilliseconds(250), trigger: true));
        Assert.False(sniffer.TakeDue(t0.AddMilliseconds(900), trigger: false));
        Assert.True(sniffer.TakeDue(t0.AddMilliseconds(1250), trigger: false));
        Assert.False(sniffer.TakeDue(t0.AddMilliseconds(1300), trigger: false));
    }

    [Fact]
    public void Novelty_IsMeasuredAgainstTheSceneLastReported()
    {
        var embedder = new ScriptedEmbedder();
        using var sniffer = new SceneSniffer(embedder);

        // The first reading is a new scene.
        embedder.Next = Unit(1);
        var first = Assert.IsType<SceneSniffSignal>(sniffer.Read(Tensor(), Source(0), CancellationToken.None));
        Assert.Equal((1d, true), (first.Novelty, first.Shift));
        Assert.True(first.IsProtocolSafe());

        // The same scene, and a slight drift: nothing.
        embedder.Next = Unit(1);
        Assert.Null(sniffer.Read(Tensor(), Source(1000), CancellationToken.None));
        embedder.Next = Unit(0.95);
        Assert.Null(sniffer.Read(Tensor(), Source(2000), CancellationToken.None));

        // More drift from the reported scene (not from the last reading) adds up to a change.
        embedder.Next = Unit(0.88);
        var change = Assert.IsType<SceneSniffSignal>(sniffer.Read(Tensor(), Source(3000), CancellationToken.None));
        Assert.Equal(0.12, change.Novelty, 3);
        Assert.False(change.Shift);

        // The changed scene is the new reference: staying there is quiet.
        embedder.Next = Unit(0.88);
        Assert.Null(sniffer.Read(Tensor(), Source(4000), CancellationToken.None));

        // A different scene altogether is a full shift.
        embedder.Next = Away(Unit(0.88), 0.5);
        var shift = Assert.IsType<SceneSniffSignal>(sniffer.Read(Tensor(), Source(5000), CancellationToken.None));
        Assert.True(shift.Shift);
        Assert.Equal(0.5, shift.Novelty, 3);

        var (reads, changes, last, maximum) = sniffer.Snapshot();
        Assert.Equal((6L, 3L), (reads, changes));
        Assert.True(maximum >= last);
    }

    [Fact]
    public void AReadingCloseToALabel_CarriesThatLabel()
    {
        var embedder = new ScriptedEmbedder();
        using var sniffer = new SceneSniffer(embedder);
        var menu = SceneVocabulary.Keys.ToList().IndexOf("menu");
        embedder.Next = LabelEmbedding(menu);
        var scene = Assert.IsType<SceneSniffSignal>(sniffer.Read(Tensor(), Source(0), CancellationToken.None));
        Assert.Equal(menu, scene.Label);
        Assert.InRange(scene.LabelConfidence, 0.99, 1);
    }

    [Fact]
    public async Task OneReadAtATime_OffTheFramePath_AndAResetSilencesTheReadInFlight()
    {
        var embedder = new ScriptedEmbedder { Next = Unit(1), Gate = new ManualResetEventSlim() };
        using var sniffer = new SceneSniffer(embedder);
        using var pixels = Pattern(320, 180);
        var scenes = new List<SceneSniffSignal>();

        Assert.True(sniffer.TryStart(pixels, trigger: false, Source(0), scenes.Add));
        Assert.False(sniffer.TryStart(pixels, trigger: true, Source(2000), scenes.Add));

        sniffer.Reset();
        embedder.Gate.Set();
        await sniffer.DrainAsync();
        Assert.Empty(scenes);
        Assert.Equal(0L, sniffer.Snapshot().Reads);

        // After the reset the same scene is new again, and due at once.
        embedder.Gate = null;
        Assert.True(sniffer.TryStart(pixels, trigger: false, Source(100), scenes.Add));
        await sniffer.DrainAsync();
        Assert.Single(scenes);
    }

    [Fact]
    public async Task AFailingModel_ReportsNothing_AndTheNextReadCarriesOn()
    {
        var embedder = new ScriptedEmbedder { Next = Unit(1), Throw = true };
        using var sniffer = new SceneSniffer(embedder);
        using var pixels = Pattern(320, 180);
        var scenes = new List<SceneSniffSignal>();
        Assert.Null(sniffer.Read(Tensor(), Source(0), CancellationToken.None));
        Assert.True(sniffer.TryStart(pixels, trigger: false, Source(0), scenes.Add));
        await sniffer.DrainAsync();
        Assert.Empty(scenes);

        embedder.Throw = false;
        Assert.True(sniffer.TryStart(pixels, trigger: false, Source(1000), scenes.Add));
        await sniffer.DrainAsync();
        Assert.Single(scenes);

        // A wrong-sized answer is no answer.
        embedder.Next = new float[Dimensions - 1];
        Assert.Null(sniffer.Read(Tensor(), Source(2000), CancellationToken.None));
    }

    [Fact]
    public async Task ThePipeline_ReadsTheFirstFrame_ThenOnTriggersAndRefresh()
    {
        var embedder = new ScriptedEmbedder { Next = Unit(1) };
        using var pipeline = new VisualObservationPipeline(sceneEmbedder: embedder);
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var scenes = new List<SceneSniffSignal>();

        async Task Process(long sequence, int milliseconds, NormalizedRegion? patch, byte shade = 230)
        {
            using var frame = Frame(sequence, milliseconds, patch, patchShade: shade);
            using (await pipeline.ProcessAsync(frame, authorization, null, null, scenes.Add, CancellationToken.None))
            {
            }

            await pipeline.Scene.DrainAsync();
        }

        await Process(1, 0, null);
        var first = Assert.Single(scenes);
        Assert.Equal((authorization.Generation, 1L), (first.Generation, first.SourceSequenceNumber));
        Assert.Equal(1, embedder.Reads);

        // A trigger inside 250 ms is not read; after it, it is.
        embedder.Next = Unit(0.5);
        await Process(2, 100, DialogueBox);
        Assert.Equal(1, embedder.Reads);
        await Process(3, 300, DialogueBox, shade: 120);
        Assert.Equal(2, embedder.Reads);
        Assert.Equal(2, scenes.Count);
        Assert.True(scenes[^1].Shift);

        // A changed frame that is no trigger (the box's shade shifts a little) waits for the refresh.
        await Process(4, 700, DialogueBox, shade: 145);
        Assert.Equal(2, embedder.Reads);

        // Duplicate frames are never read, even when a refresh is due.
        await Process(5, 1500, DialogueBox, shade: 145);
        Assert.Equal(2, embedder.Reads);

        // A changed frame after the refresh interval is read.
        await Process(6, 1600, DialogueBox, shade: 170);
        Assert.Equal(3, embedder.Reads);

        // A visual reset (a new grant or target) forgets the scene: the next frame is read and new.
        pipeline.Reset();
        embedder.Next = Unit(0.5);
        await Process(7, 1700, DialogueBox, shade: 195);
        Assert.Equal(4, embedder.Reads);
        Assert.Equal(1d, scenes[^1].Novelty);
    }

    [Fact]
    public async Task NoEmbedding_IsReachableFromAnySignalOrMetric()
    {
        var embedder = new ScriptedEmbedder { Next = Unit(0.987654321) };
        var markers = embedder.Next.Take(2).Select(value => value.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        await using var source = new ControllableCaptureSource();
        await using var engine = new CaptureWorkerEngine(source, sceneEmbedder: embedder);
        var scenes = new List<SceneSniffSignal>();
        engine.SceneSniffProduced += (_, scene) =>
        {
            lock (scenes)
            {
                scenes.Add(scene);
            }
        };
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        await engine.StartAsync(authorization, CancellationToken.None);
        source.Emit(PatchFrame(320, 180, CaptureWorkerTestSupport.FixedTime, null));
        await CaptureWorkerTestSupport.WaitUntilAsync(() =>
        {
            lock (scenes)
            {
                return scenes.Count == 1;
            }
        });

        var metrics = engine.GetMetrics();
        Assert.True(metrics.IsProtocolSafe());
        Assert.True(metrics.SceneModelAvailable);
        Assert.False(metrics.SceneModelAccelerated);
        Assert.Equal((1L, 1L), (metrics.SceneReads, metrics.SceneChanges));

        // The signal has no vector-shaped member, and nothing written out carries the embedding.
        Assert.DoesNotContain(typeof(SceneSniffSignal).GetProperties(), property =>
            property.PropertyType.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string));
        using var stream = new MemoryStream();
        await CaptureIpcProtocol.WriteAsync(stream, new CaptureIpcMessage { Kind = CaptureIpcMessageKind.SceneSniffProduced, SceneSniff = scenes[0] }, CancellationToken.None);
        var everything = System.Text.Encoding.UTF8.GetString(stream.ToArray()) + JsonSerializer.Serialize(metrics);
        Assert.All(markers, marker => Assert.DoesNotContain(marker, everything));
        await engine.StopAndClearAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ADeferredModel_IsUnavailableUntilLoaded_AndAFailedLoadStaysUnavailable()
    {
        using var gate = new ManualResetEventSlim();
        using var deferred = new DeferredSceneEmbedder(() =>
        {
            gate.Wait();
            return new ScriptedEmbedder { Next = Unit(1) };
        });
        Assert.False(deferred.IsAvailable);
        Assert.Null(deferred.Embed(Tensor()));
        gate.Set();
        await deferred.Loaded;
        Assert.True(deferred.IsAvailable);
        Assert.NotNull(deferred.Embed(Tensor()));

        using var failed = new DeferredSceneEmbedder(() => throw new InvalidOperationException("synthetic load failure"));
        await failed.Loaded;
        Assert.False(failed.IsAvailable);

        // A missing model file is simply unavailable.
        Assert.False(OnnxSceneEmbedder.Create(Path.Combine(Path.GetTempPath(), $"no-model-{Guid.NewGuid():N}.onnx"), Dimensions).IsAvailable);
    }

    [Fact]
    public void SceneSniffSignals_AreStrictlyBounded()
    {
        var good = Valid();
        Assert.True(good.IsProtocolSafe());
        Assert.False((good with { Novelty = 1.5 }).IsProtocolSafe());
        Assert.False((good with { Novelty = double.NaN }).IsProtocolSafe());
        Assert.False((good with { Novelty = -0.1 }).IsProtocolSafe());
        Assert.False((good with { Label = SceneVocabulary.Keys.Count }).IsProtocolSafe());
        Assert.False((good with { Label = -2 }).IsProtocolSafe());
        Assert.False((good with { LabelConfidence = 1.01 }).IsProtocolSafe());
        Assert.False((good with { Label = SceneVocabulary.Unsure, LabelConfidence = 0.2 }).IsProtocolSafe());
        Assert.True((good with { Label = SceneVocabulary.Unsure, LabelConfidence = 0 }).IsProtocolSafe());
        Assert.False((good with { ComputeMicroseconds = SniffSignal.MaximumComputeMicroseconds + 1 }).IsProtocolSafe());
        Assert.False((good with { SourceAccountedBytes = CaptureWorkerMetrics.ScreenshotBudgetBytes + 1 }).IsProtocolSafe());
        Assert.False((good with { Target = null }).IsProtocolSafe());
        Assert.False((good with { SourceSequenceNumber = 0 }).IsProtocolSafe());
        Assert.True(good.Matches(CaptureWorkerTestSupport.CreateGrant()));
        Assert.False(good.Matches(CaptureWorkerTestSupport.CreateGrant(generation: 8)));
        Assert.Equal("combat", SceneVocabulary.KeyOf(good.Label));
        Assert.Null(SceneVocabulary.KeyOf(SceneVocabulary.Unsure));
    }

    [Fact]
    public async Task SceneModelMetrics_AreBounded()
    {
        await using var source = new ControllableCaptureSource();
        await using var engine = new CaptureWorkerEngine(source);
        Assert.False(engine.GetMetrics().SceneModelAvailable);
        var good = engine.GetMetrics() with { SceneModelAvailable = true, SceneReads = 4, SceneChanges = 2, LastSceneReadMicroseconds = 10, MaximumSceneReadMicroseconds = 20 };
        Assert.True(good.IsProtocolSafe());
        Assert.False((good with { SceneChanges = 5 }).IsProtocolSafe());
        Assert.False((good with { LastSceneReadMicroseconds = 30 }).IsProtocolSafe());
        Assert.False((good with { SceneModelAvailable = false, SceneModelAccelerated = true }).IsProtocolSafe());
    }

    [Fact]
    public async Task SceneSniffMessages_RoundTrip_AndAreShapeChecked()
    {
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var scene = Valid();
        var notification = new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.SceneSniffProduced,
            Authorization = authorization,
            SequenceNumber = scene.SourceSequenceNumber,
            SceneSniff = scene,
        };
        using var stream = new MemoryStream();
        await CaptureIpcProtocol.WriteAsync(stream, notification, CancellationToken.None);
        stream.Position = 0;
        var read = await CaptureIpcProtocol.ReadAsync(stream, CancellationToken.None);
        Assert.Equal(scene, read.SceneSniff);
        OutOfProcessCaptureWorker.ValidateSceneSniffNotificationShape(read);

        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSceneSniffNotificationShape(read with { SequenceNumber = 99 }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSceneSniffNotificationShape(read with { SceneSniff = scene with { Novelty = 2 } }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSceneSniffNotificationShape(read with { Sniff = SniffTests.Valid() }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSceneSniffNotificationShape(read with { TextSniff = TextSniffTests.Valid() }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSceneSniffNotificationShape(read with { Authorization = authorization with { Generation = 8 } }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSceneSniffNotificationShape(read with { Authorization = authorization with { TargetSessionId = Guid.NewGuid() } }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSceneSniffNotificationShape(read with { SceneSniff = null }));

        // Neither a sniff nor a text sniff notification may smuggle a scene, nor a command carry one.
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateSniffNotificationShape(new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.SniffProduced,
            Authorization = authorization,
            SequenceNumber = SniffTests.Valid().SourceSequenceNumber,
            Sniff = SniffTests.Valid(),
            SceneSniff = scene,
        }));
        Assert.Throws<CaptureProtocolException>(() => OutOfProcessCaptureWorker.ValidateTextSniffNotificationShape(new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.TextSniffProduced,
            Authorization = authorization,
            SequenceNumber = TextSniffTests.Valid().SourceSequenceNumber,
            TextSniff = TextSniffTests.Valid(),
            SceneSniff = scene,
        }));
        Assert.Throws<CaptureProtocolException>(() => WorkerIpcHost.ValidateCommandShape(new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.GetMetrics,
            CorrelationId = Guid.NewGuid(),
            ControlSequence = 1,
            SceneSniff = scene,
        }));
    }

    [Fact]
    public void TheRealModel_LoadsAndMatchesTheReferenceEmbedding()
    {
        if (!OperatingSystem.IsWindows())
        {
            // ONNX Runtime's DirectML package ships Windows runtimes only.
            return;
        }

        var model = OnnxSceneEmbedder.DefaultModelPath;
        Assert.True(File.Exists(model), $"The scene model was not shipped next to the worker: {model}");
        var reference = Reference.Load();
        var loading = Stopwatch.StartNew();
        using var embedder = OnnxSceneEmbedder.Create(model, Dimensions);
        loading.Stop();
        Assert.True(embedder.IsAvailable);

        using var pixels = Pattern(reference.Width, reference.Height);
        var times = new List<double>();
        float[]? embedding = null;
        for (var run = 0; run < 5; run++)
        {
            var tensor = ScenePreprocessor.ToTensor(pixels);
            var started = Stopwatch.StartNew();
            embedding = embedder.Embed(tensor);
            times.Add(started.Elapsed.TotalMilliseconds);
        }

        Assert.NotNull(embedding);
        var cosine = SceneLabelTable.Dot(embedding, reference.Embedding);
        times.Sort();
        var median = times[times.Count / 2];
        Console.WriteLine($"Scene model: {(embedder.IsAccelerated ? "DirectML" : "CPU")}, load {loading.Elapsed.TotalMilliseconds:F0} ms, median read {median:F1} ms, cosine to reference {cosine:F5}");
        Assert.True(cosine >= 0.99, $"cosine to the reference embedding was {cosine:F5}");
        Assert.True(median < 2000, $"median scene read took {median:F0} ms");
    }

    internal static SceneSniffSignal Valid() => new()
    {
        TargetSessionId = CaptureWorkerTestSupport.CreateAuthorization().TargetSessionId,
        Generation = CaptureWorkerTestSupport.CreateAuthorization().Generation,
        Target = CaptureWorkerTestSupport.CreateGrant().Target,
        SourceSequenceNumber = 4,
        SourceTimestamp = CaptureWorkerTestSupport.FixedTime,
        SourceWidth = 1920,
        SourceHeight = 1080,
        SourceAccountedBytes = 1920L * 1080 * 4,
        Novelty = 0.4,
        Shift = true,
        Label = SceneVocabulary.Keys.ToList().IndexOf("combat"),
        LabelConfidence = 0.2,
        ComputeMicroseconds = 40_000,
    };

    private static SniffSource Source(int milliseconds) => new(
        CaptureWorkerTestSupport.CreateAuthorization(),
        milliseconds + 1,
        CaptureWorkerTestSupport.FixedTime.AddMilliseconds(milliseconds),
        320,
        180,
        320L * 180 * 4);

    private static float[] Tensor() => new float[ScenePreprocessor.TensorLength];

    private static float[] LabelEmbedding(int label) => SceneLabelTable.Default.EmbeddingOf(label);

    /// <summary>A unit vector at <paramref name="cosine"/> to the first axis, in the plane of the first two.</summary>
    private static float[] Unit(double cosine)
    {
        var vector = new float[Dimensions];
        vector[0] = (float)cosine;
        vector[1] = (float)Math.Sqrt(1 - (cosine * cosine));
        return vector;
    }

    /// <summary>A unit vector at <paramref name="cosine"/> to <paramref name="from"/> (in the first plane), turned toward the third axis.</summary>
    private static float[] Away(float[] from, double cosine)
    {
        var vector = new float[Dimensions];
        for (var index = 0; index < Dimensions; index++)
        {
            vector[index] = (float)(from[index] * cosine);
        }

        vector[2] += (float)Math.Sqrt(1 - (cosine * cosine));
        return vector;
    }

    /// <summary>The synthetic pattern of <c>tools/scene-labels/make_scene_reference.py</c>, as BGRA.</summary>
    private static OwnedBgra32Buffer Pattern(int width, int height)
    {
        var buffer = OwnedBgra32Buffer.Allocate(width, height);
        var pixels = buffer.Span;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int red, green, blue;
                if (x is >= 200 and < 440 && y is >= 120 and < 240)
                {
                    (red, green, blue) = (230, 40, 40);
                }
                else
                {
                    red = x * 255 / (width - 1);
                    green = y * 255 / (height - 1);
                    blue = ((x + y) % 64) * 4;
                }

                var offset = (y * buffer.Stride) + (x * 4);
                pixels[offset] = (byte)blue;
                pixels[offset + 1] = (byte)green;
                pixels[offset + 2] = (byte)red;
                pixels[offset + 3] = 255;
            }
        }

        return buffer;
    }

    private sealed record Reference(int Width, int Height, string VisionModelSha256, double TensorSum, IReadOnlyList<(int Index, float Value)> Probes, float[] Embedding)
    {
        internal static Reference Load()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SceneReference.json")));
            var root = document.RootElement;
            var probes = root.GetProperty("tensorProbes").EnumerateObject()
                .Select(property => (int.Parse(property.Name, System.Globalization.CultureInfo.InvariantCulture), (float)property.Value.GetDouble()))
                .ToArray();
            var embedding = MemoryMarshal.Cast<byte, float>(Convert.FromBase64String(root.GetProperty("embedding").GetString()!)).ToArray();
            return new Reference(
                root.GetProperty("width").GetInt32(),
                root.GetProperty("height").GetInt32(),
                root.GetProperty("visionModelSha256").GetString()!,
                root.GetProperty("tensorSum").GetDouble(),
                probes,
                embedding);
        }
    }

    /// <summary>Answers with whatever unit vector it is told to, after an optional gate.</summary>
    private sealed class ScriptedEmbedder : ISceneEmbedder
    {
        private int _reads;

        public float[] Next { get; set; } = new float[Dimensions];

        public ManualResetEventSlim? Gate { get; set; }

        public bool Throw { get; set; }

        public int Reads => Volatile.Read(ref _reads);

        public bool IsAvailable => true;

        public bool IsAccelerated => false;

        public float[]? Embed(float[] tensor)
        {
            Gate?.Wait(TimeSpan.FromSeconds(10));
            Interlocked.Increment(ref _reads);
            if (Throw)
            {
                throw new InvalidOperationException("synthetic model failure");
            }

            return (float[])Next.Clone();
        }

        public void Dispose()
        {
        }
    }
}
