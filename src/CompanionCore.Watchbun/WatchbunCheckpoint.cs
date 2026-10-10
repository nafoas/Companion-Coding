using CompanionCore.Capture.Contracts;

namespace CompanionCore.Watchbun;

/// <summary>Complete, serializable Watchbun state, validated before restore.</summary>
public sealed record WatchbunCheckpoint
{
    public int Version { get; init; }

    public Guid EngineId { get; init; }

    public long IdCounter { get; init; }

    public Guid TargetSessionId { get; init; }

    public long TargetWindowId { get; init; }

    public int TargetProcessId { get; init; }

    public string TargetExecutableFileName { get; init; } = string.Empty;

    public string TargetExecutablePathFingerprint { get; init; } = string.Empty;

    public WatchbunPhase Phase { get; init; }

    public DateTimeOffset Now { get; init; }

    public long ActiveTicks { get; init; }

    public long LastActivityTicks { get; init; }

    public long? CheckAskedAtTicks { get; init; }

    public bool CheckPending { get; init; }

    public bool IndefiniteWatch { get; init; }

    public TargetExitKind? PendingExit { get; init; }

    public List<WatchTask> Tasks { get; init; } = [];

    public List<DateTimeOffset> RecentAlerts { get; init; } = [];

    public long RejectedEvents { get; init; }

    public long SuppressedAlerts { get; init; }

    internal CaptureTargetIdentity Validate(WatchbunConfiguration configuration)
    {
        CaptureTargetIdentity target;
        try
        {
            target = new CaptureTargetIdentity(TargetWindowId, TargetProcessId, TargetExecutableFileName, TargetExecutablePathFingerprint);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("The Watchbun checkpoint target is invalid.", exception);
        }

        var tasks = Tasks;
        var valid = Version == WatchbunEngine.CheckpointVersion
            && EngineId != Guid.Empty
            && IdCounter >= 0
            && TargetSessionId != Guid.Empty
            && Enum.IsDefined(Phase)
            && Now != default
            && ActiveTicks >= 0
            && LastActivityTicks >= 0 && LastActivityTicks <= ActiveTicks
            && (CheckAskedAtTicks is null || (CheckAskedAtTicks >= 0 && CheckAskedAtTicks <= ActiveTicks))
            && CheckPending == (CheckAskedAtTicks is not null)
            && (!CheckPending || (Phase == WatchbunPhase.Dozing && !IndefiniteWatch))
            && (Phase != WatchbunPhase.Dozing || CheckPending || IndefiniteWatch)
            && (!IndefiniteWatch || Phase is WatchbunPhase.Watching or WatchbunPhase.Dozing)
            && (PendingExit is null ? Phase != WatchbunPhase.ExitPending : Phase == WatchbunPhase.ExitPending && Enum.IsDefined(PendingExit.Value))
            && tasks is not null && RecentAlerts is not null
            && tasks.Count <= configuration.MaximumWatchTasks
            && (Phase != WatchbunPhase.Closed || tasks.Count == 0)
            && tasks.All(task => task is not null
                && task.TaskId != Guid.Empty
                && WatchbunEngine.ValidEventKey(task.EventKey)
                && task.ExpiresAtActive.Ticks > ActiveTicks
                && task.ExpiresAtActive.Ticks - ActiveTicks <= configuration.MaximumTaskLifetime.Ticks)
            && tasks.Select(task => task.TaskId).Distinct().Count() == tasks.Count
            && RecentAlerts.Count <= configuration.MaximumAlertsPerWindow
            && RecentAlerts.All(alert => alert <= Now)
            && RejectedEvents >= 0
            && SuppressedAlerts >= 0;
        if (!valid)
        {
            throw new ArgumentException("The Watchbun checkpoint is invalid.");
        }

        return target;
    }
}
