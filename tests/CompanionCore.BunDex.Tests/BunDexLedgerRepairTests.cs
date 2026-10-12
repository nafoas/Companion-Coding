using System.Text;
using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>M1: Bnuy Repairs for the ledger never discards the live ledger and never trusts a bad Vault copy.</summary>
public sealed class BunDexLedgerRepairTests
{
    [Fact]
    public void NothingAnywhere_IsNone()
    {
        using var harness = new BunDexHarness();
        Assert.Equal(BunDexLedgerRestore.None, BunDexLedgerRepair.Restore(harness.Location, []).Outcome);
    }

    [Fact]
    public void ABadVaultCopy_IsUnusable_AndTheLiveLedgerIsUntouched()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "one", "first", Tags(BunDexRole.ShortTerm));
        var snapshot = BunDexLedgerRepair.SnapshotSegments(harness.Location);
        var live = Bytes(harness.LastSegment());

        var tampered = snapshot.Select(segment => (segment.SegmentName, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(segment.Bytes).Replace("first", "FIRST", StringComparison.Ordinal)))).ToList();
        Assert.Equal(BunDexLedgerRestore.VaultUnusable, BunDexLedgerRepair.Restore(harness.Location, tampered).Outcome);
        Assert.Equal(BunDexLedgerRestore.VaultUnusable, BunDexLedgerRepair.Restore(harness.Location, [("ledger-v1-000001.jsonl", "not json\n"u8.ToArray())]).Outcome);
        Assert.Equal(live, Bytes(harness.LastSegment()));
        Assert.False(Directory.Exists(harness.Location.DamagedLedgerPath));
    }

    [Fact]
    public void TheSnapshot_HoldsCompleteLinesOnly()
    {
        using var harness = new BunDexHarness();
        harness.Open().CreatePage(null, "one", "first", Tags(BunDexRole.ShortTerm));
        File.AppendAllText(harness.LastSegment(), "{\"seq\":2,\"torn");

        var snapshot = Assert.Single(BunDexLedgerRepair.SnapshotSegments(harness.Location));

        Assert.Equal((byte)'\n', snapshot.Bytes[^1]);
        Assert.DoesNotContain("torn", Encoding.UTF8.GetString(snapshot.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void ALedgerThatDoesNotHoldTheVaultsHistory_IsRebuilt_AndTheOldOneIsKeptAside()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "one", "first", Tags(BunDexRole.ShortTerm));
        store.CreatePage(null, "two", "second", Tags(BunDexRole.ShortTerm));
        var snapshot = BunDexLedgerRepair.SnapshotSegments(harness.Location);

        // A different, longer, healthy live history: the Vault's chain is not contained in it.
        Directory.Delete(harness.Location.LedgerRoot, recursive: true);
        Directory.Delete(harness.Location.BunDexRoot, recursive: true);
        var other = harness.Open();
        for (var index = 0; index < 3; index++)
        {
            other.CreatePage(null, $"other {index}", "other", Tags(BunDexRole.ShortTerm));
        }

        var report = BunDexLedgerRepair.Restore(harness.Location, snapshot);

        Assert.Equal(BunDexLedgerRestore.Restored, report.Outcome);
        Assert.Equal(2, report.EntriesAfterRestore);
        Assert.True(Directory.Exists(report.PreservedDirectory));
        Assert.Equal("first", harness.Open().PageText(page));
    }
}
