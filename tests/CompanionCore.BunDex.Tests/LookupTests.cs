using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using CompanionCore.Curtain;
using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>
/// M4b, the Bnuy lookup (Boss, 2026-10-13): "okay... now how find dis...?" Hunches in his own
/// place names, an organic search from his own cues, never wrong on purpose, truthful finding.
/// </summary>
public sealed class LookupTests
{
    [Fact]
    public void FiledWhereItBelongs_IsFoundOnTheFirstHunch()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var scary = store.CreateFolder("Scary Stuff", about: "spooky things");
        store.CreateFolder("Pumpkin Days");
        store.CreatePage(scary, "dungeon sign", "Da dungeon sign was purple wif a moon.", Tags(BunDexRole.LongTerm));

        var rummage = store.LookFor("the purple dungeon sign");

        Assert.Equal([LookStepKind.StartsLooking, LookStepKind.Hunch, LookStepKind.Found], rummage.Steps.Select(step => step.Kind));
        Assert.Equal("Scary Stuff", rummage.Steps[1].Place);
        Assert.Equal("dungeon sign", rummage.Found!.Page);
        Assert.Equal(["Scary Stuff"], rummage.Found.Place);
        Assert.True(rummage.Sure);
    }

    [Fact]
    public void FiledSomewhereUnexpected_TheRightFeelingPlaceIsCheckedFirst_ThenTheNext()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var dragons = store.CreateFolder("Dragon Stuff");
        var snacks = store.CreateFolder("Snacks");
        store.CreatePage(dragons, "dragon fight", "A big dragon fight in da cave.", Tags(BunDexRole.LongTerm));
        store.CreatePage(snacks, "egg thing", "Found a shiny dragon egg near da cave.", Tags(BunDexRole.LongTerm));

        var rummage = store.LookFor("the shiny dragon egg");

        Assert.Equal(
            [LookStepKind.StartsLooking, LookStepKind.Hunch, LookStepKind.ADifferentOne, LookStepKind.NotThere, LookStepKind.Hunch, LookStepKind.Found],
            rummage.Steps.Select(step => step.Kind));
        Assert.Equal("Dragon Stuff", rummage.Steps[1].Place);
        Assert.Equal("dragon fight", rummage.Steps[2].OtherPage);
        Assert.Equal("Snacks", rummage.Steps[4].Place);
        Assert.Equal("egg thing", rummage.Found!.Page);
    }

    [Fact]
    public void ANestedPage_IsFound_ByWalkingHisOwnPath_OneStepAtATime()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var scawy = store.CreateFolder("Scawy Stuffs");
        var hollow = store.CreateFolder("Hollow House", scawy);
        var sunny = store.CreateFolder("Sunny Acres");
        store.CreatePage(sunny, "pumpkins", "Big pumpkins.", Tags(BunDexRole.LongTerm));
        store.CreatePage(hollow, "clock man", "A clock man built da house and never stopped.", Tags(BunDexRole.LongTerm));

        var rummage = store.LookFor("the clock man");

        Assert.Equal([LookStepKind.StartsLooking, LookStepKind.Hunch, LookStepKind.Hunch, LookStepKind.Found], rummage.Steps.Select(step => step.Kind));
        Assert.Equal(["Scawy Stuffs", "Hollow House"], rummage.Steps.Where(step => step.Kind == LookStepKind.Hunch).Select(step => step.Place));
        Assert.Equal(["Scawy Stuffs", "Hollow House"], rummage.Found!.Place);
    }

    [Fact]
    public void BackingOutOfAPath_GoesToTheNextLikelyPlace_NotDeeperIntoNothing()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var snacks = store.CreateFolder("Snacks");
        store.CreatePage(snacks, "lunch", "Da clock man ate a sandwich.", Tags(BunDexRole.LongTerm));
        clock.Advance(TimeSpan.FromMinutes(1));
        var scawy = store.CreateFolder("Scawy Stuffs");
        var hollow = store.CreateFolder("Hollow House", scawy);
        store.CreatePage(hollow, "clock thing", "A clock.", Tags(BunDexRole.LongTerm));
        clock.Advance(TimeSpan.FromMinutes(1));
        store.CreateFolder("Attic", scawy);

        var rummage = store.LookFor("the clock sandwich");

        Assert.Equal(
            [LookStepKind.StartsLooking, LookStepKind.Hunch, LookStepKind.Hunch, LookStepKind.ADifferentOne, LookStepKind.NotThere, LookStepKind.Hunch, LookStepKind.Found],
            rummage.Steps.Select(step => step.Kind));
        Assert.Equal(["Scawy Stuffs", "Hollow House", "Snacks"], rummage.Steps.Where(step => step.Kind == LookStepKind.Hunch).Select(step => step.Place));
    }

    [Fact]
    public void WithNoCueAtAll_WhereHeWroteLately_ComesToMindFirst()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var older = store.CreateFolder("Aaa");
        store.CreatePage(older, "pebble", "A pebble.", Tags(BunDexRole.LongTerm));
        clock.Advance(TimeSpan.FromMinutes(1));
        var newer = store.CreateFolder("Zzz");
        store.CreatePage(newer, "rang loud", "Da tower bell rang.", Tags(BunDexRole.LongTerm));
        clock.Advance(TimeSpan.FromMinutes(1));
        store.CreateFolder("Empty Box");

        var rummage = store.LookFor("the tower bell");

        Assert.Equal([LookStepKind.StartsLooking, LookStepKind.Hunch, LookStepKind.Found], rummage.Steps.Select(step => step.Kind));
        Assert.Equal("Zzz", rummage.Steps[1].Place);
    }

    [Fact]
    public void WhereHeWroteLately_IsTheVeryPlaceHeWrote_NotTheBigPlaceAroundIt()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var zzz = store.CreateFolder("Zzz");
        store.CreatePage(zzz, "rang loud", "Da tower bell rang.", Tags(BunDexRole.LongTerm));
        clock.Advance(TimeSpan.FromMinutes(1));
        var inner = store.CreateFolder("Inner", store.CreateFolder("Box"));
        store.CreatePage(inner, "pebble", "A pebble.", Tags(BunDexRole.LongTerm));

        var rummage = store.LookFor("the tower bell");

        Assert.Equal(
            [LookStepKind.StartsLooking, LookStepKind.Hunch, LookStepKind.NotThere, LookStepKind.Hunch, LookStepKind.Found],
            rummage.Steps.Select(step => step.Kind));
        Assert.Equal(["Inner", "Zzz"], rummage.Steps.Where(step => step.Kind == LookStepKind.Hunch).Select(step => step.Place));
    }

    [Fact]
    public void APlacesOwnName_PullsMoreThanAPageNameInsideAnother()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var moon = store.CreateFolder("Moon");
        store.CreatePage(moon, "pebble", "Da moon key was under a pebble.", Tags(BunDexRole.LongTerm));
        clock.Advance(TimeSpan.FromMinutes(1));
        var things = store.CreateFolder("Things");
        store.CreatePage(things, "moon thing", "A moon thing.", Tags(BunDexRole.LongTerm));

        var rummage = store.LookFor("the moon key");

        Assert.Equal("Moon", rummage.Steps[1].Place);
        Assert.Equal("pebble", rummage.Found!.Page);
    }

    [Fact]
    public void WhereAPageHappened_IsOneOfHisCues()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var misc = store.CreateFolder("Misc");
        store.CreatePage(misc, "tick tock", "Da big clock in da hollow house.", Tags(BunDexRole.LongTerm, where: "Hollow House"));
        clock.Advance(TimeSpan.FromMinutes(1));
        var zed = store.CreateFolder("Zed");
        store.CreatePage(zed, "pebble", "A pebble.", Tags(BunDexRole.LongTerm));

        Assert.Equal("Misc", store.LookFor("the hollow house clock").Steps[1].Place);
    }

    [Fact]
    public void InOnePlace_TheBestFittingPage_IsTheOneHeFinds()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var scary = store.CreateFolder("Scary Stuff");
        store.CreatePage(scary, "sign", "Da dungeon sign was purple.", Tags(BunDexRole.LongTerm));
        clock.Advance(TimeSpan.FromMinutes(1));
        store.CreatePage(scary, "other sign", "Da dungeon sign.", Tags(BunDexRole.LongTerm));

        var rummage = store.LookFor("the purple dungeon sign");

        Assert.Equal("sign", rummage.Found!.Page);
        Assert.True(rummage.Sure);
    }

    [Fact]
    public void TheFrontOfHisBunDex_IsAPlaceToo()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreateFolder("Snacks");
        store.CreatePage(null, "moon key", "Da moon key opens da tower.", Tags(BunDexRole.LongTerm));

        var rummage = store.LookFor("the moon key");

        Assert.Equal([LookStepKind.StartsLooking, LookStepKind.Hunch, LookStepKind.Found], rummage.Steps.Select(step => step.Kind));
        Assert.Null(rummage.Steps[1].Place);
        Assert.Empty(rummage.Found!.Place);
    }

    [Fact]
    public void NeverWrittenDown_NoHunchComes_AndHeSaysSoHonestly()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        Assert.Equal([LookStepKind.StartsLooking, LookStepKind.NotWrittenDown], store.LookFor("a giant robot").Steps.Select(step => step.Kind));

        var dragons = store.CreateFolder("Dragon Stuff");
        store.CreatePage(dragons, "dragon fight", "A big dragon fight in da cave.", Tags(BunDexRole.LongTerm));
        var rummage = store.LookFor("a giant robot");

        Assert.Equal([LookStepKind.StartsLooking, LookStepKind.NotWrittenDown], rummage.Steps.Select(step => step.Kind));
        Assert.Null(rummage.Found);
    }

    [Fact]
    public void NothingToLookFor_HeCannotFindIt_WithoutClaimingItWasNeverWritten()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "moon key", "Da moon key opens da tower.", Tags(BunDexRole.LongTerm));

        Assert.Equal([LookStepKind.StartsLooking, LookStepKind.CantFind], store.LookFor("the a").Steps.Select(step => step.Kind));
    }

    [Fact]
    public void Bounded_AfterAFewPlaces_HeGivesUpGently_EvenThoughItExists()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var misc = store.CreateFolder("Misc");
        store.CreatePage(misc, "rang loud", "Da clock tower bell rang.", Tags(BunDexRole.LongTerm));
        foreach (var letter in "ABCDEF")
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            var room = store.CreateFolder($"Clock Room {letter}");
            store.CreatePage(room, "clock thing", "A clock.", Tags(BunDexRole.LongTerm));
        }

        var rummage = store.LookFor("the clock tower bell");

        Assert.Equal(5, rummage.Steps.Count(step => step.Kind == LookStepKind.Hunch));
        Assert.DoesNotContain(rummage.Steps, step => step.Place == "Misc");
        Assert.Equal(LookStepKind.CantFind, rummage.Steps[^1].Kind);
        Assert.Null(rummage.Found);
    }

    [Fact]
    public void Truthful_ACloseFit_IsOnlyWhatHeThinksItWas()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var scary = store.CreateFolder("Scary Stuff");
        store.CreatePage(scary, "dungeon sign", "Da dungeon sign was purple wif a moon.", Tags(BunDexRole.LongTerm));

        var rummage = store.LookFor("purple dungeon sign gate");

        Assert.Equal(LookStepKind.ThinksItWas, rummage.Steps[^1].Kind);
        Assert.False(rummage.Sure);
        Assert.Equal("dungeon sign", rummage.Found!.Page);
    }

    [Fact]
    public void Truthful_ASimilarButDifferentPage_IsNoticed_NeverPresented()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var castles = store.CreateFolder("Castles");
        store.CreatePage(castles, "red castle", "Da red castle had a moat.", Tags(BunDexRole.LongTerm));

        var rummage = store.LookFor("the blue castle tower");

        Assert.DoesNotContain(rummage.Steps, step => step.Kind is LookStepKind.Found or LookStepKind.ThinksItWas);
        Assert.Null(rummage.Found);
        Assert.Equal(LookStepKind.NotWrittenDown, rummage.Steps[^1].Kind);
    }

    [Fact]
    public void Fading_MakesAPagesCuePullMoreWeakly()
    {
        LookStep FirstHunch(int sessions)
        {
            using var harness = new BunDexHarness();
            var clock = new HandClock(T0);
            var store = BunDexStore.Open(harness.Location, out _, clock);
            var old = store.CreateFolder("Old Things");
            store.CreatePage(old, "moon lantern", "Da moon lantern glows in da tower.", Tags(BunDexRole.ShortTerm));
            clock.Advance(TimeSpan.FromMinutes(1));
            var shiny = store.CreateFolder("Shiny Stuff", about: "moon things");
            store.CreatePage(shiny, "rock", "A pretty rock.", Tags(BunDexRole.LongTerm));
            for (var session = 0; session < sessions; session++)
            {
                clock.Advance(TimeSpan.FromDays(7));
                store.BeginSession();
            }

            var rummage = store.LookFor("the moon lantern");
            Assert.Equal("moon lantern", rummage.Found!.Page);
            if (rummage.Steps[1].Place == "Shiny Stuff")
            {
                // Nothing on his rock page is like it, so he just moves on.
                Assert.Equal([LookStepKind.StartsLooking, LookStepKind.Hunch, LookStepKind.NotThere, LookStepKind.Hunch, LookStepKind.Found], rummage.Steps.Select(step => step.Kind));
            }

            return rummage.Steps[1];
        }

        // Fresh, its name pulls strongest.
        Assert.Equal("Old Things", FirstHunch(0).Place);

        // Ringing a bell, it pulls only as much as his line about the other place: the place he wrote in more lately wins.
        Assert.Equal("Shiny Stuff", FirstHunch(2).Place);

        // Faded, it pulls more weakly still.
        Assert.Equal("Shiny Stuff", FirstHunch(10).Place);
    }

    [Fact]
    public void FindingRefreshesTheMemory_AndTheRefreshSurvivesAReopen()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "moon key", "Da moon key opens da tower.", Tags(BunDexRole.ShortTerm));
        for (var session = 0; session < 10; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }

        Assert.Equal(Clarity.Faded, store.ClarityOf(page));

        Assert.NotNull(store.LookFor("the moon key").Found);

        Assert.Equal(Clarity.Clear, store.ClarityOf(page));
        Assert.Equal(Clarity.Clear, BunDexStore.Open(harness.Location, out _, clock).ClarityOf(page));
    }

    [Fact]
    public void Deterministic_TheSameNotebookAndQuestion_GiveTheSameRummage()
    {
        string[] Rummage()
        {
            using var harness = new BunDexHarness();
            var clock = new HandClock(T0);
            var store = BunDexStore.Open(harness.Location, out _, clock);
            var dragons = store.CreateFolder("Dragon Stuff");
            clock.Advance(TimeSpan.FromMinutes(1));
            var snacks = store.CreateFolder("Snacks");
            store.CreatePage(dragons, "dragon fight", "A big dragon fight in da cave.", Tags(BunDexRole.LongTerm));
            clock.Advance(TimeSpan.FromMinutes(1));
            store.CreatePage(snacks, "egg thing", "Found a shiny dragon egg near da cave.", Tags(BunDexRole.LongTerm));
            return [.. store.LookFor("the shiny dragon egg").Steps.Select(step => step.Line)];
        }

        Assert.Equal(Rummage(), Rummage());
    }

    [Fact]
    public void NeverWrongOnPurpose_TheBunDexHasNoRandomnessAnywhere()
    {
        using var stream = File.OpenRead(typeof(BunDexStore).Assembly.Location);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        var types = metadata.TypeReferences.Select(handle => metadata.GetString(metadata.GetTypeReference(handle).Name)).ToList();

        Assert.Contains("Guid", types);
        Assert.DoesNotContain(types, name => name.Contains("Random", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ARingingBell_LeadsToALittleLookup_ToRememberMore()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var hollow = store.CreateFolder("Hollow House");
        store.CreatePage(hollow, "clock man", "A clock man built da house and never stopped.", Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        for (var session = 0; session < 2; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }

        // A newer page in the same place: he still finds the one that rang the bell.
        store.CreatePage(hollow, "door", "Da door creaks.", Tags(BunDexRole.LongTerm, where: "Hollow House"));
        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("the clock man's house has a moon keyhole"));

        var moment = writer.Moments[0];
        Assert.Equal(["Something about this rings a bell."], moment.Feelings.Select(feeling => feeling.Line));
        Assert.Equal([LookStepKind.WantsToRememberMore, LookStepKind.Hunch, LookStepKind.Found], moment.Rummage.Select(step => step.Kind));
        Assert.Equal("clock man", moment.Rummage[^1].Page!.Page);
        var request = moment.ToRequest(store.View());
        Assert.Empty(request.FindLeaks());
        Assert.Contains("Prince wants to remember more, so he starts looking it up.", request.Text, StringComparison.Ordinal);
        Assert.Contains("Prince finds it!", request.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhereHeIs_IsACueForTheBellsLookup()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var spooky = store.CreateFolder("Spooky");
        store.CreatePage(spooky, "tick", "Da clock man.", Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        for (var session = 0; session < 2; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }

        var zed = store.CreateFolder("Zed");
        store.CreatePage(zed, "pebble", "A pebble.", Tags(BunDexRole.LongTerm));
        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("a clock"));

        Assert.Equal([LookStepKind.WantsToRememberMore, LookStepKind.Hunch, LookStepKind.Found], writer.Moments[0].Rummage.Select(step => step.Kind));
        Assert.Equal("Spooky", writer.Moments[0].Rummage[1].Place);
    }

    [Fact]
    public async Task NoBell_NoRummage()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "clock man", "A clock man built da house.", Tags(BunDexRole.LongTerm, where: "Hollow House"));

        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("the clock man house"));

        Assert.Single(writer.Moments[0].ComesToMind);
        Assert.Empty(writer.Moments[0].Rummage);
    }

    [Fact]
    public void Looking_NeverEditsOrDeletesAnything()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var scary = store.CreateFolder("Scary Stuff");
        var page = store.CreatePage(scary, "dungeon sign", "Da dungeon sign was purple wif a moon.", Tags(BunDexRole.LongTerm));
        var pageBytes = File.ReadAllBytes(store.PagePath(page));
        var ledgerBefore = File.ReadAllBytes(harness.LastSegment());

        store.LookFor("the purple dungeon sign");
        store.LookFor("a giant robot");

        Assert.Equal(pageBytes, File.ReadAllBytes(store.PagePath(page)));
        Assert.Equal(ledgerBefore, File.ReadAllBytes(harness.LastSegment()).AsSpan(0, ledgerBefore.Length).ToArray());
        Assert.Equal(BunDexHealth.Healthy, harness.Open(out var report).Health);
        Assert.Empty(report.AlteredPages);
    }

    [Fact]
    public void TheCurtain_NothingButHisOwnNamesAndOurFixedWordsCross()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var dragons = store.CreateFolder("Dragon Stuff");
        var snacks = store.CreateFolder("Snacks");
        store.CreatePage(dragons, "dragon fight", "A big dragon fight in da cave.", Tags(BunDexRole.LongTerm));
        store.CreatePage(snacks, "egg thing", "Found a shiny dragon egg near da cave.", Tags(BunDexRole.LongTerm));

        var steps = store.LookFor("the shiny dragon egg").Steps.Concat(store.LookFor("a giant robot").Steps).Concat(store.LookFor("the").Steps).ToList();
        var request = BnuyRequest.Assemble(null, rummage: steps);

        Assert.Empty(request.FindLeaks());
        Assert.DoesNotMatch(@"\d", request.Text);
        Assert.All(request.Parts.Where(part => part.Kind == BnuyPartKind.Framing), part => Assert.Empty(CurtainRules.FindFramingLeaks(part.Text)));
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
