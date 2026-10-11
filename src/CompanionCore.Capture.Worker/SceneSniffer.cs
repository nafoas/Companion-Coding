using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

/// <summary>A local image model that turns a preprocessed frame into a unit-length embedding (VIS-04).</summary>
internal interface ISceneEmbedder : IDisposable
{
    bool IsAvailable { get; }

    /// <summary>The model runs on the GPU (DirectML).</summary>
    bool IsAccelerated { get; }

    /// <summary>
    /// Embeds a <see cref="ScenePreprocessor"/> tensor. Returns a unit-length vector of the
    /// label table's dimensions, or null when the model cannot answer. Called on one
    /// background thread at a time.
    /// </summary>
    float[]? Embed(float[] tensor);
}

/// <summary>No scene model (not Windows, the model was not shipped, or it failed to load).</summary>
internal sealed class UnavailableSceneEmbedder : ISceneEmbedder
{
    internal static UnavailableSceneEmbedder Instance { get; } = new();

    public bool IsAvailable => false;

    public bool IsAccelerated => false;

    public float[]? Embed(float[] tensor) => null;

    public void Dispose()
    {
    }
}

/// <summary>
/// Loads a scene model in the background so the worker starts at once; until the model is
/// ready the Scene Sniff simply reports unavailable.
/// </summary>
internal sealed class DeferredSceneEmbedder : ISceneEmbedder
{
    private readonly Task<ISceneEmbedder> _loading;
    private int _disposed;

    internal DeferredSceneEmbedder(Func<ISceneEmbedder> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        _loading = Task.Run(() =>
        {
            try
            {
                return load();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                return (ISceneEmbedder)UnavailableSceneEmbedder.Instance;
            }
        });
    }

    internal Task Loaded => _loading;

    private ISceneEmbedder? Ready =>
        Volatile.Read(ref _disposed) == 0 && _loading.IsCompletedSuccessfully ? _loading.Result : null;

    public bool IsAvailable => Ready?.IsAvailable == true;

    public bool IsAccelerated => Ready?.IsAccelerated == true;

    public float[]? Embed(float[] tensor) => Ready?.Embed(tensor);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _loading.GetAwaiter().GetResult().Dispose();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
        }
    }
}

/// <summary>
/// SigLIP base/16 (Apache-2.0) on ONNX Runtime. At load, DirectML (the GPU) and the CPU each
/// run a short warm-up, and the faster one keeps the model; the CPU never takes every core,
/// so a game keeps its own.
/// </summary>
internal sealed class OnnxSceneEmbedder : ISceneEmbedder
{
    internal const string InputName = "pixel_values";
    internal const string OutputName = "pooler_output";
    private const int WarmupRuns = 2;
    private const int TimedRuns = 3;

    private readonly Microsoft.ML.OnnxRuntime.InferenceSession _session;
    private readonly int _dimensions;
    private readonly object _gate = new();
    private bool _disposed;

    private OnnxSceneEmbedder(Microsoft.ML.OnnxRuntime.InferenceSession session, bool accelerated, int dimensions)
    {
        _session = session;
        IsAccelerated = accelerated;
        _dimensions = dimensions;
    }

    public bool IsAvailable => true;

    public bool IsAccelerated { get; }

    /// <summary>The model shipped next to the worker (see the worker project's model fetch).</summary>
    internal static string DefaultModelPath { get; } =
        Path.Combine(AppContext.BaseDirectory, "models", "siglip", "vision_model.onnx");

