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
}

public sealed record BridgeMemoryResult(
    int ProposedCount,
    RemoteProposalRejection Rejection,
    WriteGateResult? GateResult)
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

    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan MaximumBackoff { get; init; } = TimeSpan.FromSeconds(8);

    public TimeSpan OutageInitialProbeInterval { get; init; } = TimeSpan.FromMinutes(1);

    public TimeSpan OutageMaximumProbeInterval { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan RateLimitDefault { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan RateLimitMinimum { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan RateLimitMaximum { get; init; } = TimeSpan.FromHours(6);

    /// <summary>Optional local daily input-unit budget. Null means no local budget.</summary>
    public long? DailyInputUnitBudget { get; init; }

    internal void Validate()
    {
        if (MaximumAttempts is < 1 or > 10
            || AttemptTimeout <= TimeSpan.Zero
            || AttemptTimeout > TimeSpan.FromMinutes(10)
            || InitialBackoff < TimeSpan.Zero
            || MaximumBackoff < InitialBackoff
            || MaximumBackoff > TimeSpan.FromMinutes(5)
            || OutageInitialProbeInterval <= TimeSpan.Zero
            || OutageMaximumProbeInterval < OutageInitialProbeInterval
            || RateLimitMinimum <= TimeSpan.Zero
            || RateLimitMaximum < RateLimitMinimum
            || RateLimitDefault < RateLimitMinimum
            || RateLimitDefault > RateLimitMaximum
            || DailyInputUnitBudget is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BridgeOptions), "Bridge bounds are invalid.");
        }
    }
}

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
