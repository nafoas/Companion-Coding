using CompanionCore.Curtain;
using static CompanionCore.Sightings.Tests.SightingHarness;

namespace CompanionCore.Sightings.Tests;

/// <summary>M3a, plan item 1: memories are novel, not a log. Seeing something is not the same as remembering it.</summary>
public sealed class NoveltyGateTests
{
    [Fact]
    public async Task AHundredSightingsOfTheSameThing_AreOneMemory_OneGrowingImpression_AndAHundredRecords()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        var memories = new Remembered();
        var gate = new SightingNoveltyGate(store);

        var committed = 0;
        for (var index = 0; index < 100; index++)
        {
            committed += await memories.SiftAsync(gate, Draft("baldi", $"Baldi is coming down the hall ({index % 7 + 1}/7 notebooks)", at: T0.AddSeconds(10 * index)));
        }

        Assert.Equal(1, committed);
        Assert.Single(memories.Memories);
        var impression = store.ImpressionOf("Hollow House", "baldi")!;
        Assert.Equal(100, impression.Count);
        Assert.Equal((T0, T0.AddSeconds(990)), (impression.First, impression.Last));
        Assert.Equal(100, store.Count);
        // M4e: a hundred is "so, so many": the magnitude is appreciated, never counted.
        Assert.Equal("Prince has seen this so, so many times: Baldi is coming down the hall (2/7 notebooks).", ImpressionSense.Of(impression)!.Line);

        // The record survives a reopen.
        var reopened = harness.Open(out var report);
        Assert.Equal(100, report.Sightings);
        Assert.Equal(100, reopened.ImpressionOf("Hollow House", "baldi")!.Count);
    }

    [Fact]
    public async Task OnlyNumbersChanging_IsARepeat_ButAMeaningfulChange_IsANewMemory()
    {
        using var harness = new SightingHarness();
        var memories = new Remembered();
        var gate = new SightingNoveltyGate(harness.Open());

        Assert.Equal(1, await memories.SiftAsync(gate, Draft("notebooks", "The notebook counter read 5/7.")));
        Assert.Equal(0, await memories.SiftAsync(gate, Draft("notebooks", "The notebook counter read 6/7.")));
        Assert.Equal(0, await memories.SiftAsync(gate, Draft("notebooks", "the NOTEBOOK counter read 7 / 7!")));
        Assert.Equal(1, await memories.SiftAsync(gate, Draft("notebooks", "Baldi yelled I HEAR EVERY DOOR YOU OPEN after the last notebook.")));
        Assert.Equal(1, await memories.SiftAsync(gate, Draft("soda", "Boss has a BSODA in the inventory.")));
        Assert.Equal(1, await memories.SiftAsync(gate, Draft("soda", "The BSODA is gone; Boss sprayed it at the principal.")));

        Assert.Equal(4, memories.Memories.Count);
    }

    [Theory]
    [InlineData("Coins: 120", "Coins: 135")]
    [InlineData("Level 3", "Level 4")]
    [InlineData("HP 87%", "HP 42%")]
    public async Task AStatusReadout_IsNeverANewMemory_WhenOnlyItsNumbersMove(string first, string second)
    {
        using var harness = new SightingHarness();
        var memories = new Remembered();
        var gate = new SightingNoveltyGate(harness.Open());

        Assert.Equal(1, await memories.SiftAsync(gate, Draft("status", first)));
        Assert.Equal(0, await memories.SiftAsync(gate, Draft("status", second)));
    }

    [Fact]
    public async Task ANearDuplicate_IsARepeat_ButAFewSharedWords_AreNot()
    {
        using var harness = new SightingHarness();
        var memories = new Remembered();
        var gate = new SightingNoveltyGate(harness.Open());

        Assert.Equal(1, await memories.SiftAsync(gate, Draft("rope", "Boss saved the jump rope.")));
        Assert.Equal(0, await memories.SiftAsync(gate, Draft("rope", "Boss saved the jump rope in the shed.")));
        Assert.Equal(1, await memories.SiftAsync(gate, Draft("rope", "Boss used the jump rope to trap the bully in the hallway.")));
    }

    [Fact]
    public async Task ABatchWithItsOwnRepeats_KeepsOnlyTheFirst_InOrder()
    {
        using var harness = new SightingHarness();
        var memories = new Remembered();
        var gate = new SightingNoveltyGate(harness.Open());

        var novel = await gate.SiftAsync(
            [Draft("rope", "Boss saved the jump rope."), Draft("rope", "Boss saved the jump rope again."), Draft("door", "A red door opened.")],
            memories,
            CancellationToken.None);

        Assert.Equal(["Boss saved the jump rope.", "A red door opened."], novel.Select(draft => draft.VisibleRecollection));
    }

    [Fact]
    public async Task TheSameWordsInAnotherGame_AreANewMemory()
    {
        using var harness = new SightingHarness();
        var memories = new Remembered();
        var gate = new SightingNoveltyGate(harness.Open());

        Assert.Equal(1, await memories.SiftAsync(gate, Draft("boss-fight", "The boss fight started.", game: "Sky Garden")));
        Assert.Equal(1, await memories.SiftAsync(gate, Draft("boss-fight", "The boss fight started.", game: "Hollow House")));
        Assert.Equal(0, await memories.SiftAsync(gate, Draft("boss-fight", "The boss fight started.", game: "Hollow House")));
    }

    [Fact]
    public async Task CommittedMemories_AreOnlyEverRead()
    {
        using var harness = new SightingHarness();
        var memories = new Remembered();
        var gate = new SightingNoveltyGate(harness.Open());
        await memories.SiftAsync(gate, Draft("door", "A red door opened."));
        var before = memories.Memories.Single();

        await memories.SiftAsync(gate, Draft("door", "A red door opened again."));

        Assert.Same(before, memories.Memories.Single());
        Assert.Equal("A red door opened.", before.VisibleRecollection);
    }

    [Theory]
    [InlineData(1, SenseKind.Saw, "Prince saw: a zombie.")]
    [InlineData(2, SenseKind.SawAgain, "Prince saw this again: a zombie.")]
    [InlineData(3, SenseKind.SawAFewTimes, "Prince saw this a few times: a zombie.")]
    [InlineData(9, SenseKind.SawAFewTimes, "Prince saw this a few times: a zombie.")]
    [InlineData(10, SenseKind.SawLotsOfTimes, "Prince has seen this lots of times: a zombie.")]
    [InlineData(29, SenseKind.SawLotsOfTimes, "Prince has seen this lots of times: a zombie.")]
    [InlineData(30, SenseKind.SawSoManyTimes, "Prince has seen this so, so many times: a zombie.")]
    [InlineData(500, SenseKind.SawSoManyTimes, "Prince has seen this so, so many times: a zombie.")]
    public void AnImpression_CrossesAsTheGist_NeverACount(int times, SenseKind kind, string line)
    {
        var sense = ImpressionSense.Of(new Impression("Hollow House", "zombie", times, T0, T0, "a zombie"))!;

        Assert.Equal((kind, line), (sense.Kind, sense.Line));
        Assert.DoesNotContain(times.ToString(System.Globalization.CultureInfo.InvariantCulture), sense.Line, StringComparison.Ordinal);
    }
}
