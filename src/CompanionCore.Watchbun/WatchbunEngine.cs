using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Watchbun;

/// <summary>
/// Deterministic Watchbun state machine for one authorized target session. It never
/// retargets: capture permission names the bound target or nothing, input counts only
/// while that target is foreground, and every alert is target-only and non-focus-stealing.
/// Quiet and watch-task clocks run only while attached and not suspended. The engine only
/// requests checkpoints and consolidation; it never writes memory itself.
/// </summary>
public sealed partial class WatchbunEngine
{
    public const int CheckpointVersion = 1;
    public const int MaximumEventKeyCharacters = 128;

    private readonly WatchbunConfiguration _configuration;
    private readonly Guid _engineId;
    private readonly List<WatchTask> _tasks = [];
    private readonly SortedSet<SuspendReason> _suspensions = [];
    private readonly Queue<DateTimeOffset> _recentAlerts = new();
    private long _idCounter;
    private CaptureTargetIdentity _target;
    private Guid _sessionId;
    private WatchbunPhase _phase;
    private DateTimeOffset _now;
    private TimeSpan _active;
    private TimeSpan _lastActivity;
    private TimeSpan? _checkAskedAt;
    private bool _checkPending;
    private bool _indefinite;
    private bool _targetForeground;
    private TargetExitKind? _pendingExit;
    private CaptureTargetIdentity? _candidate;
    private long _rejectedEvents;
    private long _suppressedAlerts;

    public WatchbunEngine(
        CaptureAuthorizationGrant grant,
        DateTimeOffset now,
        WatchbunConfiguration? configuration = null,
        Guid? engineId = null)
    {
        ArgumentNullException.ThrowIfNull(grant);
        _configuration = configuration ?? WatchbunConfiguration.Default;
        _configuration.Validate();
        if (now == default)
        {
            throw new ArgumentOutOfRangeException(nameof(now));
        }

        _engineId = engineId ?? Guid.NewGuid();
        if (_engineId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty engine ID is required.", nameof(engineId));
        }

        _target = grant.Target;
        _sessionId = grant.TargetSessionId;
        _phase = WatchbunPhase.Watching;
        _now = now;
    }

    private WatchbunEngine(WatchbunConfiguration configuration, Guid engineId, CaptureTargetIdentity target)
    {
        _configuration = configuration;
        _engineId = engineId;
        _target = target;
    }

    public WatchbunSnapshot Current => Snapshot();

    public WatchbunUpdate Tick(DateTimeOffset now)
    {
        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        return Update(intents);
    }

    /// <summary>Tracks only whether the bound target is foreground; the target never changes.</summary>
    public WatchbunUpdate OnForegroundChanged(ForegroundWindow foreground, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(foreground);
        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        _targetForeground = foreground.WindowId == _target.WindowId && foreground.ProcessId == _target.ProcessId;
        return Update(intents);
    }

    /// <summary>Local keyboard or mouse input, attributed to the target only while it is foreground.</summary>
    public WatchbunUpdate OnInput(DateTimeOffset now)
    {
        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        if (Gate() is { } refusal)
        {
            return Update(intents, refusal);
        }

        if (_targetForeground)
        {
            RegisterActivity(intents, bossPresent: true);
        }

        return Update(intents);
    }

    public WatchbunUpdate OnGameEvent(StructuredGameEvent gameEvent, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        if (!Enum.IsDefined(gameEvent.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(gameEvent));
        }

        RequireEventKey(gameEvent.Key);
        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        if (gameEvent.TargetSessionId != _sessionId)
        {
            _rejectedEvents++;
            return Update(intents, WatchbunRefusal.WrongTarget);
        }

        if (Gate() is { } refusal)
        {
            return Update(intents, refusal);
        }

        var completed = _tasks.Where(task => string.Equals(task.EventKey, gameEvent.Key, StringComparison.Ordinal)).ToArray();
        foreach (var task in completed)
        {
            _tasks.Remove(task);
            intents.Add(new WatchbunIntent(WatchbunIntentKind.WatchTaskCompleted, EventKey: task.EventKey, TaskId: task.TaskId));
            RaiseAlert(intents, AlertKind.WatchTaskCompleted, task.EventKey, task.TaskId);
        }

        if (gameEvent.Kind == GameEventKind.Urgent)
        {
            RaiseAlert(intents, AlertKind.Urgent, gameEvent.Key, null);
        }

        // A pending watch task already held quiet time up to this event, so only the
        // event's own significance decides whether attention wakes.
        if (gameEvent.Kind != GameEventKind.Change)
        {
            RegisterActivity(intents, bossPresent: false);
        }

        return Update(intents);
    }

