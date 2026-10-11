using System.IO;
using System.Diagnostics;
using CompanionCore.Calibration;
using CompanionCore.Capture.Contracts;
using CompanionCore.Orchestration;

namespace CompanionCore.App;

/// <summary>
/// Stage 11 calibration sampling, enabled only with <c>--calibration-log</c>. On a bounded
/// interval it records resource numbers and typed states for the main process, the capture
/// worker, the orchestrator, and Braincase usage, through <see cref="CalibrationRecorder"/>.
/// It never records titles, paths, pixels, recollections, or provider text.
/// </summary>
internal sealed class CalibrationSampler : IAsyncDisposable
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan MaximumInterval = TimeSpan.FromMinutes(10);

    private readonly CompanionHost _host;
    private readonly ICaptureWorker _worker;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private readonly DateTime _started = DateTime.UtcNow;
    private TimeSpan _lastCpu;
    private DateTime _lastCpuAt;

    public CalibrationSampler(CalibrationRecorder recorder, CompanionHost host, ICaptureWorker worker, TimeSpan interval)
    {
        Recorder = recorder;
        _host = host;
        _worker = worker;
        Interval = interval < MinimumInterval ? MinimumInterval : interval > MaximumInterval ? MaximumInterval : interval;
        using (var process = Process.GetCurrentProcess())
        {
            _lastCpu = process.TotalProcessorTime;
        }

        _lastCpuAt = DateTime.UtcNow;
        _loop = Task.Run(LoopAsync);
    }

    public CalibrationRecorder Recorder { get; }

    public TimeSpan Interval { get; }

    public long SampleFailures { get; private set; }

    /// <summary>The calibration folder beside the memory root: development data or an isolated test root.</summary>
    public static string DirectoryFor(CompanionCore.Memory.MemoryStoreLocation location) =>
        Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(location.RootPath))!, "Calibration");

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _stop.Dispose();
        Recorder.Dispose();
    }

    internal async Task<CalibrationSample> SampleAsync(CancellationToken cancellationToken)
    {
        long workingSet, privateBytes;
        int handles, threads;
        double cpu;
        using (var process = Process.GetCurrentProcess())
        {
            process.Refresh();
            workingSet = process.WorkingSet64;
            privateBytes = process.PrivateMemorySize64;
            handles = process.HandleCount;
            threads = process.Threads.Count;
            var now = DateTime.UtcNow;
            var total = process.TotalProcessorTime;
            var wall = (now - _lastCpuAt).TotalMilliseconds * Environment.ProcessorCount;
            cpu = wall <= 0 ? 0 : Math.Clamp((total - _lastCpu).TotalMilliseconds / wall * 100, 0, 100);
            _lastCpu = total;
            _lastCpuAt = now;
        }

        CaptureWorkerMetrics? worker = null;
        try
        {
            var metrics = await _worker.GetMetricsAsync(cancellationToken).ConfigureAwait(false);
            worker = metrics.WorkerProcessId != 0 ? metrics : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            worker = null;
        }

        var snapshot = await _host.Orchestrator.GetSnapshotAsync().ConfigureAwait(false);
        var braincase = _host.Bridge.GetDiagnosticsSnapshot();
        return new CalibrationSample
        {
            Utc = DateTimeOffset.UtcNow,
            UptimeSeconds = (DateTime.UtcNow - _started).TotalSeconds,
            AppWorkingSetBytes = workingSet,
            AppPrivateBytes = privateBytes,
            AppManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            AppHandleCount = handles,
            AppThreadCount = threads,
            AppCpuPercent = cpu,
            WorkerStatus = worker?.Status.ToString(),
            WorkerWorkingSetBytes = worker?.WorkingSetBytes,
            WorkerPrivateBytes = worker?.PrivateMemoryBytes,
            WorkerHandleCount = worker?.NativeHandleCount,
            RingBytes = worker?.RingBytes,
            RingFrames = worker?.RingFrameCount,
            SourceFrames = worker?.CurrentSourceFrames,
            QueueDepth = worker?.QueueDepth,
            AcceptedFrames = worker?.AcceptedFrames,
            DroppedFrames = worker?.DroppedFrames,
            AttentionSheets = worker?.ProducedAttentionSheets,
            WorkerFaults = worker?.FaultCount,
            WorkerRestarts = worker?.RestartCount,
            AttentionState = snapshot.Attention?.State.ToString(),
            WatchbunPhase = snapshot.Watchbun?.Phase.ToString(),
            BridgeInFlight = snapshot.BridgeInFlight,
            Unconsolidated = snapshot.UnconsolidatedSessions.Count,
            OrchestratorFaults = snapshot.Faults,
            BraincaseOperations = braincase.OperationsStarted,
            BraincaseAttempts = braincase.AttemptsSent,
            BraincaseRetries = braincase.Retries,
            BraincaseTimeouts = braincase.Timeouts,
            BraincaseInvalidResponses = braincase.InvalidResponses,
            BraincaseCommits = braincase.Commits,
            BraincaseNapEpisodes = braincase.NapEpisodes,
            BraincaseNapReason = braincase.Nap.Reason.ToString(),
            BraincaseInputUnitsToday = braincase.EstimatedInputUnitsToday,
        };
    }

    private async Task LoopAsync()
    {
        var token = _stop.Token;
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                Recorder.Append(await SampleAsync(token).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Contained: a sample during shutdown or a repair, never a reason to disturb Prince.
                SampleFailures++;
            }
        }
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
    }
}
