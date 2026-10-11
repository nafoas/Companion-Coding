using System.Text.Json.Serialization;

namespace CompanionCore.Calibration;

/// <summary>
/// One privacy-safe calibration sample: resource numbers, typed state names, and usage
/// counters only. It deliberately has no field that could hold a title, path, pixel,
/// recollection, provider text, or any other captured or private content.
/// </summary>
public sealed record CalibrationSample
{
    public DateTimeOffset Utc { get; init; }

    public double UptimeSeconds { get; init; }

    // ---- main process --------------------------------------------------------------

    public long AppWorkingSetBytes { get; init; }

    public long AppPrivateBytes { get; init; }

    public long AppManagedHeapBytes { get; init; }

    public int AppHandleCount { get; init; }

    public int AppThreadCount { get; init; }

    public double AppCpuPercent { get; init; }

    // ---- capture worker (absent while no target is captured) -----------------------

    public string? WorkerStatus { get; init; }

    public long? WorkerWorkingSetBytes { get; init; }

    public long? WorkerPrivateBytes { get; init; }

    public int? WorkerHandleCount { get; init; }

    public long? RingBytes { get; init; }

    public int? RingFrames { get; init; }

    public int? SourceFrames { get; init; }

    public int? QueueDepth { get; init; }

    public long? AcceptedFrames { get; init; }

    public long? DroppedFrames { get; init; }

    public long? AttentionSheets { get; init; }

    public long? WorkerFaults { get; init; }

    public long? WorkerRestarts { get; init; }

    // ---- orchestration state ---------------------------------------------------------

    public string? AttentionState { get; init; }

    public string? WatchbunPhase { get; init; }

    public bool BridgeInFlight { get; init; }

    public int Unconsolidated { get; init; }

    public long OrchestratorFaults { get; init; }

    // ---- Braincase usage (estimates; never provider text) ----------------------------

    public long BraincaseOperations { get; init; }

    public long BraincaseAttempts { get; init; }

    public long BraincaseRetries { get; init; }

    public long BraincaseTimeouts { get; init; }

    public long BraincaseInvalidResponses { get; init; }

    public long BraincaseCommits { get; init; }

    public long BraincaseNapEpisodes { get; init; }

    public string? BraincaseNapReason { get; init; }

    public long BraincaseInputUnitsToday { get; init; }

    /// <summary>
    /// The calibration state this sample belongs to, matching the design's measurement
    /// list: Resting, Noticing, Engaged, Bnuy Mode, Afterglow, or a Watchbun phase.
    /// </summary>
    [JsonIgnore]
    public string StateLabel =>
        WatchbunPhase is { Length: > 0 } phase && phase is not "Watching"
            ? $"Watchbun:{phase}"
            : AttentionState switch
            {
                null or "" => "Resting",
                "HighAttention" => "BnuyMode",
                { } state => state,
            };
}
