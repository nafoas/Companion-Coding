using CompanionCore.BunDex;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>M1: the host opens the BunDex beside the BrainCase, and it survives restarts and Bnuy Repairs.</summary>
public sealed class OrchestrationBunDexTests
{
    [Fact]
    public async Task TheHost_OpensAHealthyBunDex_ThatSurvivesARestartAndRepairs()
    {
        await using var harness = await CreateAsync();
        Assert.Equal(BunDexHealth.Healthy, harness.Host.BunDexReport.Health);
        Assert.Empty(harness.Host.BunDex.View().Folders);

        var folder = harness.Host.BunDex.CreateFolder("Big Adventures!!");
        var page = harness.Host.BunDex.CreatePage(folder, "da door", "a door glowed", new PageTags(BunDexRole.Adventures, MemoryLayer.Unspecified, "synthetic.exploration", null, [], DateTimeOffset.UnixEpoch));

        await harness.ReopenHostAsync();
        Assert.Equal(BunDexHealth.Healthy, harness.Host.BunDexReport.Health);
        Assert.Equal("a door glowed", harness.Host.BunDex.PageText(page));

        // A Vault backup carries the ledger; then the page is lost from disk and Repairs rebuild it.
        await CompanionCore.Vault.DaBunVault.CreateAsync(harness.Host.Repository, harness.Host.Keepsakes, harness.Host.State);
        var before = harness.Host.BunDex;
        File.Delete(before.PagePath(page));
        var repair = await harness.Host.RepairAsync();
        Assert.Equal(BunDexLedgerRestore.Kept, repair.BunDex!.Outcome);
        Assert.Equal(1, harness.Host.BunDexReport.PagesRestored);
        Assert.NotSame(before, harness.Host.BunDex);
        Assert.Equal(BunDexHealth.Healthy, harness.Host.BunDexReport.Health);
        Assert.Equal("a door glowed", harness.Host.BunDex.PageText(page));
        Assert.StartsWith(BunDexLocation.For(harness.Location).BunDexRoot, harness.Host.BunDex.PagePath(page), StringComparison.Ordinal);
    }
}
