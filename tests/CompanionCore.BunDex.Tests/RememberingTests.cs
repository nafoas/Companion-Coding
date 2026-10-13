using CompanionCore.Curtain;
using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>
/// M4a, plan item 4 (Boss-approved values): remembering like a living Bun. Four layers, two clocks
/// (play sessions and days, whichever is longer), remembering refreshes, never clairvoyant,
/// never edited.
/// </summary>
public sealed class RememberingTests
{
    [Fact]
    public void AMonthAway_ForgetsNothing()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var shortPage = store.CreatePage(null, "rusty key", "Boss has a rusty key from da kitchen.", Tags(BunDexRole.ShortTerm));

        clock.Advance(TimeSpan.FromDays(30));

        Assert.Equal(Clarity.Clear, store.ClarityOf(shortPage));
    }

    [Theory]
    [InlineData(BunDexRole.ShortTerm, 1, Clarity.Clear)]
    [InlineData(BunDexRole.ShortTerm, 2, Clarity.RingsABell)]
    [InlineData(BunDexRole.ShortTerm, 3, Clarity.Faded)]
    [InlineData(BunDexRole.MediumTerm, 5, Clarity.Clear)]
    [InlineData(BunDexRole.MediumTerm, 6, Clarity.RingsABell)]
    [InlineData(BunDexRole.MediumTerm, 11, Clarity.Faded)]
    [InlineData(BunDexRole.LongTerm, 10, Clarity.Clear)]
    [InlineData(BunDexRole.LongTerm, 11, Clarity.RingsABell)]
    [InlineData(BunDexRole.LongTerm, 500, Clarity.RingsABell)]
    [InlineData(BunDexRole.Cherished, 1000, Clarity.Clear)]
    public void PlaySessionsFadeEachLayer_ByItsApprovedWindow(BunDexRole role, int sessions, Clarity expected)
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "page", "something happened", Tags(role));

        // Sessions a week apart: the day clock runs past every window, so the session clock is the longer one.
        Sessions(store, clock, sessions);

        Assert.Equal(expected, store.ClarityOf(page));
    }

    [Theory]
    [InlineData(BunDexRole.ShortTerm, 2, Clarity.Clear)]
    [InlineData(BunDexRole.ShortTerm, 3, Clarity.RingsABell)]
    [InlineData(BunDexRole.ShortTerm, 5, Clarity.Faded)]
    [InlineData(BunDexRole.MediumTerm, 21, Clarity.Clear)]
    [InlineData(BunDexRole.MediumTerm, 22, Clarity.RingsABell)]
    [InlineData(BunDexRole.MediumTerm, 43, Clarity.Faded)]
    [InlineData(BunDexRole.LongTerm, 60, Clarity.Clear)]
    [InlineData(BunDexRole.LongTerm, 61, Clarity.RingsABell)]
    public void WhicheverClockIsLonger_Wins(BunDexRole role, int days, Clarity expected)
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "page", "something happened", Tags(role));

        // Many sessions, spread over the days: now the day clock is the longer one.
        for (var day = 0; day < days; day++)
        {
            clock.Advance(TimeSpan.FromDays(1));
            store.BeginSession();
            store.BeginSession();
            store.BeginSession();
        }

        Assert.Equal(expected, store.ClarityOf(page));
    }

    [Theory]
    [InlineData(BunDexRole.ShortTerm, MemoryLayer.ShortTerm)]
    [InlineData(BunDexRole.ThingsThinkingAbout, MemoryLayer.ShortTerm)]
    [InlineData(BunDexRole.LittleRequests, MemoryLayer.ShortTerm)]
    [InlineData(BunDexRole.MediumTerm, MemoryLayer.MediumTerm)]
    [InlineData(BunDexRole.LongTerm, MemoryLayer.LongTerm)]
    [InlineData(BunDexRole.Adventures, MemoryLayer.LongTerm)]
    [InlineData(BunDexRole.Lore, MemoryLayer.LongTerm)]
    [InlineData(BunDexRole.Opinions, MemoryLayer.LongTerm)]
    [InlineData(BunDexRole.Relationship, MemoryLayer.LongTerm)]
    [InlineData(BunDexRole.ScaryOrCool, MemoryLayer.LongTerm)]
    [InlineData(BunDexRole.Cherished, MemoryLayer.Cherished)]
    public void APagesLayer_ComesFromTheKindOfThingItIs(BunDexRole role, MemoryLayer expected)
    {
        Assert.Equal(expected, PageClarity.LayerOf(Tags(role)));
    }

    [Fact]
    public void ATaggedLayer_IsKeptOverTheDefault()
    {
        Assert.Equal(MemoryLayer.Cherished, PageClarity.LayerOf(Tags(BunDexRole.ShortTerm) with { Layer = MemoryLayer.Cherished }));
    }

    [Fact]
    public void ASessionThatBeganAsThePageWasWritten_DoesNotCountAgainstIt()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        store.BeginSession();
        var page = store.CreatePage(null, "page", "something happened", Tags(BunDexRole.ShortTerm));

        Sessions(store, clock, 1);

        Assert.Equal(Clarity.Clear, store.ClarityOf(page));
    }

    [Fact]
    public void WritingMoreOnAPage_RefreshesIt()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "page", "something happened", Tags(BunDexRole.ShortTerm));
        Sessions(store, clock, 2);
        Assert.Equal(Clarity.RingsABell, store.ClarityOf(page));

        store.AddToPage(page, " And then more.");

        Assert.Equal(Clarity.Clear, store.ClarityOf(page));
    }

    [Fact]
    public async Task OneSharedWord_ElsewhereIsNotEnough_ToComeToMind()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "pumpkins", "Da pumpkin patch was huge.", Tags(BunDexRole.LongTerm, where: "Sunny Acres"));

        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("a pumpkin on the porch"));

        Assert.Empty(writer.Moments[0].ComesToMind);
    }

    [Fact]
    public async Task EquallyFittingPages_OnTheSameRung_TheNewerComesFirst()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        store.CreatePage(null, "older", "pumpkin contest win", Tags(BunDexRole.ShortTerm, where: "Sunny Acres"));
        clock.Advance(TimeSpan.FromMinutes(1));
        store.CreatePage(null, "newer", "pumpkin contest win", Tags(BunDexRole.ShortTerm, where: "Sunny Acres"));

        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Sunny Acres"), Saw("pumpkin contest win"));

        Assert.Equal(["newer", "older"], writer.Moments[0].ComesToMind.Select(memory => memory.Page));
    }

    [Fact]
    public void ADamagedLedger_StillLetsHimRemember_WithoutWritingAnything()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "clock man", "A clock man built da clock house.", Tags(BunDexRole.LongTerm, where: "Hollow House"));
        store.CreatePage(null, "two", "two", Tags(BunDexRole.ShortTerm));
        var lines = File.ReadAllLines(harness.LastSegment());
        lines[^1] = lines[^1].Replace("\"two\"", "\"tw0\"", StringComparison.Ordinal);
        File.WriteAllLines(harness.LastSegment(), lines);
        var ledgerBytes = File.ReadAllBytes(harness.LastSegment());

        var damaged = harness.Open(out _);
        var (clear, _) = damaged.ComeToMind("the clock house clock man", "Hollow House");

        Assert.Equal(BunDexHealth.Damaged, damaged.Health);
        Assert.Equal("clock man", Assert.Single(clear).Page);
        Assert.Equal(ledgerBytes, File.ReadAllBytes(harness.LastSegment()));
    }

    [Fact]
    public async Task RememberingRefreshesTheClock_AndTheRefreshSurvivesAReopen()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "clock man", "A clock man built da house and never stopped.", Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        Sessions(store, clock, 2);
        Assert.Equal(Clarity.RingsABell, store.ClarityOf(page));

        // It rings a bell...
        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("the clock man's house has a moon keyhole"));
        Assert.Equal(["Something about this rings a bell."], writer.Moments[0].Feelings.Select(feeling => feeling.Line));
        Assert.Empty(writer.Moments[0].ComesToMind);

        // ...and remembering it restarted its clock, which a reopen keeps.
        Assert.Equal(Clarity.Clear, store.ClarityOf(page));
        Assert.Equal(Clarity.Clear, BunDexStore.Open(harness.Location, out _, clock).ClarityOf(page));
    }

    [Fact]
    public async Task NotClairvoyant_AFadedPage_NeverComesToMindUnprompted_ButItIsStillThere()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "sign", "Da dungeon sign was purple wif a moon.", Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        Sessions(store, clock, 10);

        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("a purple dungeon sign with a moon"));

        Assert.Empty(writer.Moments[0].ComesToMind);
        Assert.Empty(writer.Moments[0].Feelings);
        Assert.Equal("Da dungeon sign was purple wif a moon.", store.PageText(page));
    }

    [Fact]
    public async Task CherishedMemories_StayClearForever()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        store.CreatePage(null, "first adventure", "Da very first adventure wif Boss. Da sky went pink.", Tags(BunDexRole.Cherished, where: "Sky Garden"));
        Sessions(store, clock, 300);
        clock.Advance(TimeSpan.FromDays(3 * 365));

        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Sky Garden"), Saw("the sky turned pink again"));

        Assert.Equal("first adventure", Assert.Single(writer.Moments[0].ComesToMind).Page);
    }

    [Fact]
    public async Task Sparing_APageThatJustCameToMind_RestsAWhile_AndAtMostOneBellRings()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        store.CreatePage(null, "clock man", "A clock man built da clock house.", Tags(BunDexRole.LongTerm, where: "Hollow House"));
        store.CreatePage(null, "old clock", "Da old clock house had a clock man.", Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        store.CreatePage(null, "older clock", "Da clock house clock man again.", Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        Sessions(store, clock, 2);
        var writer = new Recorder();
        var keeper = new BunDexKeeper(store, writer);

        await keeper.KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("the clock house clock man"));
        Assert.Single(writer.Moments[0].ComesToMind);
        Assert.Single(writer.Moments[0].Feelings);

        clock.Advance(TimeSpan.FromMinutes(5));
        await keeper.KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("the clock house clock man"));
        Assert.Empty(writer.Moments[1].ComesToMind);

        clock.Advance(BunDexStore.CameToMindCooldown);
        await keeper.KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("the clock house clock man"));
        Assert.NotEmpty(writer.Moments[2].ComesToMind);
    }

    [Fact]
    public async Task TheLadder_BreaksTies_CherishedFirst_EvenOverNewerPages()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);

        // Written oldest to newest in ladder order, so recency alone would pick them backwards.
        foreach (var (name, role) in new[] { ("cherished", BunDexRole.Cherished), ("long", BunDexRole.LongTerm), ("medium", BunDexRole.MediumTerm), ("short", BunDexRole.ShortTerm) })
        {
            store.CreatePage(null, name, "pumpkin contest win", Tags(role, where: "Sunny Acres"));
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        var writer = new Recorder();
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Sunny Acres"), Saw("pumpkin contest win"));

        Assert.Equal(["cherished", "long", "medium"], writer.Moments[0].ComesToMind.Select(memory => memory.Page));
    }

    [Fact]
    public async Task AgingAndRemembering_NeverEditOrDeleteAnything()
    {
        using var harness = new BunDexHarness();
        var clock = new HandClock(T0);
        var store = BunDexStore.Open(harness.Location, out _, clock);
        var page = store.CreatePage(null, "clock man", "A clock man built da house.", Tags(BunDexRole.ShortTerm, where: "Hollow House"));
        var pageBytes = File.ReadAllBytes(store.PagePath(page));
        var ledgerBefore = File.ReadAllBytes(harness.LastSegment());

        Sessions(store, clock, 2);
        await new BunDexKeeper(store, new Recorder()).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Hollow House"), Saw("the clock man house"));
        Sessions(store, clock, 50);

        Assert.Equal(pageBytes, File.ReadAllBytes(store.PagePath(page)));
        Assert.Equal(ledgerBefore, File.ReadAllBytes(harness.LastSegment()).AsSpan(0, ledgerBefore.Length).ToArray());
        Assert.Equal(BunDexHealth.Healthy, BunDexStore.Open(harness.Location, out var report, clock).Health);
        Assert.Empty(report.AlteredPages);
    }

    [Fact]
    public void ABellCrossesOnlyAsAFeeling_WithNoDetails()
    {
        var bell = Membrane.RingsABell();

        Assert.Equal("Something about this rings a bell.", bell.Line);
        Assert.Empty(CurtainRules.FindFramingLeaks(bell.Line));
    }

    /// <summary>Play sessions a week apart, so only the session clock can still be holding a page.</summary>
    private static void Sessions(BunDexStore store, HandClock clock, int count)
    {
        for (var session = 0; session < count; session++)
        {
            clock.Advance(TimeSpan.FromDays(7));
            store.BeginSession();
        }
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

/// <summary>A clock turned by hand, so days and sessions can be set exactly.</summary>
internal sealed class HandClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    internal void Advance(TimeSpan by) => _now += by;
}
