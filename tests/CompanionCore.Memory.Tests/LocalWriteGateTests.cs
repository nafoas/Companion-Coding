namespace CompanionCore.Memory.Tests;

public sealed class LocalWriteGateTests
{
    [Fact]
    public async Task ValidAppend_RoundTripsEveryFieldAcrossReopen()
    {
        using var directory = new MemoryTestDirectory();
        var source = SyntheticMemory.Record(
            subjectKey: "synthetic.provenance",
            sourceKind: MemorySourceKind.Read,
            visibleRecollection: "Synthetic source material.");
        var expected = SyntheticMemory.Record(
            subjectKey: "synthetic.roundtrip",
            createdAtUtc: SyntheticMemory.BaselineUtc.AddMinutes(3),
            scope: MemoryScope.Save,
            sourceKind: MemorySourceKind.Told,
            confidence: 0.875,
            visibleRecollection: "A complete neutral round-trip fixture.",
            links: [new MemoryLink(source.RecordId, MemoryLinkKind.Source)]);
        var operationId = Guid.NewGuid();

        await using (var repository = await directory.OpenRepositoryAsync())
        {
            var result = await repository.WriteGate.SubmitAsync(
                new AppendMemoryProposal(operationId, [source, expected]));
            Assert.Equal(WriteGateStatus.Committed, result.Status);
            Assert.Equal(2, result.RecordIds.Count);
        }

        await using (var reopened = await directory.OpenRepositoryAsync())
        {
            var actual = Assert.Single(await reopened.RetrieveBySubjectAsync(expected.SubjectKey));

            Assert.Equal(operationId, actual.LocalOperationId);
            Assert.Equal(expected.RecordId, actual.Record.RecordId);
            Assert.Equal(expected.SchemaVersion, actual.Record.SchemaVersion);
            Assert.Equal(expected.CreatedAtUtc, actual.Record.CreatedAtUtc);
            Assert.Equal(expected.Scope, actual.Record.Scope);
            Assert.Equal(expected.SourceKind, actual.Record.SourceKind);
            Assert.Equal(expected.Confidence, actual.Record.Confidence);
            Assert.Equal(expected.SubjectKey, actual.Record.SubjectKey);
            Assert.Equal(
                expected.EntityReferences.Order().ToArray(),
                actual.Record.EntityReferences.Order().ToArray());
            Assert.Equal(expected.ApplicationReference, actual.Record.ApplicationReference);
            Assert.Equal(expected.GameReference, actual.Record.GameReference);
            Assert.Equal(expected.SaveReference, actual.Record.SaveReference);
            Assert.Equal(expected.SessionReference, actual.Record.SessionReference);
            Assert.Equal(expected.VisibleRecollection, actual.Record.VisibleRecollection);
            Assert.Equal(
                "{\"category\":\"synthetic\",\"priority\":2}",
                actual.Record.RetrievalMetadataJson);
            Assert.Equal(expected.Links.ToArray(), actual.Record.Links.ToArray());
            Assert.True(actual.IsCurrent);
            Assert.True(actual.JournalSequence > 0);
            Assert.Equal(64, actual.RecordChecksum.Length);
        }
    }

    [Fact]
    public async Task UnknownUpdateAndDeleteProposals_AreRejectedBeforeDurability()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        IAutomatedWriteProposal[] rejected =
        [
            new SyntheticUnknownProposal("memory.update.v1"),
            new SyntheticUnknownProposal("memory.delete.v1"),
            new SyntheticUnknownProposal(AppendMemoryProposal.AllowlistedOperationName),
        ];

        foreach (var proposal in rejected)
        {
            var result = await repository.WriteGate.SubmitAsync(proposal);
            Assert.Equal(WriteGateStatus.Rejected, result.Status);
            Assert.Equal(WriteGateRejectionReason.OperationNotAllowlisted, result.RejectionReason);
        }

