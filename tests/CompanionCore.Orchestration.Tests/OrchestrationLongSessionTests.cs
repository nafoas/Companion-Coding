using CompanionCore.Memory;
using CompanionCore.Recall;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// Long sessions (deferred WIRE-01 D1): every original of a session is consolidated, however
/// many there are, in parts that each fit one atomic append, and a replay adds nothing.
/// </summary>
public sealed class OrchestrationLongSessionTests
{
    private const string Session = "target-session:long";

    [Fact]
    public async Task ALongSessionWithManyHighlights_IsConsolidatedCompletely_InIdempotentParts()
    {
        await using var harness = await CreateAsync();
        var (originals, highlights) = await SeedLongSessionAsync(harness);

        await ReplayAsync(harness);

        var summaries = await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(Session));
        // Each part is one atomic append; ~200 highlights cannot fit in one 128-record operation.
        Assert.True(summaries.Select(summary => summary.LocalOperationId).Distinct().Count() >= 2, "expected several parts");
        var summarized = summaries.SelectMany(summary => summary.Record.Links.Select(link => link.TargetRecordId)).ToArray();
        Assert.Equal(originals.Count, summarized.Length);
        Assert.Equal(originals.Order(), summarized.Order());
        Assert.Equal(originals.Count, summaries.Sum(summary => RecallMetadata.Parse(summary.Record.RetrievalMetadataJson).ConsolidatedCount));

        var verbatim = await AllHighlightsAsync(harness);
        Assert.Equal(highlights.Order(), verbatim.Order());
        Assert.Empty(await harness.PendingConsolidationsAsync());
        Assert.Equal(1500, harness.Notices.Single(n => n.Kind == CompanionNoticeKind.Consolidated).Count);
        Assert.Equal(0, harness.Orchestrator.Faults);

        // A replay of the same intent reproduces every part exactly and appends nothing.
        await ReplayAsync(harness);
        Assert.Equal(summaries.Count, (await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(Session))).Count);
        Assert.Equal(highlights.Count, (await AllHighlightsAsync(harness)).Count);
        Assert.Empty(await harness.PendingConsolidationsAsync());
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task ARefusedPart_StopsTheConsolidation_AndLeavesTheIntentQueued()
    {
        await using var harness = await CreateAsync();
        await SeedLongSessionAsync(harness);
        var intent = CompanionOrchestrator.DeriveId("consolidation", Session);

        // The first part's operation id is already taken by different content, so that part is
        // refused; no later part may be committed past it.
        await harness.CommitSessionOriginalAsync("target-session:blocker", CompanionOrchestrator.PartOperationId(intent, 0));
        await ReplayAsync(harness);

        Assert.Empty(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(Session)));
        Assert.Empty(await AllHighlightsAsync(harness));
        var queued = Assert.Single(await harness.PendingConsolidationsAsync());
        Assert.Equal((Session, intent), (queued.Session, queued.OperationId));
        Assert.DoesNotContain(harness.Notices, n => n.Kind == CompanionNoticeKind.Consolidated);
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(256, 0, 1)]
    [InlineData(257, 0, 2)]
    [InlineData(10, 10, 11)]
    [InlineData(127, 127, 128)]
    public void PlannedRecords_CountsSummariesAndHighlights(int entries, int highlights, int expected) =>
        Assert.Equal(expected, CompanionOrchestrator.PlannedRecords(entries, highlights));

    [Fact]
    public void PartZero_KeepsTheIntentsOperation_AndLaterPartsAreDistinctAndStable()
    {
        var intent = Guid.NewGuid();
        Assert.Equal(intent, CompanionOrchestrator.PartOperationId(intent, 0));
        Assert.Equal(CompanionOrchestrator.PartOperationId(intent, 3), CompanionOrchestrator.PartOperationId(intent, 3));
        Assert.Equal(5, Enumerable.Range(0, 5).Select(part => CompanionOrchestrator.PartOperationId(intent, part)).Distinct().Count());
    }

    private static async Task<(List<Guid> Originals, HashSet<Guid> Highlights)> SeedLongSessionAsync(OrchestrationHarness harness)
    {
        var originals = new List<Guid>();
        var highlights = new HashSet<Guid>();
        for (var batch = 0; batch < 12; batch++)
        {
            var drafts = Enumerable.Range(0, 125).Select(index =>
            {
                var number = (batch * 125) + index;
                var highlight = number % 15 == 0 || number < 100;
                var draft = new MemoryRecordDraft
                {
                    RecordId = Guid.NewGuid(),
                    CreatedAtUtc = harness.Time.GetUtcNow(),
                    Scope = MemoryScope.Session,
                    SourceKind = MemorySourceKind.Observed,
                    Confidence = 0.8,
                    SubjectKey = $"synthetic.long.{number}",
                    EntityReferences = ["synthetic.entity"],
                    SessionReference = Session,
                    VisibleRecollection = $"[neutral memory] moment {number}.",
                    RetrievalMetadataJson = new RecallMetadata { Highlight = highlight }.ToJson(),
                };
                if (highlight)
                {
                    highlights.Add(draft.RecordId);
                }

                return draft;
            }).ToArray();
            originals.AddRange(drafts.Select(draft => draft.RecordId));
            Assert.True((await harness.Host.Repository.WriteGate.SubmitAsync(new AppendMemoryProposal(Guid.NewGuid(), drafts))).IsAccepted);
        }

        return (originals, highlights);
    }

    private static async Task ReplayAsync(OrchestrationHarness harness)
    {
        await harness.Host.DisposeAsync();
        await harness.Host.State.PutAsync(
            CompanionOrchestrator.ConsolidationStateName,
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[]
            {
                new CompanionOrchestrator.PendingConsolidation(Session, CompanionOrchestrator.DeriveId("consolidation", Session), harness.Time.GetUtcNow(), null),
            }));
        await harness.OpenHostAsync();
    }

    private static async Task<List<Guid>> AllHighlightsAsync(OrchestrationHarness harness)
    {
        var sources = new List<Guid>();
        Guid? after = null;
        while (true)
        {
            var page = await harness.Host.Repository.RetrieveSessionPageAsync(Session, after, MemoryQuery.MaximumLimit);
            sources.AddRange(page
                .Where(memory => RecallMetadata.Parse(memory.Record.RetrievalMetadataJson).Kind == RecallRecordKind.Highlight)
                .Select(memory => Assert.Single(memory.Record.Links).TargetRecordId));
            if (page.Count < MemoryQuery.MaximumLimit)
            {
                return sources;
            }

            after = page[^1].Record.RecordId;
        }
    }
}
