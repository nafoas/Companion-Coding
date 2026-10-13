using CompanionCore.Memory;

namespace CompanionCore.Api;

public enum BraincaseNapReason
{
    None = 0,
    Outage = 1,
    RateLimited = 2,
    RetriesExhausted = 3,
    LocalBudget = 4,
}

public enum BraincaseNoticeKind
{
    Naptime = 1,
    Awake = 2,
    Unavailable = 3,
}

/// <summary>
/// Typed, locally packaged bridge notice. It works with no remote service at all. The
/// presentation adapter, not the core, chooses any visible wording.
/// </summary>
public sealed record BraincaseNotice(
    BraincaseNoticeKind Kind,
    BraincaseNapReason NapReason,
    ProviderUnavailableReason UnavailableReason);

public sealed record BraincaseNapStatus(BraincaseNapReason Reason, DateTimeOffset? Until)
{
    public bool IsNapping => Reason != BraincaseNapReason.None;
}

public enum BridgeOutcomeKind
{
    Interpreted = 1,
    NotAuthorized = 2,
    Busy = 3,
    Napping = 4,
    Unavailable = 5,
    InvalidResponse = 6,
    Cancelled = 7,
    PrivacyFenced = 8,
    LocalStateUnavailable = 9,
    Disposed = 10,

    /// <summary>A Glance answered (VIS-02).</summary>
    Glanced = 11,

    /// <summary>A Glance did not come back in time, or came back unusable; it has no effect.</summary>
    Missed = 12,
}

/// <param name="Repeats">Proposals the novelty gate kept in the record only (M3a): seen, but not new.</param>
public sealed record BridgeMemoryResult(
    int ProposedCount,
    RemoteProposalRejection Rejection,
    WriteGateResult? GateResult,
    int Repeats = 0)
{
    public static BridgeMemoryResult None { get; } = new(0, RemoteProposalRejection.None, null);
}

public sealed record BridgeOutcome(
    BridgeOutcomeKind Kind,
    Guid OperationId,
    int Attempts,
    SemanticInterpretation? Interpretation,
    BridgeMemoryResult Memory,
    BraincaseNapReason NapReason = BraincaseNapReason.None,
    ProviderUnavailableReason UnavailableReason = ProviderUnavailableReason.None,
    SemanticResponseInvalidReason InvalidReason = SemanticResponseInvalidReason.None)
{
    /// <summary>The Glance reading, present only for <see cref="BridgeOutcomeKind.Glanced"/>.</summary>
    public GlanceReading? Glance { get; init; }

    internal static BridgeOutcome Simple(BridgeOutcomeKind kind, Guid operationId = default, int attempts = 0) =>
        new(kind, operationId, attempts, null, BridgeMemoryResult.None);
}

/// <summary>
/// Bounds for one bridge. Every value is a provisional configuration constant until
/// Stage 11 profiling and the final API gate; none is a character decision.
/// </summary>
public sealed record BridgeOptions
{
    public int MaximumAttempts { get; init; } = 3;

    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>A Glance is one attempt; past this it is stale and missed (VIS-02).</summary>
    public TimeSpan GlanceTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan MaximumBackoff { get; init; } = TimeSpan.FromSeconds(8);

    public TimeSpan OutageInitialProbeInterval { get; init; } = TimeSpan.FromMinutes(1);

