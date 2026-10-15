using CompanionCore.Curtain;
using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>
/// M4e (Boss, 2026-10-14): words weighed by how familiar they are to him, never thrown out; how
/// well he remembers comes from his own history and the moment, never chance; some things he
/// remembers clearly, some need a refresher, some he has to look up.
/// </summary>
public sealed class OrganicRememberingTests
{
    // ---- words -------------------------------------------------------------------------

    [Fact]
    public async Task NoWordIsDroppedForItsLength_ShortDistinctiveWordsCount()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "orb door", "Da orb opened da key door.", Tags(BunDexRole.LongTerm, where: "Sky Garden"));
        store.CreatePage(null, "egg thing", "Found a shiny egg.", Tags(BunDexRole.LongTerm));

        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("an orb and a key"));
        var rummage = store.LookFor("the egg");

        Assert.Equal("orb door", Assert.Single(writer.Moments[0].ComesToMind).Page);
        Assert.Equal("egg thing", rummage.Found!.Page);
        Assert.True(rummage.Sure);
    }

    [Fact]
    public void AWordGrowsFamiliar_FromHisOwnPages()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        Assert.Equal(1.0, store.Weight("tree"));
        Assert.True(store.Weight("the") < 0.02);

        var weights = new List<double>();
        for (var page = 0; page < 12; page++)
        {
            store.CreatePage(null, $"tree page {(char)('a' + page)}", "A tree.", Tags(BunDexRole.ShortTerm));
            weights.Add(store.Weight("tree"));
        }

        // M5b leans curious (Boss-approved): a word weighs half after he has met it ten times.
        Assert.Equal(weights.OrderDescending(), weights);
        Assert.Equal(0.5, weights[9], 3);
        Assert.Equal(weights[^1], harness.Open().Weight("tree"));
    }

    [Fact]
    public async Task AWordHeSeesConstantly_IsBackground_TheSameWordIsVividToABnuyWhoNeverSawOne()
    {
        async Task<int> ComesToMind(Func<string, int>? seen)
        {
            using var harness = new BunDexHarness();
            var store = harness.Open();
            store.SeenInTheWorld = seen;
            store.CreatePage(null, "big tree", "Da big tree by da pond.", Tags(BunDexRole.LongTerm, where: "Sunny Acres"));
            var writer = new Recorder();
            await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("a big tree"));
            return writer.Moments[0].ComesToMind.Count;
        }

        Assert.Equal(1, await ComesToMind(null));
        Assert.Equal(0, await ComesToMind(word => word == "tree" ? 400 : 0));
    }

    [Fact]
    public void WhatHeSaw_MakesAWordFamiliar()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.SeenInTheWorld = word => word == "tree" ? 400 : 0;

        Assert.True(store.Weight("tree") < store.Weight("moon") / 40);
        Assert.Equal(1.0, store.Weight("moon"));
    }

    // ---- how well he remembers -----------------------------------------------------------

    [Fact]
    public void Plainly_AShortTermPage_RingsABellAfterTwoSessions_AndHasFadedAfterThree()
    {
        Assert.Equal(Clarity.RingsABell, After(2, (_, _, _) => { }).Clarity);
        Assert.Equal(Clarity.Faded, After(3, (_, _, _) => { }).Clarity);
    }

    /// <summary>Four times he came back and added something new about it: what he noticed, did, and felt.</summary>
    private static readonly string[] NewThings =
    [
        " Da lantern hummed a soft little tune when it got dark outside.",
        " Inside it a tiny moth danced round and round wif glowing wings.",
        " Boss carried it up da creaky stairs to da dusty attic window.",
        " It smelled like warm wax and old paper and maybe cinnamon too.",
        " Snowflakes tapped on its glass while crickets chirped nearby.",
    ];

    [Fact]
    public void ComingBackToAddNewThings_KeepsItClear_AndAReopenRemembersIt()
    {
        var (store, page, clock, harness) = Build(2, (store, page, _) =>
        {
            foreach (var added in NewThings)
            {
                store.AddToPage(page, added);
            }
        });
        using (harness)
        {
            Assert.Equal(Clarity.Clear, store.ClarityOf(page));
            Assert.Equal(Clarity.Clear, BunDexStore.Open(harness.Location, out _, clock).ClarityOf(page));
        }
    }

    [Fact]
    public void AddingTheSameThingAgain_BringsLessEachTime_NeverNothing()
    {
        // No checklist (Boss, M5b): coming back four times with nothing new is not four new experiences.
        Assert.Equal(Clarity.RingsABell, After(2, (store, page, _) =>
        {
            for (var addition = 0; addition < 4; addition++)
            {
                store.AddToPage(page, " Da lantern hummed a soft little tune when it got dark outside.");
            }
        }).Clarity);

        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "moon lantern", "moon lantern", Tags(BunDexRole.ShortTerm));
        var gains = new List<double>();
        for (var addition = 0; addition < 5; addition++)
        {
            var before = store.RichnessOf(page);
            store.AddToPage(page, " Da lantern hummed a soft tune.");
            gains.Add(store.RichnessOf(page) - before);
        }

        Assert.Equal(gains.OrderDescending(), gains);
        Assert.All(gains, gain => Assert.True(gain > 0));
        Assert.True(gains[^1] < gains[0] / 3);
    }

    [Fact]
    public void HowMuchHeWrote_ALongPage_StaysLongerThanABriefNote()
    {
        var many = string.Join(' ', Enumerable.Range(0, 80).Select(index => $"{(char)('a' + (index / 26))}{(char)('a' + (index % 26))}word"));

        Assert.Equal(Clarity.Clear, After(3, (_, _, _) => { }, text: many).Clarity);
        Assert.Equal(Clarity.Faded, After(3, (_, _, _) => { }, text: "moon lantern " + string.Join(' ', Enumerable.Range(0, 18).Select(index => $"{(char)('a' + index)}word"))).Clarity);
    }

    [Fact]
    public async Task BeingRemindedByNewThings_MakesItStronger_AndAReopenRemembersIt()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "moon lantern", "moon lantern", Tags(BunDexRole.ShortTerm));

        // Each time something new happened that brought it to mind.
        foreach (var reminder in NewThings)
        {
            var writer = new Recorder();
            await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm), Saw("a moon lantern." + reminder));
            Assert.Equal("moon lantern", Assert.Single(writer.Moments[0].ComesToMind).Page);
            clock.Advance(BunDexStore.CameToMindCooldown);
        }

        for (var session = 0; session < 2; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }

        Assert.Equal(Clarity.Clear, store.ClarityOf(page));
        Assert.Equal(Clarity.Clear, BunDexStore.Open(harness.Location, out _, clock).ClarityOf(page));
    }

    [Fact]
    public void LookingUpTheSameThingAgain_BringsLittle()
    {
        Assert.Equal(Clarity.RingsABell, After(2, (store, _, _) =>
        {
            for (var lookup = 0; lookup < 4; lookup++)
            {
                Assert.NotNull(store.LookFor("the moon lantern").Found);
            }
        }).Clarity);
    }

    [Fact]
    public void APageHesThinkingAbout_StaysOnHisMind()
    {
        Assert.Equal(Clarity.Clear, After(2, (_, _, _) => { }, tags: Tags(BunDexRole.ThingsThinkingAbout)).Clarity);
    }

    [Fact]
    public async Task BackWhereItHappened_ItComesBackClearly()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        store.CreatePage(null, "moon lantern", "moon lantern", Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        for (var session = 0; session < 2; session++)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            store.BeginSession();
        }

        clock.Advance(TimeSpan.FromDays(2.5));
        Assert.Equal(Clarity.RingsABell, store.ClarityOf(store.FindPage(null, "moon lantern")!.Value));

        var there = new Recorder();
        await new BunDexKeeper(store, there).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("a moon lantern"));

        Assert.Equal("moon lantern", Assert.Single(there.Moments[0].ComesToMind).Page);
        Assert.Empty(there.Moments[0].Feelings);
    }

    [Fact]
    public void NothingDistinctiveToLookFor_HeCannotFindIt_EvenIfAPageHasThoseWords()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "moon key", "Da moon key is the key to a door.", Tags(BunDexRole.LongTerm));

        var rummage = store.LookFor("the a");

        Assert.Null(rummage.Found);
        Assert.Equal([LookStepKind.StartsLooking, LookStepKind.CantFind], rummage.Steps.Select(step => step.Kind));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WhatHeHoldsDear_OrHasBeenThinkingAbout_HeRealisticallyKnows(bool cherished)
    {
        Assert.Equal(Clarity.Clear, After(2, (store, _, _) =>
        {
            var favourite = store.CreatePage(null, "favourite", "Da moon lantern is da best.", Tags(cherished ? BunDexRole.ShortTerm : BunDexRole.ThingsThinkingAbout));
            if (cherished)
            {
                Cherish(store, favourite);
            }
        }).Clarity);
    }

    [Fact]
    public void AHiddenCherishedKind_IsNotHoldingItDear()
    {
        // M5b: no tag or kind makes something dear to him; only his own fond experiences do.
        Assert.Equal(Clarity.RingsABell, After(2, (store, _, _) => store.CreatePage(null, "favourite", "Da moon lantern is da best.", Tags(BunDexRole.Cherished))).Clarity);
    }

    [Fact]
    public void WhatHeWroteThisSession_IsWhatHesBeenThinkingAbout()
    {
        Assert.Equal(Clarity.Clear, After(2, (store, _, _) => store.CreatePage(null, "today", "Saw anuver moon lantern!", Tags(BunDexRole.ShortTerm)), beforeSessions: false).Clarity);
    }

    [Fact]
    public void WhatMattersToHim_PlugsIn()
    {
        Assert.Equal(Clarity.Clear, After(2, (store, _, _) => store.MattersToHim = words => words.Contains("lantern") ? 1 : 0).Clarity);
    }

    [Theory]
    [InlineData(FeelingKind.Frightened)]
    [InlineData(FeelingKind.Fond)]
    public void HowStronglyHeFelt_KeepsItLonger(FeelingKind kind)
    {
        Assert.Equal(Clarity.Faded, After(3, (_, _, _) => { }).Clarity);
        Assert.Equal(Clarity.RingsABell, After(3, (store, page, _) => store.Experience(page, "oh!! moon lantern!!", new ExpressedFeeling(kind, 1))).Clarity);
    }

    [Fact]
    public void AHiddenScaryKind_IsNotAFeeling()
    {
        // M5b: how strongly he felt comes from what he expressed, never from a hidden kind.
        Assert.Equal(Clarity.Faded, After(3, (_, _, _) => { }, tags: Tags(BunDexRole.ScaryOrCool) with { Layer = MemoryLayer.ShortTerm }).Clarity);
    }

    [Fact]
    public void BeingWhereItHappened_BringsItBack()
    {
        var (store, page, _, harness) = Build(3, (_, _, _) => { }, tags: Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        using (harness)
        {
            Assert.Equal(Clarity.Faded, store.ClarityOf(page));
            Assert.Equal(Clarity.RingsABell, store.ClarityOf(page, "Hollow House"));
        }
    }

    // ---- the bell's little lookup ----------------------------------------------------------

    [Fact]
    public async Task BackWhereItHappened_ABellNearlyThere_IsLookedUp()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        store.CreatePage(null, "moon lantern", "moon lantern", Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        for (var session = 0; session < 2; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }

        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("a moon lantern"));

        Assert.Single(writer.Moments[0].Feelings);
        Assert.Equal(LookStepKind.Found, writer.Moments[0].Rummage[^1].Kind);
    }

    [Fact]
    public async Task APlainBell_StaysAPassingFeeling()
    {
        var moment = await BellMoment((_, _) => { }, TimeSpan.FromDays(7));

        Assert.Equal(["Something about this rings a bell."], moment.Feelings.Select(feeling => feeling.Line));
        Assert.Empty(moment.Rummage);
    }

    [Fact]
    public async Task ABellCloseToHisHeart_LeadsToALittleLookup()
    {
        // Only a little of it is close to his heart, so it is neither clear nor on the tip of his tongue.
        var moment = await BellMoment((store, _) => Cherish(store, store.CreatePage(null, "pie", "Moon pie is da best.", Tags(BunDexRole.ShortTerm))), TimeSpan.FromDays(7), text: "moon lantern glows bright purple");

        Assert.Single(moment.Feelings);
        Assert.Equal(LookStepKind.Found, moment.Rummage[^1].Kind);
    }

    [Fact]
    public async Task ABellHeFeltStronglyAbout_LeadsToALittleLookup()
    {
        var moment = await BellMoment((store, page) => store.Experience(page, "oogly!! moon lantern!!", new ExpressedFeeling(FeelingKind.Frightened, 1)), TimeSpan.FromDays(7), sessions: 3);

        Assert.Single(moment.Feelings);
        Assert.Equal(LookStepKind.Found, moment.Rummage[^1].Kind);
    }

    [Theory]
    [InlineData(2.5, true)]
    [InlineData(3.5, false)]
    public async Task OnTheTipOfHisTongue_HeLooksItUp(double days, bool looks)
    {
        var moment = await BellMoment((_, _) => { }, TimeSpan.FromMinutes(1), extra: TimeSpan.FromDays(days));

        Assert.Single(moment.Feelings);
        Assert.Equal(looks, moment.Rummage.Count > 0);
    }

    // ---- helpers ---------------------------------------------------------------------------

    /// <summary>A short-term "moon lantern" page; <paramref name="sessions"/> play sessions a week apart; then its clarity.</summary>
    private static (Clarity Clarity, int Unused) After(int sessions, Action<BunDexStore, Guid, HandClock> act, string? text = null, PageTags? tags = null, bool beforeSessions = true)
    {
        var (store, page, _, harness) = Build(sessions, act, text, tags, beforeSessions);
        using (harness)
        {
            return (store.ClarityOf(page), 0);
        }
    }

    private static (BunDexStore Store, Guid Page, HandClock Clock, BunDexHarness Harness) Build(int sessions, Action<BunDexStore, Guid, HandClock> act, string? text = null, PageTags? tags = null, bool beforeSessions = true)
    {
        var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "moon lantern", text ?? "moon lantern", tags ?? Tags(BunDexRole.ShortTerm));
        if (beforeSessions)
        {
            act(store, page, clock);
        }

        for (var session = 0; session < sessions; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }

        if (!beforeSessions)
        {
            act(store, page, clock);
        }

        return (store, page, clock, harness);
    }

    /// <summary>A short-term "moon lantern" page from Hollow House, rung as a bell by a keep moment in Sky Garden.</summary>
    private static async Task<KeepMoment> BellMoment(Action<BunDexStore, Guid> before, TimeSpan between, PageTags? tags = null, int sessions = 2, TimeSpan? extra = null, string text = "moon lantern")
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "moon lantern", text, tags ?? Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        before(store, page);
        for (var session = 0; session < sessions; session++)
        {
            clock.Advance(between);
            store.BeginSession();
        }

        clock.Advance(extra ?? TimeSpan.Zero);
        var writer = new Recorder();
        // Kept somewhere else, so being where it happened is not what brings it back here.
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Sky Garden"), Saw("a moon lantern"));
        return writer.Moments[0];
    }

    private sealed class Recorder : IBunDexWriter
    {
        public List<KeepMoment> Moments { get; } = [];

        public ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken)
        {
            Moments.Add(moment);
            return ValueTask.FromResult(KeepResponse.NotNow);
        }

        public ValueTask<TidyResponse> TidyAsync(TidyMoment moment, BunDexView notebook, CancellationToken cancellationToken) =>
            ValueTask.FromResult(TidyResponse.NotNow);
    }
}
