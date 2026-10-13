using CompanionCore.Memory;
using static CompanionCore.Sightings.Tests.SightingHarness;

namespace CompanionCore.Sightings.Tests;

/// <summary>M3a, plan items 1 and 7a: the subconscious keeps a complete, factual record that survives anything.</summary>
public sealed class SightingRecordTests
{
    [Fact]
    public void TheRecord_LivesInTheBraincase_NeverProduction()
    {
        using var harness = new SightingHarness();

        Assert.Equal(Path.Combine(harness.MemoryLocation.BraincaseRootPath, "sightings-v1"), harness.Location.RootPath);
        var development = SightingLocation.For(new DevelopmentDataRootPolicy(harness.BasePath).Resolve());
        Assert.StartsWith(Path.Combine(harness.BasePath, DevelopmentDataRootPolicy.DevelopmentApplicationNamespace, "Braincase"), development.RootPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ATornTail_IsDropped_AndAnUnreadableLine_IsSkippedNotFatal()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        store.See(Seen("zombie", T0));
        store.See(Seen("zombie", T0.AddSeconds(1)));
        var segment = SightingStore.Segments(harness.Location.RootPath)[0].Path;
        var lines = File.ReadAllLines(segment);
        File.WriteAllText(segment, lines[0] + "\nnot json at all\n" + lines[1] + "\n{\"at\":\"2026-10-13T12:00:0");

        var reopened = harness.Open(out var report);

        Assert.Equal((2L, true, 1), (report.Sightings, report.TornTailDropped, report.UnreadableLines));
        Assert.Equal(2, reopened.ImpressionOf("Hollow House", "zombie")!.Count);
        reopened.See(Seen("zombie", T0.AddSeconds(2)));
        Assert.Equal(3, harness.Open().ImpressionOf("Hollow House", "zombie")!.Count);
    }

    [Fact]
    public void TheRecord_RollsIntoNewSegments_AndStaysComplete()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        var words = new string('w', SightingStore.MaximumWords);
        var count = (int)(SightingStore.MaximumSegmentBytes / 500) + 50;
        for (var index = 0; index < count; index++)
        {
            store.Record(Seen("thing " + index, T0.AddSeconds(index), words: words) with { Source = SightingSource.Proposal });
        }

        Assert.True(SightingStore.Segments(harness.Location.RootPath).Count >= 2);
        Assert.Equal(count, harness.Open(out var report).Count);
        Assert.Equal(0, report.UnreadableLines);
    }

    [Fact]
    public void LongWords_AreBounded()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        store.See(Seen("zombie", T0, words: new string('z', 5000)));

        Assert.Equal(SightingStore.MaximumWords, harness.Open().ImpressionOf("Hollow House", "zombie")!.LatestWords.Length);
    }

    [Fact]
    public void TheVault_BacksUpCompleteLines_AndRestoresOnlyWhenMissing()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        store.See(Seen("zombie", T0));
        File.AppendAllText(SightingStore.Segments(harness.Location.RootPath)[0].Path, "{\"torn");

        var snapshot = SightingVault.SnapshotSegments(harness.Location);
        Assert.Equal((byte)'\n', Assert.Single(snapshot).Bytes[^1]);

        // A live record is never overwritten.
        var live = File.ReadAllBytes(SightingStore.Segments(harness.Location.RootPath)[0].Path);
        Assert.Equal(SightingRestore.Kept, SightingVault.RestoreIfMissing(harness.Location, [("sightings-v1-000001.jsonl", "{}\n"u8.ToArray())]));
        Assert.Equal(live, File.ReadAllBytes(SightingStore.Segments(harness.Location.RootPath)[0].Path));

        // A missing record comes back from the Vault.
        Directory.Delete(harness.Location.RootPath, recursive: true);
        Assert.Equal(SightingRestore.Restored, SightingVault.RestoreIfMissing(harness.Location, [.. snapshot, ("../../escape.jsonl", "x\n"u8.ToArray())]));
        Assert.Equal(1, harness.Open().ImpressionOf("Hollow House", "zombie")!.Count);
        Assert.False(File.Exists(Path.Combine(harness.Location.RootPath, "..", "..", "escape.jsonl")));

        Directory.Delete(harness.Location.RootPath, recursive: true);
        Assert.Equal(SightingRestore.None, SightingVault.RestoreIfMissing(harness.Location, []));
    }
}
