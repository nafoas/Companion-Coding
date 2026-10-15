using CompanionCore.Curtain;
using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>
/// M5b (Boss, 2026-10-14): memories form the way ours do. "We don't decide for him- we let his
/// actions speak for him… there isn't a memory checklist, it reinforces in different ways every
/// time." What his experiences of a memory brought makes it stronger; fondness grows only from
/// his own expressions read as fond; past a high bar it is cherished, and never demoted.
/// </summary>
public sealed class WhatMattersTests
{
    private static readonly ExpressedFeeling Delighted = new(FeelingKind.Fond, 1);

    // ---- fondness comes only from him ------------------------------------------------------

    [Fact]
    public void HisFondExpressions_GrowFondness_FrightAndPlainWordsDoNot()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var sweet = store.CreatePage(null, "carrots", "Found carrots.", Tags(BunDexRole.ShortTerm));
        var scary = store.CreatePage(null, "mannequin", "A mannequin.", Tags(BunDexRole.ShortTerm));
        var plain = store.CreatePage(null, "fence", "A fence.", Tags(BunDexRole.ShortTerm));

        store.Experience(sweet, "dese carrots is so sweet", new ExpressedFeeling(FeelingKind.Fond, 0.5));
        store.Experience(scary, "it turned its head!! oogly", new ExpressedFeeling(FeelingKind.Frightened, 1));
        store.Experience(plain, "a wooden fence wif posts", null);