    /// <summary>Loads the model, racing DirectML against the CPU; unavailable when it cannot load.</summary>
    internal static ISceneEmbedder Create(string modelPath, int dimensions, bool tryAccelerated = true)
    {
        if (!File.Exists(modelPath))
        {
            return UnavailableSceneEmbedder.Instance;
        }

        // ONNX Runtime's official Windows builds emit usage telemetry by default; Prince's
        // worker turns it off before any session exists.
        DisableTelemetry();

        var cpu = TryLoad(modelPath, accelerated: false, dimensions);
        var gpu = tryAccelerated && OperatingSystem.IsWindows() ? TryLoad(modelPath, accelerated: true, dimensions) : null;
        if (cpu is null && gpu is null)
        {
            return UnavailableSceneEmbedder.Instance;
        }

        if (cpu is null || gpu is null)
        {
            return cpu?.Embedder ?? gpu!.Embedder;
        }

        if (gpu.Time < cpu.Time)
        {
            cpu.Embedder.Dispose();
            return gpu.Embedder;
        }

        gpu.Embedder.Dispose();
        return cpu.Embedder;
    }

    private static void DisableTelemetry()
    {
        try
        {
            Microsoft.ML.OnnxRuntime.OrtEnv.Instance().DisableTelemetryEvents();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // No runtime, no telemetry: the load below reports unavailable.
        }
    }

    private static Loaded? TryLoad(string modelPath, bool accelerated, int dimensions)
    {
        Microsoft.ML.OnnxRuntime.SessionOptions? options = null;
        Microsoft.ML.OnnxRuntime.InferenceSession? session = null;
        try
        {
            options = new Microsoft.ML.OnnxRuntime.SessionOptions
            {
                GraphOptimizationLevel = Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = Microsoft.ML.OnnxRuntime.ExecutionMode.ORT_SEQUENTIAL,
            };
            if (accelerated)
            {
                options.EnableMemoryPattern = false;
                options.AppendExecutionProvider_DML(0);
            }
            else
            {
                options.IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
            }

            session = new Microsoft.ML.OnnxRuntime.InferenceSession(modelPath, options);
            var embedder = new OnnxSceneEmbedder(session, accelerated, dimensions);
            var probe = new float[ScenePreprocessor.TensorLength];
            for (var run = 0; run < WarmupRuns; run++)
            {
                if (embedder.Embed(probe) is null)
                {
                    embedder.Dispose();
                    return null;
                }
            }

            var started = Stopwatch.GetTimestamp();
            for (var run = 0; run < TimedRuns; run++)
            {
                embedder.Embed(probe);
            }

            session = null;
            return new Loaded(embedder, Stopwatch.GetElapsedTime(started));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            session?.Dispose();
            return null;
        }
        finally
        {
            options?.Dispose();
        }
    }

    public float[]? Embed(float[] tensor)
    {
        ArgumentNullException.ThrowIfNull(tensor);
        if (tensor.Length != ScenePreprocessor.TensorLength)
        {
            throw new ArgumentException("The scene tensor has the wrong shape.", nameof(tensor));
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return null;
            }

            var input = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(
                tensor,
                [1, 3, ScenePreprocessor.Size, ScenePreprocessor.Size]);
            using var results = _session.Run(
                [Microsoft.ML.OnnxRuntime.NamedOnnxValue.CreateFromTensor(InputName, input)],
                [OutputName]);
            var output = results.First().AsEnumerable<float>().ToArray();
            return output.Length == _dimensions ? SceneLabelTable.Normalize(output) : null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _session.Dispose();
        }
    }

    private sealed record Loaded(OnnxSceneEmbedder Embedder, TimeSpan Time);
}

/// <summary>
/// The whole frame as SigLIP's 224×224 input: an area-sampled squash resize (4×4 taps per
/// output pixel, the model's own resize-without-crop), RGB, scaled to [−1, 1], channel-major.
/// The generator tool's reference implementation matches it exactly.
/// </summary>
internal static class ScenePreprocessor
{
    internal const int Size = 224;
    internal const int Taps = 4;
    internal const int TensorLength = 3 * Size * Size;

