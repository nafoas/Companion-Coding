namespace CompanionCore.Memory.Tests;

public sealed class MemoryRetrievalTests
{
    [Fact]
    public async Task CorrectionRanksFirst_AndOriginalRemainsHistorical()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        const string subject = "synthetic.corrected-subject";
        var original = SyntheticMemory.Record(
            subjectKey: subject,
            sourceKind: MemorySourceKind.Observed,
            confidence: 0.95,
            visibleRecollection: "Synthetic original conception.");
        var correction = SyntheticMemory.Record(
            subjectKey: subject,
            createdAtUtc: SyntheticMemory.BaselineUtc.AddMinutes(1),
            sourceKind: MemorySourceKind.UserCorrection,
            confidence: 0.8,
            visibleRecollection: "Synthetic correction.",
            links: [new MemoryLink(original.RecordId, MemoryLinkKind.Corrects)]);

        Assert.True((await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(original))).IsAccepted);
        Assert.True((await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(correction))).IsAccepted);

        var retrieved = await repository.RetrieveBySubjectAsync(subject);

        Assert.Equal(2, retrieved.Count);
        Assert.Equal(correction.RecordId, retrieved[0].Record.RecordId);
        Assert.True(retrieved[0].IsCurrent);
        Assert.Equal(original.RecordId, retrieved[1].Record.RecordId);
        Assert.False(retrieved[1].IsCurrent);
        Assert.Contains(
            retrieved[0].Record.Links,
            link => link.Kind == MemoryLinkKind.Corrects
                && link.TargetRecordId == original.RecordId);
    }

    [Fact]
    public async Task SupersessionMarksOnlyTargetNonCurrent()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        const string subject = "synthetic.superseded-subject";
        var earlier = SyntheticMemory.Record(subjectKey: subject);
        var later = SyntheticMemory.Record(
            subjectKey: subject,
            createdAtUtc: SyntheticMemory.BaselineUtc.AddMinutes(2),
            links: [new MemoryLink(earlier.RecordId, MemoryLinkKind.Supersedes)]);

        var result = await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(earlier, later));
        var retrieved = await repository.RetrieveBySubjectAsync(subject);

        Assert.Equal(WriteGateStatus.Committed, result.Status);
        Assert.Equal(later.RecordId, retrieved[0].Record.RecordId);
        Assert.True(retrieved[0].IsCurrent);
        Assert.False(retrieved.Single(item => item.Record.RecordId == earlier.RecordId).IsCurrent);
    }

    [Fact]
    public async Task RecurrencePreservesBothOccurrencesAsCurrent()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        const string subject = "synthetic.recurring-subject";
        var first = SyntheticMemory.Record(
            subjectKey: subject,
            createdAtUtc: SyntheticMemory.BaselineUtc);
        var recurrence = SyntheticMemory.Record(
            subjectKey: subject,
            createdAtUtc: SyntheticMemory.BaselineUtc.AddHours(1),
            links: [new MemoryLink(first.RecordId, MemoryLinkKind.RecursWith)]);

        var result = await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(first, recurrence));
        var retrieved = await repository.RetrieveBySubjectAsync(subject);

        Assert.Equal(WriteGateStatus.Committed, result.Status);
        Assert.Equal(2, retrieved.Count);
        Assert.All(retrieved, memory => Assert.True(memory.IsCurrent));
        Assert.True(new HashSet<Guid> { first.RecordId, recurrence.RecordId }
            .SetEquals(retrieved.Select(memory => memory.Record.RecordId)));
    }

    [Fact]
    public async Task SourceAuthorityAndConfidenceOutrankSimpleRecency()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        const string subject = "synthetic.ranking-subject";
        var authoritativeOlder = SyntheticMemory.Record(
            subjectKey: subject,
            createdAtUtc: SyntheticMemory.BaselineUtc,
            sourceKind: MemorySourceKind.UserCorrection,
            confidence: 0.6);
        var guessNewer = SyntheticMemory.Record(
            subjectKey: subject,
            createdAtUtc: SyntheticMemory.BaselineUtc.AddDays(1),
            sourceKind: MemorySourceKind.Guess,
            confidence: 1.0);
        var sameSourceLowerConfidenceNewer = SyntheticMemory.Record(
            subjectKey: subject,
            createdAtUtc: SyntheticMemory.BaselineUtc.AddDays(2),
            sourceKind: MemorySourceKind.UserCorrection,
            confidence: 0.4);

        Assert.True((await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(
                authoritativeOlder,
                guessNewer,
                sameSourceLowerConfidenceNewer))).IsAccepted);

        var retrieved = await repository.RetrieveBySubjectAsync(subject);

        Assert.Equal(authoritativeOlder.RecordId, retrieved[0].Record.RecordId);
        Assert.Equal(sameSourceLowerConfidenceNewer.RecordId, retrieved[1].Record.RecordId);
        Assert.Equal(guessNewer.RecordId, retrieved[2].Record.RecordId);
    }

    [Fact]
    public async Task RetrievalUsesExactSubjectOnly()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var exact = SyntheticMemory.Record(subjectKey: "synthetic.exact");
        var near = SyntheticMemory.Record(subjectKey: "synthetic.exact.near");
        await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(exact, near));

        var retrieved = await repository.RetrieveBySubjectAsync("synthetic.exact");

        Assert.Equal(exact.RecordId, Assert.Single(retrieved).Record.RecordId);
        Assert.Empty(await repository.RetrieveBySubjectAsync("Synthetic.Exact"));
    }
}
