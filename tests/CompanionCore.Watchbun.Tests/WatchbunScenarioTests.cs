using CompanionCore.Capture.Contracts;
using static CompanionCore.Watchbun.Tests.WatchbunTestKit;

namespace CompanionCore.Watchbun.Tests;

/// <summary>Roadmap Stage 9 Paw Gate scenarios, in neutral form.</summary>
public sealed class WatchbunScenarioTests
{
    [Fact]
    public void Scenario1_TabAway_GameChange_RaisesTargetOnlyNonFocusStealingAlert()
    {
        var engine = Engine(out var grant);
        var other = Target(processId: 999, windowId: 0x9999, executable: "browser.exe", fingerprint: 'B');
        AssertCaptureBound(engine.OnForegroundChanged(new ForegroundWindow(other.WindowId, other.ProcessId), At(1)), grant.Target);

        var alert = engine.OnGameEvent(Event(grant, GameEventKind.Urgent, "enemy.group"), At(2));

        var raised = Assert.Single(alert.Intents);
        Assert.Equal(WatchbunIntentKind.AlertRaised, raised.Kind);
        Assert.Equal(AlertKind.Urgent, raised.Alert);
        Assert.Equal("enemy.group", raised.EventKey);
        Assert.Equal(grant.Target, raised.Target);
        Assert.True(raised.NoFocusSteal);
        Assert.Equal(CaptureMode.Full, alert.Snapshot.Capture);
        Assert.Equal(grant.Target, alert.Snapshot.CaptureTarget);
        Assert.False(alert.Snapshot.TargetForeground);
        AssertCaptureBound(alert, grant.Target);

        var foreign = engine.OnGameEvent(new StructuredGameEvent(Guid.NewGuid(), GameEventKind.Urgent, "enemy.group"), At(3));
        Assert.Equal(WatchbunRefusal.WrongTarget, foreign.Refusal);
        Assert.Empty(foreign.Intents);
        Assert.Equal(1, foreign.Snapshot.RejectedEvents);

        Assert.Empty(engine.OnGameEvent(Event(grant, GameEventKind.Change), At(4)).Intents);
        Assert.Empty(engine.OnGameEvent(Event(grant, GameEventKind.Meaningful), At(5)).Intents);
    }

    [Fact]
    public void Scenario2_NapListedForeground_IsNeverAttributedOrCaptured_TargetNeverRetargets()
    {
        var engine = Engine(out var grant);
        var napListed = Target(processId: 777, windowId: 0x7777, executable: "password-manager.exe", fingerprint: 'C');
        var updates = new List<WatchbunUpdate> { engine.OnForegroundChanged(new ForegroundWindow(napListed.WindowId, napListed.ProcessId), At(0)) };

        for (var minute = 1; minute < 60; minute++)
        {
            updates.Add(engine.OnInput(At(minute)));
        }

        Assert.Equal(TimeSpan.FromMinutes(59), engine.Current.QuietElapsed);
        Assert.Equal(WatchbunPhase.Watching, engine.Current.Phase);
        var asked = engine.OnInput(At(60));
        updates.Add(asked);
        Assert.Contains(WatchbunIntentKind.QuietCheckAsked, Kinds(asked));

        // Same window ID in a different process (or the reverse) is still not the target.
        updates.Add(engine.OnForegroundChanged(new ForegroundWindow(grant.Target.WindowId, napListed.ProcessId), At(61)));
        updates.Add(engine.OnInput(At(62)));
        Assert.True(engine.Current.QuietCheckPending);
        updates.Add(engine.OnForegroundChanged(new ForegroundWindow(napListed.WindowId, grant.Target.ProcessId), At(62.5)));
        updates.Add(engine.OnInput(At(62.6)));
        Assert.True(engine.Current.QuietCheckPending);
        Assert.False(engine.Current.TargetForeground);

        updates.Add(engine.OnForegroundChanged(new ForegroundWindow(grant.Target.WindowId, grant.Target.ProcessId), At(63)));
        var back = engine.OnInput(At(64));
        updates.Add(back);
        Assert.Equal([WatchbunIntentKind.QuietCheckCleared, WatchbunIntentKind.SemanticSpendingResumed], Kinds(back));
        Assert.True(back.Snapshot.TargetForeground);

        Assert.All(updates, update =>
        {
            AssertCaptureBound(update, grant.Target);
            Assert.Equal(grant.Target, update.Snapshot.BoundTarget);
            Assert.DoesNotContain(update.Intents, intent => intent.Target == napListed);
        });
    }