    internal static float[] ToTensor(OwnedBgra32Buffer pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var tensor = new float[TensorLength];
        var source = pixels.ReadOnlySpan;
        var width = pixels.Width;
        var height = pixels.Height;
        Span<int> columns = stackalloc int[Size * Taps];
        for (var x = 0; x < Size; x++)
        {
            for (var tap = 0; tap < Taps; tap++)
            {
                columns[(x * Taps) + tap] = Math.Min(width - 1, (int)(((x + ((tap + 0.5) / Taps)) * width) / Size));
            }
        }

        const int plane = Size * Size;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                int red = 0, green = 0, blue = 0;
                for (var tapY = 0; tapY < Taps; tapY++)
                {
                    var row = Math.Min(height - 1, (int)(((y + ((tapY + 0.5) / Taps)) * height) / Size)) * pixels.Stride;
                    for (var tapX = 0; tapX < Taps; tapX++)
                    {
                        var offset = row + (columns[(x * Taps) + tapX] * 4);
                        blue += source[offset];
                        green += source[offset + 1];
                        red += source[offset + 2];
                    }
                }

                var index = (y * Size) + x;
                const float scale = 1f / (Taps * Taps * 127.5f);
                tensor[index] = (red * scale) - 1f;
                tensor[plane + index] = (green * scale) - 1f;
                tensor[(2 * plane) + index] = (blue * scale) - 1f;
            }
        }

        return tensor;
    }
}

/// <summary>
/// The neutral scene vocabulary's text embeddings (precomputed by
/// <c>tools/scene-labels/make_scene_labels.py</c>) and SigLIP's calibration.
/// </summary>
internal sealed class SceneLabelTable
{
    private readonly float[][] _labels;

    private SceneLabelTable(float[][] labels, double logitScale, double logitBias)
    {
        _labels = labels;
        LogitScale = logitScale;
        LogitBias = logitBias;
    }

    internal int Dimensions => _labels[0].Length;

    internal int Count => _labels.Length;

    internal double LogitScale { get; }

    internal double LogitBias { get; }

    internal string VisionModelSha256 { get; private init; } = string.Empty;

    internal static SceneLabelTable Default { get; } = LoadEmbedded();

    /// <summary>A copy of one label's text embedding (tests and diagnostics).</summary>
    internal float[] EmbeddingOf(int label) => (float[])_labels[label].Clone();

    /// <summary>The closest label and its calibrated confidence; unsure below <paramref name="minimumConfidence"/>.</summary>
    internal (int Label, double Confidence) Score(ReadOnlySpan<float> embedding, double minimumConfidence)
    {
        var best = SceneVocabulary.Unsure;
        var bestCosine = double.NegativeInfinity;
        for (var label = 0; label < _labels.Length; label++)
        {
            var cosine = Dot(embedding, _labels[label]);
            if (cosine > bestCosine)
            {
                bestCosine = cosine;
                best = label;
            }
        }

        var confidence = 1 / (1 + Math.Exp(-((bestCosine * Math.Exp(LogitScale)) + LogitBias)));
        return confidence >= minimumConfidence && double.IsFinite(confidence)
            ? (best, Math.Clamp(confidence, 0, 1))
            : (SceneVocabulary.Unsure, 0);
    }

    internal static double Dot(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Embeddings differ in length.");
        }

        var sum = 0d;
        for (var index = 0; index < left.Length; index++)
        {
            sum += left[index] * (double)right[index];
        }

