using CompanionCore.BunDex;

namespace CompanionCore.Orchestration.Tests;

/// <summary>M4a end to end: every play session that begins turns the session clock for remembering.</summary>
public sealed class OrchestrationRememberingTests
{
    [Fact]
    public async Task EachPlaySession_TurnsTheSessionClock_Once()
    {
        await using var harness = await OrchestrationHarness.CreateAsync();
        Assert.Equal(0, harness.Host.BunDex.SessionsPlayed);

        await harness.AuthorizeAsync();

        Assert.Equal(1, harness.Host.BunDex.SessionsPlayed);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task ADamagedBunDex_CountsNoSession_AndTheSessionStillStarts()
    {
        await using var harness = await OrchestrationHarness.CreateAsync();
        harness.Host.BunDex.CreatePage(null, "page", "something happened", new PageTags(BunDexRole.ShortTerm, MemoryLayer.Unspecified, null, null, [], harness.Time.GetUtcNow()));
        await harness.Host.DisposeAsync();
        var segment = BunDexLedger.Segments(BunDexLocation.For(harness.Location).LedgerRoot)[^1].Path;
        var lines = File.ReadAllLines(segment);
        lines[0] = lines[0].Replace("\"page\"", "\"p4ge\"", StringComparison.Ordinal);
        File.WriteAllLines(segment, lines);
        var ledgerBytes = File.ReadAllBytes(segment);

        await harness.OpenHostAsync();
        await harness.AuthorizeAsync();

        Assert.Equal(BunDexHealth.Damaged, harness.Host.BunDex.Health);
        Assert.Equal(0, harness.Host.BunDex.SessionsPlayed);
        Assert.Equal(ledgerBytes, File.ReadAllBytes(segment));
        Assert.Equal(0, harness.Orchestrator.Faults);
    }
}
