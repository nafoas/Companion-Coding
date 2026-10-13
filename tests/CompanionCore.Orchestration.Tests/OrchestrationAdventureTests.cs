using CompanionCore.Memory;
using CompanionCore.Recall;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>M4c end to end: returning to an adventure makes it active again, append-only.</summary>
public sealed class OrchestrationAdventureTests
{
    private const string Game = "synthetic-game";

    [Fact]
    public async Task ASessionBeginning_MakesItsAdventureActive_Once()
    {
        await using var harness = await CreateAsync();

        await harness.AuthorizeAsync();
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded));

        // A second session while it is still active adds nothing.
        await harness.AuthorizeAsync();

        var chain = await ChainAsync(harness);
        Assert.Equal([AdventureStatus.Active], chain.Select(StatusOf));
        Assert.True(chain[0].IsCurrent);
        Assert.Equal(AdventureStage.Active, AdventureLifecycle.StageOf(chain[0], harness.Time.GetUtcNow(), _ => 0));
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task ReturningToAFinishedAdventure_ReactivatesIt_AndTheOldRecordsRemain()
    {
        await using var harness = await CreateAsync();
        var finished = ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), harness.Time.GetUtcNow().AddDays(-90), Game, CompanionOrchestrator.DefaultSave, AdventureStatus.Finished, null);
        await harness.Host.Repository.WriteGate.SubmitAsync(finished);
        var before = Assert.Single(await ChainAsync(harness));
        Assert.Equal(AdventureStage.AdventureRecord, AdventureLifecycle.StageOf(before, harness.Time.GetUtcNow(), _ => 20));

        await harness.AuthorizeAsync();

        var chain = await ChainAsync(harness);
        Assert.Equal([AdventureStatus.Finished, AdventureStatus.Active], chain.Select(StatusOf));
        Assert.Equal([false, true], chain.Select(memory => memory.IsCurrent));
        Assert.Contains(chain[1].Record.Links, link => link.Kind == MemoryLinkKind.Supersedes && link.TargetRecordId == chain[0].Record.RecordId);
        Assert.Equal(before.RecordChecksum, chain[0].RecordChecksum);
        Assert.Equal(AdventureStage.Active, AdventureLifecycle.StageOf(chain[1], harness.Time.GetUtcNow(), _ => 0));
    }

    [Fact]
    public async Task AFailedReactivation_IsContained_AndTheSessionStillStarts()
    {
        await using var harness = await CreateAsync();
        await harness.Host.Repository.DisposeAsync();

        await harness.AuthorizeAsync();

        Assert.NotNull((await harness.Orchestrator.GetSnapshotAsync()).TargetSessionId);
        Assert.True(harness.Orchestrator.Faults > 0);
    }

    private static async Task<List<RetrievedMemory>> ChainAsync(OrchestrationHarness harness) =>
        [.. (await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Adventure(Game, CompanionOrchestrator.DefaultSave))).OrderBy(memory => memory.JournalSequence)];

    private static AdventureStatus? StatusOf(RetrievedMemory memory) => RecallMetadata.Parse(memory.Record.RetrievalMetadataJson).Adventure;
}