    public TimeSpan OutageMaximumProbeInterval { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan RateLimitDefault { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan RateLimitMinimum { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan RateLimitMaximum { get; init; } = TimeSpan.FromHours(6);

    /// <summary>Optional local daily input-unit budget. Null means no local budget.</summary>
    public long? DailyInputUnitBudget { get; init; }

    /// <summary>Optional local daily spending limit (the Bun Budget). Null means no cost limit.</summary>
    public DailyCostBudget? DailyCostBudget { get; init; }

    /// <summary>
    /// The subconscious novelty gate (M3a). Every admitted proposal is recorded as a sighting;
    /// only the ones that add something new become memories. Null keeps every proposal.
    /// </summary>
    public IMemoryNoveltyGate? NoveltyGate { get; init; }

    internal void Validate()
    {
        if (MaximumAttempts is < 1 or > 10
            || AttemptTimeout <= TimeSpan.Zero
            || AttemptTimeout > TimeSpan.FromMinutes(10)
            || GlanceTimeout <= TimeSpan.Zero
            || GlanceTimeout > TimeSpan.FromMinutes(1)
            || InitialBackoff < TimeSpan.Zero
            || MaximumBackoff < InitialBackoff
            || MaximumBackoff > TimeSpan.FromMinutes(5)
            || OutageInitialProbeInterval <= TimeSpan.Zero
            || OutageMaximumProbeInterval < OutageInitialProbeInterval
            || RateLimitMinimum <= TimeSpan.Zero
            || RateLimitMaximum < RateLimitMinimum
            || RateLimitDefault < RateLimitMinimum
            || RateLimitDefault > RateLimitMaximum
            || DailyInputUnitBudget is <= 0
            || DailyCostBudget is { IsValid: false })
        {
            throw new ArgumentOutOfRangeException(nameof(BridgeOptions), "Bridge bounds are invalid.");
        }
    }
}

/// <summary>
/// The Bun Budget: a local daily spending limit (UTC day) in the provider's currency,
/// priced per million input and output units. Reaching it puts the Braincase to sleep
/// until the next day; it never touches memory.
/// </summary>
public sealed record DailyCostBudget(decimal LimitPerDay, decimal InputCostPerMillionUnits, decimal OutputCostPerMillionUnits)
{
    /// <summary>Glance-lane prices (VIS-02); null means the Glance is priced like the Inspect.</summary>
    public decimal? GlanceInputCostPerMillionUnits { get; init; }

    public decimal? GlanceOutputCostPerMillionUnits { get; init; }

    internal bool IsValid =>
        LimitPerDay is > 0 and <= 100_000
        && InputCostPerMillionUnits is >= 0 and <= 10_000
        && OutputCostPerMillionUnits is >= 0 and <= 10_000
        && InputCostPerMillionUnits + OutputCostPerMillionUnits > 0
        && GlanceInputCostPerMillionUnits is null or (>= 0 and <= 10_000)
        && GlanceOutputCostPerMillionUnits is null or (>= 0 and <= 10_000);

    public decimal CostOf(long inputUnits, long outputUnits) =>
        ((inputUnits * InputCostPerMillionUnits) + (outputUnits * OutputCostPerMillionUnits)) / 1_000_000m;

    /// <summary>The day's spend across both lanes, each at its own prices.</summary>
    public decimal CostOf(long inputUnits, long outputUnits, long glanceInputUnits, long glanceOutputUnits) =>
        CostOf(inputUnits, outputUnits)
        + (((glanceInputUnits * (GlanceInputCostPerMillionUnits ?? InputCostPerMillionUnits))
            + (glanceOutputUnits * (GlanceOutputCostPerMillionUnits ?? OutputCostPerMillionUnits))) / 1_000_000m);
}

/// <summary>The Glance lane at a glance (VIS-02): counts and how long the last and slowest Glance took.</summary>
public sealed record GlanceDiagnostics(
    long Glanced,
    long Missed,
    long Busy,
    TimeSpan LastLatency,
    TimeSpan MaximumLatency);

/// <summary>Hidden technical diagnostics, returned only on explicit request.</summary>
public sealed record BridgeDiagnosticsSnapshot(
    long OperationsStarted,
    long OperationsCompleted,
    long OperationsInterrupted,
    long AttemptsSent,
    long Retries,
    long Timeouts,
    long TransientFailures,
    long InvalidResponses,
    long RejectedProposalBatches,
    long Commits,
    long AlreadyCommitted,
    long NotAuthorized,
    long Busy,
    long RefusedWhileNapping,
    long NapEpisodes,
    BraincaseNapStatus Nap,
    long EstimatedInputUnits,
    long EstimatedOutputUnits,
    long EstimatedInputUnitsToday,
    bool JournalTornTailTruncated,
    bool JournalCorruptionPreserved);
