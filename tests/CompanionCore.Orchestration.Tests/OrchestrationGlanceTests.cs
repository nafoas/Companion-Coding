using CompanionCore.Api;
using CompanionCore.Attention;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// VIS-02 — Glance and Inspect. The routine look is a quick Glance (peripheral vision);
/// the Inspect is the main focus. Glances add to attention but never overwrite the focus,
/// and keep looking while an Inspect runs; escalations ask for one Inspect at most.
/// </summary>
public sealed class OrchestrationGlanceTests
{
    private static OrchestratorOptions Glancing => new() { Glance = true };

    private static MockSemanticProvider.MockStep Glance(
        double interest = 0.3,
        bool escalate = false,
        int nextLookSeconds = 10,
        string changedRegion = "fullContext",
        double confidence = 0.8,
        string label = "synthetic scene") =>
        MockSemanticProvider.Respond(request => MockSemanticProvider.GlanceResponseJson(
            request, interest, confidence, changedRegion, label, escalate, nextLookSeconds));

    [Fact]
    public async Task TheRoutineLook_IsAGlance()
    {
        await using var harness = await CreateAsync(Glancing);
        await harness.AuthorizeAsync();

        await harness.SheetAsync(changeScore: 0.05);

        Assert.Equal((1, 0), (harness.Provider.GlanceCount, harness.Provider.InspectCount));
        Assert.Contains(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Braincase && notice.Braincase == BridgeOutcomeKind.Interpreted);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task ThePulse_CountsSheetsAndLooks_AndNamesTheLastOutcome()
    {
        // FIX-07: Boss's window shows these counts every second.
        await using var harness = await CreateAsync(Glancing);
        var before = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Equal((0L, 0L, 0L, (BridgeOutcomeKind?)null), (before.SheetsReceived, before.GlancesSent, before.InspectsSent, before.LastLookOutcome));
        await harness.AuthorizeAsync();

        harness.Provider.EnqueueGlance(Glance(interest: 0.4, escalate: true, nextLookSeconds: 20));
        await harness.SheetAsync(changeScore: 0.05);
        await harness.SheetAsync(changeScore: 0.05);
        await harness.SheetAsync(changeScore: 0.05);

        var after = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Equal((3L, 1L, 1L), (after.SheetsReceived, after.GlancesSent, after.InspectsSent));
        Assert.Equal(BridgeOutcomeKind.Interpreted, after.LastLookOutcome);
    }

    [Fact]
    public async Task TheNextGlance_FollowsItsClampedRecommendation_AndASniffTriggerLooksAtOnce()
    {
        await using var harness = await CreateAsync(Glancing);
        await harness.AuthorizeAsync();

        // "Look again in 1 s" is clamped to the 2 s floor.
        harness.Provider.EnqueueGlance(Glance(nextLookSeconds: 1));
        await harness.SheetAsync(changeScore: 0.05);
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(1, harness.Provider.GlanceCount);
        harness.Time.Advance(TimeSpan.FromSeconds(1));

        // "Look again in 120 s" is clamped to the 30 s ceiling.
        harness.Provider.EnqueueGlance(Glance(nextLookSeconds: 120));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.GlanceCount);
        harness.Time.Advance(TimeSpan.FromSeconds(29));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.GlanceCount);
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        harness.Provider.EnqueueGlance(Glance(nextLookSeconds: 30));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(3, harness.Provider.GlanceCount);

