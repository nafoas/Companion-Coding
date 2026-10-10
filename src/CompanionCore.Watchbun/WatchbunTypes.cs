using CompanionCore.Capture.Contracts;

namespace CompanionCore.Watchbun;

public enum WatchbunPhase
{
    /// <summary>Attached to the bound target with full semantic attention.</summary>
    Watching = 1,

    /// <summary>Quiet: semantic spending paused, only local wake detection on the bound target.</summary>
    Dozing = 2,

    /// <summary>The target exited; capture stopped and the session waits for Boss's decision.</summary>
    ExitPending = 3,

    /// <summary>Waiting for the same executable to relaunch and be re-authorized.</summary>
    AwaitingRelaunch = 4,

    /// <summary>A preserved Paused Adventure, recoverable by re-authorization.</summary>
    PausedAdventure = 5,

    /// <summary>Restored after a runtime restart; nothing is captured until reattached or exited.</summary>
    Recovering = 6,

    /// <summary>Safely closed: checkpointed, consolidation requested, napping.</summary>
    Closed = 7,
}

public enum CaptureMode
{
    None = 0,
    LocalWakeOnly = 1,
    Full = 2,
}

public enum SuspendReason
{
    Lock = 1,
    Sleep = 2,
}

public enum GameEventKind
{
    /// <summary>A minor change; it neither resets quiet time nor alerts on its own.</summary>
    Change = 1,

    /// <summary>A meaningful game change; it resets quiet time and wakes semantic attention.</summary>
    Meaningful = 2,

    /// <summary>An urgent event; it raises a target-only alert and counts as meaningful.</summary>
    Urgent = 3,
}

public enum QuietAnswer
{
    KeepWatching = 1,
    Naptime = 2,
    BackEventually = 3,
}

public enum TargetExitKind
{
    DeliberateClose = 1,
    SuspectedCrash = 2,
}

public enum ExitDecision
{
    Consolidate = 1,
    WaitForRelaunch = 2,
    PreservePaused = 3,
}

public enum CloseReason
{
    Naptime = 1,
    QuietUnanswered = 2,
    ExitConsolidated = 3,
}

public enum AlertKind
{
    Urgent = 1,
    WatchTaskCompleted = 2,
}

public enum WatchbunRefusal
{
    None = 0,
    NotAllowed = 1,
    Closed = 2,
    WrongTarget = 3,
    DifferentExecutable = 4,
    Suspended = 5,
    TaskLimit = 6,
    UnknownTask = 7,
    NotSuspended = 8,
}

public enum WatchbunIntentKind
{
    AlertRaised = 1,
    QuietCheckAsked = 2,
    QuietCheckCleared = 3,
    SemanticSpendingPaused = 4,
    SemanticSpendingResumed = 5,
    IndefiniteWatchEnabled = 6,
    IndefiniteWatchEnded = 7,
    CheckpointRequested = 8,
    ConsolidationRequested = 9,
    SessionClosed = 10,
    Napping = 11,
    CaptureStopped = 12,
    ExitPromptRaised = 13,
    AwaitingRelaunch = 14,
    AdventurePaused = 15,
    ReauthorizationRequested = 16,
    Reattached = 17,
    CaptureSuspended = 18,
    CaptureRestored = 19,
    WatchTaskAdded = 20,
    WatchTaskCompleted = 21,
    WatchTaskExpired = 22,
    WatchTaskCancelled = 23,
}

/// <summary>
/// One typed engine output. Wording belongs to the presentation adapter. Alerts always carry
/// <see cref="NoFocusSteal"/> and name only the bound target.
/// </summary>
public sealed record WatchbunIntent(
    WatchbunIntentKind Kind,
    AlertKind? Alert = null,
    string? EventKey = null,
    Guid? TaskId = null,
    TargetExitKind? Exit = null,
    CloseReason? Close = null,
    CaptureTargetIdentity? Target = null,
    bool NoFocusSteal = true);

public sealed record WatchTask(Guid TaskId, string EventKey, TimeSpan ExpiresAtActive);

/// <summary>A synthetic structured game event for one authorized target session.</summary>
public sealed record StructuredGameEvent(Guid TargetSessionId, GameEventKind Kind, string Key);

/// <summary>
/// The current foreground window, as reported by the local foreground monitor. Only its
/// identity is compared with the bound target; nothing about another application is kept.
/// </summary>
public sealed record ForegroundWindow(long WindowId, int ProcessId);

/// <summary>Facts about a target process exit, as observed by the local process monitor.</summary>
public sealed record TargetExit(int ProcessId, int ExitCode, bool WindowClosedFirst, bool WasHung);

public sealed record WatchbunSnapshot(
    WatchbunPhase Phase,
    CaptureMode Capture,
    bool SemanticSpendingAllowed,
    CaptureTargetIdentity BoundTarget,
    CaptureTargetIdentity? CaptureTarget,
    Guid TargetSessionId,
    bool TargetForeground,
    bool QuietCheckPending,
    bool IndefiniteWatch,
    IReadOnlyList<SuspendReason> Suspensions,
    TimeSpan QuietElapsed,
    IReadOnlyList<WatchTask> WatchTasks,
    TargetExitKind? PendingExit,
    CaptureTargetIdentity? RelaunchCandidate,
    long RejectedEvents,
    long SuppressedAlerts,
    DateTimeOffset AsOf);

public sealed record WatchbunUpdate(
    IReadOnlyList<WatchbunIntent> Intents,
    WatchbunSnapshot Snapshot,
    WatchbunRefusal Refusal = WatchbunRefusal.None);
