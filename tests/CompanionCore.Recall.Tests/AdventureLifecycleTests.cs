using CompanionCore.Memory;

namespace CompanionCore.Recall.Tests;

/// <summary>M4c: Active → Freshly Finished → Remembered → Adventure Record, with Treasured Episodes forever.</summary>
public sealed class AdventureLifecycleTests
{
    private const string Game = "synthetic.lifecycle";
    private const string Save = "save.one";
    private static readonly DateTimeOffset Now = RecallTestHarness.BaselineUtc;

    [Theory]
    [InlineData(AdventureStatus.Active, 1000, 1000, AdventureStage.Active)]
    [InlineData(AdventureStatus.Paused, 1000, 1000, AdventureStage.Active)]
    [InlineData(AdventureStatus.Finished, 0, 0, AdventureStage.FreshlyFinished)]
    [InlineData(AdventureStatus.Finished, 5, 300, AdventureStage.FreshlyFinished)]
    [InlineData(AdventureStatus.Finished, 300, 21, AdventureStage.FreshlyFinished)]
    [InlineData(AdventureStatus.Finished, 6, 22, AdventureStage.Remembered)]
    [InlineData(AdventureStatus.Finished, 10, 300, AdventureStage.Remembered)]
    [InlineData(AdventureStatus.Finished, 300, 60, AdventureStage.Remembered)]
    [InlineData(AdventureStatus.Finished, 11, 61, AdventureStage.AdventureRecord)]
    public void EachStage_FollowsItsWindow_OnBothClocks(AdventureStatus status, int sessions, int days, AdventureStage expected)
    {
        Assert.Equal(expected, AdventureLifecycle.StageOf(status, TimeSpan.FromDays(days), sessions));
    }

    [Fact]
    public void TimeAwayAlone_NeverMovesAnAdventureAlong()
    {
        Assert.Equal(AdventureStage.FreshlyFinished, AdventureLifecycle.StageOf(AdventureStatus.Finished, TimeSpan.FromDays(3 * 365), 0));
    }

    [Fact]
    public void TheWindows_AreTheMediumAndLongLayers()
    {
        Assert.Equal((5, TimeSpan.FromDays(21)), AdventureLifecycle.FreshWindow);
        Assert.Equal((10, TimeSpan.FromDays(60)), AdventureLifecycle.RememberedWindow);
    }

    [Fact]
    public async Task TheStage_ComesFromTheCurrentStatusRecord_AndItsTime()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        Assert.Null(await AdventureLifecycle.CurrentStatusAsync(harness.Repository, Game, Save));
        Assert.Null(AdventureLifecycle.StageOf(null, Now, _ => 0));