    [Fact]
    public void Scenario3_TwoQuietHours_AskOnceThenSafelyClose_InOrder()
    {
        var engine = Engine(out var grant);

        Assert.Empty(engine.Tick(At(59.99)).Intents);
        var asked = engine.Tick(At(60));
        Assert.Equal([WatchbunIntentKind.SemanticSpendingPaused, WatchbunIntentKind.QuietCheckAsked], Kinds(asked));
        Assert.Equal(WatchbunPhase.Dozing, asked.Snapshot.Phase);
        Assert.Equal(CaptureMode.LocalWakeOnly, asked.Snapshot.Capture);
        Assert.False(asked.Snapshot.SemanticSpendingAllowed);
        Assert.Equal(grant.Target, asked.Snapshot.CaptureTarget);

        Assert.Empty(engine.Tick(At(119.99)).Intents);
        var closed = engine.Tick(At(120));
        Assert.Equal(
            [WatchbunIntentKind.CheckpointRequested, WatchbunIntentKind.ConsolidationRequested, WatchbunIntentKind.SessionClosed, WatchbunIntentKind.Napping],
            Kinds(closed));
        Assert.Equal(CloseReason.QuietUnanswered, closed.Intents[2].Close);
        Assert.Equal(WatchbunPhase.Closed, closed.Snapshot.Phase);
        Assert.Equal(CaptureMode.None, closed.Snapshot.Capture);
        Assert.Null(closed.Snapshot.CaptureTarget);

        // One long jump passes through both stages in order.
        var jumping = Engine(out _);
        Assert.Equal(
            [WatchbunIntentKind.SemanticSpendingPaused, WatchbunIntentKind.QuietCheckAsked, WatchbunIntentKind.CheckpointRequested, WatchbunIntentKind.ConsolidationRequested, WatchbunIntentKind.SessionClosed, WatchbunIntentKind.Napping],
            Kinds(jumping.Tick(At(600))));
    }

    [Fact]
    public void Scenario3_ActivityResetsQuiet_AndAPendingWatchTaskHoldsItOff()
    {
        var engine = Engine(out var grant);
        engine.OnForegroundChanged(new ForegroundWindow(grant.Target.WindowId, grant.Target.ProcessId), At(0));
        engine.OnInput(At(30));
        Assert.Empty(engine.Tick(At(89.99)).Intents);
        Assert.Contains(WatchbunIntentKind.QuietCheckAsked, Kinds(engine.Tick(At(90))));

        // A minor change neither wakes attention nor resets quiet time.
        var minor = engine.OnGameEvent(Event(grant, GameEventKind.Change), At(95));
        Assert.Empty(minor.Intents);
        Assert.Equal(WatchbunPhase.Dozing, minor.Snapshot.Phase);
        Assert.Equal(TimeSpan.FromMinutes(65), minor.Snapshot.QuietElapsed);
        var meaningful = engine.OnGameEvent(Event(grant, GameEventKind.Meaningful), At(100));
        Assert.Equal([WatchbunIntentKind.QuietCheckCleared, WatchbunIntentKind.SemanticSpendingResumed], Kinds(meaningful));
        Assert.Empty(engine.Tick(At(159.99)).Intents);
        Assert.Contains(WatchbunIntentKind.QuietCheckAsked, Kinds(engine.Tick(At(160))));

        var held = Engine(out _);
        held.AddWatchTask("treasure.found", TimeSpan.FromHours(2), At(0));
        Assert.Empty(held.Tick(At(119)).Intents);
        Assert.Equal(TimeSpan.Zero, held.Current.QuietElapsed);
        Assert.Equal([WatchbunIntentKind.WatchTaskExpired], Kinds(held.Tick(At(120))));
        Assert.Empty(held.Tick(At(179.99)).Intents);
        Assert.Contains(WatchbunIntentKind.QuietCheckAsked, Kinds(held.Tick(At(180))));
    }

