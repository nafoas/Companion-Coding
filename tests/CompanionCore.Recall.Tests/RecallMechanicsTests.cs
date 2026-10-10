using System.Reflection;
using CompanionCore.Memory;

namespace CompanionCore.Recall.Tests;

/// <summary>Pure planner, selector, metadata, and root mechanics over synthetic records.</summary>
public sealed class RecallMechanicsTests
{
    private static readonly DateTimeOffset Now = RecallTestHarness.BaselineUtc;

    private static RetrievedMemory SessionEntry(int minute, string session = "s", string? game = "g", string? save = "v", bool highlight = false, bool spoiler = false) =>
        RecallTestHarness.Retrieved(RecallTestHarness.Entry($"[neutral entry] {minute}.", session, game, save, minute, highlight, spoiler), sequence: minute);

    [Fact]
    public void SessionSummary_RefusesEmptyForeignOrMismatchedOriginals()
    {
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", []));
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, " ", "g", "v", [SessionEntry(1)]));
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", [SessionEntry(1), SessionEntry(2, session: "other")]));
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", [SessionEntry(1, game: "other")]));
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", [SessionEntry(1, save: "other")]));
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", [null!]));

        // Only earlier consolidation output: nothing left to consolidate.
        var earlier = ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", [SessionEntry(1, highlight: true)]);
        var outputs = earlier.Proposal.Records.Select(record => RecallTestHarness.Retrieved(record)).ToArray();
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", outputs));
    }

    [Fact]
    public void SessionSummary_DeduplicatesOriginals_AndAcceptsUnattributedEntries()
    {
        var entry = SessionEntry(1);
        var unattributed = SessionEntry(2, game: null, save: null);

        var plan = ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", [entry, entry, unattributed]);

        var summary = Assert.Single(plan.Proposal.Records);
        Assert.Equal(2, summary.Links.Count);
        Assert.Equal(2, RecallMetadata.Parse(summary.RetrievalMetadataJson).ConsolidatedCount);
        Assert.Equal(2, plan.RoutineCount);
    }

    [Fact]
    public async Task SessionSummary_ConsolidatesBridgeStyleSessionReferences()
    {
        // Composition regression (WIRE-01 C1): bridge records carry "target-session:<id>".
        await using var harness = await RecallTestHarness.CreateAsync();
        var session = $"target-session:{Guid.NewGuid():N}";
        await harness.CommitAsync(new AppendMemoryProposal(Guid.NewGuid(), [
            RecallTestHarness.Entry("[neutral entry] one.", session, null, null, 1),
            RecallTestHarness.Entry("[neutral entry] two.", session, null, null, 2, subject: "entry:bridge:2"),
        ]));
        var originals = await harness.Repository.RetrieveAsync(new MemoryQuery { SessionReference = session });

        var plan = ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, session, null, null, originals);
        await harness.CommitAsync(plan.Proposal);

        var summary = Assert.Single(plan.Proposal.Records);
        Assert.DoesNotContain(':', summary.SubjectKey["summary:".Length..]);
        Assert.Equal($"summary:{session.Replace(":", "%3A", StringComparison.Ordinal)}", summary.SubjectKey);
        Assert.Equal(2, summary.Links.Count);
        Assert.NotEqual(RecallSubjects.Summary("a:b"), RecallSubjects.Summary("a%3Ab"));
        Assert.Equal("summary:plain-session", RecallSubjects.Summary("plain-session"));
    }

    [Fact]
    public void SessionSummary_ChunksLargeSessions_WithinStoreBounds()
    {
        var originals = Enumerable.Range(1, 300).Select(minute => SessionEntry(minute)).ToArray();

        var plan = ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", originals);

        Assert.Equal(2, plan.SummaryRecordIds.Count);
        Assert.Equal([256, 44], plan.Proposal.Records.Select(record => record.Links.Count));
        Assert.Equal(300, plan.Proposal.Records.SelectMany(record => record.Links).Select(link => link.TargetRecordId).Distinct().Count());
        Assert.All(plan.Proposal.Records, record => Assert.Equal(RecallSubjects.Summary("s"), record.SubjectKey));
    }

    [Fact]
    public void SessionSummary_RefusesConsolidationsBeyondOneAppendOperation()
    {
        var withinBound = Enumerable.Range(1, ConsolidationPlanner.MaximumRecordsPerConsolidation - 1).Select(minute => SessionEntry(minute, highlight: true)).ToArray();
        Assert.Equal(ConsolidationPlanner.MaximumRecordsPerConsolidation, ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", withinBound).Proposal.Records.Count);

        var beyond = Enumerable.Range(1, ConsolidationPlanner.MaximumRecordsPerConsolidation).Select(minute => SessionEntry(minute, highlight: true)).ToArray();
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", beyond));
    }

    [Fact]
    public async Task SessionSummary_AtTheBound_IsAcceptedByTheStore()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var entries = Enumerable.Range(1, ConsolidationPlanner.MaximumRecordsPerConsolidation - 1)
            .Select(minute => RecallTestHarness.Entry($"[neutral highlight] {minute}.", "s", "g", "v", minute, highlight: true))
            .ToArray();
        await harness.CommitAsync(new AppendMemoryProposal(Guid.NewGuid(), entries));
        var originals = await harness.Repository.RetrieveAsync(new MemoryQuery { SessionReference = "s", Limit = 1000 });

        var plan = ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", originals);
        var result = await harness.CommitAsync(plan.Proposal);

        Assert.Equal(ConsolidationPlanner.MaximumRecordsPerConsolidation, result.RecordIds.Count);
    }

    [Theory]
    [InlineData("g", "v", MemoryScope.Save, MemoryScope.Game)]
    [InlineData("g", null, MemoryScope.Game, MemoryScope.Game)]
    [InlineData(null, null, MemoryScope.General, MemoryScope.General)]
    public void SessionSummary_ScopeFollowsTheMostSpecificReference(string? game, string? save, MemoryScope summaryScope, MemoryScope highlightScope)
    {
        var plan = ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", game, save, [SessionEntry(1, game: game, save: save, highlight: true)]);

        Assert.Equal(summaryScope, plan.Proposal.Records[0].Scope);
        Assert.Equal(highlightScope, plan.Proposal.Records[1].Scope);
        Assert.All(plan.Proposal.Records, record => Assert.Equal(TimeSpan.Zero, record.CreatedAtUtc.Offset));
    }

    [Fact]
    public void SessionSummary_PropagatesSpoilerFlags_ToSummaryAndHighlight()
    {
        var plan = ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", [SessionEntry(1), SessionEntry(2, highlight: true, spoiler: true)]);

        Assert.All(plan.Proposal.Records, record => Assert.True(RecallMetadata.Parse(record.RetrievalMetadataJson).Spoiler));
        var clean = ConsolidationPlanner.PlanSessionSummary(Guid.NewGuid(), Now, "s", "g", "v", [SessionEntry(1), SessionEntry(2, highlight: true)]);
        Assert.All(clean.Proposal.Records, record => Assert.False(RecallMetadata.Parse(record.RetrievalMetadataJson).Spoiler));
    }

    [Fact]
    public void RecordIds_AreDeterministicPerOperationAndIndex()
    {
        var operation = Guid.NewGuid();
        Assert.Equal(ConsolidationPlanner.DeriveRecordId(operation, 0), ConsolidationPlanner.DeriveRecordId(operation, 0));
        Assert.NotEqual(ConsolidationPlanner.DeriveRecordId(operation, 0), ConsolidationPlanner.DeriveRecordId(operation, 1));
        Assert.NotEqual(ConsolidationPlanner.DeriveRecordId(operation, 0), ConsolidationPlanner.DeriveRecordId(Guid.NewGuid(), 0));
        Assert.NotEqual(Guid.Empty, ConsolidationPlanner.DeriveRecordId(Guid.Empty, 0));
        Assert.NotEqual(operation, ConsolidationPlanner.DeriveRecordId(operation, 0));

        var originals = new[] { SessionEntry(1), SessionEntry(2, highlight: true) };
        var first = ConsolidationPlanner.PlanSessionSummary(operation, Now, "s", "g", "v", originals);
        var second = ConsolidationPlanner.PlanSessionSummary(operation, Now, "s", "g", "v", originals.Reverse().ToArray());
        Assert.Equal(first.Proposal.Records.Select(record => record.RecordId), second.Proposal.Records.Select(record => record.RecordId));
        Assert.Equal(first.Proposal.Records[0].Links, second.Proposal.Records[0].Links);
    }

    [Fact]
    public void LinkedPlans_RequireTheExactSubject()
    {
        var status = RecallTestHarness.Retrieved(ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), Now, "g", "v", AdventureStatus.Active, null).Records[0]);
        var opinion = RecallTestHarness.Retrieved(ConsolidationPlanner.PlanOpinion(Guid.NewGuid(), Now, "tea", "[neutral opinion] tea.").Records[0]);
        var lore = RecallTestHarness.Retrieved(ConsolidationPlanner.PlanLore(Guid.NewGuid(), Now, "g", "gate", "[neutral lore] gate.", LoreStatus.Read).Records[0]);

        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), Now, "g", "other", AdventureStatus.Paused, status));
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanOpinion(Guid.NewGuid(), Now, "coffee", "[neutral opinion] coffee.", opinion));
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanLore(Guid.NewGuid(), Now, "g", "tower", "[neutral lore] tower.", LoreStatus.Confirmed, lore));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConsolidationPlanner.PlanLore(Guid.NewGuid(), Now, "g", "gate", "x", (LoreStatus)99));
        Assert.Throws<ArgumentException>(() => ConsolidationPlanner.PlanLore(Guid.NewGuid(), Now, "g", "gate", "x", LoreStatus.Observed, spoiler: true));

        var paused = ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), Now, "g", "v", AdventureStatus.Paused, status).Records[0];
        Assert.Equal([new MemoryLink(status.Record.RecordId, MemoryLinkKind.Supersedes)], paused.Links);
    }

    [Theory]
    [InlineData(LoreStatus.Observed, MemorySourceKind.Observed, 0.75)]
    [InlineData(LoreStatus.Read, MemorySourceKind.Read, 0.75)]
    [InlineData(LoreStatus.Told, MemorySourceKind.Told, 0.75)]
    [InlineData(LoreStatus.Suspected, MemorySourceKind.Guess, 0.4)]
    [InlineData(LoreStatus.Confirmed, MemorySourceKind.Observed, 0.95)]
    public void LoreProvenance_MapsToSourceAndConfidence(LoreStatus status, MemorySourceKind source, double confidence)
    {
        var record = ConsolidationPlanner.PlanLore(Guid.NewGuid(), Now, "g", "gate", "[neutral lore] gate.", status).Records[0];

        Assert.Equal(source, record.SourceKind);
        Assert.Equal(confidence, record.Confidence);
        Assert.Equal(status, RecallMetadata.Parse(record.RetrievalMetadataJson).Lore);
        Assert.Equal(MemoryScope.Game, record.Scope);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a:b")]
    public void SubjectComponents_MustBeShortNonBlankAndColonFree(string component)
    {
        Assert.Throws<ArgumentException>(() => RecallSubjects.Lore("g", component));
        Assert.Throws<ArgumentException>(() => RecallSubjects.Adventure(component, "v"));
    }

    [Fact]
    public void SubjectComponents_AreLengthBounded()
    {
        Assert.Equal($"opinion:{new string('t', 120)}", RecallSubjects.Opinion(new string('t', 120)));
        Assert.Throws<ArgumentException>(() => RecallSubjects.Opinion(new string('t', 121)));
    }

    [Fact]
    public void Metadata_RoundTrips_AndForeignMetadataParsesAsEntry()
    {
        var metadata = new RecallMetadata
        {
            Kind = RecallRecordKind.Summary,
            Highlight = true,
            Spoiler = true,
            Adventure = AdventureStatus.Paused,
            Hypothesis = AdventureHypothesis.NewSave,
            Lore = LoreStatus.Told,
            RootId = "music",
            RootsFingerprint = "ab",
            ConsolidatedCount = 3,
            HighlightCount = 1,
        };

        Assert.Equal(metadata, RecallMetadata.Parse(metadata.ToJson()));
        Assert.Equal(new RecallMetadata(), RecallMetadata.Parse(null));
        Assert.Equal(new RecallMetadata(), RecallMetadata.Parse("not json"));
        Assert.Equal(new RecallMetadata(), RecallMetadata.Parse("{\"category\":\"synthetic\"}"));
        Assert.Equal(new RecallMetadata(), RecallMetadata.Parse("{\"recall\":[1]}"));
        Assert.Equal(RecallRecordKind.Entry, RecallMetadata.Parse("{\"recall\":{\"kind\":\"Summary\",\"highlight\":\"true\"}}").Kind);
        Assert.False(RecallMetadata.Parse("{\"recall\":{\"highlight\":\"true\"}}").Highlight);
        Assert.Equal("{\"recall\":{\"highlight\":false,\"kind\":\"entry\",\"spoiler\":false}}", new RecallMetadata().ToJson());
    }

    [Fact]
    public void Selector_EnforcesScopeRules()
    {
        RetrievedMemory Record(MemoryScope scope, string? game, string? save, string? session) =>
            RecallTestHarness.Retrieved(RecallTestHarness.Entry("[neutral entry]", session ?? "unused", game, save, 1, scope: scope) with { });

        var sameSession = Record(MemoryScope.Session, "g", "v", "s");
        var otherSession = Record(MemoryScope.Session, "g", "v", "s2");
        var sameSave = Record(MemoryScope.Save, "g", "v", "s2");
        var sameSaveOtherGame = Record(MemoryScope.Save, "g2", "v", "s2");
        var otherSave = Record(MemoryScope.Save, "g", "v2", "s2");
        var sameGame = Record(MemoryScope.Game, "g", "v2", "s2");
        var otherGame = Record(MemoryScope.Game, "g2", null, "s2");
        var general = Record(MemoryScope.General, null, null, "s2");
        var all = new[] { sameSession, otherSession, sameSave, sameSaveOtherGame, otherSave, sameGame, otherGame, general };

        var selection = RecallSelector.Select(all, new RecallContext("g", "v", "s", []));
        Assert.Equal(
            new[] { sameSession, sameSave, sameGame, general }.Select(memory => memory.Record.RecordId).Order(),
            selection.Items.Select(memory => memory.Record.RecordId).Order());
        Assert.Equal(4, selection.OutOfScope);

        var noContext = RecallSelector.Select(all, new RecallContext(null, null, null, []));
        Assert.Equal([general.Record.RecordId], noContext.Items.Select(memory => memory.Record.RecordId));
    }

    [Fact]
    public void Selector_UserCorrectionRanksFirst_AndAuthorityOrderIsStrict()
    {
        RetrievedMemory Record(MemorySourceKind source, double confidence, bool current = true) =>
            RecallTestHarness.Retrieved(RecallTestHarness.Entry($"[neutral] {source}", "s", "g", null, 1, scope: MemoryScope.General) with { SourceKind = source, Confidence = confidence }, current);

        var correction = Record(MemorySourceKind.UserCorrection, 0.0);
        var integration = Record(MemorySourceKind.Integration, 1.0);
        var told = Record(MemorySourceKind.Told, 0.0);
        var observed = Record(MemorySourceKind.Observed, 1.0);
        var read = Record(MemorySourceKind.Read, 1.0);
        var remembered = Record(MemorySourceKind.Remembered, 1.0);
        var inferred = Record(MemorySourceKind.Inferred, 1.0);
        var guess = Record(MemorySourceKind.Guess, 1.0);
        var staleCorrection = Record(MemorySourceKind.UserCorrection, 1.0, current: false);
        var highlightedGuess = RecallTestHarness.Retrieved(RecallTestHarness.Entry("[neutral] joke", "s", "g", null, 1, highlight: true, scope: MemoryScope.General) with { SourceKind = MemorySourceKind.Guess });

        var selection = RecallSelector.Select([staleCorrection, guess, inferred, remembered, read, observed, told, integration, correction, highlightedGuess], new RecallContext("g", null, "s", []));

        Assert.Equal(
            [correction, highlightedGuess, integration, told, observed, read, remembered, inferred, guess, staleCorrection],
            selection.Items);
    }

    [Fact]
    public void Selector_FocusSubjectsRankAboveUnfocusedPeers()
    {
        var focused = RecallTestHarness.Retrieved(RecallTestHarness.Entry("[neutral] focused", "s", null, null, 1, scope: MemoryScope.General, subject: "opinion:tea") with { SourceKind = MemorySourceKind.Guess });
        var unfocused = RecallTestHarness.Retrieved(RecallTestHarness.Entry("[neutral] unfocused", "s", null, null, 2, scope: MemoryScope.General) with { SourceKind = MemorySourceKind.Integration });

        var selection = RecallSelector.Select([unfocused, focused], new RecallContext(null, null, null, ["opinion:tea"]));

        Assert.Equal([focused, unfocused], selection.Items);
    }

    [Fact]
    public void BeliefHistory_PicksTheCurrentRecord_RegardlessOfInputOrder()
    {
        var earlier = RecallTestHarness.Retrieved(RecallTestHarness.Entry("[neutral] earlier", "s", null, null, 1, scope: MemoryScope.General), isCurrent: false);
        var current = RecallTestHarness.Retrieved(RecallTestHarness.Entry("[neutral] current", "s", null, null, 2, scope: MemoryScope.General));

        var history = BeliefHistory.From([earlier, current]);

        Assert.Same(current, history.Current);
        Assert.Same(earlier, Assert.Single(history.Earlier));
        Assert.Null(BeliefHistory.From([earlier]).Current);
    }

    [Fact]
    public async Task RecallService_ReachesSessionMemoriesWithoutAGame()
    {
        await using var harness = await RecallTestHarness.CreateAsync();
        var entry = await harness.CommitOneAsync(new AppendMemoryProposal(Guid.NewGuid(), [RecallTestHarness.Entry("[neutral] desktop moment", "session.desk", null, null, 1)]));

        var selection = await RecallService.RecallAsync(harness.Repository, new RecallContext(null, null, "session.desk", []));
        var elsewhere = await RecallService.RecallAsync(harness.Repository, new RecallContext(null, null, "session.other", []));

        Assert.Equal([entry.Record.RecordId], selection.Items.Select(item => item.Record.RecordId));
        Assert.Empty(elsewhere.Items);
    }

    [Fact]
    public void Selector_RespectsRecordAndByteBudgets()
    {
        var records = Enumerable.Range(1, 5)
            .Select(index => RecallTestHarness.Retrieved(RecallTestHarness.Entry(new string('x', 100), "s", null, null, index, scope: MemoryScope.General) with { Confidence = index / 10.0 }))
            .ToArray();

        var byCount = RecallSelector.Select(records, new RecallContext(null, null, null, [], MaximumRecords: 2));
        Assert.Equal([records[4], records[3]], byCount.Items);
        Assert.True(byCount.Truncated);

        var byBytes = RecallSelector.Select(records, new RecallContext(null, null, null, [], MaximumRecollectionBytes: 299));
        Assert.Equal([records[4], records[3]], byBytes.Items);
        Assert.True(byBytes.Truncated);

        var exact = RecallSelector.Select(records, new RecallContext(null, null, null, [], MaximumRecords: 5, MaximumRecollectionBytes: 500));
        Assert.Equal(5, exact.Items.Count);
        Assert.False(exact.Truncated);

        Assert.Throws<ArgumentOutOfRangeException>(() => RecallSelector.Select(records, new RecallContext(null, null, null, [], MaximumRecords: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecallSelector.Select(records, new RecallContext(null, null, null, [], MaximumRecollectionBytes: 0)));
    }

    [Fact]
    public void Selector_DeduplicatesCandidates()
    {
        var record = RecallTestHarness.Retrieved(RecallTestHarness.Entry("[neutral]", "s", null, null, 1, scope: MemoryScope.General));

        var selection = RecallSelector.Select([record, record, record], new RecallContext(null, null, null, []));

        Assert.Single(selection.Items);
    }

    [Fact]
    public void InterestRoots_AreValidatedSortedAndImmutable()
    {
        var roots = InterestRootSet.Create([new InterestRoot("weather", "[neutral] weather."), new InterestRoot("music", "[neutral] music.")]);

        Assert.Equal(["music", "weather"], roots.Roots.Select(root => root.RootId));
        Assert.True(roots.Contains("music"));
        Assert.False(roots.Contains("Music"));
        Assert.False(roots.Contains(null!));
        Assert.Throws<NotSupportedException>(() => ((IList<InterestRoot>)roots.Roots).Add(new InterestRoot("x", "y")));
        Assert.DoesNotContain(typeof(InterestRootSet).GetMethods(BindingFlags.Public | BindingFlags.Instance), method =>
            method.DeclaringType == typeof(InterestRootSet) && (method.Name.StartsWith("Add", StringComparison.Ordinal) || method.Name.StartsWith("Remove", StringComparison.Ordinal) || method.Name.StartsWith("set_", StringComparison.Ordinal)));
        Assert.Equal(roots.Fingerprint, InterestRootSet.Create(roots.Roots.Reverse()).Fingerprint);
        Assert.Matches("^[0-9a-f]{64}$", roots.Fingerprint);

        Assert.Throws<ArgumentException>(() => InterestRootSet.Create([]));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Create([new InterestRoot("a", "x"), new InterestRoot("a", "y")]));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Create([new InterestRoot("Upper", "x")]));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Create([new InterestRoot("-lead", "x")]));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Create([new InterestRoot(new string('a', 65), "x")]));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Create([new InterestRoot("a", " ")]));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Create([new InterestRoot("a", new string('d', 513))]));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Create([new InterestRoot("a", "bell\u0007")]));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Create([null!]));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Create(Enumerable.Range(0, InterestRootSet.MaximumRoots + 1).Select(index => new InterestRoot($"r{index}", "x"))));
        Assert.Equal(InterestRootSet.MaximumRoots, InterestRootSet.Create(Enumerable.Range(0, InterestRootSet.MaximumRoots).Select(index => new InterestRoot($"r{index}", "x"))).Roots.Count);
    }

    [Fact]
    public void InterestRoots_FingerprintIsUnambiguous()
    {
        var joined = InterestRootSet.Create([new InterestRoot("a", "x\nb\ty")]);
        var split = InterestRootSet.Create([new InterestRoot("a", "x"), new InterestRoot("b", "y")]);

        Assert.NotEqual(joined.Fingerprint, split.Fingerprint);
    }

    [Fact]
    public void InterestRoots_LoadRequiresVersionAndVerifiedFingerprint()
    {
        var roots = InterestRootSet.Create([new InterestRoot("music", "[neutral] music.")]);
        var json = roots.ToJson();

        Assert.Equal(roots.Roots, InterestRootSet.Load(json).Roots);
        Assert.Throws<ArgumentException>(() => InterestRootSet.Load(json.Replace("[neutral] music.", "[neutral] tampered.", StringComparison.Ordinal)));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Load(json.Replace(roots.Fingerprint, new string('0', 64), StringComparison.Ordinal)));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Load("{\"version\":1,\"roots\":[{\"rootId\":\"music\",\"description\":\"[neutral] music.\"}]}"));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Load(json.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal)));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Load(json.Replace("\"version\":1", "\"version\":1,\"extra\":true", StringComparison.Ordinal)));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Load("{\"version\":1,\"fingerprint\":\"x\"}"));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Load("null"));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Load("{"));
        Assert.Throws<ArgumentException>(() => InterestRootSet.Load(" "));
    }

    [Fact]
    public void GeneratedSeeds_AreValidatedAgainstAnExistingRoot()
    {
        var roots = InterestRootSet.Create([new InterestRoot("music", "[neutral] music.")]);
        var operation = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => GeneratedSeedPlanner.Plan(roots, "weather", "[neutral seed]", operation, Now));
        Assert.Throws<ArgumentException>(() => GeneratedSeedPlanner.Plan(roots, null!, "[neutral seed]", operation, Now));
        Assert.Throws<ArgumentException>(() => GeneratedSeedPlanner.Plan(roots, "music", " ", operation, Now));
        Assert.Throws<ArgumentException>(() => GeneratedSeedPlanner.Plan(roots, "music", "escape\u001b[2J", operation, Now));
        Assert.Throws<ArgumentException>(() => GeneratedSeedPlanner.Plan(roots, "music", new string('s', GeneratedSeedPlanner.MaximumSeedCharacters + 1), operation, Now));
        Assert.Throws<ArgumentNullException>(() => GeneratedSeedPlanner.Plan(null!, "music", "[neutral seed]", operation, Now));

        var seed = GeneratedSeedPlanner.Plan(roots, "music", new string('s', GeneratedSeedPlanner.MaximumSeedCharacters), operation, Now);
        Assert.Equal(ConsolidationPlanner.DeriveRecordId(operation, 0), Assert.Single(seed.Records).RecordId);
    }

    [Fact]
    public void RecallAssembly_ReferencesOnlyMemory_AndExposesNoMutationOfCommittedMemory()
    {
        var assembly = typeof(ConsolidationPlanner).Assembly;
        var companionReferences = assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith("CompanionCore.", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(["CompanionCore.Memory"], companionReferences);

        var publicMethods = assembly.GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(method => method.Name)
            .ToArray();
        Assert.DoesNotContain(publicMethods, name =>
            name.Contains("Delete", StringComparison.Ordinal)
            || name.Contains("Remove", StringComparison.Ordinal)
            || name.StartsWith("Update", StringComparison.Ordinal)
            || name.Contains("Compact", StringComparison.Ordinal));
    }
}
