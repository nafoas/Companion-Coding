using System.Reflection;
using System.Text.Json;
using CompanionCore.Capture.Contracts;
using static CompanionCore.Watchbun.Tests.WatchbunTestKit;

namespace CompanionCore.Watchbun.Tests;

public sealed class WatchbunMechanicsTests
{
    [Fact]
    public void Scenario7_WatchTasks_AreBoundedInCountAndLifetime()
    {
        var engine = Engine(out _);
        for (var index = 0; index < WatchbunConfiguration.Default.MaximumWatchTasks; index++)
        {
            Assert.Equal(WatchbunRefusal.None, engine.AddWatchTask($"task.{index}", null, At(index)).Refusal);
        }

        Assert.Equal(WatchbunRefusal.TaskLimit, engine.AddWatchTask("task.extra", null, At(10)).Refusal);
        Assert.Equal(8, engine.Current.WatchTasks.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.AddWatchTask("task.long", TimeSpan.FromHours(4) + TimeSpan.FromTicks(1), At(11)));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.AddWatchTask("task.zero", TimeSpan.Zero, At(11)));
        Assert.Throws<ArgumentException>(() => engine.AddWatchTask("Upper.Case", null, At(11)));
        Assert.Throws<ArgumentException>(() => engine.AddWatchTask(new string('k', 129), null, At(11)));
        Assert.Throws<ArgumentException>(() => engine.AddWatchTask(null!, null, At(11)));

