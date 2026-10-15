using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.BunDex;
using CompanionCore.Curtain;
using CompanionCore.Memory;
using CompanionCore.Sightings;
using CompanionCore.Watchbun;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// M5b (Boss, 2026-10-14): "'Remember this, Prince' reaches him not as a command, but as an
/// earnest ask from me, that makes him go 'maybees I should write this down'… What he decides to
/// do with that is up to him." It changes nothing underneath by itself.
/// </summary>
public sealed class OrchestrationRememberThisTests
{
    private const string Ask = "Remember this, Prince!";
    private static readonly string Game = CompanionOrchestrator.GameReference("synthetic-game.exe");

    [Fact]
    public async Task RememberThis_ReachesHimAsBossWords_WithWhatJustHappened_AndHisPageIsLinkedToIt()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        await harness.AuthorizeAsync();
        await SeeTreesAsync(harness, "tree", 3);
        await harness.WaitForAsync(() => writer.Moments.Count == 1);
        await harness.SettleAsync();

        Assert.True(await harness.Orchestrator.RememberThisAsync(Ask));
        await harness.WaitForAsync(() => writer.Moments.Count == 2);
        await harness.SettleAsync();

        var asked = writer.Moments[1];
        Assert.Equal(writer.Moments[0].WhatHappened, asked.WhatHappened);
        Assert.Equal(SenseKind.BossSaid, asked.Heard!.Kind);
        Assert.Equal(Ask, asked.Heard.Quoted);
        Assert.Null(writer.Moments[0].Heard);
        var request = asked.ToRequest(harness.Host.BunDex.View()).Text;
        Assert.Contains("Boss said:", request, StringComparison.Ordinal);
        Assert.Contains(Ask, request, StringComparison.Ordinal);
        Assert.All(harness.Host.BunDex.FindPages(_ => true), page => Assert.Equal([PageSources.Sightings(Game, "tree", 3)], harness.Host.BunDex.SourcesOf(page.PageId)));
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task WithNothingRecent_HeHearsBossWordsAlone()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        await harness.AuthorizeAsync();

        Assert.True(await harness.Orchestrator.RememberThisAsync(Ask));
        await harness.WaitForAsync(() => writer.Moments.Count == 1);
        await harness.SettleAsync();