        Assert.Equal((0L, 0L, 0L), await repository.Store.ReadCountsAsync(default));
        Assert.Equal(0, repository.Journal.HighestAppendSequence);
        Assert.Equal(0, repository.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task IdenticalRetryIsIdempotent_ChangedPayloadIsConflict()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var operationId = Guid.NewGuid();
        var record = SyntheticMemory.Record();
        var proposal = new AppendMemoryProposal(operationId, [record]);

        var first = await repository.WriteGate.SubmitAsync(proposal);
        var retry = await repository.WriteGate.SubmitAsync(proposal);
        var conflict = await repository.WriteGate.SubmitAsync(
            new AppendMemoryProposal(
                operationId,
                [record with { VisibleRecollection = "Different synthetic content." }]));

        Assert.Equal(WriteGateStatus.Committed, first.Status);
        Assert.Equal(WriteGateStatus.AlreadyCommitted, retry.Status);
        Assert.Equal(WriteGateStatus.Conflict, conflict.Status);
        Assert.Equal(WriteGateRejectionReason.OperationConflict, conflict.RejectionReason);
        Assert.Equal((1L, 1L, 0L), await repository.Store.ReadCountsAsync(default));
        Assert.Equal(1, repository.Journal.HighestAppendSequence);
        Assert.Equal(1, repository.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task ProposalSnapshotsMutableInputBeforeSubmission()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var mutableEntities = new List<string> { "original.entity" };
        var mutableRecords = new List<MemoryRecordDraft>
        {
            SyntheticMemory.Record() with { EntityReferences = mutableEntities },
        };
        var proposal = new AppendMemoryProposal(Guid.NewGuid(), mutableRecords);

        mutableEntities[0] = "mutated.entity";
        mutableRecords.Clear();
        var result = await repository.WriteGate.SubmitAsync(proposal);

        Assert.Equal(WriteGateStatus.Committed, result.Status);
        var retrieved = Assert.Single(await repository.RetrieveBySubjectAsync("synthetic.subject"));
        Assert.Equal(new[] { "original.entity" }, retrieved.Record.EntityReferences.ToArray());
    }

    [Fact]
    public async Task InvalidBatch_IsRejectedAtomicallyWithoutJournalWrite()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var valid = SyntheticMemory.Record(subjectKey: "synthetic.valid");
        var invalid = SyntheticMemory.Record(
            subjectKey: "synthetic.invalid",
            confidence: 1.01);

        var result = await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(valid, invalid));

        Assert.Equal(WriteGateStatus.Rejected, result.Status);
        Assert.Equal(WriteGateRejectionReason.InvalidProposal, result.RejectionReason);
        Assert.Equal((0L, 0L, 0L), await repository.Store.ReadCountsAsync(default));
        Assert.Equal(0, repository.Journal.HighestAppendSequence);
    }

    [Fact]
    public async Task MalformedAndOversizedRecords_AreRejectedBeforeDurability()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var invalidRecords = new[]
        {
            SyntheticMemory.Record(recordId: Guid.Empty),
            SyntheticMemory.Record(createdAtUtc: SyntheticMemory.BaselineUtc.ToOffset(TimeSpan.FromHours(1))),
            SyntheticMemory.Record() with { RetrievalMetadataJson = "not-json" },
            SyntheticMemory.Record(visibleRecollection: new string('x', 16 * 1024 + 1)),
            SyntheticMemory.Record() with { SubjectKey = string.Empty },
        };

        foreach (var record in invalidRecords)
        {
            var result = await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(record));
            Assert.Equal(WriteGateStatus.Rejected, result.Status);
            Assert.Equal(WriteGateRejectionReason.InvalidProposal, result.RejectionReason);
        }

        Assert.Equal((0L, 0L, 0L), await repository.Store.ReadCountsAsync(default));
        Assert.Equal(0, repository.Journal.HighestAppendSequence);
    }

    [Fact]
    public async Task MissingAndCrossSubjectRelationshipTargets_AreRejectedAtomically()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var missingTarget = SyntheticMemory.Record(
            links: [new MemoryLink(Guid.NewGuid(), MemoryLinkKind.Corrects)]);
        var target = SyntheticMemory.Record(subjectKey: "synthetic.first-subject");
        var crossSubject = SyntheticMemory.Record(
            subjectKey: "synthetic.second-subject",
            links: [new MemoryLink(target.RecordId, MemoryLinkKind.Supersedes)]);

        var missingResult = await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(missingTarget));
        var crossSubjectResult = await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(target, crossSubject));

        Assert.Equal(WriteGateStatus.Rejected, missingResult.Status);
        Assert.Equal(WriteGateStatus.Rejected, crossSubjectResult.Status);
        Assert.Equal((0L, 0L, 0L), await repository.Store.ReadCountsAsync(default));
        Assert.Equal(0, repository.Journal.HighestAppendSequence);
    }

    [Fact]
    public async Task DuplicateLinks_AreRejectedAsNonCanonical()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var target = SyntheticMemory.Record(subjectKey: "synthetic.duplicate-link");
        var link = new MemoryLink(target.RecordId, MemoryLinkKind.Source);
        var source = SyntheticMemory.Record(
            subjectKey: "synthetic.duplicate-link",
            links: [link, link]);

        var result = await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(target, source));

        Assert.Equal(WriteGateStatus.Rejected, result.Status);
        Assert.Equal((0L, 0L, 0L), await repository.Store.ReadCountsAsync(default));
        Assert.Equal(0, repository.Journal.HighestAppendSequence);
    }

    private sealed record SyntheticUnknownProposal(string OperationName) : IAutomatedWriteProposal
    {
        public Guid LocalOperationId { get; } = Guid.NewGuid();
    }
}
