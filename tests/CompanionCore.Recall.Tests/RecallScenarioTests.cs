using CompanionCore.Memory;

namespace CompanionCore.Recall.Tests;

/// <summary>Roadmap Stage 8 Paw Gate scenarios, in neutral form, against a real test store.</summary>
public sealed class RecallScenarioTests
{
    private const string Game = "game.alpha";
    private const string SaveOne = "save.one";
    private const string SaveTwo = "save.two";
    private const string SessionOne = "session.one";
    private const string Joke = "[synthetic shared joke] the round boulder blinked first.";

    [Fact]
    public async Task Scenario1_AdventureCompletes_SummaryLinksOriginals_NothingDeleted_ReplayIsIdempotent()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var adventure = await SeedAdventureAsync(harness);
        var plan = adventure.Plan;

        Assert.Single(plan.SummaryRecordIds);
        Assert.Single(plan.HighlightRecordIds);
        Assert.Equal(5, plan.RoutineCount);

        var summary = Assert.Single(await harness.Repository.RetrieveAsync(new MemoryQuery { RecordIds = plan.SummaryRecordIds }));
        Assert.Equal(MemoryScope.Save, summary.Record.Scope);
        Assert.Equal(RecallSubjects.Summary(SessionOne), summary.Record.SubjectKey);
        Assert.Equal(
            adventure.Originals.Select(original => original.RecordId).Order(),
            summary.Record.Links.Where(link => link.Kind == MemoryLinkKind.Source).Select(link => link.TargetRecordId).Order());
        Assert.All(summary.Record.Links, link => Assert.Equal(MemoryLinkKind.Source, link.Kind));
        var summaryMetadata = RecallMetadata.Parse(summary.Record.RetrievalMetadataJson);
        Assert.Equal(RecallRecordKind.Summary, summaryMetadata.Kind);
        Assert.Equal(6, summaryMetadata.ConsolidatedCount);
        Assert.Equal(1, summaryMetadata.HighlightCount);

        // Every original remains retrievable, unchanged, and current.
        var originals = await harness.Repository.RetrieveAsync(new MemoryQuery { RecordIds = adventure.Originals.Select(original => original.RecordId).ToArray() });
        Assert.Equal(6, originals.Count);
        Assert.All(originals, memory => Assert.True(memory.IsCurrent));
        Assert.Equal(
            adventure.OriginalChecksums.OrderBy(pair => pair.Key).Select(pair => pair.Value),
            originals.OrderBy(memory => memory.Record.RecordId).Select(memory => memory.RecordChecksum));
        foreach (var original in adventure.Originals)
        {
            Assert.Equal(original.VisibleRecollection, originals.Single(memory => memory.Record.RecordId == original.RecordId).Record.VisibleRecollection);
        }

        // Replay of the same proposal and a re-plan from the grown session are both AlreadyCommitted.
        var replay = await harness.Repository.WriteGate.SubmitAsync(plan.Proposal);
        Assert.Equal(WriteGateStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(plan.SummaryRecordIds.Concat(plan.HighlightRecordIds).Order(), replay.RecordIds.Order());
        var grownSession = await harness.Repository.RetrieveAsync(new MemoryQuery { SessionReference = SessionOne });
        Assert.Equal(8, grownSession.Count);
        var replanned = ConsolidationPlanner.PlanSessionSummary(adventure.SummaryOperation, adventure.SummaryTime, SessionOne, Game, SaveOne, grownSession);
        Assert.Equal(WriteGateStatus.AlreadyCommitted, (await harness.Repository.WriteGate.SubmitAsync(replanned.Proposal)).Status);

        // A drifted re-plan under the same operation can never double-commit: it conflicts.
        var drifted = ConsolidationPlanner.PlanSessionSummary(adventure.SummaryOperation, adventure.SummaryTime.AddHours(1), SessionOne, Game, SaveOne, grownSession);
        Assert.Equal(WriteGateStatus.Conflict, (await harness.Repository.WriteGate.SubmitAsync(drifted.Proposal)).Status);
        Assert.Equal(8, (await harness.Repository.RetrieveAsync(new MemoryQuery { SessionReference = SessionOne })).Count);

        // The adventure's current status is Finished and its history remains, across reopen.
        await harness.ReopenAsync();
        var history = BeliefHistory.From(await harness.Repository.RetrieveBySubjectAsync(RecallSubjects.Adventure(Game, SaveOne)));
        Assert.NotNull(history.Current);
        Assert.Equal(AdventureStatus.Finished, RecallMetadata.Parse(history.Current.Record.RetrievalMetadataJson).Adventure);
        var earlier = Assert.Single(history.Earlier);
        Assert.False(earlier.IsCurrent);
        Assert.Equal(AdventureStatus.Active, RecallMetadata.Parse(earlier.Record.RetrievalMetadataJson).Adventure);
        // Six originals, two statuses, one summary, one highlight: appends only.
        Assert.Equal(10, (await harness.Repository.RetrieveAsync(new MemoryQuery { GameReference = Game })).Count);
    }