    public WatchbunUpdate AddWatchTask(string eventKey, TimeSpan? lifetime, DateTimeOffset now)
    {
        RequireEventKey(eventKey);
        var duration = lifetime ?? _configuration.DefaultTaskLifetime;
        if (duration <= TimeSpan.Zero || duration > _configuration.MaximumTaskLifetime)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        if (Gate() is { } refusal)
        {
            return Update(intents, refusal);
        }

        if (_tasks.Count >= _configuration.MaximumWatchTasks)
        {
            return Update(intents, WatchbunRefusal.TaskLimit);
        }

        RegisterActivity(intents, bossPresent: true);
        var task = new WatchTask(NextId(), eventKey, _active + duration);
        _tasks.Add(task);
        intents.Add(new WatchbunIntent(WatchbunIntentKind.WatchTaskAdded, EventKey: eventKey, TaskId: task.TaskId));
        return Update(intents);
    }

    public WatchbunUpdate CancelWatchTask(Guid taskId, DateTimeOffset now)
    {
        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        if (Gate() is { } refusal)
        {
            return Update(intents, refusal);
        }

        var task = _tasks.FirstOrDefault(candidate => candidate.TaskId == taskId);
        if (task is null)
        {
            return Update(intents, WatchbunRefusal.UnknownTask);
        }

        _tasks.Remove(task);
        intents.Add(new WatchbunIntent(WatchbunIntentKind.WatchTaskCancelled, EventKey: task.EventKey, TaskId: task.TaskId));
        return Update(intents);
    }

    public WatchbunUpdate AnswerQuietCheck(QuietAnswer answer, DateTimeOffset now)
    {
        if (!Enum.IsDefined(answer))
        {
            throw new ArgumentOutOfRangeException(nameof(answer));
        }

        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        if (Gate() is { } refusal)
        {
            return Update(intents, refusal);
        }

        if (_phase != WatchbunPhase.Dozing || !_checkPending)
        {
            return Update(intents, WatchbunRefusal.NotAllowed);
        }

        switch (answer)
        {
            case QuietAnswer.KeepWatching:
                RegisterActivity(intents, bossPresent: true);
                break;
            case QuietAnswer.Naptime:
                SafeClose(intents, CloseReason.Naptime);
                break;
            default:
                // Back eventually: no abandonment timers for this Watchbun period; spending
                // stays paused until real activity, so costs remain bounded.
                _checkPending = false;
                _checkAskedAt = null;
                _indefinite = true;
                intents.Add(new WatchbunIntent(WatchbunIntentKind.QuietCheckCleared));
                intents.Add(new WatchbunIntent(WatchbunIntentKind.IndefiniteWatchEnabled));
                break;
        }

        return Update(intents);
    }

    public WatchbunUpdate OnTargetExited(TargetExit exit, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(exit);
        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        if (_phase == WatchbunPhase.Closed)
        {
            return Update(intents, WatchbunRefusal.Closed);
        }

        if (exit.ProcessId != _target.ProcessId)
        {
            return Update(intents, WatchbunRefusal.WrongTarget);
        }

        if (_phase is not (WatchbunPhase.Watching or WatchbunPhase.Dozing or WatchbunPhase.Recovering))
        {
            return Update(intents, WatchbunRefusal.NotAllowed);
        }

        var kind = Classify(exit);
        _phase = WatchbunPhase.ExitPending;
        _pendingExit = kind;
        _checkPending = false;
        _checkAskedAt = null;
        _indefinite = false;
        _targetForeground = false;
        intents.Add(new WatchbunIntent(WatchbunIntentKind.CaptureStopped));
        intents.Add(new WatchbunIntent(WatchbunIntentKind.ExitPromptRaised, Exit: kind));
        return Update(intents);
    }