        var active = await harness.CommitOneAsync(ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), Now, Game, Save, AdventureStatus.Active, null));
        var finishedAt = Now.AddDays(1);
        await harness.CommitOneAsync(ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), finishedAt, Game, Save, AdventureStatus.Finished, active));
        var current = await AdventureLifecycle.CurrentStatusAsync(harness.Repository, Game, Save);

        Assert.Equal(AdventureStatus.Finished, RecallMetadata.Parse(current!.Record.RetrievalMetadataJson).Adventure);
        DateTimeOffset? askedSince = null;
        var stage = AdventureLifecycle.StageOf(current, finishedAt.AddDays(30), since =>
        {
            askedSince = since;
            return 7;
        });
        Assert.Equal(AdventureStage.Remembered, stage);
        Assert.Equal(finishedAt, askedSince);
    }

    [Fact]
    public void AnUnrelatedRecord_HasNoStage()
    {
        var lore = RecallTestHarness.Retrieved(ConsolidationPlanner.PlanLore(Guid.NewGuid(), Now, Game, "gate", "[neutral lore] the gate.", LoreStatus.Observed).Records[0]);

        Assert.Null(AdventureLifecycle.StageOf(lore, Now, _ => 0));
    }

    [Theory]
    [InlineData(AdventureStage.Active, 6, 0)]
    [InlineData(AdventureStage.FreshlyFinished, 6, 0)]
    [InlineData(AdventureStage.Remembered, 0, 6)]
    [InlineData(AdventureStage.AdventureRecord, 0, 7)]
    public async Task WhatCanStillComeUp_FollowsTheStage_AndTreasuredEpisodesAlwaysDo(AdventureStage stage, int details, int resting)
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var seeded = await SeedAsync(harness);

        var selection = RecallSelector.SelectAdventure(seeded.All, Game, Save, stage);

        Assert.Equal(details, selection.Items.Count(item => RecallMetadata.Parse(item.Record.RetrievalMetadataJson).Kind == RecallRecordKind.Entry));
        Assert.Equal(resting, selection.Resting);
        Assert.Contains(selection.Items, item => item.Record.RecordId == seeded.Highlight);
        Assert.Contains(selection.Items, item => RecallMetadata.Parse(item.Record.RetrievalMetadataJson).Kind == RecallRecordKind.Adventure);
        Assert.Equal(stage != AdventureStage.AdventureRecord, selection.Items.Any(item => item.Record.RecordId == seeded.Summary));

        // Something treasured leads (the ladder); another save and a hypothesis are never part of it.
        Assert.True(RecallMetadata.Parse(selection.Items[0].Record.RetrievalMetadataJson).Highlight);
        Assert.Equal(3, selection.OutOfScope);
        Assert.False(selection.Truncated);
    }

    [Fact]
    public async Task Resting_IsNotGone_ReactivationBringsTheDetailForward()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var seeded = await SeedAsync(harness);
        var checksums = seeded.All.ToDictionary(memory => memory.Record.RecordId, memory => memory.RecordChecksum);

        Assert.Equal(7, RecallSelector.SelectAdventure(seeded.All, Game, Save, AdventureStage.AdventureRecord).Resting);
        var again = await harness.Repository.RetrieveAsync(new MemoryQuery { GameReference = Game, Limit = 100 });
        var active = RecallSelector.SelectAdventure(again, Game, Save, AdventureStage.Active);

        Assert.Equal(0, active.Resting);
        Assert.Equal(6, active.Items.Count(item => RecallMetadata.Parse(item.Record.RetrievalMetadataJson).Kind == RecallRecordKind.Entry));
        Assert.All(again, memory => Assert.Equal(checksums[memory.Record.RecordId], memory.RecordChecksum));
    }

    [Fact]
    public async Task TheLadder_TreasuredThenContextThenDetail()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var seeded = await SeedAsync(harness);

        // Among current understanding (a superseded status ranks after it, as everywhere).
        var order = RecallSelector.SelectAdventure(seeded.All, Game, Save, AdventureStage.Active).Items
            .Where(item => item.IsCurrent)
            .Select(item => RecallMetadata.Parse(item.Record.RetrievalMetadataJson))
            .Select(metadata => metadata.Highlight ? 0 : metadata.Kind is RecallRecordKind.Summary or RecallRecordKind.Adventure ? 1 : 2)
            .ToList();

        Assert.Equal(order.Order(), order);
        Assert.Equal(0, order[0]);
    }

    [Fact]
    public async Task SelectingAnAdventure_IsBounded()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var seeded = await SeedAsync(harness);

        var selection = RecallSelector.SelectAdventure(seeded.All, Game, Save, AdventureStage.Active, maximumRecords: 2);

        Assert.Equal(2, selection.Items.Count);
        Assert.True(selection.Truncated);
        Assert.Throws<ArgumentOutOfRangeException>(() => RecallSelector.SelectAdventure(seeded.All, Game, Save, AdventureStage.Active, maximumRecords: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecallSelector.SelectAdventure(seeded.All, Game, Save, AdventureStage.Active, maximumRecollectionBytes: 0));
        Assert.Throws<ArgumentException>(() => RecallSelector.SelectAdventure(seeded.All, " ", Save, AdventureStage.Active));
        Assert.Throws<ArgumentException>(() => RecallSelector.SelectAdventure(seeded.All, Game, " ", AdventureStage.Active));
    }

    [Theory]
    [InlineData(AdventureStage.Remembered)]
    [InlineData(AdventureStage.AdventureRecord)]
    public async Task AMemoryBehindAPageHeCherishes_IsATreasuredEpisode_AndAlwaysComesUp(AdventureStage stage)
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var seeded = await SeedAsync(harness);
        var routine = seeded.All.Single(memory => memory.Record.VisibleRecollection == "[neutral routine] two.").Record.RecordId;

        var plain = RecallSelector.SelectAdventure(seeded.All, Game, Save, stage);
        var cherished = RecallSelector.SelectAdventure(seeded.All, Game, Save, stage, treasured: new HashSet<Guid> { routine });

        Assert.DoesNotContain(plain.Items, item => item.Record.RecordId == routine);
        Assert.Contains(cherished.Items, item => item.Record.RecordId == routine);
        Assert.Equal(plain.Resting - 1, cherished.Resting);

        // On the ladder it stands with the treasured highlight, above context and detail.
        var top = cherished.Items.Take(2).Select(item => item.Record.RecordId).ToHashSet();
        Assert.Contains(routine, top);
        Assert.Contains(seeded.Highlight, top);

        // A summary that covers it is not itself treasured.
        var summary = cherished.Items.ToList().FindIndex(item => item.Record.RecordId == seeded.Summary);
        Assert.True(summary is -1 or > 1);
    }

    [Fact]
    public async Task WithoutCherishedPages_NothingChanges()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var seeded = await SeedAsync(harness);

        foreach (var stage in Enum.GetValues<AdventureStage>())
        {
            var before = RecallSelector.SelectAdventure(seeded.All, Game, Save, stage).Items.Select(item => item.Record.RecordId);
            var empty = RecallSelector.SelectAdventure(seeded.All, Game, Save, stage, treasured: new HashSet<Guid>()).Items.Select(item => item.Record.RecordId);
            Assert.Equal(before, empty);
        }
    }

    /// <summary>One adventure: six session entries (one a highlight), an active then finished status, its summary and highlight; plus another save's entry and a hypothesis.</summary>
    private static async Task<(List<RetrievedMemory> All, Guid Summary, Guid Highlight)> SeedAsync(RecallTestHarness harness)
    {
        MemoryRecordDraft[] originals =
        [
            RecallTestHarness.Entry("[neutral routine] one.", "session.one", Game, Save, 1),
            RecallTestHarness.Entry("[neutral routine] two.", "session.one", Game, Save, 2),
            RecallTestHarness.Entry("[neutral joke] a very good joke.", "session.one", Game, Save, 3, highlight: true),
            RecallTestHarness.Entry("[neutral routine] three.", "session.one", Game, Save, 4),
            RecallTestHarness.Entry("[neutral routine] four.", "session.one", Game, Save, 5),
            RecallTestHarness.Entry("[neutral routine] five.", "session.one", Game, Save, 6),
        ];
        await harness.CommitAsync(new AppendMemoryProposal(Guid.NewGuid(), originals));
        await harness.CommitAsync(new AppendMemoryProposal(Guid.NewGuid(), [RecallTestHarness.Entry("[neutral routine] other save.", "session.other", Game, "save.two", 7)]));
        await harness.CommitAsync(ConsolidationPlanner.PlanAdventureHypothesis(Guid.NewGuid(), Now, Game, Save, AdventureHypothesis.Ending));
        var active = await harness.CommitOneAsync(ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), Now, Game, Save, AdventureStatus.Active, null));
        await harness.CommitOneAsync(ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), Now.AddMinutes(10), Game, Save, AdventureStatus.Finished, active));
        var session = await harness.Repository.RetrieveAsync(new MemoryQuery { SessionReference = "session.one" });
        var plan = ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now.AddMinutes(11), "session.one", Game, Save, session);
        await harness.CommitAsync(plan.Proposal);
        await harness.CommitAsync(new AppendMemoryProposal(Guid.NewGuid(), [RecallTestHarness.Entry("[neutral routine] another game, same save name.", "session.elsewhere", "synthetic.elsewhere", Save, 8)]));
        var all = await harness.Repository.RetrieveAsync(new MemoryQuery { GameReference = Game, Limit = 100 });
        var elsewhere = await harness.Repository.RetrieveAsync(new MemoryQuery { GameReference = "synthetic.elsewhere", Limit = 100 });
        return ([.. all, .. elsewhere], plan.SummaryRecordIds[0], plan.HighlightRecordIds[0]);
    }
}
