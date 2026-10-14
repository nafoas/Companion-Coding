using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using CompanionCore.Api;
using CompanionCore.Curtain;
using static CompanionCore.Sightings.Tests.SightingHarness;

namespace CompanionCore.Sightings.Tests;

/// <summary>
/// M4e (Boss, 2026-10-14): repeats are part of a pattern, not new salient memories; they still
/// accumulate, and a pattern is offered only when it grows into a new magnitude ("saws MANYS trees!!").
/// </summary>
public sealed class PatternTests
{
    [Fact]
    public void APattern_IsOfferedOncePerNewMagnitude_NeverPerSighting_AndEverySightingStillCounts()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        var grownAt = new List<int>();

        for (var count = 1; count <= 40; count++)
        {
            var (_, grown) = store.SeeAndNotice(Seen("tree", T0.AddMinutes(count), kind: ObservationKind.Other, words: "a tall tree"));
            if (grown is not null)
            {
                grownAt.Add(count);
                Assert.Equal(count, grown.Count);
            }
        }

        Assert.Equal([3, 10, 30], grownAt);
        Assert.Equal(40, store.Count);
        Assert.Equal(40, store.ImpressionOf("Hollow House", "tree")!.Count);
        Assert.Equal(40, harness.Open().ImpressionOf("Hollow House", "tree")!.Count);
    }

    [Fact]
    public void RememberingProposals_GrowPatternsToo()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        var grown = Enumerable.Range(1, 10)
            .Select(count => store.RecordAndNotice(Seen("zombie", T0.AddMinutes(count), words: "a zombie shambled") with { Source = SightingSource.Proposal }))
            .Select((impression, index) => (impression, count: index + 1))
            .Where(item => item.impression is not null)
            .Select(item => item.count);

        Assert.Equal([3, 10], grown);
    }

    [Fact]
    public void AFightIsNeverAPattern()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        for (var count = 1; count <= 5; count++)
        {
            Assert.Null(store.RecordAndNotice(new Sighting(T0.AddMinutes(count), "Hollow House", null, "zombie", ObservationKind.Creature, false, "zombie", SightingSource.Encounter, Outcome: EncounterOutcome.Ongoing)));
        }

        Assert.Equal(0, store.TimesSeen("zombie"));
    }

    [Fact]
    public void TheMagnitude_CrossesAsAFeelingOfHowMany_NeverANumber()
    {
        var lines = new[] { 1, 2, 3, 10, 30 }
            .Select(times => ImpressionSense.Of(new Impression("g", "tree", times, T0, T0, "a tree"))!)
            .ToList();

        Assert.Equal([SenseKind.Saw, SenseKind.SawAgain, SenseKind.SawAFewTimes, SenseKind.SawLotsOfTimes, SenseKind.SawSoManyTimes], lines.Select(sense => sense.Kind));
        Assert.Equal("Prince has seen this so, so many times: a tree.", lines[^1].Line);
        Assert.All(lines, sense => Assert.DoesNotMatch(@"\d", sense.Line));
        Assert.Equal(SightingStore.Magnitudes, new[] { 3, 10, 30 });
    }

    [Fact]
    public void WhatHeSaw_TeachesHowFamiliarEachWordIs()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        Assert.Equal(0, store.TimesSeen("tree"));

        for (var count = 1; count <= 7; count++)
        {
            store.See(Seen("tree", T0.AddMinutes(count), kind: ObservationKind.Other, words: "a tall Tree"));
        }

        Assert.Equal(7, store.TimesSeen("tree"));
        Assert.Equal(7, store.TimesSeen("TALL"));
        Assert.Equal(7, harness.Open().TimesSeen("tree"));
        Assert.Throws<ArgumentNullException>(() => store.TimesSeen(null!));
    }

    [Fact]
    public void NeverRandom_TheSightingRecordHasNoRandomnessAnywhere()
    {
        using var stream = File.OpenRead(typeof(SightingStore).Assembly.Location);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        var types = metadata.TypeReferences.Select(handle => metadata.GetString(metadata.GetTypeReference(handle).Name)).ToList();

        Assert.NotEmpty(types);
        Assert.DoesNotContain(types, name => name.Contains("Random", StringComparison.OrdinalIgnoreCase));
    }
}
