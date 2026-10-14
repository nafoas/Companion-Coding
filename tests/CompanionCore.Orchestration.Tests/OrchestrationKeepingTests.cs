using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.BunDex;
using CompanionCore.Conversation;
using CompanionCore.Curtain;
using CompanionCore.Memory;
using CompanionCore.Sightings;
using CompanionCore.Watchbun;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// M5a end to end (Boss, 2026-10-14: "The Little Self decides. The subconscious helps facilitate
/// it. He decides."): moments reach him during play, one at a time, never during intense moments
/// or a conversation; whatever he writes is linked to where it came from, in the BrainCase only.
/// </summary>
public sealed class OrchestrationKeepingTests
{
    private static readonly string Game = CompanionOrchestrator.GameReference("synthetic-game.exe");

    [Fact]
    public async Task ANewMemoryDuringPlay_ReachesHim_AndHisPageIsLinkedToIt_WhileTheMemoryStoreIsUntouched()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "Glowing Door", "synthetic.door", "[neutral memory] the door glowed.")));

        await harness.SheetAsync(changeScore: 0.6);
        var memory = Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.door" }));
        await harness.WaitForAsync(() => harness.Host.BunDex.PagesFrom(PageSources.Memory(memory.Record.RecordId)).Count == 1);

        var page = Assert.Single(harness.Host.BunDex.PagesFrom(PageSources.Memory(memory.Record.RecordId)));
        Assert.Equal([PageSources.Memory(memory.Record.RecordId)], harness.Host.BunDex.SourcesOf(page));
        Assert.Contains("the door glowed", harness.Host.BunDex.PageText(page), StringComparison.Ordinal);

        // Keeping only reads memories: the store holds the same one record, byte for byte.
        var after = Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.door" }));
        Assert.Equal(memory.RecordChecksum, after.RecordChecksum);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task ThirtyTreesDuringPlay_ReachHimThreeTimes_NeverThirty_AndThePagesKeepTheMagnitude()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        await harness.AuthorizeAsync();

        await SeeTreesAsync(harness, "tree", 30);
        await harness.WaitForAsync(() => writer.Moments.Count == 3);
        await harness.SettleAsync();

        Assert.Equal([SenseKind.SawAFewTimes, SenseKind.SawLotsOfTimes, SenseKind.SawSoManyTimes], writer.Moments.Select(moment => moment.WhatHappened.Kind));
        var page = Assert.Single(harness.Host.BunDex.PagesFrom(PageSources.Sightings(Game, "tree", 30)));
        Assert.Contains("so, so many times", harness.Host.BunDex.PageText(page), StringComparison.Ordinal);
        // No number ever crosses, not even in his notebook's page names.
        Assert.All(writer.Moments, moment => Assert.DoesNotMatch(@"\d", moment.ToRequest(harness.Host.BunDex.View()).Text));
    }

    [Fact]
    public async Task NeverDuringAnIntenseMoment_OrAConversation_AndItWaitsItsTurn()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        var grant = await harness.AuthorizeAsync();
        await BnuyModeAsync(harness, grant);

        await SeeTreesAsync(harness, "tree", 3);
        Assert.Empty(writer.Moments);
        Assert.Equal(1, await PendingAsync(harness));

        // It calms down: the moment that waited reaches him.
        var target = new ReplyTarget();
        for (var step = 0; step < 60 && !harness.Notices.Any(IsOpening); step++)
        {
            await harness.AdvanceAndTickAsync(TimeSpan.FromSeconds(5));
        }

        await harness.WaitForAsync(() => writer.Moments.Count == 1);

        // A conversation starts: a new moment waits while Boss and Prince are talking.
        foreach (var notice in harness.Notices)
        {
            target.Observe(notice);
        }

        Assert.Equal(ConversationRefusal.None, await target.ReplyAsync(harness.Orchestrator, UserTurnKind.Substantive));
        await harness.SettleAsync();
        Assert.NotNull((await harness.Orchestrator.GetSnapshotAsync()).Conversation.Thread);
        await SeeTreesAsync(harness, "lantern", 3);
        await harness.SettleAsync();

        Assert.Single(writer.Moments);
        Assert.Equal(1, await PendingAsync(harness));
    }

    [Fact]
    public async Task APrivacyStop_ClearsWhatWaits_AndNothingIsKept()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        var grant = await harness.AuthorizeAsync();
        await BnuyModeAsync(harness, grant);
        await SeeTreesAsync(harness, "tree", 3);
        Assert.Equal(1, await PendingAsync(harness));

        await harness.Controller.PrivacyStopAsync();
        await harness.SettleAsync();
        await harness.AdvanceAndTickAsync(TimeSpan.FromMinutes(5));

        Assert.Equal(0, await PendingAsync(harness));
        Assert.Empty(writer.Moments);
        Assert.Empty(harness.Host.BunDex.FindPages(_ => true));
    }

    [Fact]
    public async Task TheQueueIsBounded_AndMomentsFade()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        var grant = await harness.AuthorizeAsync();
        await BnuyModeAsync(harness, grant);

        foreach (var thing in Enumerable.Range(0, 12).Select(index => $"thing {(char)('a' + index)}"))
        {
            await SeeTreesAsync(harness, thing, 3);
        }

        Assert.Equal(CompanionOrchestrator.MaximumPendingKeeps, await PendingAsync(harness));

        await harness.AdvanceAndTickAsync(CompanionOrchestrator.KeepFadesAfter + TimeSpan.FromMinutes(1));
        await harness.SettleAsync();

        Assert.Equal(0, await PendingAsync(harness));
        Assert.Empty(writer.Moments);
    }

    [Fact]
    public async Task ADamagedBunDex_KeepsNothing_AndPlayGoesOn()
    {
        await using var harness = await CreateAsync();
        harness.Host.BunDex.CreatePage(null, "page", "something happened", new PageTags(BunDexRole.ShortTerm, MemoryLayer.Unspecified, null, null, [], harness.Time.GetUtcNow()));
        await harness.Host.DisposeAsync();
        var segment = BunDexLedger.Segments(BunDexLocation.For(harness.Location).LedgerRoot)[^1].Path;
        var lines = File.ReadAllLines(segment);
        lines[0] = lines[0].Replace("\"page\"", "\"p4ge\"", StringComparison.Ordinal);
        File.WriteAllLines(segment, lines);
        var ledgerBytes = File.ReadAllBytes(segment);
        var writer = new StandInWriter();
        harness.BunDexWriter = writer;
        await harness.OpenHostAsync();
        await harness.AuthorizeAsync();

        await SeeTreesAsync(harness, "tree", 3);
        await harness.AdvanceAndTickAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(BunDexHealth.Damaged, harness.Host.BunDex.Health);
        Assert.Empty(writer.Moments);
        Assert.Equal(ledgerBytes, File.ReadAllBytes(segment));
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task AKeepingFailure_IsContained_AndTheNextMomentStillReachesHim()
    {
        await using var harness = await CreateAsync();
        var writer = new ThrowingWriter();
        harness.BunDexWriter = writer;
        await harness.ReopenHostAsync();
        await harness.AuthorizeAsync();

        await SeeTreesAsync(harness, "tree", 3);
        await harness.WaitForAsync(() => harness.Orchestrator.Faults > 0);
        await SeeTreesAsync(harness, "lantern", 3);
        await harness.WaitForAsync(() => writer.Calls == 2);

        Assert.NotNull((await harness.Orchestrator.GetSnapshotAsync()).TargetSessionId);
    }

    private static bool IsOpening(CompanionNotice notice) =>
        notice.Conversation?.Kind is ConversationIntentKind.AmbientExpression or ConversationIntentKind.OfferOpening;

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

    private sealed class ThrowingWriter : IBunDexWriter
    {
        public int Calls;

        public ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            throw new IOException("synthetic writer failure");
        }

        public ValueTask<TidyResponse> TidyAsync(TidyMoment moment, BunDexView notebook, CancellationToken cancellationToken) =>
            ValueTask.FromResult(TidyResponse.NotNow);
    }
}