        Assert.True(store.FondnessOf(sweet) > 0);
        Assert.Equal(0, store.FondnessOf(scary));
        Assert.Equal(0, store.FondnessOf(plain));
        Assert.True(store.RichnessOf(plain) > store.RichnessOf(store.CreatePage(null, "fence two", "A fence.", Tags(BunDexRole.ShortTerm))));
    }

    [Fact]
    public void HowFond_ScalesWhatAFondExperienceBrings()
    {
        double FondnessAt(double strength)
        {
            using var harness = new BunDexHarness();
            var store = harness.Open();
            var page = store.CreatePage(null, "carrots", "Found carrots.", Tags(BunDexRole.ShortTerm));
            store.Experience(page, "dese carrots is so sweet", new ExpressedFeeling(FeelingKind.Fond, strength));
            return store.FondnessOf(page);
        }

        Assert.Equal(2 * FondnessAt(0.25), FondnessAt(0.5), 6);
        Assert.Equal(0, FondnessAt(0));
    }

    [Fact]
    public void TheCarrotPatch_VariedFondExperiences_GrowIntoCherished_TheSameDelightRepeated_DoesNot()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var patch = store.CreatePage(null, "carrot patch", "Da carrot patch by da path.", Tags(BunDexRole.ShortTerm));
        var again = store.CreatePage(null, "carrot again", "Da carrot patch by da path.", Tags(BunDexRole.ShortTerm));

        Cherish(store, patch);
        for (var time = 0; time < 200; time++)
        {
            store.Experience(again, "carrots so sweet!!", Delighted);
        }

        Assert.True(store.Cherishes(patch));
        Assert.False(store.Cherishes(again));
        Assert.True(store.FondnessOf(again) < BunDexStore.CherishedAt / 2, "Two hundred of the same delight are still far from cherished.");
    }

    [Fact]
    public void Cherished_IsReachedOnlyPastTheHighBar()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "carrot patch", "Da carrot patch.", Tags(BunDexRole.ShortTerm));

        foreach (var fond in FondThings)
        {
            Assert.Equal(store.FondnessOf(page) >= BunDexStore.CherishedAt, store.Cherishes(page));
            store.Experience(page, fond, Delighted);
        }

        Assert.True(store.Cherishes(page));
    }

    [Fact]
    public void Cherished_IsNeverDemoted_StaysClear_AndAReopenRemembersExactly()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "carrot patch", "Da carrot patch.", Tags(BunDexRole.ShortTerm));
        Cherish(store, page);
        store.Experience(page, "a scary crow stood dere!!", new ExpressedFeeling(FeelingKind.Frightened, 1));
        for (var session = 0; session < 1000; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }

        Assert.True(store.Cherishes(page));
        Assert.Equal(Clarity.Clear, store.ClarityOf(page));
        var reopened = BunDexStore.Open(harness.Location, out _, clock);
        Assert.True(reopened.Cherishes(page));
        Assert.Equal(store.FondnessOf(page), reopened.FondnessOf(page));
        Assert.Equal(store.RichnessOf(page), reopened.RichnessOf(page));
        Assert.Equal(Clarity.Clear, reopened.ClarityOf(page));
    }

    [Fact]
    public void AReopen_RemembersWhatEachExperienceBroughtThen_EvenAsHisWorldChanged()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "carrots", "Found carrots.", Tags(BunDexRole.ShortTerm));
        store.Experience(page, "dese carrots is so sweet", Delighted);
        store.SeenInTheWorld = word => word == "carrots" ? 500 : 0;
        store.Experience(page, "more carrots by da barn", Delighted);
        var richness = store.RichnessOf(page);
        var fondness = store.FondnessOf(page);

        var reopened = harness.Open();

        Assert.Equal(richness, reopened.RichnessOf(page));
        Assert.Equal(fondness, reopened.FondnessOf(page));
    }

    [Fact]
    public void BeforeM5b_ARememberingWithNoWords_IsHisOwnPageComingBackToHim()
    {
        using var harness = new BunDexHarness();
        var page = harness.Open().CreatePage(null, "moon lantern", "moon lantern", Tags(BunDexRole.ShortTerm));
        var before = harness.Open().RichnessOf(page);

        // An older ledger line: a remembering that kept no words and no gain.
        BunDexLedger.Open(harness.Location.LedgerRoot).Append(new LedgerEntry(0, T0, LedgerKind.PageRecalled, PageId: page));

        var after = harness.Open().RichnessOf(page);
        Assert.True(after > before);
        Assert.True(after - before < before, "His own page again is familiar to it: it brings less than writing it did.");
    }

    [Fact]
    public async Task BeingRemindedByNewThings_BringsMore_ThanTheSameReminderAgain()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var varied = store.CreatePage(null, "moon lantern", "Da moon lantern.", Tags(BunDexRole.ShortTerm));
        var keeper = new BunDexKeeper(store, new Recorder());
        var gains = new List<double>();
        foreach (var reminder in new[] { "a moon lantern by a misty pond", "a moon lantern by a misty pond", "a moon lantern on a snowy rooftop" })
        {
            var before = store.RichnessOf(varied);
            await keeper.KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm), Saw(reminder));
            gains.Add(store.RichnessOf(varied) - before);
            clock.Advance(BunDexStore.CameToMindCooldown);
        }

        Assert.True(gains[1] > 0);
        Assert.True(gains[1] < gains[0]);
        Assert.True(gains[2] > gains[1], "Something new about it brings more than the same thing again.");
    }

    // ---- what he is fond of spreads, fades like a memory, and comes home ----------------

    [Fact]
    public void WhatHesFondOf_SpreadsToWhatSharesItsDistinctiveThings()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var patch = store.CreatePage(null, "carrot patch", "Da carrot patch.", Tags(BunDexRole.ShortTerm));
        foreach (var fond in FondThings.Take(3))
        {
            store.Experience(patch, fond, Delighted);
        }

        var giant = store.CreatePage(null, "giant carrot", "A giant carrot.", Tags(BunDexRole.ShortTerm));
        var bucket = store.CreatePage(null, "rusty bucket", "A rusty bucket.", Tags(BunDexRole.ShortTerm));

        Assert.False(store.Cherishes(patch));
        Assert.True(store.CareOf(giant) > 0);
        Assert.Equal(0, store.CareOf(bucket));
    }

    [Fact]
    public void AFrightenedMemory_DoesNotSpreadFondness()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var crow = store.CreatePage(null, "scary crow", "Da scary crow.", Tags(BunDexRole.ShortTerm));
        store.Experience(crow, "it flapped at me!! oogly", new ExpressedFeeling(FeelingKind.Frightened, 1));
        var another = store.CreatePage(null, "another crow", "Anuver crow.", Tags(BunDexRole.ShortTerm));

        Assert.Equal(0, store.CareOf(another));
    }

    [Fact]
    public void Fondness_FadesLikeAMemory_AndComesHomeWhenHesReminded_NeverDemoted()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var patch = store.CreatePage(null, "carrot patch", "Da carrot patch.", Tags(BunDexRole.ShortTerm));
        foreach (var fond in FondThings.Take(3))
        {
            store.Experience(patch, fond, Delighted);
        }

        var giant = store.CreatePage(null, "giant carrot", "A giant carrot.", Tags(BunDexRole.ShortTerm));
        var fondness = store.FondnessOf(patch);
        var near = store.CareOf(giant);
        for (var session = 0; session < 5; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }

        var faded = store.CareOf(giant);
        Assert.True(faded > 0);
        Assert.True(faded < near);
        Assert.Equal(fondness, store.FondnessOf(patch));

        Assert.NotNull(store.LookFor("da carrot patch").Found);
        Assert.Equal(near, store.CareOf(giant), 6);
        Assert.True(store.FondnessOf(patch) >= fondness);
    }

    [Fact]
    public async Task ABellOfSomethingLikeWhatHesFondOf_IsLookedUp()
    {
        async Task<KeepMoment> Bell(bool fond)
        {
            using var harness = new BunDexHarness();
            var clock = new HandClock(T0);
            var store = BunDexStore.Open(harness.Location, out _, clock);
            store.CreatePage(null, "giant carrot", "A giant carrot by da well.", Tags(BunDexRole.ShortTerm, where: "Hollow House"));
            var patch = store.CreatePage(null, "carrot patch", "Da carrot patch.", Tags(BunDexRole.LongTerm));
            if (fond)
            {
                foreach (var thing in FondThings.Take(3))
                {
                    store.Experience(patch, thing, Delighted);
                }
            }

            for (var session = 0; session < 2; session++)
            {
                clock.Advance(TimeSpan.FromDays(7));
                store.BeginSession();
            }

            var writer = new Recorder();
            await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Sky Garden"), Saw("a giant carrot by a well"));
            return writer.Moments[0];
        }

        var plain = await Bell(fond: false);
        var fondOf = await Bell(fond: true);

        Assert.Empty(plain.Rummage);
        Assert.Equal(LookStepKind.Found, fondOf.Rummage[^1].Kind);
    }

    // ---- nothing decides for him ------------------------------------------------------------

    [Fact]
    public void NoTagKindOrLayer_EverCherishes_OrIsAFeeling()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var pages = new List<Guid>();
        foreach (var role in Enum.GetValues<BunDexRole>())
        {
            foreach (var layer in Enum.GetValues<MemoryLayer>())
            {
                pages.Add(store.CreatePage(null, $"{role} {layer}", "something happened", Tags(role) with { Layer = layer }));
            }
        }

        for (var session = 0; session < 1000; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }

        Assert.All(pages, page =>
        {
            Assert.False(store.Cherishes(page));
            Assert.Equal(0, store.FondnessOf(page));
            Assert.NotEqual(Clarity.Clear, store.ClarityOf(page));
        });
        Assert.Empty(store.CherishedMemories());
    }

    [Fact]
    public void CherishedMemories_AreTheMemoryRecordsBehindHisCherishedPages()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var dear = Guid.NewGuid();
        var plain = Guid.NewGuid();
        var cherished = store.CreatePage(null, "carrot patch", "Da carrot patch.", Tags(BunDexRole.ShortTerm) with { Sources = [PageSources.Memory(dear), PageSources.Sightings("synthetic-game", "carrot", 10)] });
        store.CreatePage(null, "fence", "A fence.", Tags(BunDexRole.ShortTerm) with { Sources = [PageSources.Memory(plain)] });
        // Another kind of hidden source, even one shaped like a memory's, is never taken for one.
        store.AddToPage(cherished, " so many carrots.", ["photos:" + Guid.NewGuid().ToString("N")]);
        Assert.Empty(store.CherishedMemories());

        Cherish(store, cherished);

        Assert.Equal([dear], store.CherishedMemories());
        Assert.Equal([dear], harness.Open().CherishedMemories());
    }

    [Fact]
    public void AFeeling_HasAKnownKindAndAStrengthFromNothingToAll()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpressedFeeling(FeelingKind.Fond, 1.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpressedFeeling(FeelingKind.Fond, -0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpressedFeeling(FeelingKind.Fond, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpressedFeeling((FeelingKind)99, 0.5));
        Assert.Equal(0, new ExpressedFeeling(FeelingKind.Frightened, 0).Strength);
        Assert.Equal(1, new ExpressedFeeling(FeelingKind.Fond, 1).Strength);
    }

    // ---- his own words, read by the subconscious ------------------------------------------

    [Fact]
    public void WhatHeExpresses_IsKeptAsHisOwnWords_WithWhateverFeelingTheyExpress()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "carrots", "Found carrots.", Tags(BunDexRole.ShortTerm));
        var reader = new ScriptedReader(words => words.Contains("!!", StringComparison.Ordinal) ? Delighted : null);
        var keeper = new BunDexKeeper(store, new Recorder(), reader: reader);

        keeper.Expressed(page, "dese carrots is so sweet!!");
        keeper.Expressed(page, "da barn is red");

        Assert.Equal(["dese carrots is so sweet!!", "da barn is red"], reader.Read);
        Assert.True(store.FondnessOf(page) > 0);
        Assert.Throws<ArgumentException>(() => keeper.Expressed(page, " "));
        Assert.Throws<BunDexException>(() => keeper.Expressed(Guid.NewGuid(), "so sweet!!"));
    }

    [Fact]
    public void TheStandInReader_ReadsNothing_SoNoFeelingIsEverMadeUp()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "carrots", "Found carrots.", Tags(BunDexRole.ShortTerm));

        new BunDexKeeper(store, new Recorder()).Expressed(page, "dese carrots is so sweet!! i love dem!!");

        Assert.Equal(0, store.FondnessOf(page));
        Assert.Null(ReadsNothing.Instance.Read("i love dem!!"));
    }

    [Fact]
    public async Task WhatHeWrites_IsHisOwnExpressionToo()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var reader = new ScriptedReader(_ => Delighted);
        var writer = new StandInWriter();

        var outcome = await new BunDexKeeper(store, writer, reader: reader).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Sunny Acres"), Saw("a sweet carrot"));
        var afterWriting = store.FondnessOf(outcome.PageId!.Value);
        Assert.True(afterWriting > 0);
        var added = await new BunDexKeeper(store, new AddsTo("Sunny Acres stuffs", outcome.PageId!.Value, store), reader: reader).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Sunny Acres"), Saw("anuver carrot"));

        Assert.Equal(KeepOutcomeKind.Written, added.Kind);
        Assert.Equal(2, reader.Read.Count);
        Assert.Equal(store.PageText(outcome.PageId!.Value), string.Concat(reader.Read));
        Assert.True(store.FondnessOf(outcome.PageId!.Value) > afterWriting);
    }

    [Fact]
    public void ExpressingSomethingAboutIt_BringsItBackToHim_LikeRemembering()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "moon lantern", "moon lantern", Tags(BunDexRole.ShortTerm));
        for (var session = 0; session < 3; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }

        Assert.Equal(Clarity.Faded, store.ClarityOf(page));
        store.Experience(page, "moon lantern", null);
        Assert.Equal(Clarity.Clear, store.ClarityOf(page));
    }

    [Fact]
    public void LookingItUp_WithSomethingNew_IsAnExperienceOfIt()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "moon lantern", "Da moon lantern.", Tags(BunDexRole.ShortTerm));
        var before = store.RichnessOf(page);

        Assert.NotNull(store.LookFor("moon lantern glowing").Found);

        Assert.True(store.RichnessOf(page) - before > 0.9, "Something new he looked for with it brings almost a whole new thing.");
    }

    // ---- "Remember this, Prince" ------------------------------------------------------------

    [Fact]
    public async Task RememberThisPrince_ReachesHimAsBossWords_AndChangesNothingUnderneath()
    {
        async Task<(string Request, string Ledger, double Richness, double Care)> Moment(bool asked)
        {
            using var harness = new BunDexHarness();
            var clock = new HandClock(T0);
            var store = BunDexStore.Open(harness.Location, out _, clock);
            var patch = store.CreatePage(null, "carrot patch", "Da carrot patch by da path.", Tags(BunDexRole.ShortTerm, where: "Sunny Acres"));
            var writer = new Recorder();
            await new BunDexKeeper(store, writer).KeepAsync(
                BunDexRole.ShortTerm,
                Tags(BunDexRole.ShortTerm, where: "Sunny Acres"),
                Saw("a carrot patch"),
                heard: asked ? Membrane.BossSaid("Remember this, Prince!") : null);
            var kinds = string.Join(',', BunDexLedger.Open(harness.Location.LedgerRoot).Entries.Select(entry => entry.Kind));
            return (writer.Moments[0].ToRequest(store.View()).Text, kinds, store.RichnessOf(patch), store.CareOf(patch));
        }

        var plain = await Moment(asked: false);
        var asked = await Moment(asked: true);

        Assert.Contains("Boss said:", asked.Request, StringComparison.Ordinal);
        Assert.Contains("Remember this, Prince!", asked.Request, StringComparison.Ordinal);
        Assert.DoesNotContain("Boss said:", plain.Request, StringComparison.Ordinal);
        Assert.Equal(plain.Ledger, asked.Ledger);
        Assert.Equal(plain.Richness, asked.Richness);
        Assert.Equal(plain.Care, asked.Care);
    }

    [Fact]
    public async Task NothingHeIsGiven_NamesWhatMattersToHim()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var patch = store.CreatePage(null, "carrot patch", "Da carrot patch by da path.", Tags(BunDexRole.ShortTerm, where: "Sunny Acres"));
        Cherish(store, patch);
        var writer = new Recorder();

        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Sunny Acres"), Saw("a carrot patch"), heard: Membrane.BossSaid("Remember this, Prince!"));

        var request = writer.Moments[0].ToRequest(store.View()).Text;
        Assert.Contains("carrot patch", request, StringComparison.Ordinal);
        foreach (var hidden in new[] { "cherish", "fond", "significan", "important", "matters", "strength", "tier", "treasur", "rich", "feeling", "memory:", "sightings:" })
        {
            Assert.DoesNotContain(hidden, request, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- helpers ---------------------------------------------------------------------------

    private sealed class ScriptedReader(Func<string, ExpressedFeeling?> read) : IExpressionReader
    {
        public List<string> Read { get; } = [];

        ExpressedFeeling? IExpressionReader.Read(string hisWords)
        {
            Read.Add(hisWords);
            return read(hisWords);
        }
    }

    private sealed class AddsTo(string folder, Guid page, BunDexStore store) : IBunDexWriter
    {
        public ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new KeepResponse(KeepChoice.Write, [folder], store.FindPages(_ => true).Single(indexed => indexed.PageId == page).RelativePath.Split('/', '\\')[^1].Replace(".md", string.Empty, StringComparison.Ordinal), AddToExistingPage: true, Text: " anuver one!!"));

        public ValueTask<TidyResponse> TidyAsync(TidyMoment moment, BunDexView notebook, CancellationToken cancellationToken) =>
            ValueTask.FromResult(TidyResponse.NotNow);
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