        Assert.Equal(SenseKind.BossSaid, writer.Moments[0].WhatHappened.Kind);
        Assert.Equal(Ask, writer.Moments[0].WhatHappened.Quoted);
        Assert.Null(writer.Moments[0].Heard);
    }

    [Fact]
    public async Task ALongAgoMoment_IsNotWhatThisMeans()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        await harness.AuthorizeAsync();
        await SeeTreesAsync(harness, "tree", 3);
        await harness.WaitForAsync(() => writer.Moments.Count == 1);
        await harness.SettleAsync();
        await harness.AdvanceAndTickAsync(CompanionOrchestrator.KeepFadesAfter + TimeSpan.FromSeconds(1));

        Assert.True(await harness.Orchestrator.RememberThisAsync(Ask));
        await harness.WaitForAsync(() => writer.Moments.Count == 2);
        await harness.SettleAsync();

        Assert.Equal(SenseKind.BossSaid, writer.Moments[1].WhatHappened.Kind);
    }

    [Fact]
    public async Task WithNoOneToHearIt_NothingHappens()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();

        Assert.False(await harness.Orchestrator.RememberThisAsync(Ask));
        await harness.AuthorizeAsync();
        Assert.False(await harness.Orchestrator.RememberThisAsync(" "));
        Assert.False(await harness.Orchestrator.RememberThisAsync(null!));

        await harness.Controller.PrivacyStopAsync();
        await harness.SettleAsync();
        Assert.False(await harness.Orchestrator.RememberThisAsync(Ask));
        await harness.AdvanceAndTickAsync(TimeSpan.FromMinutes(1));

        Assert.Empty(writer.Moments);
        Assert.Equal(0, await PendingAsync(harness));
    }

    [Fact]
    public async Task AfterAPrivacyStopAndResume_AnEarlierMomentIsNotWhatThisMeans()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        await harness.AuthorizeAsync();
        await SeeTreesAsync(harness, "tree", 3);
        await harness.WaitForAsync(() => writer.Moments.Count == 1);
        await harness.SettleAsync();

        await harness.Controller.PrivacyStopAsync();
        await harness.SettleAsync();
        Assert.True((await harness.Controller.ResumeExplicitlyAsync()).Succeeded);
        await harness.SettleAsync();

        Assert.True(await harness.Orchestrator.RememberThisAsync(Ask));
        await harness.WaitForAsync(() => writer.Moments.Count == 2);
        await harness.SettleAsync();

        Assert.Equal(SenseKind.BossSaid, writer.Moments[1].WhatHappened.Kind);
    }

    [Fact]
    public async Task ItReachesHimFirst_WhenHeIsFree()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        var grant = await harness.AuthorizeAsync();
        await BnuyModeAsync(harness, grant);
        await SeeTreesAsync(harness, "tree", 3);
        await SeeTreesAsync(harness, "lantern", 3);
        Assert.Equal(2, await PendingAsync(harness));

        Assert.True(await harness.Orchestrator.RememberThisAsync(Ask));
        Assert.Equal(3, await PendingAsync(harness));
        Assert.Empty(writer.Moments);

        for (var step = 0; step < 120 && writer.Moments.Count == 0; step++)
        {
            await harness.AdvanceAndTickAsync(TimeSpan.FromSeconds(5));
        }

        await harness.WaitForAsync(() => writer.Moments.Count >= 1);
        Assert.NotNull(writer.Moments[0].Heard);
        Assert.Contains("lantern", writer.Moments[0].WhatHappened.Words, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ItChangesNothingUnderneath_ByItself()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter(declineWhen: _ => true);
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "Glowing Door", "synthetic.door", "[neutral memory] the door glowed.")));
        await harness.SheetAsync(changeScore: 0.6);
        await harness.WaitForAsync(() => writer.Moments.Count == 1);
        await harness.SettleAsync();
        var memory = Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.door" }));

        Assert.True(await harness.Orchestrator.RememberThisAsync(Ask));
        await harness.WaitForAsync(() => writer.Moments.Count == 2);
        await harness.SettleAsync();

        // He chose not to write: nothing was written, nothing was made dear, and the memory store is untouched.
        Assert.Empty(harness.Host.BunDex.FindPages(_ => true));
        Assert.Empty(harness.Host.BunDex.CherishedMemories());
        var after = Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.door" }));
        Assert.Equal(memory.RecordChecksum, after.RecordChecksum);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    private static async Task SeeTreesAsync(OrchestrationHarness harness, string thing, int count)
    {
        for (var seen = 0; seen < count; seen++)
        {
            harness.Host.Sightings.See(new Sighting(harness.Time.GetUtcNow(), Game, null, thing, ObservationKind.Other, false, "a tall " + thing, SightingSource.Observation));
        }

        await harness.SettleAsync();
    }

    private static async Task BnuyModeAsync(OrchestrationHarness harness, CompanionCore.Capture.Contracts.CaptureAuthorizationGrant grant)
    {
        Assert.Equal(WatchbunRefusal.None, await harness.Orchestrator.AddWatchTaskAsync("door.opened", TimeSpan.FromHours(2)));
        await harness.Orchestrator.SubmitGameEventLineAsync($"{{\"session\":\"{grant.TargetSessionId:D}\",\"kind\":\"meaningful\",\"key\":\"door.opened\"}}");
        await harness.SettleAsync();
        Assert.Equal(AttentionState.HighAttention, (await harness.Orchestrator.GetSnapshotAsync()).Attention!.State);
    }

    private static Task<int> PendingAsync(OrchestrationHarness harness)
    {
        var completion = new TaskCompletionSource<int>();
        return harness.Orchestrator.InvokeOnMailboxAsync(() =>
        {
            completion.SetResult(harness.Orchestrator.PendingKeeps);
            return Task.CompletedTask;
        }).ContinueWith(_ => completion.Task).Unwrap();
    }
}
