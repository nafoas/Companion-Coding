using CompanionCore.BunDex;
using static CompanionCore.Vault.Tests.VaultHarness;

namespace CompanionCore.Vault.Tests;

/// <summary>M1: Da Bun Vault backs up the BrainCase's BunDex ledger, and Bnuy Repairs restores it without losing newer pages.</summary>
public sealed class BunDexVaultTests
{
    private static PageTags Tags() => new(BunDexRole.LongTerm, MemoryLayer.Unspecified, "synthetic.exploration", null, [], T0);

    [Fact]
    public async Task AHealthyLedger_IsKept_AndNewerPagesStay()
    {
        await using var harness = await CreateAsync();
        var location = BunDexLocation.For(harness.Location);
        var store = BunDexStore.Open(location, out _);
        var before = store.CreatePage(null, "before", "we found a key", Tags());
        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        var after = store.CreatePage(null, "after", "we opened da door", Tags());
        await harness.CloseAsync();

        var report = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);

        Assert.Equal(BunDexLedgerRestore.Kept, report.BunDex!.Outcome);
        Assert.Equal(1, report.BunDex.VaultEntries);
        Assert.Equal(2, report.BunDex.EntriesAfterRestore);
        Assert.Null(report.BunDex.PreservedDirectory);
        var reopened = BunDexStore.Open(location, out var open);
        Assert.Equal(BunDexHealth.Healthy, open.Health);
        Assert.Equal("we found a key", reopened.PageText(before));
        Assert.Equal("we opened da door", reopened.PageText(after));
    }

    [Fact]
    public async Task ALostLedgerAndBunDex_AreRebuiltFromTheVault()
    {
        await using var harness = await CreateAsync();
        var location = BunDexLocation.For(harness.Location);
        var store = BunDexStore.Open(location, out _);
        var folder = store.CreateFolder("Big Adventures!!");
        var page = store.CreatePage(folder, "castle", "we beat da castle boss", Tags());
        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        await harness.CloseAsync();
        Directory.Delete(location.LedgerRoot, recursive: true);
        Directory.Delete(location.BunDexRoot, recursive: true);

        var report = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);

        Assert.Equal(BunDexLedgerRestore.Restored, report.BunDex!.Outcome);
        Assert.Equal(2, report.BunDex.EntriesAfterRestore);
        Assert.True(report.Complete);
        var reopened = BunDexStore.Open(location, out var open);
        Assert.Equal(BunDexHealth.Healthy, open.Health);
        Assert.Equal("we beat da castle boss", reopened.PageText(page));
        Assert.Equal(Path.Combine(location.BunDexRoot, "Big Adventures!!", "castle.md"), reopened.PagePath(page));
    }

    [Fact]
    public async Task ABrokenChain_IsPreserved_AndRebuiltFromTheVaultPlusEveryValidNewerEntry()
    {
        await using var harness = await CreateAsync();
        var location = BunDexLocation.For(harness.Location);
        var store = BunDexStore.Open(location, out _);
        var backedUp = store.CreatePage(null, "one", "first page", Tags());
        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        var newer = store.CreatePage(null, "two", "second page", Tags());
        store.CreatePage(null, "three", "third page", Tags());
        await harness.CloseAsync();

        // Damage the last committed line: the chain breaks after the newer valid entry.
        var segment = BunDexLedger.Segments(location.LedgerRoot)[^1].Path;
        var lines = File.ReadAllLines(segment);
        lines[^1] = lines[^1].Replace("third page", "THIRD page", StringComparison.Ordinal);
        File.WriteAllLines(segment, lines);
        var damaged = File.ReadAllBytes(segment);

        var report = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);

        Assert.Equal(BunDexLedgerRestore.Restored, report.BunDex!.Outcome);
        Assert.Equal(1, report.BunDex.VaultEntries);
        Assert.Equal(2, report.BunDex.EntriesAfterRestore);
        Assert.Equal(damaged, File.ReadAllBytes(Path.Combine(report.BunDex.PreservedDirectory!, Path.GetFileName(segment))));
        var reopened = BunDexStore.Open(location, out var open);
        Assert.NotEqual(BunDexHealth.Damaged, open.Health);
        Assert.Equal("first page", reopened.PageText(backedUp));
        Assert.Equal("second page", reopened.PageText(newer));
    }
}