        var defaultTask = engine.Current.WatchTasks[0];
        Assert.Equal(WatchbunConfiguration.Default.DefaultTaskLifetime, defaultTask.ExpiresAtActive);
        var longest = Engine(out _).AddWatchTask("task.max", TimeSpan.FromHours(4), At(0));
        Assert.Equal(TimeSpan.FromHours(4), Assert.Single(longest.Snapshot.WatchTasks).ExpiresAtActive);
    }

    [Fact]
    public void Scenario7_WatchTaskCompletion_AlertsAndExpiryIsVisible()
    {
        var engine = Engine(out var grant);
        var first = engine.AddWatchTask("door.opened", null, At(0)).Snapshot.WatchTasks.Single();
        var second = engine.AddWatchTask("door.opened", null, At(1)).Snapshot.WatchTasks.Last();
        var other = engine.AddWatchTask("bell.rang", TimeSpan.FromMinutes(10), At(2)).Snapshot.WatchTasks.Last();
        Assert.Equal(3, new[] { first.TaskId, second.TaskId, other.TaskId }.Distinct().Count());

        var completed = engine.OnGameEvent(Event(grant, GameEventKind.Change, "door.opened"), At(3));
        Assert.Equal(
            [WatchbunIntentKind.WatchTaskCompleted, WatchbunIntentKind.AlertRaised, WatchbunIntentKind.WatchTaskCompleted, WatchbunIntentKind.AlertRaised],
            Kinds(completed));
        Assert.All(completed.Intents.Where(intent => intent.Kind == WatchbunIntentKind.AlertRaised), alert =>
        {
            Assert.Equal(AlertKind.WatchTaskCompleted, alert.Alert);
            Assert.Equal(grant.Target, alert.Target);
            Assert.True(alert.NoFocusSteal);
        });
        Assert.Equal([first.TaskId, second.TaskId], completed.Intents.Where(intent => intent.Kind == WatchbunIntentKind.AlertRaised).Select(intent => intent.TaskId!.Value));
        Assert.Equal([other], completed.Snapshot.WatchTasks);

        var expired = engine.Tick(At(12));
        var expiry = Assert.Single(expired.Intents);
        Assert.Equal(WatchbunIntentKind.WatchTaskExpired, expiry.Kind);
        Assert.Equal(other.TaskId, expiry.TaskId);
        Assert.Empty(expired.Snapshot.WatchTasks);
    }

    [Fact]
    public void Scenario7_CompletionCountsAsActivity_AndCancellationIsVisible()
    {
        var engine = Engine(out var grant);
        var task = engine.AddWatchTask("door.opened", TimeSpan.FromHours(4), At(0)).Snapshot.WatchTasks.Single();
        engine.Tick(At(30));
        Assert.Equal(WatchbunRefusal.UnknownTask, engine.CancelWatchTask(Guid.NewGuid(), At(31)).Refusal);
        var cancelled = engine.CancelWatchTask(task.TaskId, At(31));
        Assert.Equal([WatchbunIntentKind.WatchTaskCancelled], Kinds(cancelled));
        Assert.Empty(cancelled.Snapshot.WatchTasks);
        Assert.Empty(engine.Tick(At(90.99)).Intents);

        // A completion of a minor change still counts as a meaningful watched event.
        var dozing = Engine(out var dozingGrant);
        dozing.Tick(At(60));
        dozing.AnswerQuietCheck(QuietAnswer.BackEventually, At(61));
        dozing.AddWatchTask("door.opened", null, At(62));
        Assert.Equal(WatchbunPhase.Watching, dozing.Current.Phase);
        Assert.True(dozing.Current.SemanticSpendingAllowed);
        Assert.False(dozing.Current.IndefiniteWatch);
        Assert.Contains(WatchbunIntentKind.AlertRaised, Kinds(dozing.OnGameEvent(Event(dozingGrant, GameEventKind.Change, "door.opened"), At(63))));
    }

    [Fact]
    public void Alerts_AreRateBounded_AndRecoverAfterTheWindow()
    {
        var engine = Engine(out var grant);
        var raised = 0;
        for (var index = 0; index < 10; index++)
        {
            raised += engine.OnGameEvent(Event(grant, GameEventKind.Urgent), T0.AddSeconds(index)).Intents.Count(intent => intent.Kind == WatchbunIntentKind.AlertRaised);
        }

        Assert.Equal(6, raised);
        Assert.Equal(4, engine.Current.SuppressedAlerts);
        Assert.Empty(engine.OnGameEvent(Event(grant, GameEventKind.Urgent), T0.AddSeconds(59)).Intents);
        Assert.Single(engine.OnGameEvent(Event(grant, GameEventKind.Urgent), T0.AddSeconds(60)).Intents);
    }

    [Fact]
    public void ClosedEngine_RefusesEveryAction()
    {
        var engine = Engine(out var grant);
        engine.Tick(At(60));
        engine.AnswerQuietCheck(QuietAnswer.Naptime, At(61));

        Assert.Equal(WatchbunRefusal.Closed, engine.OnInput(At(62)).Refusal);
        Assert.Equal(WatchbunRefusal.Closed, engine.OnGameEvent(Event(grant, GameEventKind.Urgent), At(62)).Refusal);
        Assert.Equal(WatchbunRefusal.Closed, engine.AddWatchTask("x", null, At(62)).Refusal);
        Assert.Equal(WatchbunRefusal.Closed, engine.CancelWatchTask(Guid.NewGuid(), At(62)).Refusal);
        Assert.Equal(WatchbunRefusal.Closed, engine.OnTargetExited(new TargetExit(grant.Target.ProcessId, 0, true, false), At(62)).Refusal);
        Assert.Equal(WatchbunRefusal.Closed, engine.Decide(ExitDecision.WaitForRelaunch, At(62)).Refusal);
        Assert.Equal(WatchbunRefusal.Closed, engine.OnTargetLaunched(grant.Target, At(62)).Refusal);
        Assert.Equal(WatchbunRefusal.Closed, engine.Reattach(Grant(), At(62)).Refusal);
        Assert.Equal(WatchbunRefusal.Closed, engine.OnSystemResume(SuspendReason.Lock, At(62)).Refusal);
        Assert.Empty(engine.Tick(At(10_000)).Intents);
        Assert.Equal(CaptureMode.None, engine.Current.Capture);
    }

    [Fact]
    public void SafeClose_CancelsPendingTasksVisibly()
    {
        var engine = Engine(out var grant);
        engine.AddWatchTask("door.opened", null, At(0));
        engine.OnTargetExited(new TargetExit(grant.Target.ProcessId, 0, true, false), At(1));

        var closed = engine.Decide(ExitDecision.Consolidate, At(2));

        Assert.Equal(
            [WatchbunIntentKind.WatchTaskCancelled, WatchbunIntentKind.CheckpointRequested, WatchbunIntentKind.ConsolidationRequested, WatchbunIntentKind.SessionClosed, WatchbunIntentKind.Napping],
            Kinds(closed));
        Assert.Empty(closed.Snapshot.WatchTasks);
    }

    [Fact]
    public void Reattach_IsRefusedWhileAttachedOrSuspended()
    {
        var engine = Engine(out var grant);
        Assert.Equal(WatchbunRefusal.NotAllowed, engine.Reattach(Grant(grant.Target), At(1)).Refusal);
        Assert.Equal(WatchbunRefusal.NotAllowed, engine.OnTargetLaunched(grant.Target, At(1)).Refusal);

        engine.OnTargetExited(new TargetExit(grant.Target.ProcessId, 0, true, false), At(2));
        Assert.Equal(WatchbunRefusal.NotAllowed, engine.OnTargetLaunched(grant.Target, At(3)).Refusal);
        Assert.Equal(WatchbunRefusal.NotAllowed, engine.OnTargetExited(new TargetExit(grant.Target.ProcessId, 0, true, false), At(3)).Refusal);
        engine.OnSystemSuspend(SuspendReason.Lock, At(4));
        Assert.Equal(WatchbunRefusal.Suspended, engine.Reattach(Grant(grant.Target), At(5)).Refusal);
        engine.OnSystemResume(SuspendReason.Lock, At(6));

        // Reattaching straight from the exit prompt is Boss's decision to continue.
        Assert.Equal(WatchbunPhase.Watching, engine.Reattach(Grant(Target(processId: 7)), At(7)).Snapshot.Phase);
    }

    [Fact]
    public void TimeCannotMoveBackwards_AndInputsAreValidated()
    {
        var engine = Engine(out var grant);
        engine.Tick(At(5));

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Tick(At(4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.OnGameEvent(new StructuredGameEvent(grant.TargetSessionId, (GameEventKind)9, "x"), At(6)));
        Assert.Throws<ArgumentException>(() => engine.OnGameEvent(new StructuredGameEvent(grant.TargetSessionId, GameEventKind.Urgent, "bad key"), At(6)));
        Assert.Throws<ArgumentNullException>(() => engine.OnGameEvent(null!, At(6)));
        Assert.Throws<ArgumentNullException>(() => engine.OnForegroundChanged(null!, At(6)));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.AnswerQuietCheck((QuietAnswer)9, At(6)));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Decide((ExitDecision)9, At(6)));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.OnSystemSuspend((SuspendReason)9, At(6)));
        Assert.Throws<ArgumentNullException>(() => new WatchbunEngine(null!, T0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WatchbunEngine(grant, default));
        Assert.Throws<ArgumentException>(() => new WatchbunEngine(grant, T0, engineId: Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WatchbunEngine(grant, T0, new WatchbunConfiguration { QuietThreshold = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WatchbunEngine(grant, T0, new WatchbunConfiguration { MaximumTaskLifetime = TimeSpan.FromMinutes(1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WatchbunEngine(grant, T0, new WatchbunConfiguration { MaximumAlertsPerWindow = 0 }));
    }

    [Fact]
    public void TaskIds_AreDeterministicPerEngine()
    {
        var first = Engine(out _);
        var second = Engine(out _);

        Assert.Equal(
            first.AddWatchTask("a", null, At(0)).Snapshot.WatchTasks.Single().TaskId,
            second.AddWatchTask("a", null, At(0)).Snapshot.WatchTasks.Single().TaskId);
        Assert.NotEqual(Guid.Empty, first.Current.WatchTasks.Single().TaskId);
    }

    public static TheoryData<string> InvalidCheckpoints => new()
    {
        "version", "engine", "session", "fingerprint", "phase", "last-activity", "check-without-ask",
        "ask-without-check", "dozing-without-reason", "indefinite-closed", "exit-mismatch", "expired-task",
        "overlong-task", "duplicate-task", "too-many-tasks", "closed-with-task", "future-alert", "negative-counter",
        "null-tasks", "bad-task-key",
    };

    [Theory]
    [MemberData(nameof(InvalidCheckpoints))]
    public void Scenario8_InvalidCheckpoints_AreRefused(string mutation)
    {
        var engine = Engine(out _);
        engine.AddWatchTask("door.opened", null, At(0));
        engine.Tick(At(10));
        var valid = engine.Checkpoint();
        var task = valid.Tasks[0];
        var checkpoint = mutation switch
        {
            "version" => valid with { Version = 2 },
            "engine" => valid with { EngineId = Guid.Empty },
            "session" => valid with { TargetSessionId = Guid.Empty },
            "fingerprint" => valid with { TargetExecutablePathFingerprint = "abc" },
            "phase" => valid with { Phase = (WatchbunPhase)42 },
            "last-activity" => valid with { LastActivityTicks = valid.ActiveTicks + 1 },
            "check-without-ask" => valid with { Phase = WatchbunPhase.Dozing, CheckPending = true },
            "ask-without-check" => valid with { CheckAskedAtTicks = 0 },
            "dozing-without-reason" => valid with { Phase = WatchbunPhase.Dozing },
            "indefinite-closed" => valid with { Phase = WatchbunPhase.AwaitingRelaunch, IndefiniteWatch = true },
            "exit-mismatch" => valid with { PendingExit = TargetExitKind.SuspectedCrash },
            "expired-task" => valid with { Tasks = [task with { ExpiresAtActive = TimeSpan.FromTicks(valid.ActiveTicks) }] },
            "overlong-task" => valid with { Tasks = [task with { ExpiresAtActive = TimeSpan.FromTicks(valid.ActiveTicks) + TimeSpan.FromHours(5) }] },
            "duplicate-task" => valid with { Tasks = [task, task] },
            "too-many-tasks" => valid with { Tasks = Enumerable.Range(0, 9).Select(index => task with { TaskId = Guid.NewGuid() }).ToList() },
            "closed-with-task" => valid with { Phase = WatchbunPhase.Closed },
            "future-alert" => valid with { RecentAlerts = [valid.Now.AddSeconds(1)] },
            "negative-counter" => valid with { RejectedEvents = -1 },
            "null-tasks" => valid with { Tasks = null! },
            "bad-task-key" => valid with { Tasks = [task with { EventKey = "Bad Key" }] },
            _ => throw new InvalidOperationException(mutation),
        };

        Assert.ThrowsAny<ArgumentException>(() => WatchbunEngine.Restore(checkpoint, At(20)));
        Assert.Equal(WatchbunPhase.Recovering, WatchbunEngine.Restore(valid, At(20)).Current.Phase);
    }

    [Fact]
    public void WatchbunAssembly_ReferencesOnlyCaptureContracts_AndCannotMintGrants()
    {
        var assembly = typeof(WatchbunEngine).Assembly;
        Assert.Equal(
            ["CompanionCore.Capture.Contracts"],
            assembly.GetReferencedAssemblies().Select(name => name.Name!).Where(name => name.StartsWith("CompanionCore.", StringComparison.Ordinal)).ToArray());
        Assert.Null(typeof(CaptureAuthorizationGrant).GetMethod("Issue", BindingFlags.Public | BindingFlags.Static));
        Assert.DoesNotContain(assembly.GetExportedTypes().SelectMany(type => type.GetMethods()).Where(method => !method.IsSpecialName), method =>
            method.Name.Contains("Retarget", StringComparison.Ordinal) || method.Name.Contains("Focus", StringComparison.Ordinal));
    }
}

public sealed class StructuredGameEventAdapterTests
{
    private static readonly Guid Session = Guid.Parse("11111111-2222-4333-8444-555555555555");

    [Theory]
    [InlineData("change", GameEventKind.Change)]
    [InlineData("meaningful", GameEventKind.Meaningful)]
    [InlineData("urgent", GameEventKind.Urgent)]
    public void ValidLines_AreAccepted(string kind, GameEventKind expected)
    {
        var adapter = new StructuredGameEventAdapter(Session);

        var verdict = adapter.Parse($"{{\"session\":\"{Session:D}\",\"kind\":\"{kind}\",\"key\":\"boss.spawned\"}}", out var gameEvent);

        Assert.Equal(AdapterVerdict.Accepted, verdict);
        Assert.Equal(new StructuredGameEvent(Session, expected, "boss.spawned"), gameEvent);
    }

    [Theory]
    [InlineData(null, AdapterVerdict.Malformed)]
    [InlineData("", AdapterVerdict.Malformed)]
    [InlineData("not json", AdapterVerdict.Malformed)]
    [InlineData("[1]", AdapterVerdict.Malformed)]
    [InlineData("{\"session\":\"S\",\"kind\":\"urgent\"}", AdapterVerdict.Malformed)]
    [InlineData("{\"session\":\"S\",\"kind\":\"urgent\",\"key\":\"a\",\"key\":\"b\"}", AdapterVerdict.Malformed)]
    [InlineData("{\"session\":\"S\",\"kind\":\"urgent\",\"key\":\"a\",\"extra\":\"x\"}", AdapterVerdict.UnknownField)]
    [InlineData("{\"session\":\"S\",\"kind\":\"urgent\",\"key\":\"a\",\"extra\":1}", AdapterVerdict.UnknownField)]
    [InlineData("{\"session\":\"S\",\"kind\":1,\"key\":\"a\"}", AdapterVerdict.InvalidValue)]
    [InlineData("{\"session\":\"S\",\"kind\":\"loud\",\"key\":\"a\"}", AdapterVerdict.InvalidValue)]
    [InlineData("{\"session\":\"S\",\"kind\":\"Urgent\",\"key\":\"a\"}", AdapterVerdict.InvalidValue)]
    [InlineData("{\"session\":\"S\",\"kind\":\"urgent\",\"key\":\"Bad Key\"}", AdapterVerdict.InvalidValue)]
    [InlineData("{\"session\":\"not-a-guid\",\"kind\":\"urgent\",\"key\":\"a\"}", AdapterVerdict.InvalidValue)]
    [InlineData("{\"session\":\"S\",\"kind\":\"urgent\",\"key\":{\"a\":{}}}", AdapterVerdict.Malformed)]
    [InlineData("{\"session\":\"99999999-2222-4333-8444-555555555555\",\"kind\":\"urgent\",\"key\":\"a\"}", AdapterVerdict.WrongTarget)]
    public void InvalidLines_AreRefused(string? line, AdapterVerdict expected)
    {
        var adapter = new StructuredGameEventAdapter(Session);

        var verdict = adapter.Parse(line?.Replace("\"S\"", $"\"{Session:D}\"", StringComparison.Ordinal), out var gameEvent);

        Assert.Equal(expected, verdict);
        Assert.Null(gameEvent);
    }

    [Fact]
    public void OversizedLines_AreRefused_AtTheByteBound()
    {
        var adapter = new StructuredGameEventAdapter(Session);
        var prefix = $"{{\"session\":\"{Session:D}\",\"kind\":\"urgent\",\"key\":\"a\"}}";
        var padded = prefix + new string(' ', StructuredGameEventAdapter.MaximumLineBytes - prefix.Length);

        Assert.Equal(AdapterVerdict.Accepted, adapter.Parse(padded, out _));
        Assert.Equal(AdapterVerdict.Oversized, adapter.Parse(padded + " ", out _));
        Assert.Equal(AdapterVerdict.Oversized, adapter.Parse(prefix + new string('é', 600), out _));
        Assert.Throws<ArgumentException>(() => new StructuredGameEventAdapter(Guid.Empty));
    }

    [Fact]
    public void AdapterEvents_DriveTheEngineEndToEnd()
    {
        var grant = Grant();
        var engine = new WatchbunEngine(grant, T0);
        var adapter = new StructuredGameEventAdapter(grant.TargetSessionId);
        Assert.Equal(AdapterVerdict.Accepted, adapter.Parse($"{{\"key\":\"enemy.group\",\"kind\":\"urgent\",\"session\":\"{grant.TargetSessionId:D}\"}}", out var gameEvent));

        var alert = Assert.Single(engine.OnGameEvent(gameEvent!, At(1)).Intents);

        Assert.Equal(AlertKind.Urgent, alert.Alert);
        Assert.Equal(grant.Target, alert.Target);
    }
}