    [Fact]
    public async Task Scenario2_ResumingSave_RetrievesCurrentStatusAndSummaryFirst()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var adventure = await SeedAdventureAsync(harness);

        var selection = await RecallService.RecallAsync(
            harness.Repository,
            new RecallContext(Game, SaveOne, "session.two", [RecallSubjects.Adventure(Game, SaveOne)]));

        Assert.Equal(AdventureStatus.Finished, RecallMetadata.Parse(selection.Items[0].Record.RetrievalMetadataJson).Adventure);
        Assert.Equal(adventure.Plan.SummaryRecordIds[0], selection.Items[1].Record.RecordId);
        Assert.Equal(adventure.Plan.HighlightRecordIds[0], selection.Items[2].Record.RecordId);
        Assert.Equal(Joke, selection.Items[2].Record.VisibleRecollection);

        // The superseded status stays reachable but ranks after current understanding.
        var active = selection.Items.Single(item => RecallMetadata.Parse(item.Record.RetrievalMetadataJson).Adventure == AdventureStatus.Active);
        Assert.False(active.IsCurrent);
        Assert.Equal(selection.Items.Count - 1, selection.Items.ToList().IndexOf(active));

        // Raw session-scoped entries belong to the earlier session only.
        Assert.Equal(6, selection.OutOfScope);
        Assert.DoesNotContain(selection.Items, item => item.Record.Scope == MemoryScope.Session);
        Assert.Equal(0, selection.SpoilersSuppressed);
        Assert.False(selection.Truncated);
    }

    [Fact]
    public async Task Scenario3_NewSave_GetsNoSaveOrSpoilerKnowledge_ButKeepsGameAndGeneralKnowledge()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var adventure = await SeedAdventureAsync(harness);
        var openLore = await harness.CommitOneAsync(ConsolidationPlanner.PlanLore(Guid.NewGuid(), RecallTestHarness.BaselineUtc, Game, "gate", "[neutral lore] the gate opens at dawn.", LoreStatus.Observed));
        var spoilerLore = await harness.CommitOneAsync(ConsolidationPlanner.PlanLore(Guid.NewGuid(), RecallTestHarness.BaselineUtc, Game, "ending", "[neutral lore] the ending reveal.", LoreStatus.Observed, spoiler: true, save: SaveOne));
        var unattributedSpoiler = await harness.CommitOneAsync(new AppendMemoryProposal(Guid.NewGuid(), [
            RecallTestHarness.Entry("[neutral entry] an unattributed spoiler.", "session.other", Game, save: null, minute: 30, spoiler: true, scope: MemoryScope.Game),
        ]));
        var opinion = await harness.CommitOneAsync(ConsolidationPlanner.PlanOpinion(Guid.NewGuid(), RecallTestHarness.BaselineUtc, "music", "[neutral opinion] music: liked."));

        var fresh = await RecallService.RecallAsync(
            harness.Repository,
            new RecallContext(Game, SaveTwo, "session.three", [RecallSubjects.Opinion("music"), RecallSubjects.Adventure(Game, SaveTwo)]));

        var ids = fresh.Items.Select(item => item.Record.RecordId).ToHashSet();
        Assert.Contains(openLore.Record.RecordId, ids);
        Assert.Contains(opinion.Record.RecordId, ids);
        Assert.Contains(adventure.Plan.HighlightRecordIds[0], ids);
        Assert.DoesNotContain(spoilerLore.Record.RecordId, ids);
        Assert.DoesNotContain(unattributedSpoiler.Record.RecordId, ids);
        Assert.DoesNotContain(adventure.Plan.SummaryRecordIds[0], ids);
        Assert.DoesNotContain(fresh.Items, item => item.Record.Scope is MemoryScope.Save or MemoryScope.Session);
        Assert.DoesNotContain(fresh.Items, item => RecallMetadata.Parse(item.Record.RetrievalMetadataJson).Spoiler);
        Assert.DoesNotContain(fresh.Items, item => RecallMetadata.Parse(item.Record.RetrievalMetadataJson).Kind == RecallRecordKind.Adventure);
        Assert.Equal(2, fresh.SpoilersSuppressed);

        // The save where the spoiler was learned still recalls it.
        var resumed = await RecallService.RecallAsync(harness.Repository, new RecallContext(Game, SaveOne, "session.four", []));
        Assert.Contains(resumed.Items, item => item.Record.RecordId == spoilerLore.Record.RecordId);
        Assert.DoesNotContain(resumed.Items, item => item.Record.RecordId == unattributedSpoiler.Record.RecordId);
    }

    [Fact]
    public async Task Scenario4_LoreCorrection_RanksValidatedUnderstandingFirst_TheoryRemains()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var theory = await harness.CommitOneAsync(ConsolidationPlanner.PlanLore(Guid.NewGuid(), RecallTestHarness.BaselineUtc, Game, "tower", "[neutral lore] suspected: the tower is hollow.", LoreStatus.Suspected));
        var confirmed = await harness.CommitOneAsync(ConsolidationPlanner.PlanLore(Guid.NewGuid(), RecallTestHarness.BaselineUtc.AddMinutes(5), Game, "tower", "[neutral lore] confirmed: the tower holds a bell.", LoreStatus.Confirmed, replaces: theory));

        var subject = await harness.Repository.RetrieveBySubjectAsync(RecallSubjects.Lore(Game, "tower"));
        Assert.Equal(2, subject.Count);
        Assert.Equal(confirmed.Record.RecordId, subject[0].Record.RecordId);
        Assert.True(subject[0].IsCurrent);
        Assert.Equal(theory.Record.RecordId, subject[1].Record.RecordId);
        Assert.False(subject[1].IsCurrent);
        Assert.Equal(theory.RecordChecksum, subject[1].RecordChecksum);
        Assert.Equal(LoreStatus.Suspected, RecallMetadata.Parse(subject[1].Record.RetrievalMetadataJson).Lore);
        Assert.Contains(subject[0].Record.Links, link => link.Kind == MemoryLinkKind.Corrects && link.TargetRecordId == theory.Record.RecordId);

        var history = BeliefHistory.From(subject);
        Assert.Equal(confirmed.Record.RecordId, history.Current?.Record.RecordId);
        Assert.Equal(theory.Record.RecordId, Assert.Single(history.Earlier).Record.RecordId);

        var selection = await RecallService.RecallAsync(harness.Repository, new RecallContext(Game, SaveOne, null, [RecallSubjects.Lore(Game, "tower")]));
        Assert.Equal([confirmed.Record.RecordId, theory.Record.RecordId], selection.Items.Select(item => item.Record.RecordId));
    }

    [Fact]
    public async Task Scenario5_SharedJokePreservedVerbatim_RoutineCompressedIntoCounts()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var adventure = await SeedAdventureAsync(harness);

        var highlight = Assert.Single(await harness.Repository.RetrieveAsync(new MemoryQuery { RecordIds = adventure.Plan.HighlightRecordIds }));
        var jokeOriginal = adventure.Originals.Single(original => original.VisibleRecollection == Joke);
        Assert.Equal(Joke, highlight.Record.VisibleRecollection);
        Assert.Equal(jokeOriginal.EntityReferences, highlight.Record.EntityReferences);
        Assert.Equal([new MemoryLink(jokeOriginal.RecordId, MemoryLinkKind.Source)], highlight.Record.Links);
        Assert.Equal(RecallSubjects.Highlight(jokeOriginal.RecordId), highlight.Record.SubjectKey);
        Assert.True(RecallMetadata.Parse(highlight.Record.RetrievalMetadataJson).Highlight);

        var summary = Assert.Single(await harness.Repository.RetrieveAsync(new MemoryQuery { RecordIds = adventure.Plan.SummaryRecordIds }));
        Assert.Equal("[neutral session summary] 6 entries consolidated; 1 highlights preserved.", summary.Record.VisibleRecollection);
        Assert.All(adventure.Originals.Where(original => original.VisibleRecollection != Joke), routine =>
            Assert.DoesNotContain(routine.VisibleRecollection, summary.Record.VisibleRecollection, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scenario6_UserCorrectionOutranksEverySource_OpinionsReconstructCurrentFirst()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var observed = await harness.CommitOneAsync(ConsolidationPlanner.PlanLore(Guid.NewGuid(), RecallTestHarness.BaselineUtc, Game, "river", "[neutral lore] the river flows north.", LoreStatus.Observed, sourceKind: MemorySourceKind.Integration));
        var correction = await harness.CommitOneAsync(ConsolidationPlanner.PlanUserCorrection(Guid.NewGuid(), RecallTestHarness.BaselineUtc.AddMinutes(1), observed, "[neutral correction] the river flows south."));

        var subject = await harness.Repository.RetrieveBySubjectAsync(RecallSubjects.Lore(Game, "river"));
        Assert.Equal(correction.Record.RecordId, subject[0].Record.RecordId);
        Assert.Equal(MemorySourceKind.UserCorrection, subject[0].Record.SourceKind);
        Assert.False(subject[1].IsCurrent);
        Assert.Equal(RecallRecordKind.Correction, RecallMetadata.Parse(correction.Record.RetrievalMetadataJson).Kind);

        var first = await harness.CommitOneAsync(ConsolidationPlanner.PlanOpinion(Guid.NewGuid(), RecallTestHarness.BaselineUtc, "rain", "[neutral opinion] rain: disliked."));
        var second = await harness.CommitOneAsync(ConsolidationPlanner.PlanOpinion(Guid.NewGuid(), RecallTestHarness.BaselineUtc.AddDays(1), "rain", "[neutral opinion] rain: tolerable.", first));
        var third = await harness.CommitOneAsync(ConsolidationPlanner.PlanOpinion(Guid.NewGuid(), RecallTestHarness.BaselineUtc.AddDays(2), "rain", "[neutral opinion] rain: liked.", second));

        var history = BeliefHistory.From(await harness.Repository.RetrieveBySubjectAsync(RecallSubjects.Opinion("rain")));
        Assert.Equal(third.Record.RecordId, history.Current?.Record.RecordId);
        Assert.Equal([second.Record.RecordId, first.Record.RecordId], history.Earlier.Select(memory => memory.Record.RecordId));
        Assert.All(history.Earlier, memory => Assert.False(memory.IsCurrent));
    }

    [Fact]
    public async Task Scenario7_HypothesesAwaitConfirmation_AndTheAnswerSupersedesThem()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var newSave = await harness.CommitOneAsync(ConsolidationPlanner.PlanAdventureHypothesis(Guid.NewGuid(), RecallTestHarness.BaselineUtc, Game, SaveTwo, AdventureHypothesis.NewSave));
        var ending = await harness.CommitOneAsync(ConsolidationPlanner.PlanAdventureHypothesis(Guid.NewGuid(), RecallTestHarness.BaselineUtc, Game, SaveOne, AdventureHypothesis.Ending));
        Assert.Equal(MemorySourceKind.Guess, newSave.Record.SourceKind);
        Assert.Equal(0.5, newSave.Record.Confidence);
        Assert.True(newSave.IsCurrent);

        var confirmed = await harness.CommitOneAsync(ConsolidationPlanner.PlanHypothesisAnswer(Guid.NewGuid(), RecallTestHarness.BaselineUtc.AddMinutes(1), newSave, confirmed: true));
        var declined = await harness.CommitOneAsync(ConsolidationPlanner.PlanHypothesisAnswer(Guid.NewGuid(), RecallTestHarness.BaselineUtc.AddMinutes(1), ending, confirmed: false));
        Assert.Equal("[neutral confirmation] NewSave: confirmed.", confirmed.Record.VisibleRecollection);
        Assert.Equal("[neutral confirmation] Ending: declined.", declined.Record.VisibleRecollection);
        Assert.Equal(MemorySourceKind.UserCorrection, confirmed.Record.SourceKind);

        var subject = await harness.Repository.RetrieveBySubjectAsync(RecallSubjects.AdventureHypothesis(Game, SaveTwo));
        Assert.Equal(confirmed.Record.RecordId, subject[0].Record.RecordId);
        Assert.False(subject[1].IsCurrent);
        Assert.Contains(subject[0].Record.Links, link => link.Kind == MemoryLinkKind.Supersedes && link.TargetRecordId == newSave.Record.RecordId);
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanHypothesisAnswer(Guid.NewGuid(), RecallTestHarness.BaselineUtc, confirmed, confirmed: true));
    }

    [Fact]
    public async Task Scenario8_ValidSeedsAppend_RootsStayUntouched()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var roots = InterestRootSet.Create([new InterestRoot("weather", "[neutral root] weather."), new InterestRoot("music", "[neutral root] music.")]);
        var definition = roots.ToJson();
        var fingerprint = roots.Fingerprint;

        var seed = await harness.CommitOneAsync(GeneratedSeedPlanner.Plan(roots, "music", "[neutral seed] a question about rhythm.", Guid.NewGuid(), RecallTestHarness.BaselineUtc));
        var metadata = RecallMetadata.Parse(seed.Record.RetrievalMetadataJson);
        Assert.Equal(RecallRecordKind.Seed, metadata.Kind);
        Assert.Equal("music", metadata.RootId);
        Assert.Equal(fingerprint, metadata.RootsFingerprint);
        Assert.Equal(RecallSubjects.Seed("music"), seed.Record.SubjectKey);
        Assert.Equal(MemoryScope.General, seed.Record.Scope);

        Assert.Equal(fingerprint, roots.Fingerprint);
        Assert.Equal(definition, roots.ToJson());
        Assert.Equal(fingerprint, InterestRootSet.Load(definition).Fingerprint);
    }

    private static async Task<SeededAdventure> SeedAdventureAsync(RecallTestHarness harness)
    {
        MemoryRecordDraft[] originals =
        [
            RecallTestHarness.Entry("[neutral routine] reached checkpoint one.", SessionOne, Game, SaveOne, 1),
            RecallTestHarness.Entry("[neutral routine] reached checkpoint two.", SessionOne, Game, SaveOne, 2),
            RecallTestHarness.Entry(Joke, SessionOne, Game, SaveOne, 3, highlight: true),
            RecallTestHarness.Entry("[neutral routine] reached checkpoint three.", SessionOne, Game, SaveOne, 4),
            RecallTestHarness.Entry("[neutral routine] reached checkpoint four.", SessionOne, Game, SaveOne, 5),
            RecallTestHarness.Entry("[neutral routine] reached the final checkpoint.", SessionOne, Game, SaveOne, 6),
        ];
        await harness.CommitAsync(new AppendMemoryProposal(Guid.NewGuid(), originals));
        var active = await harness.CommitOneAsync(ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), RecallTestHarness.BaselineUtc, Game, SaveOne, AdventureStatus.Active, previousStatus: null));
        await harness.CommitOneAsync(ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), RecallTestHarness.BaselineUtc.AddMinutes(10), Game, SaveOne, AdventureStatus.Finished, active));

        var committedOriginals = await harness.Repository.RetrieveAsync(new MemoryQuery { SessionReference = SessionOne });
        Assert.Equal(6, committedOriginals.Count);
        var summaryOperation = Guid.NewGuid();
        var summaryTime = RecallTestHarness.BaselineUtc.AddMinutes(11);
        var plan = ConsolidationPlanner.PlanSessionSummary(summaryOperation, summaryTime, SessionOne, Game, SaveOne, committedOriginals);
        var result = await harness.CommitAsync(plan.Proposal);
        Assert.Equal(plan.SummaryRecordIds.Concat(plan.HighlightRecordIds).Order(), result.RecordIds.Order());
        return new SeededAdventure(originals, committedOriginals.ToDictionary(memory => memory.Record.RecordId, memory => memory.RecordChecksum), plan, summaryOperation, summaryTime);
    }

    private sealed record SeededAdventure(
        IReadOnlyList<MemoryRecordDraft> Originals,
        IReadOnlyDictionary<Guid, string> OriginalChecksums,
        ConsolidationPlan Plan,
        Guid SummaryOperation,
        DateTimeOffset SummaryTime);
}
