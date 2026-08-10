namespace CompanionCore.Memory.Tests;

public sealed class MemoryIntegrityTests
{
    [Fact]
    public async Task RecordContentTampering_FailsChecksumValidationAfterReopen()
    {
        using var directory = new MemoryTestDirectory();
        const string subject = "synthetic.record-tamper";
        await CommitOneAsync(directory, SyntheticMemory.Record(subjectKey: subject));

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
            reopened.RetrieveBySubjectAsync(subject));
    }

    [Fact]
    public async Task OperationChecksumTampering_FailsEnvelopeValidationAfterReopen()
    {
        using var directory = new MemoryTestDirectory();
        const string subject = "synthetic.operation-tamper";
        await CommitOneAsync(directory, SyntheticMemory.Record(subjectKey: subject));

        await SyntheticMemory.ExecuteSqlAsync(
            directory.Location.DatabasePath,
            """
            DROP TRIGGER immutable_append_operations_update;
            UPDATE append_operations
            SET operation_checksum = '0000000000000000000000000000000000000000000000000000000000000000';
            CREATE TRIGGER immutable_append_operations_update
            BEFORE UPDATE ON append_operations
            BEGIN SELECT RAISE(ABORT, 'append-only committed operation'); END;
            """);

        await using var reopened = await directory.OpenRepositoryAsync();
        await Assert.ThrowsAsync<MemoryIntegrityException>(() =>
            reopened.RetrieveBySubjectAsync(subject));
    }

    [Fact]
    public async Task CanonicalOperationPayloadTampering_FailsClosedAfterReopen()
    {
        using var directory = new MemoryTestDirectory();
        const string subject = "synthetic.payload-tamper";
        await CommitOneAsync(directory, SyntheticMemory.Record(subjectKey: subject));

        await SyntheticMemory.ExecuteSqlAsync(
            directory.Location.DatabasePath,
            """
            DROP TRIGGER immutable_append_operations_update;
            UPDATE append_operations
            SET canonical_payload = X'7B7D';
            CREATE TRIGGER immutable_append_operations_update
            BEFORE UPDATE ON append_operations
            BEGIN SELECT RAISE(ABORT, 'append-only committed operation'); END;
            """);

        await using var reopened = await directory.OpenRepositoryAsync();
        await Assert.ThrowsAsync<MemoryIntegrityException>(() =>
            reopened.RetrieveBySubjectAsync(subject));
    }

    [Fact]
    public async Task MissingRecordFromCommittedOperation_IsDetectedOnIdempotentRetry()
    {
        using var directory = new MemoryTestDirectory();
        var operationId = Guid.NewGuid();
        var record = SyntheticMemory.Record(subjectKey: "synthetic.missing-record");
        await using (var repository = await directory.OpenRepositoryAsync())
        {
            var result = await repository.WriteGate.SubmitAsync(
                new AppendMemoryProposal(operationId, [record]));
            Assert.Equal(WriteGateStatus.Committed, result.Status);
        }

        await SyntheticMemory.ExecuteSqlAsync(
            directory.Location.DatabasePath,
            """
            DROP TRIGGER immutable_memory_records_delete;
            DELETE FROM memory_records;
            CREATE TRIGGER immutable_memory_records_delete
            BEFORE DELETE ON memory_records
            BEGIN SELECT RAISE(ABORT, 'append-only committed memory'); END;
            """);

        await using var reopened = await directory.OpenRepositoryAsync();
        await Assert.ThrowsAsync<MemoryIntegrityException>(() =>
            reopened.WriteGate.SubmitAsync(new AppendMemoryProposal(operationId, [record])));
    }

    private static async Task CommitOneAsync(
        MemoryTestDirectory directory,
        MemoryRecordDraft record)
    {
        await using var repository = await directory.OpenRepositoryAsync();
        var result = await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(record));
        Assert.Equal(WriteGateStatus.Committed, result.Status);
    }
}