        return sum;
    }

    internal static float[] Normalize(float[] vector)
    {
        var length = Math.Sqrt(Dot(vector, vector));
        if (!double.IsFinite(length) || length <= 0)
        {
            throw new InvalidOperationException("The scene embedding is degenerate.");
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = (float)(vector[index] / length);
        }

        return vector;
    }

    private static SceneLabelTable LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CompanionCore.Capture.Worker.SceneLabels.json")
            ?? throw new InvalidOperationException("The scene label table is missing.");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var dimensions = root.GetProperty("dimensions").GetInt32();
        var labels = root.GetProperty("labels").EnumerateArray().ToArray();
        if (labels.Length != SceneVocabulary.Keys.Count)
        {
            throw new InvalidOperationException("The scene label table does not match the vocabulary.");
        }

        var vectors = new float[labels.Length][];
        for (var index = 0; index < labels.Length; index++)
        {
            if (labels[index].GetProperty("key").GetString() != SceneVocabulary.Keys[index])
            {
                throw new InvalidOperationException("The scene label table does not match the vocabulary.");
            }

            var bytes = Convert.FromBase64String(labels[index].GetProperty("embedding").GetString()!);
            if (bytes.Length != dimensions * sizeof(float))
            {
                throw new InvalidOperationException("A scene label embedding has the wrong length.");
            }

            vectors[index] = MemoryMarshal.Cast<byte, float>(bytes).ToArray();
        }

        return new SceneLabelTable(vectors, root.GetProperty("logitScale").GetDouble(), root.GetProperty("logitBias").GetDouble())
        {
            VisionModelSha256 = root.GetProperty("visionModelSha256").GetString() ?? string.Empty,
        };
    }
}

/// <summary>
/// The Scene Sniff (VIS-04). The local vision model reads the whole frame, one read at a time
/// and off the frame path: soon after a Sniff trigger (at most every <see cref="TriggerSpacing"/>),
/// otherwise every <see cref="RefreshInterval"/>. Each reading is compared with the scene last
/// reported; at <see cref="ChangeNovelty"/> it is reported as a change (and becomes the new
/// reference), at <see cref="ShiftNovelty"/> as a full shift. The embedding never leaves this
/// class; it is zeroed on reset. Time comes from the frames' own timestamps.
/// </summary>
internal sealed class SceneSniffer : IDisposable
{
    internal static readonly TimeSpan TriggerSpacing = TimeSpan.FromMilliseconds(250);
    internal static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    /// <summary>Measured: the same scene stays above 0.97 cosine; a dialogue box appearing is about 0.88.</summary>
    internal const double ChangeNovelty = 0.10;

    /// <summary>Measured: different scenes are 0.47–0.62 cosine apart.</summary>
    internal const double ShiftNovelty = 0.20;

    /// <summary>Below this calibrated confidence the closest label is reported as unsure.</summary>
    internal const double MinimumLabelConfidence = 0.001;

    private readonly object _gate = new();
    private readonly ISceneEmbedder _embedder;
    private readonly SceneLabelTable _labels;
    private CancellationTokenSource _lifetime = new();
    private Task _inFlight = Task.CompletedTask;
    private DateTimeOffset? _lastReadAt;
    private float[]? _reference;
    private long _reads;
    private long _changes;
    private long _lastMicroseconds;
    private long _maximumMicroseconds;
    private bool _disposed;

    internal SceneSniffer(ISceneEmbedder embedder, SceneLabelTable? labels = null)
    {
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _labels = labels ?? SceneLabelTable.Default;
    }

    internal bool IsAvailable => _embedder.IsAvailable;

    internal bool IsAccelerated => _embedder.IsAccelerated;

    internal (long Reads, long Changes, long LastMicroseconds, long MaximumMicroseconds) Snapshot()
    {
        lock (_gate)
        {
            return (_reads, _changes, _lastMicroseconds, _maximumMicroseconds);
        }
    }

    /// <summary>Whether a read is due at <paramref name="at"/>; reserves it when it is.</summary>
    internal bool TakeDue(DateTimeOffset at, bool trigger)
    {
        if (!_embedder.IsAvailable)
        {
            return false;
        }

        lock (_gate)
        {
            if (_disposed || !_inFlight.IsCompleted)
            {
                return false;
            }

            var due = _lastReadAt is not { } last
                || at < last
                || at - last >= RefreshInterval
                || (trigger && at - last >= TriggerSpacing);
            if (due)
            {
                _lastReadAt = at;
            }

            return due;
        }
    }