        // A sniff noticed something: the very next sheet is glanced at, early.
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        harness.Worker.EmitSniff(harness.Time.GetUtcNow(), hotChange: 0.4, motion: 0.1);
        await harness.SettleAsync();
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(4, harness.Provider.GlanceCount);
        Assert.Equal(0, harness.Provider.InspectCount);
    }

    [Fact]
    public async Task AnEscalatingGlance_AsksForOneCloserLook()
    {
        await using var harness = await CreateAsync(Glancing);
        await harness.AuthorizeAsync();

        harness.Provider.EnqueueGlance(Glance(interest: 0.4, escalate: true, nextLookSeconds: 20));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.True((await harness.Orchestrator.GetSnapshotAsync()).InspectQueued);

        // The next sheet is the closer look; the one after is not (the queue held just one).
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal((1, 1), (harness.Provider.GlanceCount, harness.Provider.InspectCount));
        Assert.False((await harness.Orchestrator.GetSnapshotAsync()).InspectQueued);
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal((1, 1), (harness.Provider.GlanceCount, harness.Provider.InspectCount));

        // Interest above 0.70 escalates even without the flag.
        harness.Time.Advance(TimeSpan.FromSeconds(20));
        harness.Provider.EnqueueGlance(Glance(interest: 0.75, escalate: false, nextLookSeconds: 20));
        await harness.SheetAsync(changeScore: 0.05);
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal((2, 2), (harness.Provider.GlanceCount, harness.Provider.InspectCount));
    }

    [Fact]
    public async Task GlancesKeepLookingDuringAnInspect_AndManyEscalationsFoldIntoOne()
    {
        await using var harness = await CreateAsync(Glancing);
        await harness.AuthorizeAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Provider.Enqueue(async (request, cancellationToken) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return ProviderReply.Success(ResponseWithMemory(request.OperationId, "focus", "synthetic.focus", "[neutral memory] the main focus."));
        });

        // An escalation starts the Inspect (the main focus), which stays in flight.
        harness.Provider.EnqueueGlance(Glance(escalate: true, nextLookSeconds: 2));
        await harness.SheetAsync(changeScore: 0.05);
        harness.Worker.EmitSheet(harness.Time.GetUtcNow(), changeScore: 0.05);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Two more Glances run beside it and both escalate: no tunnel vision, no scatterbrain.
        for (var index = 0; index < 2; index++)
        {
            harness.Time.Advance(TimeSpan.FromSeconds(2));
            harness.Provider.EnqueueGlance(Glance(interest: 0.9, escalate: true, nextLookSeconds: 2));
            harness.Worker.EmitSheet(harness.Time.GetUtcNow(), changeScore: 0.05);
            await harness.WaitForAsync(() => harness.Provider.GlanceCount == 2 + index);
            await WaitUntilGlanceSettled(harness);
        }

        var during = await harness.Orchestrator.GetSnapshotAsync();
        Assert.True(during.BridgeInFlight);
        Assert.True(during.InspectQueued);
        Assert.Equal(1, harness.Provider.InspectCount);

        // The focus completes untouched: its memory is appended.
        release.SetResult();
        await harness.SettleAsync();
        Assert.Single(await harness.Host.Repository.RetrieveAsync(new CompanionCore.Memory.MemoryQuery { SubjectPrefix = "synthetic.focus" }));

        // Exactly one more Inspect for the two folded escalations, then Glances again.
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.InspectCount);
        Assert.False((await harness.Orchestrator.GetSnapshotAsync()).InspectQueued);
        harness.Time.Advance(TimeSpan.FromSeconds(2));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.InspectCount);
        Assert.Equal(4, harness.Provider.GlanceCount);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task GlancesAlone_NeverEnterBnuyMode()
    {
        await using var harness = await CreateAsync(Glancing);
        await harness.AuthorizeAsync();
        for (var index = 0; index < 20; index++)
        {
            // Distinct moments, so habituation cannot be what holds him back.
            harness.Provider.EnqueueGlance(Glance(interest: 1, confidence: 1, nextLookSeconds: 2, changedRegion: "fullContext", label: $"moment {index}"));
            await harness.SheetAsync(changeScore: 0.05);
            harness.Time.Advance(TimeSpan.FromSeconds(2));
        }

        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.NotEqual(AttentionState.HighAttention, snapshot.Attention!.State);
        Assert.DoesNotContain(harness.Notices, notice => notice.Attention?.Kind == AttentionIntentKind.HighAttentionStarted);
        Assert.True(snapshot.Attention.Score > 0);
    }

    [Fact]
    public async Task BnuyMode_KeepsItsFocusedLooks()
    {
        await using var harness = await CreateAsync(Glancing);
        var grant = await harness.AuthorizeAsync();
        await harness.Orchestrator.AddWatchTaskAsync("door.opened", TimeSpan.FromHours(2));
        await harness.Orchestrator.SubmitGameEventLineAsync(
            $"{{\"session\":\"{grant.TargetSessionId:D}\",\"kind\":\"meaningful\",\"key\":\"door.opened\"}}");
        await harness.SettleAsync();
        Assert.Equal(AttentionState.HighAttention, (await harness.Orchestrator.GetSnapshotAsync()).Attention!.State);

        // In Bnuy Mode the focus looks closely at the event floor; Glances fill the gaps.
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(1, harness.Provider.InspectCount);
        harness.Time.Advance(TimeSpan.FromSeconds(3));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.InspectCount);
    }

    [Fact]
    public void GlanceTopics_AreNeutralAndBounded()
    {
        Assert.Equal("glance:a-door-opens", CompanionOrchestrator.GlanceTopic("A door opens!"));
        Assert.Null(CompanionOrchestrator.GlanceTopic("!!!"));
        Assert.True(CompanionOrchestrator.GlanceTopic(new string('x', 200))!.Length <= 7 + 48);
    }

    private static async Task WaitUntilGlanceSettled(OrchestrationHarness harness)
    {
        for (var attempt = 0; attempt < 1000 && (await harness.Orchestrator.GetSnapshotAsync()).GlanceInFlight; attempt++)
        {
            await Task.Delay(5);
        }
    }
}
