namespace CompanionCore.Memory.Tests;

public sealed class MemoryPagingTests
{
    [Fact]
    public async Task SessionPaging_EnumeratesEveryRecordOfOneSessionOnce_AndNothingElse()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var expected = new List<Guid>();
        for (var batch = 0; batch < 9; batch++)
        {
            var records = Enumerable.Range(0, 128)
                .Select(index => SyntheticMemory.Record(subjectKey: $"long:{batch}:{index}") with
                {
                    SessionReference = "session.long",

                    // Varied confidence: the relevance order differs from the record-id order.
                    Confidence = 0.1 + ((index % 9) / 10.0),
                })
                .ToArray();
            expected.AddRange(records.Select(record => record.RecordId));
            Assert.True((await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(records))).IsAccepted);
        }

        Assert.True((await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(
            SyntheticMemory.Record(subjectKey: "other:session") with { SessionReference = "session.other" }))).IsAccepted);

        var seen = new List<Guid>();
        Guid? after = null;
        while (true)
        {
            var page = await repository.RetrieveSessionPageAsync("session.long", after, 500);
            Assert.All(page, memory => Assert.Equal("session.long", memory.Record.SessionReference));
            seen.AddRange(page.Select(memory => memory.Record.RecordId));
            if (page.Count < 500)
            {
                break;
            }

            after = page[^1].Record.RecordId;
        }

        Assert.Equal(expected.Order(), seen.Order());
        Assert.Equal(seen.Distinct().Count(), seen.Count);
        Assert.Equal(seen.Select(id => id.ToString("D")).Order(StringComparer.Ordinal), seen.Select(id => id.ToString("D")));
        Assert.Empty(await repository.RetrieveSessionPageAsync("session.missing", null, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.RetrieveSessionPageAsync("session.long", Guid.Empty, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.RetrieveSessionPageAsync("session.long", null, MemoryQuery.MaximumLimit + 1));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.RetrieveSessionPageAsync(" ", null, 10));
    }

    [Fact]
    public async Task KeysetPaging_EnumeratesEveryRecordOnceInRecordIdOrder()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var expected = new List<Guid>();
        for (var batch = 0; batch < 9; batch++)
        {
            var records = Enumerable.Range(0, 128)
                .Select(index => SyntheticMemory.Record(subjectKey: $"page:{batch}:{index}"))
                .ToArray();
            expected.AddRange(records.Select(record => record.RecordId));
            Assert.True((await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(records))).IsAccepted);
        }

        Assert.True((await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(SyntheticMemory.Record(subjectKey: "other:x")))).IsAccepted);

        var seen = new List<Guid>();
        Guid? after = null;
        var pages = 0;
        while (true)
        {
            var page = await repository.RetrieveSubjectPrefixPageAsync("page:", after, 500);
            pages++;
            seen.AddRange(page.Select(memory => memory.Record.RecordId));
            if (page.Count < 500)
            {
                break;
            }

            after = page[^1].Record.RecordId;
        }

        Assert.Equal(3, pages);
        Assert.Equal(1152, seen.Count);
        Assert.Equal(seen.Distinct().Count(), seen.Count);
        Assert.Equal(expected.Order(), seen.Order());
        Assert.Equal(seen.Select(id => id.ToString("D")).Order(StringComparer.Ordinal), seen.Select(id => id.ToString("D")));
        Assert.Empty(await repository.RetrieveSubjectPrefixPageAsync("page:", seen[^1], 500));
    }

    [Fact]
    public async Task KeysetPaging_IsVerifiedAndBounded()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var record = SyntheticMemory.Record(subjectKey: "page:only");
        Assert.True((await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(record))).IsAccepted);

        var page = await repository.RetrieveSubjectPrefixPageAsync("page:", null, 1);
        Assert.Equal(record.RecordId, Assert.Single(page).Record.RecordId);
        Assert.Equal((await repository.RetrieveBySubjectAsync("page:only"))[0].RecordChecksum, page[0].RecordChecksum);
        Assert.Empty(await repository.RetrieveSubjectPrefixPageAsync("page_", null, 10));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.RetrieveSubjectPrefixPageAsync(" ", null, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.RetrieveSubjectPrefixPageAsync("page:", null, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.RetrieveSubjectPrefixPageAsync("page:", null, MemoryQuery.MaximumLimit + 1));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.RetrieveSubjectPrefixPageAsync("page:", Guid.Empty, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.RetrieveSubjectPrefixPageAsync(new string('p', 513), null, 10));
    }
}
