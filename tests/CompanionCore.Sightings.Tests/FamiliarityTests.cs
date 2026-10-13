using CompanionCore.Api;
using static CompanionCore.Sightings.Tests.SightingHarness;

namespace CompanionCore.Sightings.Tests;

/// <summary>M3a, plan item 12: getting used to things, the way a living Bun does.</summary>
public sealed class FamiliarityTests
{
    [Fact]
    public void InterestFalls_WithEveryRepeat_ButNeverToNothing()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();

        var novelties = Enumerable.Range(0, 50).Select(index => store.See(Seen("zombie", T0.AddSeconds(10 * index)))).ToList();

        Assert.Equal(Familiarity.Peak, novelties[0], 3);
        Assert.All(novelties.Zip(novelties.Skip(1)), pair => Assert.True(pair.Second < pair.First || pair.Second == Familiarity.Floor));
        Assert.True(novelties.Count(novelty => novelty > Familiarity.Floor) >= 10, "It fades gradually, not all at once.");
        Assert.True(novelties[^1] < 0.1);
        Assert.True(novelties[^1] >= Familiarity.Floor);
    }

    [Fact]
    public void ItFallsByKind_TooButMoreGently()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        for (var index = 0; index < 30; index++)
        {
            store.See(Seen($"zombie {index}", T0.AddSeconds(index), kind: ObservationKind.Creature));
        }

        var anotherCreature = store.NoveltyOf(Seen("skeleton", T0.AddMinutes(1), kind: ObservationKind.Creature));
        var somethingElse = store.NoveltyOf(Seen("golden key", T0.AddMinutes(1), kind: ObservationKind.Item));

        Assert.True(anotherCreature < somethingElse);
        Assert.True(anotherCreature >= Familiarity.Peak * 0.5);
        Assert.Equal(Familiarity.Peak, somethingElse, 3);
    }

    [Fact]
    public void ItRecovers_AfterALongGap()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        for (var index = 0; index < 40; index++)
        {
            store.See(Seen("zombie", T0.AddSeconds(10 * index)));
        }

        var soon = store.NoveltyOf(Seen("zombie", T0.AddMinutes(10)));
        var aWeek = store.NoveltyOf(Seen("zombie", T0.AddDays(7)));
        var aMonth = store.NoveltyOf(Seen("zombie", T0.AddDays(30)));

        Assert.True(soon < aWeek && aWeek < aMonth);
        Assert.True(aMonth > 0.8);
    }

    [Fact]
    public void ANewSituation_MakesAFamiliarThingInterestingAgain()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        for (var index = 0; index < 20; index++)
        {
            store.See(Seen("zombie", T0.AddSeconds(10 * index), situation: "forest"));
        }

        var sameSituation = store.NoveltyOf(Seen("zombie", T0.AddMinutes(4), situation: "forest"));
        var newSituation = store.NoveltyOf(Seen("zombie", T0.AddMinutes(4), situation: "village"));

        Assert.True(newSituation > sameSituation * 1.5);
    }

    [Fact]
    public void EveryGame_IsItsOwn()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        for (var index = 0; index < 40; index++)
        {
            store.See(Seen("zombie", T0.AddSeconds(index), game: "Blocky World"));
        }

        Assert.Equal(Familiarity.Peak, store.NoveltyOf(Seen("zombie", T0.AddMinutes(1), game: "Hollow House")), 3);
    }

    [Fact]
    public void Danger_NeverFadesIntoTheBackground()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        double last = 1;
        for (var index = 0; index < 200; index++)
        {
            last = store.See(Seen("zombie horde", T0.AddSeconds(index), danger: true));
        }

        Assert.True(last >= Familiarity.DangerFloor);
        Assert.True(store.NoveltyOf(Seen("zombie horde", T0.AddSeconds(300), danger: false)) < Familiarity.DangerFloor);
    }

    [Fact]
    public void Familiarity_SurvivesAReopen()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        for (var index = 0; index < 25; index++)
        {
            store.See(Seen("zombie", T0.AddSeconds(10 * index), situation: "forest"));
        }

        var probe = Seen("zombie", T0.AddMinutes(30), situation: "forest");
        Assert.Equal(store.NoveltyOf(probe), harness.Open().NoveltyOf(probe), 9);
    }
}