    /// <summary>Clean close: the window closed before a zero exit and the window was not hung.</summary>
    public static TargetExitKind Classify(TargetExit exit)
    {
        ArgumentNullException.ThrowIfNull(exit);
        return exit.ExitCode == 0 && exit.WindowClosedFirst && !exit.WasHung
            ? TargetExitKind.DeliberateClose
            : TargetExitKind.SuspectedCrash;
    }

    public WatchbunUpdate Decide(ExitDecision decision, DateTimeOffset now)
    {
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        var allowed = _phase switch
        {
            WatchbunPhase.ExitPending => true,
            WatchbunPhase.AwaitingRelaunch => decision != ExitDecision.WaitForRelaunch,
            WatchbunPhase.PausedAdventure => decision != ExitDecision.PreservePaused,
            _ => false,
        };
        if (!allowed)
        {
            return Update(intents, _phase == WatchbunPhase.Closed ? WatchbunRefusal.Closed : WatchbunRefusal.NotAllowed);
        }

        _pendingExit = null;
        switch (decision)
        {
            case ExitDecision.Consolidate:
                SafeClose(intents, CloseReason.ExitConsolidated);
                break;
            case ExitDecision.WaitForRelaunch:
                _phase = WatchbunPhase.AwaitingRelaunch;
                intents.Add(new WatchbunIntent(WatchbunIntentKind.AwaitingRelaunch));
                break;
            default:
                _phase = WatchbunPhase.PausedAdventure;
                intents.Add(new WatchbunIntent(WatchbunIntentKind.CheckpointRequested));
                intents.Add(new WatchbunIntent(WatchbunIntentKind.AdventurePaused));
                break;
        }

        return Update(intents);
    }

    /// <summary>A relaunch of the same executable only requests re-authorization; nothing is captured.</summary>
    public WatchbunUpdate OnTargetLaunched(CaptureTargetIdentity candidate, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        if (_phase is not (WatchbunPhase.AwaitingRelaunch or WatchbunPhase.PausedAdventure))
        {
            return Update(intents, _phase == WatchbunPhase.Closed ? WatchbunRefusal.Closed : WatchbunRefusal.NotAllowed);
        }

        if (!SameExecutable(candidate, _target))
        {
            return Update(intents, WatchbunRefusal.DifferentExecutable);
        }

        _candidate = candidate;
        intents.Add(new WatchbunIntent(WatchbunIntentKind.ReauthorizationRequested, Target: candidate));
        return Update(intents);
    }

    /// <summary>Reattaches only through an explicit authorization grant for the same executable.</summary>
    public WatchbunUpdate Reattach(CaptureAuthorizationGrant grant, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grant);
        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        if (_phase is not (WatchbunPhase.ExitPending or WatchbunPhase.AwaitingRelaunch or WatchbunPhase.PausedAdventure or WatchbunPhase.Recovering))
        {
            return Update(intents, _phase == WatchbunPhase.Closed ? WatchbunRefusal.Closed : WatchbunRefusal.NotAllowed);
        }

        if (_suspensions.Count > 0)
        {
            return Update(intents, WatchbunRefusal.Suspended);
        }

        if (!SameExecutable(grant.Target, _target))
        {
            return Update(intents, WatchbunRefusal.DifferentExecutable);
        }

