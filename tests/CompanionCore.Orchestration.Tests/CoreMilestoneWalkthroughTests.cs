using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.Conversation;
using CompanionCore.Memory;
using CompanionCore.Recall;
using CompanionCore.Watchbun;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// CORE-12: the Stage 12 Core Prince milestone's ten checks, chained through one Prince's
/// life on synthetic fakes. Each check is also covered on its own elsewhere; this proves they
/// compose. Boss's hand-run of the same list on the real App is the Core Bun Approval Hop.
/// </summary>
public sealed class CoreMilestoneWalkthroughTests
{
    [Fact]
    public async Task TheTenMilestoneChecks_RunInOnePrincesLife()
    {
        await using var harness = await CreateAsync(new OrchestratorOptions { SemanticCadence = SemanticCadence.Unthrottled });
        var lineage = (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage;

        // 1. Invitation and target consent.
        var grant = await harness.AuthorizeAsync();
        Assert.True(harness.Has(CompanionNoticeKind.SessionStarted));
        var reference = $"target-session:{grant.TargetSessionId:N}";

        // 2. Noticing through Bnuy Mode: a completed watch task is a decisive moment.
        Assert.Equal(WatchbunRefusal.None, await harness.Orchestrator.AddWatchTaskAsync("door.opened", TimeSpan.FromHours(2)));
        Assert.Equal(AdapterVerdict.Accepted, await harness.Orchestrator.SubmitGameEventLineAsync(
            $"{{\"session\":\"{grant.TargetSessionId:D}\",\"kind\":\"meaningful\",\"key\":\"door.opened\"}}"));
        await harness.SettleAsync();
        Assert.Equal(AttentionState.HighAttention, (await harness.Orchestrator.GetSnapshotAsync()).Attention!.State);

        // 5. A Watchbun background event (the watched door opened).
        Assert.True(harness.HasWatchbun(WatchbunIntentKind.WatchTaskCompleted));

        // 3 and 4. A look is interpreted, the conversation reacts, and a substantive moment
        // becomes an appended memory; a reply goes to the one Conversation Thread.
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "Glowing Door", "synthetic.door", "[neutral memory] the door glowed.")));
        await harness.SheetAsync(changeScore: 0.6);
        Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.door" }));
        Assert.Contains(harness.Notices, n => n.Kind == CompanionNoticeKind.Conversation);
        var reply = await harness.Orchestrator.SubmitUserTurnAsync(new UserTurn(UserTurnKind.Substantive));
        Assert.True(Enum.IsDefined(reply));
        Assert.Equal(lineage, (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage);

        // 9a. API outage: the Braincase naps; nothing is lost.
        harness.Provider.Enqueue(MockSemanticProvider.Reply(ProviderReply.Outage()));
        await harness.SheetAsync();
        Assert.Contains(harness.Notices, n => n.Kind == CompanionNoticeKind.Braincase && n.Braincase == BridgeOutcomeKind.Napping);

        // 9b. Privacy Loaf: an instant stop, then an explicit resume of the same session.
        await harness.Controller.PrivacyStopAsync();
        await harness.SettleAsync();
        Assert.True(harness.Has(CompanionNoticeKind.PrivacyPaused));
        Assert.True((await harness.Controller.ResumeExplicitlyAsync()).Succeeded);
        await harness.SettleAsync();
        Assert.Equal(grant.TargetSessionId, (await harness.Orchestrator.GetSnapshotAsync()).TargetSessionId);

        // 6. The session ends: Adventure Record consolidation and a Vault backup.
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded));
        Assert.True(harness.Has(CompanionNoticeKind.Consolidated));
        Assert.True(harness.Has(CompanionNoticeKind.VaultBackedUp));
        Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(reference)));

        // 7. A restart is the same Prince: the same lineage and the same memories.
        await harness.ReopenHostAsync();
        Assert.Equal(lineage, (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage);
        Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.door" }));

        // 8. Da Bun Vault restoration (Boss-initiated only), continuing the same line.
        var repair = await harness.Host.RepairAsync();
        Assert.True(repair.Complete);
        Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.door" }));
        Assert.Equal(lineage, (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage);

        // 10. Stable and contained: no faults, and no sheet bytes ever reached the disk.
        Assert.Equal(0, harness.Orchestrator.Faults);
        await harness.Host.DisposeAsync();
        harness.AssertNoSheetBytesOnDisk();
    }
}
