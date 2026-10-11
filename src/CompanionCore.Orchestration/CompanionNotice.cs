using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;
using CompanionCore.Conversation;
using CompanionCore.Keepsakes;
using CompanionCore.Watchbun;

namespace CompanionCore.Orchestration;

public enum CompanionNoticeKind
{
    SessionStarted = 1,
    SessionEnded = 2,
    Attention = 3,
    Conversation = 4,
    Watchbun = 5,
    Keepsake = 6,
    Braincase = 7,
    Consolidated = 8,
    VaultBackedUp = 9,
    VaultBackupFailed = 10,
    PrivacyPaused = 11,
    PhotographRefused = 12,
    Recovering = 13,

    /// <summary>An internal failure was contained; the pipeline continues. Key names the failure type.</summary>
    Fault = 14,
}

/// <summary>
/// One typed orchestration output for presentation. It carries the originating subsystem's
/// typed intent; every visible word belongs to the presentation adapter.
/// </summary>
public sealed record CompanionNotice(
    CompanionNoticeKind Kind,
    AttentionIntent? Attention = null,
    ConversationIntent? Conversation = null,
    WatchbunIntent? Watchbun = null,
    KeepsakeIntent? Keepsake = null,
    BridgeOutcomeKind? Braincase = null,
    BraincaseNapReason NapReason = BraincaseNapReason.None,
    KeepsakeRefusal PhotographRefusal = KeepsakeRefusal.None,
    string? Key = null,
    int Count = 0);

/// <summary>
/// Local platform signals for the authorized target. The Windows implementation lives in
/// the app layer; every signal names only the bound target or carries no application data.
/// </summary>
public interface IPlatformSignals
{
    event EventHandler<ForegroundWindow>? ForegroundChanged;

    event EventHandler? InputObserved;

    event EventHandler<TargetExit>? TargetExited;

    event EventHandler<CaptureTargetIdentity>? TargetLaunched;

    event EventHandler<SuspendReason>? Suspended;

    event EventHandler<SuspendReason>? Resumed;

    /// <summary>Begins watching exactly this target (its process and window), or nothing.</summary>
    void WatchTarget(CaptureTargetIdentity? target);

    /// <summary>Begins watching for a relaunch of this executable, or nothing.</summary>
    void WatchForRelaunch(CaptureTargetIdentity? previous);
}

public sealed record OrchestratorOptions
{
    /// <summary>How often checkpoints persist while nothing else requests it.</summary>
    public TimeSpan CheckpointInterval { get; init; } = TimeSpan.FromSeconds(30);

    public ulong ExpressionSeed { get; init; } = 0x5EED_B0B0UL;

    public AttentionConfiguration? Attention { get; init; }

    public ConversationConfiguration? Conversation { get; init; }

    public WatchbunConfiguration? Watchbun { get; init; }

    public KeepsakeConfiguration? Keepsakes { get; init; }

    /// <summary>How often Braincase interpretation may run in each attention state.</summary>
    public SemanticCadence SemanticCadence { get; init; } = new();

    internal void Validate()
    {
        if (CheckpointInterval <= TimeSpan.Zero || CheckpointInterval > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(OrchestratorOptions), "The checkpoint interval is invalid.");
        }

        ArgumentNullException.ThrowIfNull(SemanticCadence);
        SemanticCadence.Validate();
    }
}

/// <summary>
/// The agreed semantic cadence (Design BunDex, provisional capture timing). A sheet is
/// interpreted only when this much time has passed since the last interpretation; any other
/// sheet is released at once. Noticing looks roughly every 10–15 s "or triggered", so a
/// meaningful visual change uses the event-driven floor instead. Investigating looks every
/// 4–8 s. Bnuy Mode is event-driven with a bounded frequency. Watchbun's spending pause
/// still stops interpretation entirely. All values remain Stage 11 calibration inputs.
/// </summary>
public sealed record SemanticCadence
{
    /// <summary>Agreed range 10–15 s.</summary>
    public TimeSpan Noticing { get; init; } = TimeSpan.FromSeconds(12);

    /// <summary>Investigating / Engaged (and Afterglow). Agreed range 4–8 s.</summary>
    public TimeSpan Engaged { get; init; } = TimeSpan.FromSeconds(6);

    /// <summary>Bnuy Mode and triggered looks: event-driven, bounded (the 2–4 s dialogue range).</summary>
    public TimeSpan EventFloor { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>A sheet change at or above this triggers an early look at the event floor.</summary>
    public double TriggerChange { get; init; } = SemanticEvidenceMapper.MeaningfulChange;

    /// <summary>No throttling: every eligible sheet may be interpreted (tests and diagnostics only).</summary>
    public static SemanticCadence Unthrottled { get; } = new()
    {
        Noticing = TimeSpan.Zero,
        Engaged = TimeSpan.Zero,
        EventFloor = TimeSpan.Zero,
    };

    internal TimeSpan For(AttentionState? state, double changeScore)
    {
        var interval = state switch
        {
            AttentionState.HighAttention => EventFloor,
            AttentionState.Engaged or AttentionState.Afterglow => Engaged,
            _ => Noticing,
        };
        return double.IsFinite(changeScore) && changeScore >= TriggerChange && EventFloor < interval ? EventFloor : interval;
    }

    internal void Validate()
    {
        static bool Valid(TimeSpan value) => value >= TimeSpan.Zero && value <= TimeSpan.FromMinutes(5);
        if (!Valid(Noticing) || !Valid(Engaged) || !Valid(EventFloor)
            || EventFloor > Engaged || Engaged > Noticing
            || !double.IsFinite(TriggerChange) || TriggerChange is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SemanticCadence), "The semantic cadence is invalid.");
        }
    }
}
