using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;
using CompanionCore.Watchbun;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// VIS-01 — the reflex lane: a local Sniff moves Prince's attention at once, without
/// waiting for the mailbox or any cloud look, and his looking cadence follows attention.
/// Local evidence alone never enters Bnuy Mode, and a looping cell habituates.
/// </summary>
public sealed class OrchestrationSniffTests
{
    [Fact]
    public async Task ASniff_PerksHisEars_EvenWhileTheMailboxIsBusy()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var occupied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var busy = harness.Orchestrator.InvokeOnMailboxAsync(async () =>
        {
            occupied.SetResult();
            await gate.Task;
        });
        try
        {
            await occupied.Task.WaitAsync(TimeSpan.FromSeconds(10));
            harness.Worker.EmitSniff(harness.Time.GetUtcNow(), hotChange: 0.5, motion: 0.4);

            // The Attention notice arrived synchronously on the sniff's own thread.
            Assert.Contains(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Attention
                && notice.Attention!.Kind == AttentionIntentKind.Observing);
            Assert.False(busy.IsCompleted);
        }
        finally
        {
            gate.TrySetResult();
        }

        await busy;
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task SpreadChange_ReachesEngaged_ButLocalEvidenceAloneNeverEntersBnuyMode()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var at = harness.Time.GetUtcNow();

        // A busy screen: strong change moving across many cells for several seconds.
        for (var index = 0; index < 120; index++)
        {
            at += TimeSpan.FromMilliseconds(100);
            harness.Time.Advance(TimeSpan.FromMilliseconds(100));
            harness.Worker.EmitSniff(at, hotChange: 0.9, motion: 0.9, hotCell: index % 48);
        }

        await harness.SettleAsync();
        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Equal(AttentionState.Engaged, snapshot.Attention!.State);
        Assert.True(snapshot.Attention.Score < new AttentionConfiguration().HighEnter);
        Assert.DoesNotContain(harness.Notices, notice => notice.Attention?.Kind == AttentionIntentKind.HighAttentionStarted);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task OneLoopingCell_Habituates_AndNeverEngagesHim()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var at = harness.Time.GetUtcNow();

        // A waterfall: the same cell changing strongly, four times a second, for a minute.
        for (var index = 0; index < 240; index++)
        {
            at += TimeSpan.FromMilliseconds(250);
            harness.Time.Advance(TimeSpan.FromMilliseconds(250));
            harness.Worker.EmitSniff(at, hotChange: 0.6, motion: 0.05, hotCell: 9);
        }

        await harness.SettleAsync();
        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Equal(AttentionState.Noticing, snapshot.Attention!.State);

        // The first exposures perked his ears; the loop then faded to nothing.
        var observing = harness.Notices.Count(notice => notice.Attention?.Kind == AttentionIntentKind.Observing);
        Assert.InRange(observing, 1, 12);
    }

    [Fact]
    public async Task HisLookingCadence_FollowsAttention()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        await harness.SettleAsync();
        Assert.Equal(CaptureCadence.Noticing, Assert.Single(harness.Worker.Cadences));

        // Spread change engages him: the worker looks faster.
        var at = harness.Time.GetUtcNow();
        for (var index = 0; index < 60 && (await harness.Orchestrator.GetSnapshotAsync()).Attention!.State != AttentionState.Engaged; index++)
        {
            at += TimeSpan.FromMilliseconds(100);
            harness.Time.Advance(TimeSpan.FromMilliseconds(100));
            harness.Worker.EmitSniff(at, hotChange: 0.9, motion: 0.9, hotCell: index % 48);
        }

        await harness.WaitForAsync(() => harness.Worker.Cadences.Contains(CaptureCadence.Engaged));

        // A decisive moment is Bnuy Mode: ten looks a second.
        Assert.Equal(WatchbunRefusal.None, await harness.Orchestrator.AddWatchTaskAsync("door.opened", TimeSpan.FromHours(2)));
        await harness.Orchestrator.SubmitGameEventLineAsync(
            $"{{\"session\":\"{grant.TargetSessionId:D}\",\"kind\":\"meaningful\",\"key\":\"door.opened\"}}");
        await harness.SettleAsync();
        await harness.WaitForAsync(() => harness.Worker.Cadences.Contains(CaptureCadence.BnuyMode));
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task TheCadence_IsReassertedAfterAResumeAndNowAndThen()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        await harness.SettleAsync();
        Assert.Single(harness.Worker.Cadences);

        // A tick inside the interval sends nothing new.
        harness.Time.Advance(TimeSpan.FromSeconds(2));
        await harness.Orchestrator.TickAsync();
        Assert.Single(harness.Worker.Cadences);

        // Past it, the cadence is told again (a restarted worker may have lost it).
        harness.Time.Advance(CompanionOrchestrator.CadenceReassertInterval);
        await harness.Orchestrator.TickAsync();
        await harness.WaitForAsync(() => harness.Worker.Cadences.Count == 2);
        Assert.All(harness.Worker.Cadences, cadence => Assert.Equal(CaptureCadence.Noticing, cadence));
    }

    [Fact]
    public async Task AResumedSession_IsToldItsCadenceAgainAtOnce()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        await harness.SettleAsync();
        Assert.Single(harness.Worker.Cadences);

        // A privacy stop and explicit resume restart the worker at its default pace.
        await harness.Controller.PrivacyStopAsync();
        Assert.True((await harness.Controller.ResumeExplicitlyAsync()).Succeeded);
        await harness.WaitForAsync(() => harness.Worker.Cadences.Count == 2);
        Assert.Equal(CaptureCadence.Noticing, harness.Worker.Cadences[^1]);
    }

    [Fact]
    public async Task ASniffTrigger_BringsTheNextLookForward()
    {
        await using var harness = await CreateAsync(new OrchestratorOptions());
        await harness.AuthorizeAsync();
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(1, harness.Provider.CallCount);

        // Inside the 12 s Noticing pace, a quiet sheet is released.
        harness.Time.Advance(TimeSpan.FromSeconds(4));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(1, harness.Provider.CallCount);

        // A sniff notices something: the next sheet is looked at, at the event floor.
        harness.Worker.EmitSniff(harness.Time.GetUtcNow(), hotChange: 0.4, motion: 0.1);
        await harness.SettleAsync();
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.CallCount);

        // The trigger is spent by that look.
        harness.Time.Advance(TimeSpan.FromSeconds(4));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.CallCount);
    }

    [Fact]
    public async Task SniffsForAStaleGrant_AreIgnored_AndDiagnosticsCountWhatArrived()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        var stale = CaptureAuthorizationGrant.Issue(Guid.NewGuid(), grant.Generation, grant.Target);
        harness.Worker.EmitSniff(harness.Time.GetUtcNow(), grant: stale);
        Assert.DoesNotContain(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Attention);

        harness.Worker.EmitSniff(DateTimeOffset.UtcNow, hotChange: 0.5, motion: 0.4);
        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Equal(1, snapshot.Sniff.Received);
        Assert.Equal(1, snapshot.Sniff.Forwarded);
        Assert.InRange(snapshot.Sniff.LastLatency, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        Assert.True(snapshot.Sniff.MaximumLatency >= snapshot.Sniff.LastLatency);
        Assert.True(snapshot.Sniff.Percentile95Latency <= snapshot.Sniff.MaximumLatency);
    }
}
