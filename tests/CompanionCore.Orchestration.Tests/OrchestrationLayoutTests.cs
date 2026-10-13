using System.Text;
using CompanionCore.BunDex;
using CompanionCore.Memory;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>M1b: a host opened over the old layout moves it into the BrainCase and sees exactly the same Prince.</summary>
public sealed class OrchestrationLayoutTests
{
    [Fact]
    public async Task AHostOverTheOldLayout_MovesItIntoTheBraincase_AndKeepsEverything()
    {
        await using var harness = await CreateAsync();
        Assert.Equal(DataLayoutMigrationOutcome.Current, harness.Host.LayoutMigration.Outcome);

        var record = new MemoryRecordDraft
        {
            RecordId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            Scope = MemoryScope.General,
            SourceKind = MemorySourceKind.Observed,
            Confidence = 0.8,
            SubjectKey = "synthetic.layout",
            VisibleRecollection = "[neutral memory] a door glowed.",
            RetrievalMetadataJson = "{}",
        };
        Assert.Equal(WriteGateStatus.Committed, (await harness.Host.Repository.WriteGate.SubmitAsync(new AppendMemoryProposal(Guid.NewGuid(), [record]))).Status);
        await harness.Host.State.PutAsync("settings", Encoding.UTF8.GetBytes("{\"volume\":3}"));
        var page = harness.Host.BunDex.CreatePage(null, "da door", "a door glowed", new PageTags(BunDexRole.Adventures, MemoryLayer.Unspecified, null, null, [], DateTimeOffset.UnixEpoch));
        await harness.Host.DisposeAsync();

        // Put the machine stores back where the old layout kept them.
        var location = harness.Location;
        var moved = new List<string>();
        foreach (var name in DataLayout.BraincaseStoreNames)
        {
            var current = Path.Combine(location.BraincaseRootPath, name);
            if (Directory.Exists(current))
            {
                Directory.Move(current, Path.Combine(location.DataRootPath, name));
                moved.Add(name);
            }
        }

        Assert.Contains("Memory", moved);
        await harness.OpenHostAsync();

        Assert.Equal(DataLayoutMigrationOutcome.Moved, harness.Host.LayoutMigration.Outcome);
        Assert.Equal(moved, harness.Host.LayoutMigration.Moved);
        Assert.False(DataLayoutMigration.HasLegacyStores(location));
        Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { RecordIds = [record.RecordId] }));
        Assert.Equal("{\"volume\":3}", Encoding.UTF8.GetString((await harness.Host.State.GetAsync("settings")).Payload.Span));
        Assert.Equal("a door glowed", harness.Host.BunDex.PageText(page));
    }
}
