namespace CompanionCore.Memory.Tests;

public sealed class MemoryQueryTests
{
    public static TheoryData<string> InvalidQueries => new()
    {
        "no-filter",
        "scope-only",
        "empty-record-ids-only",
        "limit-zero",
        "limit-over-maximum",
        "blank-game",
        "overlong-save",
        "blank-prefix",
        "undefined-scope",
        "empty-record-id",
        "too-many-record-ids",
    };

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task InvalidOrUnboundedQueries_AreRefused(string name)
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var query = name switch
        {
            "no-filter" => new MemoryQuery(),
            "scope-only" => new MemoryQuery { Scopes = [MemoryScope.General] },
            "empty-record-ids-only" => new MemoryQuery { RecordIds = [] },
            "limit-zero" => new MemoryQuery { GameReference = "synthetic.game", Limit = 0 },
            "limit-over-maximum" => new MemoryQuery { GameReference = "synthetic.game", Limit = MemoryQuery.MaximumLimit + 1 },
            "blank-game" => new MemoryQuery { GameReference = "  " },
            "overlong-save" => new MemoryQuery { SaveReference = new string('s', 513) },
            "blank-prefix" => new MemoryQuery { SubjectPrefix = string.Empty },
            "undefined-scope" => new MemoryQuery { GameReference = "synthetic.game", Scopes = [(MemoryScope)99] },
            "empty-record-id" => new MemoryQuery { RecordIds = [Guid.Empty] },
            "too-many-record-ids" => new MemoryQuery
            {
                RecordIds = Enumerable.Range(0, MemoryQuery.MaximumRecordIds + 1).Select(_ => Guid.NewGuid()).ToArray(),
            },
            _ => throw new InvalidOperationException(name),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => repository.RetrieveAsync(query));
    }

    [Fact]
    public async Task MaximumBounds_AreAccepted()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var ids = Enumerable.Range(0, MemoryQuery.MaximumRecordIds).Select(_ => Guid.NewGuid()).ToArray();

        Assert.Empty(await repository.RetrieveAsync(new MemoryQuery { RecordIds = ids, Limit = MemoryQuery.MaximumLimit }));
        Assert.Empty(await repository.RetrieveAsync(new MemoryQuery { SaveReference = new string('s', 512), Limit = 1 }));
    }

    [Fact]
    public async Task Filters_SelectExactlyTheMatchingRecords()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var gameA = Variant(SyntheticMemory.Record(subjectKey: "lore:a_b:x", scope: MemoryScope.Game), "game.a", "save.1", "session.1");
        var gameASave2 = Variant(SyntheticMemory.Record(subjectKey: "lore:a%b:y", scope: MemoryScope.Save), "game.a", "save.2", "session.2");
        var gameB = Variant(SyntheticMemory.Record(subjectKey: "lore:axb:z", scope: MemoryScope.Session), "game.b", "save.1", "session.1");
        var general = Variant(SyntheticMemory.Record(subjectKey: "opinion:tea", scope: MemoryScope.General), null, null, null);
        Assert.True((await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(gameA, gameASave2, gameB, general))).IsAccepted);

        Assert.Equal(Ids(gameA, gameASave2), Ids(await repository.RetrieveAsync(new MemoryQuery { GameReference = "game.a" })));
        Assert.Equal(Ids(gameA, gameB), Ids(await repository.RetrieveAsync(new MemoryQuery { SaveReference = "save.1" })));
        Assert.Equal(Ids(gameA), Ids(await repository.RetrieveAsync(new MemoryQuery { GameReference = "game.a", SaveReference = "save.1" })));
        Assert.Equal(Ids(gameASave2), Ids(await repository.RetrieveAsync(new MemoryQuery { SessionReference = "session.2" })));
        Assert.Equal(Ids(gameB), Ids(await repository.RetrieveAsync(new MemoryQuery { SessionReference = "session.1", Scopes = [MemoryScope.Session, MemoryScope.Session] })));
        Assert.Equal(Ids(gameA, gameASave2), Ids(await repository.RetrieveAsync(new MemoryQuery { GameReference = "game.a", Scopes = [MemoryScope.Game, MemoryScope.Save] })));
        Assert.Equal(Ids(general, gameA), Ids(await repository.RetrieveAsync(new MemoryQuery { RecordIds = [general.RecordId, gameA.RecordId, general.RecordId] })));

        // Prefixes are literal: SQL wildcard characters never widen the match.
        Assert.Equal(Ids(gameA), Ids(await repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "lore:a_b" })));
        Assert.Equal(Ids(gameASave2), Ids(await repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "lore:a%" })));
        Assert.Equal(Ids(gameA, gameASave2, gameB), Ids(await repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "lore:" })));
        Assert.Empty(await repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "LORE:" }));
    }

    [Fact]
    public async Task Limit_BoundsResults_AfterCurrentFirstOrdering()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        const string subject = "synthetic.limited";
        var earlier = SyntheticMemory.Record(subjectKey: subject, confidence: 1.0);
        var later = SyntheticMemory.Record(
            subjectKey: subject,
            createdAtUtc: SyntheticMemory.BaselineUtc.AddMinutes(1),
            confidence: 0.1,
            links: [new MemoryLink(earlier.RecordId, MemoryLinkKind.Supersedes)]);
        var other = SyntheticMemory.Record(subjectKey: "synthetic.other", confidence: 0.5);
        Assert.True((await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(earlier, later, other))).IsAccepted);

        var limited = await repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.", Limit = 2 });

        Assert.Equal(2, limited.Count);
        Assert.All(limited, memory => Assert.True(memory.IsCurrent));
        Assert.DoesNotContain(limited, memory => memory.Record.RecordId == earlier.RecordId);
        var all = await repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic." });
        Assert.Equal(3, all.Count);
        Assert.Equal(earlier.RecordId, all[2].Record.RecordId);
        Assert.False(all[2].IsCurrent);
    }

    [Fact]
    public async Task QueryResults_MatchSubjectRetrieval_IncludingChecksumsAndLinks()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var original = SyntheticMemory.Record(subjectKey: "synthetic.same-path");
        var correction = SyntheticMemory.Record(
            subjectKey: "synthetic.same-path",
            sourceKind: MemorySourceKind.UserCorrection,
            links: [new MemoryLink(original.RecordId, MemoryLinkKind.Corrects)]);
        Assert.True((await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(original, correction))).IsAccepted);

        var bySubject = await repository.RetrieveBySubjectAsync("synthetic.same-path");
        var byQuery = await repository.RetrieveAsync(new MemoryQuery { RecordIds = [original.RecordId, correction.RecordId] });

        Assert.Equal(bySubject.Select(memory => (memory.Record.RecordId, memory.RecordChecksum, memory.IsCurrent, memory.JournalSequence)),
            byQuery.Select(memory => (memory.Record.RecordId, memory.RecordChecksum, memory.IsCurrent, memory.JournalSequence)));
        Assert.Equal(bySubject[0].Record.Links, byQuery[0].Record.Links);
    }

    [Fact]
    public async Task TamperedRecord_FailsVerification_ThroughQuery()
    {
        using var directory = new MemoryTestDirectory();
        var record = SyntheticMemory.Record(subjectKey: "synthetic.query-tamper");
        await using (var repository = await directory.OpenRepositoryAsync())
        {
            Assert.True((await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(record))).IsAccepted);
        }

        await SyntheticMemory.ExecuteSqlAsync(
            directory.Location.DatabasePath,
            """
            DROP TRIGGER immutable_memory_records_update;
            UPDATE memory_records
            SET visible_recollection = 'tampered content';
            CREATE TRIGGER immutable_memory_records_update
            BEFORE UPDATE ON memory_records
            BEGIN SELECT RAISE(ABORT, 'append-only committed memory'); END;
            """);

        await using var reopened = await directory.OpenRepositoryAsync();
        await Assert.ThrowsAsync<MemoryIntegrityException>(() =>
            reopened.RetrieveAsync(new MemoryQuery { GameReference = "synthetic.game" }));
        await Assert.ThrowsAsync<MemoryIntegrityException>(() =>
            reopened.RetrieveAsync(new MemoryQuery { RecordIds = [record.RecordId] }));
    }

    [Fact]
    public async Task DisposedRepository_RefusesQueries()
    {
        using var directory = new MemoryTestDirectory();
        var repository = await directory.OpenRepositoryAsync();
        await repository.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            repository.RetrieveAsync(new MemoryQuery { GameReference = "synthetic.game" }));
    }

    private static MemoryRecordDraft Variant(MemoryRecordDraft record, string? game, string? save, string? session) =>
        record with { GameReference = game, SaveReference = save, SessionReference = session };

    private static Guid[] Ids(params MemoryRecordDraft[] records) =>
        records.Select(record => record.RecordId).Order().ToArray();

    private static Guid[] Ids(IReadOnlyList<RetrievedMemory> memories) =>
        memories.Select(memory => memory.Record.RecordId).Order().ToArray();
}