        _target = grant.Target;
        _sessionId = grant.TargetSessionId;
        _phase = WatchbunPhase.Watching;
        _pendingExit = null;
        _candidate = null;
        _checkPending = false;
        _checkAskedAt = null;
        _indefinite = false;
        _targetForeground = false;
        _lastActivity = _active;
        intents.Add(new WatchbunIntent(WatchbunIntentKind.Reattached, Target: _target));
        return Update(intents);
    }

    public WatchbunUpdate OnSystemSuspend(SuspendReason reason, DateTimeOffset now)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        if (_phase == WatchbunPhase.Closed)
        {
            return Update(intents, WatchbunRefusal.Closed);
        }

        if (_suspensions.Add(reason) && _suspensions.Count == 1)
        {
            intents.Add(new WatchbunIntent(WatchbunIntentKind.CaptureSuspended));
        }

        return Update(intents);
    }

    public WatchbunUpdate OnSystemResume(SuspendReason reason, DateTimeOffset now)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var intents = new List<WatchbunIntent>();
        Advance(now, intents);
        if (_phase == WatchbunPhase.Closed)
        {
            return Update(intents, WatchbunRefusal.Closed);
        }

        if (!_suspensions.Remove(reason))
        {
            return Update(intents, WatchbunRefusal.NotSuspended);
        }

        if (_suspensions.Count == 0)
        {
            intents.Add(new WatchbunIntent(WatchbunIntentKind.CaptureRestored));
        }

        return Update(intents);
    }

    public WatchbunCheckpoint Checkpoint() => new()
    {
        Version = CheckpointVersion,
        EngineId = _engineId,
        IdCounter = _idCounter,
        TargetSessionId = _sessionId,
        TargetWindowId = _target.WindowId,
        TargetProcessId = _target.ProcessId,
        TargetExecutableFileName = _target.ExecutableFileName,
        TargetExecutablePathFingerprint = _target.ExecutablePathFingerprint,
        Phase = _phase,
        Now = _now,
        ActiveTicks = _active.Ticks,
        LastActivityTicks = _lastActivity.Ticks,
        CheckAskedAtTicks = _checkAskedAt?.Ticks,
        CheckPending = _checkPending,
        IndefiniteWatch = _indefinite,
        PendingExit = _pendingExit,
        Tasks = [.. _tasks],
        RecentAlerts = [.. _recentAlerts],
        RejectedEvents = _rejectedEvents,
        SuppressedAlerts = _suppressedAlerts,
    };

    /// <summary>
    /// Restores a validated checkpoint after a runtime restart. A session that was attached
    /// starts in recovery: nothing is captured until it is re-authorized or its exit is reported.
    /// Suspensions are not restored; the restarted runtime observes the system afresh.
    /// </summary>
    public static WatchbunEngine Restore(WatchbunCheckpoint checkpoint, DateTimeOffset now, WatchbunConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var config = configuration ?? WatchbunConfiguration.Default;
        config.Validate();
        var target = checkpoint.Validate(config);
        if (now < checkpoint.Now)
        {
            throw new ArgumentOutOfRangeException(nameof(now), "Restore time precedes the checkpoint.");
        }

        var engine = new WatchbunEngine(config, checkpoint.EngineId, target)
        {
            _idCounter = checkpoint.IdCounter,
            _sessionId = checkpoint.TargetSessionId,
            _phase = checkpoint.Phase is WatchbunPhase.Watching or WatchbunPhase.Dozing ? WatchbunPhase.Recovering : checkpoint.Phase,
            _now = now,
            _active = TimeSpan.FromTicks(checkpoint.ActiveTicks),
            _lastActivity = TimeSpan.FromTicks(checkpoint.LastActivityTicks),
            _pendingExit = checkpoint.PendingExit,
            _rejectedEvents = checkpoint.RejectedEvents,
            _suppressedAlerts = checkpoint.SuppressedAlerts,
        };
        if (engine._phase != WatchbunPhase.Recovering)
        {
            engine._checkPending = checkpoint.CheckPending;
            engine._checkAskedAt = checkpoint.CheckAskedAtTicks is { } asked ? TimeSpan.FromTicks(asked) : null;
            engine._indefinite = checkpoint.IndefiniteWatch;
        }

        engine._tasks.AddRange(checkpoint.Tasks);
        foreach (var alert in checkpoint.RecentAlerts)
        {
            engine._recentAlerts.Enqueue(alert);
        }

        return engine;
    }

    internal static bool ValidEventKey(string? key) => key is not null && EventKeyPattern().IsMatch(key);

    internal static bool SameExecutable(CaptureTargetIdentity candidate, CaptureTargetIdentity bound) =>
        string.Equals(candidate.ExecutableFileName, bound.ExecutableFileName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(candidate.ExecutablePathFingerprint, bound.ExecutablePathFingerprint, StringComparison.Ordinal);

    private static void RequireEventKey(string? key)
    {
        if (!ValidEventKey(key))
        {
            throw new ArgumentException("Event keys are short lowercase identifiers.", nameof(key));
        }
    }

    private bool ClockRuns => _suspensions.Count == 0 && _phase is WatchbunPhase.Watching or WatchbunPhase.Dozing;

    private WatchbunRefusal? Gate()
    {
        if (_phase == WatchbunPhase.Closed)
        {
            return WatchbunRefusal.Closed;
        }

        if (_phase is not (WatchbunPhase.Watching or WatchbunPhase.Dozing))
        {
            return WatchbunRefusal.NotAllowed;
        }

        return _suspensions.Count > 0 ? WatchbunRefusal.Suspended : null;
    }

    /// <summary>
    /// Advances wall time to <paramref name="now"/>, stepping active (attached, unsuspended)
    /// time deadline by deadline so a long jump passes through each stage in order.
    /// </summary>
    private void Advance(DateTimeOffset now, List<WatchbunIntent> intents)
    {
        if (now < _now)
        {
            throw new ArgumentOutOfRangeException(nameof(now), "Time cannot move backwards.");
        }

        var remaining = now - _now;
        while (ClockRuns)
        {
            HoldQuietForTasks();
            var deadline = NextDeadline();
            if (deadline < _active)
            {
                deadline = _active;
            }

            if (deadline is null || deadline.Value - _active > remaining)
            {
                _active += remaining;
                HoldQuietForTasks();
                break;
            }

            var step = deadline.Value - _active;
            _active = deadline.Value;
            remaining -= step;
            HoldQuietForTasks();
            HandleDeadline(intents);
        }

        _now = now;
    }

    private void HoldQuietForTasks()
    {
        // A pending Watchbun Task means the session is not quiet.
        if (_tasks.Count > 0)
        {
            _lastActivity = _active;
        }
    }

    private TimeSpan? NextDeadline()
    {
        TimeSpan? next = _tasks.Count > 0 ? _tasks.Min(task => task.ExpiresAtActive) : null;
        if (_phase == WatchbunPhase.Watching && _tasks.Count == 0)
        {
            next = Min(next, _lastActivity + _configuration.QuietThreshold);
        }

        if (_phase == WatchbunPhase.Dozing && _checkPending && !_indefinite && _checkAskedAt is { } asked)
        {
            next = Min(next, asked + _configuration.SecondQuietThreshold);
        }

        return next;
    }

    private static TimeSpan? Min(TimeSpan? left, TimeSpan right) => left is { } value && value < right ? value : right;

    private void HandleDeadline(List<WatchbunIntent> intents)
    {
        foreach (var task in _tasks.Where(task => task.ExpiresAtActive <= _active).ToArray())
        {
            _tasks.Remove(task);
            intents.Add(new WatchbunIntent(WatchbunIntentKind.WatchTaskExpired, EventKey: task.EventKey, TaskId: task.TaskId));
        }

        if (_phase == WatchbunPhase.Watching && _tasks.Count == 0 && _active - _lastActivity >= _configuration.QuietThreshold)
        {
            _phase = WatchbunPhase.Dozing;
            intents.Add(new WatchbunIntent(WatchbunIntentKind.SemanticSpendingPaused));
            if (!_indefinite)
            {
                _checkPending = true;
                _checkAskedAt = _active;
                intents.Add(new WatchbunIntent(WatchbunIntentKind.QuietCheckAsked));
            }
        }
        else if (_phase == WatchbunPhase.Dozing && _checkPending && !_indefinite
                 && _checkAskedAt is { } asked && _active - asked >= _configuration.SecondQuietThreshold)
        {
            // Unanswered: a neutral, safe close. Non-response carries no preference signal.
            SafeClose(intents, CloseReason.QuietUnanswered);
        }
    }

    private void RegisterActivity(List<WatchbunIntent> intents, bool bossPresent)
    {
        _lastActivity = _active;
        if (_phase == WatchbunPhase.Dozing)
        {
            _phase = WatchbunPhase.Watching;
            if (_checkPending)
            {
                _checkPending = false;
                _checkAskedAt = null;
                intents.Add(new WatchbunIntent(WatchbunIntentKind.QuietCheckCleared));
            }

            intents.Add(new WatchbunIntent(WatchbunIntentKind.SemanticSpendingResumed));
        }

        if (bossPresent && _indefinite)
        {
            _indefinite = false;
            intents.Add(new WatchbunIntent(WatchbunIntentKind.IndefiniteWatchEnded));
        }
    }

    private void RaiseAlert(List<WatchbunIntent> intents, AlertKind kind, string key, Guid? taskId)
    {
        while (_recentAlerts.Count > 0 && _recentAlerts.Peek() <= _now - _configuration.AlertWindow)
        {
            _recentAlerts.Dequeue();
        }

        if (_recentAlerts.Count >= _configuration.MaximumAlertsPerWindow)
        {
            _suppressedAlerts++;
            return;
        }

        _recentAlerts.Enqueue(_now);
        intents.Add(new WatchbunIntent(WatchbunIntentKind.AlertRaised, Alert: kind, EventKey: key, TaskId: taskId, Target: _target, NoFocusSteal: true));
    }

    private void SafeClose(List<WatchbunIntent> intents, CloseReason reason)
    {
        foreach (var task in _tasks)
        {
            intents.Add(new WatchbunIntent(WatchbunIntentKind.WatchTaskCancelled, EventKey: task.EventKey, TaskId: task.TaskId));
        }

        _tasks.Clear();
        _phase = WatchbunPhase.Closed;
        _checkPending = false;
        _checkAskedAt = null;
        _indefinite = false;
        _pendingExit = null;
        _candidate = null;
        intents.Add(new WatchbunIntent(WatchbunIntentKind.CheckpointRequested));
        intents.Add(new WatchbunIntent(WatchbunIntentKind.ConsolidationRequested));
        intents.Add(new WatchbunIntent(WatchbunIntentKind.SessionClosed, Close: reason));
        intents.Add(new WatchbunIntent(WatchbunIntentKind.Napping));
    }

    private Guid NextId()
    {
        Span<byte> input = stackalloc byte[24];
        _engineId.TryWriteBytes(input, bigEndian: true, out _);
        BinaryPrimitives.WriteInt64BigEndian(input[16..], ++_idCounter);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        var bytes = hash[..16].ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private WatchbunUpdate Update(List<WatchbunIntent> intents, WatchbunRefusal refusal = WatchbunRefusal.None) =>
        new(intents.AsReadOnly(), Snapshot(), refusal);

    private WatchbunSnapshot Snapshot()
    {
        var unsuspended = _suspensions.Count == 0;
        var capture = !unsuspended ? CaptureMode.None : _phase switch
        {
            WatchbunPhase.Watching => CaptureMode.Full,
            WatchbunPhase.Dozing => CaptureMode.LocalWakeOnly,
            _ => CaptureMode.None,
        };
        var attached = _phase is WatchbunPhase.Watching or WatchbunPhase.Dozing;
        return new WatchbunSnapshot(
            _phase,
            capture,
            capture == CaptureMode.Full,
            _target,
            capture == CaptureMode.None ? null : _target,
            _sessionId,
            attached && _targetForeground,
            _checkPending,
            _indefinite,
            [.. _suspensions],
            attached ? _active - _lastActivity : TimeSpan.Zero,
            [.. _tasks],
            _pendingExit,
            _candidate,
            _rejectedEvents,
            _suppressedAlerts,
            _now);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex EventKeyPattern();
}