    [Fact]
    public void Scenario3_NonResponseCarriesNoPreferenceOrRelationshipSignal()
    {
        Assert.DoesNotContain(Enum.GetNames<WatchbunIntentKind>(), name =>
            name.Contains("Preference", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Relationship", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Mood", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(WatchbunIntent).GetProperties(), property =>
            property.Name.Contains("Preference", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Relationship", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scenario4_IndefiniteWatch_NoAbandonmentTimer_SpendingStaysBounded_ReturnRestoresTimers()
    {
        var engine = Engine(out var grant);
        engine.Tick(At(60));

        var eventually = engine.AnswerQuietCheck(QuietAnswer.BackEventually, At(61));
        Assert.Equal([WatchbunIntentKind.QuietCheckCleared, WatchbunIntentKind.IndefiniteWatchEnabled], Kinds(eventually));
        Assert.Equal(WatchbunRefusal.NotAllowed, engine.AnswerQuietCheck(QuietAnswer.Naptime, At(61)).Refusal);
        Assert.Equal(WatchbunPhase.Dozing, engine.Current.Phase);
        Assert.Equal(WatchbunPhase.Dozing, eventually.Snapshot.Phase);
        Assert.False(eventually.Snapshot.SemanticSpendingAllowed);
        Assert.Equal(CaptureMode.LocalWakeOnly, eventually.Snapshot.Capture);

        for (var hour = 2; hour <= 48; hour++)
        {
            var tick = engine.Tick(At(hour * 60));
            Assert.Empty(tick.Intents);
            Assert.False(tick.Snapshot.SemanticSpendingAllowed);
            Assert.Equal(WatchbunPhase.Dozing, tick.Snapshot.Phase);
        }

        // A meaningful game change wakes attention; the indefinite period continues without asking.
        var woke = engine.OnGameEvent(Event(grant, GameEventKind.Meaningful), At(48 * 60 + 1));
        Assert.Equal([WatchbunIntentKind.SemanticSpendingResumed], Kinds(woke));
        Assert.True(woke.Snapshot.IndefiniteWatch);
        Assert.Equal([WatchbunIntentKind.SemanticSpendingPaused], Kinds(engine.Tick(At(49 * 60 + 1))));
        Assert.Empty(engine.Tick(At(60 * 60)).Intents);

        // Boss returns: the indefinite period ends and normal timers apply again.
        engine.OnForegroundChanged(new ForegroundWindow(grant.Target.WindowId, grant.Target.ProcessId), At(60 * 60));
        var returned = engine.OnInput(At(60 * 60 + 1));
        Assert.Equal([WatchbunIntentKind.SemanticSpendingResumed, WatchbunIntentKind.IndefiniteWatchEnded], Kinds(returned));
        Assert.Contains(WatchbunIntentKind.QuietCheckAsked, Kinds(engine.Tick(At(61 * 60 + 1))));
        Assert.Contains(WatchbunIntentKind.SessionClosed, Kinds(engine.Tick(At(62 * 60 + 1))));
    }

    [Fact]
    public void Scenario4_OtherAnswers_ResumeOrCloseSafely()
    {
        var keep = Engine(out _);
        keep.Tick(At(60));
        var resumed = keep.AnswerQuietCheck(QuietAnswer.KeepWatching, At(61));
        Assert.Equal([WatchbunIntentKind.QuietCheckCleared, WatchbunIntentKind.SemanticSpendingResumed], Kinds(resumed));
        Assert.Equal(WatchbunPhase.Watching, resumed.Snapshot.Phase);
        Assert.Equal(WatchbunRefusal.NotAllowed, keep.AnswerQuietCheck(QuietAnswer.KeepWatching, At(62)).Refusal);
        Assert.Empty(keep.Tick(At(120.99)).Intents);

        var nap = Engine(out _);
        nap.Tick(At(60));
        var napped = nap.AnswerQuietCheck(QuietAnswer.Naptime, At(61));
        Assert.Equal(CloseReason.Naptime, napped.Intents.Single(intent => intent.Kind == WatchbunIntentKind.SessionClosed).Close);
        Assert.Equal(WatchbunRefusal.Closed, nap.AnswerQuietCheck(QuietAnswer.KeepWatching, At(62)).Refusal);
    }

    [Theory]
    [InlineData(0, true, false, TargetExitKind.DeliberateClose)]
    [InlineData(1, true, false, TargetExitKind.SuspectedCrash)]
    [InlineData(-1073741819, false, false, TargetExitKind.SuspectedCrash)]
    [InlineData(0, false, false, TargetExitKind.SuspectedCrash)]
    [InlineData(0, true, true, TargetExitKind.SuspectedCrash)]
    public void Scenario5_ExitClassification_DistinguishesCloseFromSuspectedCrash(int exitCode, bool windowClosedFirst, bool hung, TargetExitKind expected)
    {
        var engine = Engine(out var grant);

        var exited = engine.OnTargetExited(new TargetExit(grant.Target.ProcessId, exitCode, windowClosedFirst, hung), At(5));

        Assert.Equal([WatchbunIntentKind.CaptureStopped, WatchbunIntentKind.ExitPromptRaised], Kinds(exited));
        Assert.Equal(expected, exited.Intents[1].Exit);
        Assert.Equal(WatchbunPhase.ExitPending, exited.Snapshot.Phase);
        Assert.Equal(CaptureMode.None, exited.Snapshot.Capture);
        Assert.False(exited.Snapshot.SemanticSpendingAllowed);
        Assert.Equal(expected, exited.Snapshot.PendingExit);
    }

    [Fact]
    public void Scenario5_CrashRelaunch_PreservesSessionUntilBossDecides_AndReauthorizesSameExecutableOnly()
    {
        var engine = Engine(out var grant);
        Assert.Equal(WatchbunRefusal.WrongTarget, engine.OnTargetExited(new TargetExit(1, 1, false, false), At(1)).Refusal);
        engine.OnTargetExited(new TargetExit(grant.Target.ProcessId, 1, false, false), At(2));

        // Preserved indefinitely: no timer ends a session awaiting Boss's decision.
        var waiting = engine.Tick(At(10 * 60));
        Assert.Empty(waiting.Intents);
        Assert.Equal(WatchbunPhase.ExitPending, waiting.Snapshot.Phase);

        Assert.Equal([WatchbunIntentKind.AwaitingRelaunch], Kinds(engine.Decide(ExitDecision.WaitForRelaunch, At(10 * 60 + 1))));
        Assert.Equal(WatchbunRefusal.NotAllowed, engine.Decide(ExitDecision.WaitForRelaunch, At(10 * 60 + 2)).Refusal);

        var otherExecutable = Target(processId: 5000, executable: "other-game.exe");
        var otherPath = Target(processId: 5000, fingerprint: 'D');
        Assert.Equal(WatchbunRefusal.DifferentExecutable, engine.OnTargetLaunched(otherExecutable, At(10 * 60 + 3)).Refusal);
        Assert.Equal(WatchbunRefusal.DifferentExecutable, engine.OnTargetLaunched(otherPath, At(10 * 60 + 3)).Refusal);

        var relaunched = Target(processId: 5001, windowId: 0x5001, executable: "SYNTHETIC-GAME.EXE");
        var request = engine.OnTargetLaunched(relaunched, At(10 * 60 + 4));
        var reauth = Assert.Single(request.Intents);
        Assert.Equal(WatchbunIntentKind.ReauthorizationRequested, reauth.Kind);
        Assert.Equal(relaunched, reauth.Target);
        Assert.Equal(CaptureMode.None, request.Snapshot.Capture);
        Assert.Null(request.Snapshot.CaptureTarget);
        Assert.Equal(relaunched, request.Snapshot.RelaunchCandidate);

        Assert.Equal(WatchbunRefusal.DifferentExecutable, engine.Reattach(Grant(otherExecutable), At(10 * 60 + 5)).Refusal);
        var newGrant = Grant(relaunched);
        var reattached = engine.Reattach(newGrant, At(10 * 60 + 6));
        Assert.Equal([WatchbunIntentKind.Reattached], Kinds(reattached));
        Assert.Equal(WatchbunPhase.Watching, reattached.Snapshot.Phase);
        Assert.Equal(CaptureMode.Full, reattached.Snapshot.Capture);
        Assert.Equal(relaunched, reattached.Snapshot.CaptureTarget);
        Assert.Equal(newGrant.TargetSessionId, reattached.Snapshot.TargetSessionId);
        Assert.Null(reattached.Snapshot.RelaunchCandidate);

        Assert.Equal(WatchbunRefusal.WrongTarget, engine.OnGameEvent(Event(grant, GameEventKind.Urgent), At(10 * 60 + 7)).Refusal);
        Assert.Single(engine.OnGameEvent(Event(newGrant, GameEventKind.Urgent), At(10 * 60 + 8)).Intents);
        Assert.Equal(WatchbunRefusal.NotAllowed, engine.Decide(ExitDecision.Consolidate, At(10 * 60 + 9)).Refusal);
    }

    [Fact]
    public void Scenario5_PausedAdventure_StaysRecoverable_OrConsolidates()
    {
        var engine = Engine(out var grant);
        engine.OnTargetExited(new TargetExit(grant.Target.ProcessId, 0, true, false), At(1));

        var paused = engine.Decide(ExitDecision.PreservePaused, At(2));
        Assert.Equal([WatchbunIntentKind.CheckpointRequested, WatchbunIntentKind.AdventurePaused], Kinds(paused));
        Assert.Equal(WatchbunPhase.PausedAdventure, paused.Snapshot.Phase);
        Assert.Null(paused.Snapshot.PendingExit);
        Assert.Empty(engine.Tick(At(30 * 24 * 60)).Intents);

        var relaunched = Target(processId: 6000);
        Assert.Equal(WatchbunIntentKind.ReauthorizationRequested, Assert.Single(engine.OnTargetLaunched(relaunched, At(30 * 24 * 60 + 1)).Intents).Kind);
        Assert.Equal(WatchbunPhase.Watching, engine.Reattach(Grant(relaunched), At(30 * 24 * 60 + 2)).Snapshot.Phase);

        var consolidating = Engine(out var second);
        consolidating.OnTargetExited(new TargetExit(second.Target.ProcessId, 0, true, false), At(1));
        consolidating.Decide(ExitDecision.PreservePaused, At(2));
        var closed = consolidating.Decide(ExitDecision.Consolidate, At(3));
        Assert.Equal(CloseReason.ExitConsolidated, closed.Intents.Single(intent => intent.Kind == WatchbunIntentKind.SessionClosed).Close);
        Assert.Contains(WatchbunIntentKind.ConsolidationRequested, Kinds(closed));
    }

    [Fact]
    public void Scenario6_LockSleepWake_SuspendsCaptureFreezesClocksAndRestoresExactState()
    {
        var engine = Engine(out var grant);
        engine.AddWatchTask("door.opened", TimeSpan.FromMinutes(45), At(0));
        engine.Tick(At(30));

        var locked = engine.OnSystemSuspend(SuspendReason.Lock, At(30));
        Assert.Equal([WatchbunIntentKind.CaptureSuspended], Kinds(locked));
        Assert.Equal(CaptureMode.None, locked.Snapshot.Capture);
        Assert.False(locked.Snapshot.SemanticSpendingAllowed);
        Assert.Empty(engine.OnSystemSuspend(SuspendReason.Sleep, At(40)).Intents);
        Assert.Empty(engine.OnSystemSuspend(SuspendReason.Sleep, At(41)).Intents);
        Assert.Empty(engine.Tick(At(8 * 60)).Intents);
        Assert.Equal(WatchbunRefusal.Suspended, engine.OnGameEvent(Event(grant, GameEventKind.Urgent), At(8 * 60)).Refusal);
        Assert.Equal(WatchbunRefusal.Suspended, engine.AddWatchTask("x", null, At(8 * 60)).Refusal);

        Assert.Empty(engine.OnSystemResume(SuspendReason.Sleep, At(8 * 60 + 1)).Intents);
        Assert.Equal(CaptureMode.None, engine.Current.Capture);
        var restored = engine.OnSystemResume(SuspendReason.Lock, At(8 * 60 + 2));
        Assert.Equal([WatchbunIntentKind.CaptureRestored], Kinds(restored));
        Assert.Equal(CaptureMode.Full, restored.Snapshot.Capture);
        Assert.Single(restored.Snapshot.WatchTasks);
        Assert.Equal(WatchbunRefusal.NotSuspended, engine.OnSystemResume(SuspendReason.Lock, At(8 * 60 + 3)).Refusal);

        // The task had 15 active minutes left; quiet starts after it expires.
        Assert.Empty(engine.Tick(At(8 * 60 + 16)).Intents);
        Assert.Equal([WatchbunIntentKind.WatchTaskExpired], Kinds(engine.Tick(At(8 * 60 + 17))));
        Assert.Contains(WatchbunIntentKind.QuietCheckAsked, Kinds(engine.Tick(At(9 * 60 + 17))));

        // Suspending while dozing freezes the second-stage timer and restores the doze exactly.
        engine.OnSystemSuspend(SuspendReason.Sleep, At(9 * 60 + 47));
        Assert.Empty(engine.Tick(At(20 * 60)).Intents);
        var woke = engine.OnSystemResume(SuspendReason.Sleep, At(20 * 60));
        Assert.Equal(WatchbunPhase.Dozing, woke.Snapshot.Phase);
        Assert.Equal(CaptureMode.LocalWakeOnly, woke.Snapshot.Capture);
        Assert.True(woke.Snapshot.QuietCheckPending);
        Assert.Empty(engine.Tick(At(20 * 60 + 29.99)).Intents);
        Assert.Contains(WatchbunIntentKind.SessionClosed, Kinds(engine.Tick(At(20 * 60 + 30))));
        Assert.Equal(WatchbunRefusal.Closed, engine.OnSystemSuspend(SuspendReason.Lock, At(20 * 60 + 31)).Refusal);
    }

    [Fact]
    public void Scenario8_CheckpointRestore_RoundTripsAndRecoversWithoutCapture()
    {
        var engine = Engine(out var grant);
        var task = engine.AddWatchTask("bell.rang", TimeSpan.FromHours(3), At(0)).Snapshot.WatchTasks.Single();
        engine.OnGameEvent(Event(grant, GameEventKind.Urgent), At(1));
        engine.OnGameEvent(new StructuredGameEvent(Guid.NewGuid(), GameEventKind.Meaningful, "x"), At(2));
        engine.Tick(At(50));
        var checkpoint = engine.Checkpoint();

        var json = System.Text.Json.JsonSerializer.Serialize(checkpoint);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<WatchbunCheckpoint>(json)!;
        var restored = WatchbunEngine.Restore(parsed, At(55));

        var snapshot = restored.Current;
        Assert.Equal(WatchbunPhase.Recovering, snapshot.Phase);
        Assert.Equal(CaptureMode.None, snapshot.Capture);
        Assert.Null(snapshot.CaptureTarget);
        Assert.False(snapshot.SemanticSpendingAllowed);
        Assert.Equal([task], snapshot.WatchTasks);
        Assert.Equal(1, snapshot.RejectedEvents);
        Assert.Equal(grant.Target, snapshot.BoundTarget);
        Assert.Empty(restored.Tick(At(10 * 60)).Intents);
        Assert.Equal(WatchbunRefusal.NotAllowed, restored.OnInput(At(10 * 60)).Refusal);
        Assert.Equal(WatchbunRefusal.NotAllowed, restored.OnGameEvent(Event(grant, GameEventKind.Urgent), At(10 * 60)).Refusal);

        var regrant = Grant(Target(processId: 4400));
        var reattached = restored.Reattach(regrant, At(10 * 60 + 1));
        Assert.Equal(WatchbunPhase.Watching, reattached.Snapshot.Phase);
        Assert.Equal(task.TaskId, Assert.Single(reattached.Snapshot.WatchTasks).TaskId);
        var next = restored.AddWatchTask("bell.rang2", null, At(10 * 60 + 2)).Snapshot.WatchTasks.Last();
        Assert.NotEqual(task.TaskId, next.TaskId);

        // A non-attached phase restores as-is and checkpoints identically.
        var exited = Engine(out var exitGrant);
        exited.OnTargetExited(new TargetExit(exitGrant.Target.ProcessId, 3, false, false), At(1));
        var exitCheckpoint = exited.Checkpoint();
        var exitRestored = WatchbunEngine.Restore(exitCheckpoint, exitCheckpoint.Now);
        Assert.Equal(WatchbunPhase.ExitPending, exitRestored.Current.Phase);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(exitCheckpoint), System.Text.Json.JsonSerializer.Serialize(exitRestored.Checkpoint()));
        Assert.Throws<ArgumentOutOfRangeException>(() => WatchbunEngine.Restore(exitCheckpoint, At(0)));

        // A restored session whose target is gone reports the exit and waits for Boss.
        var gone = WatchbunEngine.Restore(checkpoint, At(60));
        Assert.Equal(WatchbunPhase.ExitPending, gone.OnTargetExited(new TargetExit(grant.Target.ProcessId, 0, true, false), At(61)).Snapshot.Phase);
    }
}
