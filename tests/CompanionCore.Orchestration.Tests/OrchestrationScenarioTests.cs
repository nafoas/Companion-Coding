using System.Security.Cryptography;
using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.Keepsakes;
using CompanionCore.Memory;
using CompanionCore.Recall;
using CompanionCore.TargetAuth;
using CompanionCore.Transcript;
using CompanionCore.Watchbun;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

public sealed class OrchestrationScenarioTests
{
    [Fact]
    public async Task Scenario1_FullLoop_SheetFlowsThroughBridgeAttentionConversationTranscriptAndMemory()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "Glowing Door", "synthetic.door", "[neutral memory] a door glowed.")));

        var sheet = await harness.SheetAsync(changeScore: 0.6);

        Assert.Equal(1, harness.Provider.CallCount);
        Assert.Equal(0, sheet.Length);
        Assert.Contains(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Braincase && notice.Braincase == BridgeOutcomeKind.Interpreted);
        Assert.Contains(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Attention);

        var reference = $"target-session:{grant.TargetSessionId:N}";
        var memories = await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SessionReference = reference });
        var memory = Assert.Single(memories);
        Assert.Equal("synthetic.door", memory.Record.SubjectKey);

        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Equal(grant.TargetSessionId, snapshot.TargetSessionId);
        Assert.NotNull(snapshot.Attention);
        Assert.Equal(WatchbunPhase.Watching, snapshot.Watchbun!.Phase);
        Assert.Equal(0, snapshot.Faults);

        // The transcript exists for the session and reconstructs without committed memory.
        var context = TranscriptReader.Reconstruct(harness.Host.Transcripts, grant.TargetSessionId);
        Assert.NotNull(context);

        // With every file closed, no byte of any sheet exists anywhere under the data root.
        await harness.Host.DisposeAsync();
        harness.AssertNoSheetBytesOnDisk();
    }

    [Fact]
    public async Task Scenario1_SheetsForOtherGrantsOrWithoutASession_AreDisposedUnread()
    {
        await using var harness = await CreateAsync();
        var stray = harness.Worker.EmitSheet(T0, grant: CompanionCore.Capture.Contracts.CaptureAuthorizationGrant.Issue(Guid.NewGuid(), 1, Candidate().Identity));
        await harness.SettleAsync();
        Assert.Equal(0, harness.Provider.CallCount);

        await harness.AuthorizeAsync();
        Assert.Equal(AdapterVerdict.WrongTarget, await harness.Orchestrator.SubmitGameEventLineAsync(
            $"{{\"session\":\"{Guid.NewGuid():D}\",\"kind\":\"urgent\",\"key\":\"x\"}}"));
        Assert.Equal(0, (await harness.Orchestrator.GetSnapshotAsync()).Faults);
        GC.KeepAlive(stray);
    }

    [Fact]
    public async Task Scenario2_PrivacyPauseCancelsBridgeWork_AndResumeKeepsContinuity()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Provider.Enqueue(async (request, cancellationToken) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return ProviderReply.Success(ResponseWithMemory(request.OperationId, "late", "synthetic.late", "[neutral memory] late."));
        });

        harness.Worker.EmitSheet(harness.Time.GetUtcNow());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await harness.Controller.PrivacyStopAsync();
        await harness.SettleAsync();

        Assert.Empty(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.late" }));
        Assert.True(harness.Has(CompanionNoticeKind.PrivacyPaused));

        var resumed = await harness.Controller.ResumeExplicitlyAsync();
        Assert.True(resumed.Succeeded);
        await harness.SettleAsync();
        var refreshed = harness.Controller.CurrentSession.Grant!;
        Assert.Equal(grant.TargetSessionId, refreshed.TargetSessionId);
        Assert.NotEqual(grant.Generation, refreshed.Generation);

        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "after", "synthetic.after", "[neutral memory] after resume.")));
        await harness.SheetAsync();
        Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.after" }));
        Assert.Equal(grant.TargetSessionId, (await harness.Orchestrator.GetSnapshotAsync()).TargetSessionId);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task Scenario3_QuietHoursPauseSpending_ThenCloseWithConsolidationVaultAndSessionEnd()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "map", "synthetic.map", "[neutral memory] a map.")));
        await harness.SheetAsync();
        Assert.Equal(1, harness.Provider.CallCount);

        await harness.AdvanceAndTickAsync(TimeSpan.FromMinutes(61));
        Assert.True(harness.HasWatchbun(WatchbunIntentKind.QuietCheckAsked));
        Assert.False((await harness.Orchestrator.GetSnapshotAsync()).Watchbun!.SemanticSpendingAllowed);

        await harness.SheetAsync();
        Assert.Equal(1, harness.Provider.CallCount);

        await harness.AdvanceAndTickAsync(TimeSpan.FromMinutes(61));
        Assert.True(harness.HasWatchbun(WatchbunIntentKind.SessionClosed));
        Assert.True(harness.Has(CompanionNoticeKind.Consolidated));
        Assert.True(harness.Has(CompanionNoticeKind.VaultBackedUp));
        Assert.True(harness.Has(CompanionNoticeKind.SessionEnded));
        Assert.Equal(TargetSessionPhase.None, harness.Controller.CurrentSession.Phase);

        var reference = $"target-session:{grant.TargetSessionId:N}";
        var summary = Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(reference)));
        Assert.Single(summary.Record.Links);
        Assert.True(File.Exists(harness.Location.CompanionArchivePath));
        Assert.True(File.Exists(harness.Location.BackupArchivePath));
        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Null(snapshot.TargetSessionId);
        Assert.Null(snapshot.Watchbun);
        Assert.Empty(snapshot.UnconsolidatedSessions);
        Assert.Equal(0, snapshot.Faults);
    }

    [Fact]
    public async Task Scenario4_TabAwayUrgentEvent_RaisesTargetOnlyNonFocusStealingAlert()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        harness.Platform.Foreground(windowId: 0x9999, processId: 999);

        var verdict = await harness.Orchestrator.SubmitGameEventLineAsync(
            $"{{\"session\":\"{grant.TargetSessionId:D}\",\"kind\":\"urgent\",\"key\":\"enemy.group\"}}");
        await harness.SettleAsync();

        Assert.Equal(AdapterVerdict.Accepted, verdict);
        var alert = Assert.Single(harness.Notices, notice => notice.Watchbun?.Kind == WatchbunIntentKind.AlertRaised).Watchbun!;
        Assert.Equal(grant.Target, alert.Target);
        Assert.True(alert.NoFocusSteal);
        Assert.Contains(harness.Notices, notice => notice.Attention?.Kind == AttentionIntentKind.Urgent);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task Scenario5_ExitRelaunchReauthorize_ReattachesWatchbunToTheNewGrant()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();

        harness.Platform.Exit(new TargetExit(grant.Target.ProcessId, ExitCode: 1, WindowClosedFirst: false, WasHung: false));
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.ExitPromptRaised));
        Assert.Equal(TargetExitKind.SuspectedCrash, harness.Notices.Single(n => n.Watchbun?.Kind == WatchbunIntentKind.ExitPromptRaised).Watchbun!.Exit);
        await harness.SettleAsync();
        Assert.Equal(TargetSessionPhase.None, harness.Controller.CurrentSession.Phase);
        Assert.False(harness.Has(CompanionNoticeKind.SessionEnded));

        Assert.Equal(WatchbunRefusal.None, await harness.Orchestrator.DecideAfterExitAsync(ExitDecision.WaitForRelaunch));
        Assert.Equal(grant.Target, harness.Platform.AwaitingRelaunchOf);
        var relaunched = Candidate(processId: 5555, windowId: 0x5555);
        harness.Platform.Launch(relaunched.Identity);
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.ReauthorizationRequested));

        var newGrant = await harness.AuthorizeAsync(relaunched);
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.Reattached));
        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Equal(WatchbunPhase.Watching, snapshot.Watchbun!.Phase);
        Assert.Equal(relaunched.Identity, snapshot.Watchbun.BoundTarget);
        Assert.Equal(newGrant.TargetSessionId, snapshot.TargetSessionId);
        Assert.Equal(2, snapshot.UnconsolidatedSessions.Count);
        Assert.Equal(relaunched.Identity, harness.Platform.Watched);
        Assert.Equal(0, snapshot.Faults);
    }

    [Fact]
    public async Task Scenario6_CameraActionTakesAVerifiedPhotographFromTheNextSheet()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();

        Assert.Equal(KeepsakeRefusal.None, await harness.Orchestrator.TakePhotographAsync());
        var shown = Assert.Single(harness.Notices, notice => notice.Keepsake?.Kind == KeepsakeIntentKind.CameraShown).Keepsake!;
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.SheetAsync(shade: 120);

        var saved = Assert.Single(harness.Notices, notice => notice.Keepsake?.Kind == KeepsakeIntentKind.PhotographSaved).Keepsake!;
        Assert.Equal(shown.ActionId, saved.ActionId);
        Assert.Equal(0, harness.Provider.CallCount);
        var inspection = await harness.Orchestrator.Keepsakes.InspectAsync(saved.PhotographId!.Value);
        Assert.Equal(InspectionStatus.Verified, inspection.Status);
        var entry = Assert.Single(await harness.Orchestrator.Keepsakes.ListAsync());
        Assert.Equal((ScriptedCaptureWorker.SheetWidth, ScriptedCaptureWorker.SheetHeight), (entry.Width, entry.Height));
        Assert.Equal($"target-session:{grant.TargetSessionId:N}", entry.Session);
        Assert.False((await harness.Orchestrator.GetSnapshotAsync()).PhotographPending);

        Assert.Equal(DeletionStatus.Deleted, await harness.Orchestrator.DeletePhotographAsync(saved.PhotographId.Value));
        Assert.Equal(InspectionStatus.Deleted, (await harness.Orchestrator.Keepsakes.InspectAsync(saved.PhotographId.Value)).Status);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task Scenario6_ACameraActionWithoutASessionOrAfterExpiry_WritesNothing()
    {
        await using var harness = await CreateAsync();
        Assert.Equal(KeepsakeRefusal.UnknownAction, await harness.Orchestrator.TakePhotographAsync());
        await harness.AuthorizeAsync();
        await harness.Orchestrator.TakePhotographAsync();

        await harness.AdvanceAndTickAsync(TimeSpan.FromSeconds(11));

        Assert.Contains(harness.Notices, notice => notice.PhotographRefusal == KeepsakeRefusal.ActionExpired);
        Assert.False((await harness.Orchestrator.GetSnapshotAsync()).PhotographPending);
        Assert.Empty(await harness.Orchestrator.Keepsakes.ListAsync());
    }

    [Fact]
    public async Task Scenario7_RestartRestoresConversationAndWatchbun_RecoveringWithoutCaptureUntilReauthorized()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var before = await harness.Orchestrator.GetSnapshotAsync();
        await harness.Orchestrator.AddWatchTaskAsync("door.opened", TimeSpan.FromHours(2));

        // Simulate a runtime restart: the target session survives in the controller only
        // until the runtime is gone, so end it without a Boss decision, then reopen.
        await harness.ReopenHostAsync();
        var restored = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Equal(before.Conversation.Thread?.ThreadId, restored.Conversation.Thread?.ThreadId);
        Assert.Equal(WatchbunPhase.Recovering, restored.Watchbun!.Phase);
        Assert.Equal(CaptureMode.None, restored.Watchbun.Capture);
        Assert.Single(restored.Watchbun.WatchTasks);
        Assert.True(harness.Has(CompanionNoticeKind.Recovering));

        await harness.Controller.EndSessionAsync();
        await harness.AuthorizeAsync(Candidate(processId: 7777));
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.Reattached));
        Assert.Equal(WatchbunPhase.Watching, (await harness.Orchestrator.GetSnapshotAsync()).Watchbun!.Phase);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task Scenario8_InterruptedConsolidationReplaysIdempotently()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "key", "synthetic.key", "[neutral memory] a key.")));
        await harness.SheetAsync();
        var reference = $"target-session:{grant.TargetSessionId:N}";

        // Persist the intent exactly as the orchestrator would, then "crash" before the commit.
        var operation = CompanionOrchestrator.DeriveId("consolidation", reference);
        var intent = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[] { new CompanionOrchestrator.PendingConsolidation(reference, operation, harness.Time.GetUtcNow(), null) });
        await harness.Host.State.PutAsync(CompanionOrchestrator.ConsolidationStateName, intent);
        await harness.ReopenHostAsync();
        await harness.ReopenHostAsync();

        var summaries = await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(reference));
        Assert.Single(summaries);
        Assert.Equal(CompanionOrchestrator.DeriveId("consolidation", reference), summaries[0].LocalOperationId);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task Scenario8b_AnotherSessionsConsolidationNeverOverwritesAnUnfinishedIntent()
    {
        await using var harness = await CreateAsync();

        // An earlier session whose consolidation was interrupted at runtime: its originals are
        // committed and its intent is durable, but it is no longer an attached session.
        const string orphan = "target-session:orphaned";
        await harness.CommitSessionOriginalAsync(orphan);
        var orphanOperation = CompanionOrchestrator.DeriveId("consolidation", orphan);
        await harness.Host.State.PutAsync(
            CompanionOrchestrator.ConsolidationStateName,
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[] { new CompanionOrchestrator.PendingConsolidation(orphan, orphanOperation, harness.Time.GetUtcNow(), null) }));

        var grant = await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "key", "synthetic.key", "[neutral memory] a key.")));
        await harness.SheetAsync();
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded));

        var current = $"target-session:{grant.TargetSessionId:N}";
        Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(current)));
        var orphanSummary = Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(orphan)));
        Assert.Equal(orphanOperation, orphanSummary.LocalOperationId);
        Assert.Equal(2, harness.Notices.Count(n => n.Kind == CompanionNoticeKind.Consolidated));

        // The queue drained: a restart replays nothing and writes nothing new.
        await harness.ReopenHostAsync();
        Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(orphan)));
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task Scenario9_RepairRestoresTheBunDexPhotographsAndState()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "gem", "synthetic.gem", "[neutral memory] a gem.")));
        await harness.SheetAsync();
        await harness.Orchestrator.TakePhotographAsync();
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.SheetAsync(shade: 150);
        var photo = harness.Notices.Single(notice => notice.Keepsake?.Kind == KeepsakeIntentKind.PhotographSaved).Keepsake!;
        await harness.Host.State.PutAsync("settings", "{\"volume\":2}"u8.ToArray());

        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.VaultBackedUp) && harness.Has(CompanionNoticeKind.SessionEnded));
        Assert.Equal(0, harness.Orchestrator.Faults);

        // Repairs are refused while a target session is active.
        await harness.AuthorizeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Host.RepairAsync());
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Notices.Count(n => n.Kind == CompanionNoticeKind.SessionEnded) == 2);

        // Damage the BunDex, a photograph, and the settings; the host can no longer open.
        await harness.Host.DisposeAsync();
        await File.WriteAllBytesAsync(harness.Location.DatabasePath, "damaged"u8.ToArray());
        File.Delete(Path.Combine(harness.Host.Keepsakes.RootPath, $"{photo.ActionId:N}.png"));
        await File.WriteAllBytesAsync(harness.Host.State.PathFor("settings"), "garbage"u8.ToArray());
        await Assert.ThrowsAnyAsync<Exception>(() => harness.OpenHostAsync());

        var report = await CompanionHost.RepairOfflineAsync(new CompanionHostOptions(harness.Location, harness.Privacy, harness.Controller, harness.Provider, new InMemoryCredentialStore()));
        await harness.OpenHostAsync();

        Assert.Equal(CompanionCore.Vault.CompanionStatus.Valid, report.Companion);
        Assert.Equal(1, report.PhotographsRestored);
        Assert.Empty(report.MissingPhotographs);
        Assert.True(report.StatesRestored >= 1);
        Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.gem" }));
        Assert.Equal(InspectionStatus.Verified, (await harness.Orchestrator.Keepsakes.InspectAsync(photo.PhotographId!.Value)).Status);
        Assert.Equal("{\"volume\":2}"u8.ToArray(), (await harness.Host.State.GetAsync("settings")).Payload.ToArray());

        // The online repair path also works on a healthy store and reports completeness.
        var online = await harness.Host.RepairAsync();
        Assert.True(online.Complete);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }
}
