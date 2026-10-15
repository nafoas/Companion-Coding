using CompanionCore.Api;
using CompanionCore.BunDex;
using CompanionCore.Curtain;
using CompanionCore.Sightings;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>M4e end to end: "saws MANYS trees!!", never "saw a tree, saw a tree, saw a tree".</summary>
public sealed class OrchestrationPatternTests
{
    [Fact]
    public async Task ThirtyTrees_AreKeptAsTheyGrow_AndThePageKeepsTheMagnitude()
    {
        await using var harness = await CreateAsync();
        var writer = new StandInWriter();
        var keeper = new BunDexKeeper(harness.Host.BunDex, writer);
        var tags = new PageTags(BunDexRole.ShortTerm, MemoryLayer.Unspecified, "synthetic-game", null, [], harness.Time.GetUtcNow(), "Sunny Acres");

        for (var tree = 1; tree <= 30; tree++)
        {
            var (_, grown) = harness.Host.Sightings.SeeAndNotice(new Sighting(harness.Time.GetUtcNow().AddMinutes(tree), "synthetic-game", null, "tree", ObservationKind.Other, false, "a tall tree", SightingSource.Observation));
            if (grown is not null)
            {
                Assert.Equal(KeepOutcomeKind.Written, (await keeper.KeepAsync(BunDexRole.ShortTerm, tags, ImpressionSense.Of(grown)!)).Kind);
            }
        }

        // Three moments to keep, not thirty: every tree still counted.
        Assert.Equal([SenseKind.SawAFewTimes, SenseKind.SawLotsOfTimes, SenseKind.SawSoManyTimes], writer.Moments.Select(moment => moment.WhatHappened.Kind));
        Assert.Equal(30, harness.Host.Sightings.ImpressionOf("synthetic-game", "tree")!.Count);
        var pages = Directory.EnumerateFiles(harness.Host.BunDex.Location.BunDexRoot, "*.md", SearchOption.AllDirectories).Select(File.ReadAllText).ToList();
        Assert.Equal(3, pages.Count);
        Assert.Contains(pages, text => text.Contains("so, so many times: a tall tree", StringComparison.Ordinal));
        Assert.All(pages, text => Assert.DoesNotMatch(@"\d", text));
    }

    [Fact]
    public async Task WhatHisEyesSee_MakesWordsFamiliar_InHisRemembering()
    {
        await using var harness = await CreateAsync();
        Assert.Equal(1.0, harness.Host.BunDex.Weight("tree"));

        for (var tree = 1; tree <= 30; tree++)
        {
            harness.Host.Sightings.See(new Sighting(harness.Time.GetUtcNow().AddMinutes(tree), "synthetic-game", null, "tree", ObservationKind.Other, false, "a tall tree", SightingSource.Observation));
        }

        // M5b leans curious (Boss-approved): after thirty trees, "tree" still weighs a quarter.
        Assert.Equal(0.25, harness.Host.BunDex.Weight("tree"), 6);
        Assert.Equal(1.0, harness.Host.BunDex.Weight("moon"));
    }
}