    /// <summary>
    /// Starts a background read of this frame when one is due. The frame is downscaled now
    /// (its pixels are not kept); a change is reported through <paramref name="onScene"/>.
    /// </summary>
    internal bool TryStart(OwnedBgra32Buffer pixels, bool trigger, SniffSource source, Action<SceneSniffSignal> onScene)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onScene);
        if (!TakeDue(source.Timestamp, trigger))
        {
            return false;
        }

        var tensor = ScenePreprocessor.ToTensor(pixels);
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            var cancellationToken = _lifetime.Token;
            _inFlight = Task.Run(
                () =>
                {
                    if (Read(tensor, source, cancellationToken) is { } signal && !cancellationToken.IsCancellationRequested)
                    {
                        onScene(signal);
                    }
                },
                CancellationToken.None);
        }

        return true;
    }

    /// <summary>Reads one tensor and returns a signal when the scene changed (also used directly by tests).</summary>
    internal SceneSniffSignal? Read(float[] tensor, SniffSource source, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        float[]? embedding;
        try
        {
            embedding = _embedder.Embed(tensor);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
        finally
        {
            Array.Clear(tensor);
        }

        if (embedding is null || embedding.Length != _labels.Dimensions)
        {
            return null;
        }

        var micros = Math.Clamp((long)Stopwatch.GetElapsedTime(started).TotalMicroseconds, 0, SniffSignal.MaximumComputeMicroseconds);
        double novelty;
        lock (_gate)
        {
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                Array.Clear(embedding);
                return null;
            }

            _reads++;
            _lastMicroseconds = micros;
            _maximumMicroseconds = Math.Max(_maximumMicroseconds, micros);
            novelty = _reference is { } reference
                ? Math.Clamp(1 - SceneLabelTable.Dot(reference, embedding), 0, 1)
                : 1;
            if (novelty < ChangeNovelty)
            {
                Array.Clear(embedding);
                return null;
            }

            if (_reference is { } old)
            {
                Array.Clear(old);
            }

            _reference = embedding;
            _changes++;
        }

        var (label, confidence) = _labels.Score(embedding, MinimumLabelConfidence);
        return new SceneSniffSignal
        {
            TargetSessionId = source.Authorization.TargetSessionId,
            Generation = source.Authorization.Generation,
            Target = new CaptureTargetIdentity(
                source.Authorization.WindowId,
                source.Authorization.ProcessId,
                source.Authorization.ExecutableFileName,
                source.Authorization.ExecutablePathFingerprint),
            SourceSequenceNumber = source.SequenceNumber,
            SourceTimestamp = source.Timestamp,
            SourceWidth = source.Width,
            SourceHeight = source.Height,
            SourceAccountedBytes = source.AccountedBytes,
            Novelty = novelty,
            Shift = novelty >= ShiftNovelty,
            Label = label,
            LabelConfidence = confidence,
            Accelerated = _embedder.IsAccelerated,
            ComputeMicroseconds = micros,
        };
    }

    /// <summary>Waits for the read in flight, if any (shutdown and tests).</summary>
    internal async Task DrainAsync()
    {
        Task inFlight;
        lock (_gate)
        {
            inFlight = _inFlight;
        }

        try
        {
            await inFlight.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
        }
    }

    /// <summary>Forgets the reference scene (a new grant or target): the read in flight reports nothing.</summary>
    internal void Reset()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            // The old source is only cancelled: a read in flight may still hold its token.
            _lifetime.Cancel();
            _lifetime = new CancellationTokenSource();
            ClearReference();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            ClearReference();
        }

        DrainAsync().GetAwaiter().GetResult();
        _lifetime.Dispose();
        _embedder.Dispose();
    }

    private void ClearReference()
    {
        if (_reference is { } reference)
        {
            Array.Clear(reference);
        }

        _reference = null;
        _lastReadAt = null;
    }
}
