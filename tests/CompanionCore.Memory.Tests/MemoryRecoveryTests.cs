namespace CompanionCore.Memory.Tests;

public sealed class MemoryRecoveryTests
{
    [Fact]
    public async Task JournalOnlyAppend_ReplaysAndCheckpointsAfterReopen()
    {
        using var directory = new MemoryTestDirectory();
        var record = SyntheticMemory.Record(subjectKey: "synthetic.journal-only");
        var prepared = MemoryProposalValidator.Prepare(SyntheticMemory.Proposal(record));

        await using (var repository = await directory.OpenRepositoryAsync())
        {
            var sequence = await repository.Journal.AppendOperationAsync(
                prepared.CanonicalPayload,
                default);
            Assert.Equal(1, sequence);
            Assert.Equal((0L, 0L, 0L), await repository.Store.ReadCountsAsync(default));
            Assert.Equal(0, repository.Journal.ConfirmedThrough);
        }

        await using (var recovered = await directory.OpenRepositoryAsync())
        {
            Assert.Equal((1L, 1L, 0L), await recovered.Store.ReadCountsAsync(default));
            Assert.Equal(1, recovered.Journal.ConfirmedThrough);
            Assert.Equal(record.RecordId, Assert.Single(
                await recovered.RetrieveBySubjectAsync(record.SubjectKey)).Record.RecordId);
        }

        await using var cleanReopen = await directory.OpenRepositoryAsync();
        Assert.Empty(cleanReopen.Journal.RecoveryTail);
        Assert.Equal(1, cleanReopen.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task StoreCommitBeforeCheckpoint_ReplaysIdempotentlyWithoutDuplicate()
    {
        using var directory = new MemoryTestDirectory();
        var record = SyntheticMemory.Record(subjectKey: "synthetic.commit-before-checkpoint");
        var prepared = MemoryProposalValidator.Prepare(SyntheticMemory.Proposal(record));

        await using (var repository = await directory.OpenRepositoryAsync())
        {
            var sequence = await repository.Journal.AppendOperationAsync(
                prepared.CanonicalPayload,
                default);
            var status = await repository.Store.CommitAsync(prepared, sequence, default);
            Assert.Equal(StoreCommitStatus.Committed, status);
            Assert.Equal(0, repository.Journal.ConfirmedThrough);
        }

        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Equal((1L, 1L, 0L), await recovered.Store.ReadCountsAsync(default));
        Assert.Equal(1, recovered.Journal.ConfirmedThrough);
        Assert.Single(await recovered.RetrieveBySubjectAsync(record.SubjectKey));
    }

    [Fact]
    public async Task UnconfirmedLiveTail_BlocksLaterWriteUntilReopenRecovery()
    {
        using var directory = new MemoryTestDirectory();
        var stranded = SyntheticMemory.Record(subjectKey: "synthetic.stranded-live-tail");
        var later = SyntheticMemory.Record(subjectKey: "synthetic.after-live-tail");

        await using (var repository = await directory.OpenRepositoryAsync())
        {
            var prepared = MemoryProposalValidator.Prepare(SyntheticMemory.Proposal(stranded));
            var sequence = await repository.Journal.AppendOperationAsync(
                prepared.CanonicalPayload,
                default);
            Assert.Equal(1, sequence);

            await Assert.ThrowsAsync<MemoryIntegrityException>(() =>
                repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(later)));

            Assert.Equal((0L, 0L, 0L), await repository.Store.ReadCountsAsync(default));
            Assert.Equal(1, repository.Journal.HighestAppendSequence);
            Assert.Equal(0, repository.Journal.ConfirmedThrough);
        }

        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Single(await recovered.RetrieveBySubjectAsync(stranded.SubjectKey));

        var laterResult = await recovered.WriteGate.SubmitAsync(SyntheticMemory.Proposal(later));
        Assert.Equal(WriteGateStatus.Committed, laterResult.Status);
        Assert.Equal((2L, 2L, 0L), await recovered.Store.ReadCountsAsync(default));
        Assert.Equal(2, recovered.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task TornLaterFrame_DoesNotLoseEarlierRecoverableAppend()
    {
        using var directory = new MemoryTestDirectory();
        var committed = SyntheticMemory.Record(subjectKey: "synthetic.committed-before-torn");
        var recoverable = SyntheticMemory.Record(subjectKey: "synthetic.recoverable-before-torn");
        var torn = SyntheticMemory.Record(subjectKey: "synthetic.torn");

        await using (var repository = await directory.OpenRepositoryAsync())
        {
            Assert.True((await repository.WriteGate.SubmitAsync(
                SyntheticMemory.Proposal(committed))).IsAccepted);
            var recoverablePrepared = MemoryProposalValidator.Prepare(
                SyntheticMemory.Proposal(recoverable));
            await repository.Journal.AppendOperationAsync(
                recoverablePrepared.CanonicalPayload,
                default);
            var tornPrepared = MemoryProposalValidator.Prepare(SyntheticMemory.Proposal(torn));
            await repository.Journal.AppendTornFrameForTestAsync(
                tornPrepared.CanonicalPayload,
                bytesToWrite: 20);
        }

        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Equal((2L, 2L, 0L), await recovered.Store.ReadCountsAsync(default));
        Assert.Single(await recovered.RetrieveBySubjectAsync(committed.SubjectKey));
        Assert.Single(await recovered.RetrieveBySubjectAsync(recoverable.SubjectKey));
        Assert.Empty(await recovered.RetrieveBySubjectAsync(torn.SubjectKey));
        Assert.Equal(2, recovered.Journal.HighestAppendSequence);
        Assert.Equal(2, recovered.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task ChecksumInvalidTrailingFrame_IsDiscardedWithoutLosingEarlierCommit()
    {
        using var directory = new MemoryTestDirectory();
        var committed = SyntheticMemory.Record(subjectKey: "synthetic.before-bad-checksum");
        var corruptTail = SyntheticMemory.Record(subjectKey: "synthetic.bad-checksum-tail");

        await using (var repository = await directory.OpenRepositoryAsync())
        {
            Assert.True((await repository.WriteGate.SubmitAsync(
                SyntheticMemory.Proposal(committed))).IsAccepted);
            var prepared = MemoryProposalValidator.Prepare(SyntheticMemory.Proposal(corruptTail));
            await repository.Journal.AppendOperationAsync(prepared.CanonicalPayload, default);
        }

        await FlipLastByteAsync(directory.Location.JournalPath);

        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Single(await recovered.RetrieveBySubjectAsync(committed.SubjectKey));
        Assert.Empty(await recovered.RetrieveBySubjectAsync(corruptTail.SubjectKey));
        Assert.Equal((1L, 1L, 0L), await recovered.Store.ReadCountsAsync(default));
        Assert.Equal(1, recovered.Journal.HighestAppendSequence);
        Assert.Equal(1, recovered.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task MissingCommittedJournalHistory_FailsClosed()
    {
        using var directory = new MemoryTestDirectory();
        await using (var repository = await directory.OpenRepositoryAsync())
        {
            var result = await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(
                SyntheticMemory.Record(subjectKey: "synthetic.missing-journal")));
            Assert.Equal(WriteGateStatus.Committed, result.Status);
        }

        await File.WriteAllBytesAsync(
            directory.Location.JournalPath,
            [(byte)'C', (byte)'C', (byte)'S', (byte)'J', 1, 0, 0, 0]);

        await Assert.ThrowsAsync<MemoryIntegrityException>(() =>
            MemoryRepository.OpenAsync(directory.Location, directory.PrivacyState));
    }

    [Fact]
    public async Task CheckpointBeyondCommittedStore_FailsClosed()
    {
        using var directory = new MemoryTestDirectory();
        await using (var repository = await directory.OpenRepositoryAsync())
        {
            var prepared = MemoryProposalValidator.Prepare(
                SyntheticMemory.Proposal(SyntheticMemory.Record()));
            var sequence = await repository.Journal.AppendOperationAsync(
                prepared.CanonicalPayload,
                default);
            await repository.Journal.AppendCheckpointAsync(sequence, default);
            Assert.Equal((0L, 0L, 0L), await repository.Store.ReadCountsAsync(default));
        }

        await Assert.ThrowsAsync<MemoryIntegrityException>(() =>
            MemoryRepository.OpenAsync(directory.Location, directory.PrivacyState));
    }

    [Fact]
    public async Task CancellationBeforeDurableAppend_WritesNothing()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.WriteGate.SubmitAsync(
                SyntheticMemory.Proposal(SyntheticMemory.Record()),
                cancellation.Token));

        Assert.Equal((0L, 0L, 0L), await repository.Store.ReadCountsAsync(default));
        Assert.Equal(0, repository.Journal.HighestAppendSequence);
        Assert.Equal(0, repository.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task OversizedRawFrame_IsRejectedWithoutFaultingJournal()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var oversized = new byte[SessionJournal.MaximumOperationPayloadLength + 1];

        await Assert.ThrowsAsync<MemoryValidationException>(() =>
            repository.Journal.AppendOperationAsync(oversized, default));

        var valid = MemoryProposalValidator.Prepare(
            SyntheticMemory.Proposal(SyntheticMemory.Record()));
        var sequence = await repository.Journal.AppendOperationAsync(
            valid.CanonicalPayload,
            default);
        Assert.Equal(1, sequence);
        Assert.Equal(1, repository.Journal.HighestAppendSequence);
    }

    private static async Task FlipLastByteAsync(string path)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        stream.Position = stream.Length - 1;
        var value = stream.ReadByte();
        Assert.True(value >= 0);
        stream.Position = stream.Length - 1;
        stream.WriteByte((byte)(value ^ 0xff));
        await stream.FlushAsync();
        stream.Flush(flushToDisk: true);
    }
}
