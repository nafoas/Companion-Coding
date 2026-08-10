namespace CompanionCore.Memory;

internal sealed class MemoryCommitCoordinator : IDisposable
{
    private readonly MemoryStore _store;
    private readonly SessionJournal _journal;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    internal MemoryCommitCoordinator(MemoryStore store, SessionJournal journal)
    {
        _store = store;
        _journal = journal;
    }

    internal async Task<WriteGateResult> SubmitAsync(
        AppendMemoryProposal proposal,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var prepared = MemoryProposalValidator.Prepare(proposal);
            var existing = await _store.FindOperationAsync(
                    prepared.Proposal.LocalOperationId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                return string.Equals(
                    existing.OperationChecksum,
                    prepared.OperationChecksum,
                    StringComparison.Ordinal)
                    ? WriteGateResult.AlreadyCommitted(existing.RecordIds)
                    : WriteGateResult.Conflict();
            }

            await _store.ValidateAppendAsync(prepared, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // Once this durable append begins, caller cancellation is deliberately no
            // longer propagated. A later failure leaves a valid replay tail.
            var journalSequence = await _journal.AppendOperationAsync(
                    prepared.CanonicalPayload,
                    cancellationToken)
                .ConfigureAwait(false);
            var commitStatus = await _store.CommitAsync(
                    prepared,
                    journalSequence,
                    CancellationToken.None)
                .ConfigureAwait(false);

            if (commitStatus == StoreCommitStatus.Conflict)
            {
                throw new MemoryIntegrityException(
                    "An operation ID changed payload after its journal append was made durable.");
            }

            await _journal.AppendCheckpointAsync(journalSequence, CancellationToken.None)
                .ConfigureAwait(false);

            var recordIds = prepared.Proposal.Records.Select(record => record.RecordId).ToArray();
            return commitStatus == StoreCommitStatus.Committed
                ? WriteGateResult.Committed(recordIds)
                : WriteGateResult.AlreadyCommitted(recordIds);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal async Task RecoverAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var maximumDatabaseSequence = await _store.ReadMaximumJournalSequenceAsync(cancellationToken)
                .ConfigureAwait(false);
            if (_journal.ConfirmedThrough > maximumDatabaseSequence)
            {
                throw new MemoryIntegrityException(
                    "The journal checkpoint advances beyond the committed SQLite authority.");
            }

            if (maximumDatabaseSequence > _journal.HighestAppendSequence)
            {
                throw new MemoryIntegrityException(
                    "The committed store references journal history that is missing from the recovery tail.");
            }

            var confirmedThrough = _journal.ConfirmedThrough;
            foreach (var frame in _journal.RecoveryTail.OrderBy(frame => frame.Sequence))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendMemoryProposal proposal;
                PreparedAppendOperation prepared;
                try
                {
                    proposal = MemoryPayloadParser.ParseOperation(frame.CanonicalOperationPayload);
                    prepared = MemoryProposalValidator.Prepare(proposal);
                }
                catch (Exception exception) when (exception is MemoryValidationException or JournalCorruptionException)
                {
                    throw new JournalCorruptionException(
                        $"A validly framed recovery operation is structurally invalid: {exception.Message}");
                }

                if (!frame.CanonicalOperationPayload.AsSpan().SequenceEqual(prepared.CanonicalPayload))
                {
                    throw new JournalCorruptionException(
                        "A recovery operation is checksummed but not in canonical form.");
                }

                var existing = await _store.FindOperationAsync(
                        proposal.LocalOperationId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (existing is null)
                {
                    try
                    {
                        await _store.ValidateAppendAsync(prepared, cancellationToken).ConfigureAwait(false);
                    }
                    catch (MemoryValidationException exception)
                    {
                        throw new JournalCorruptionException(
                            $"A recovery operation is not commit-safe: {exception.Message}");
                    }
                }
                else if (!string.Equals(
                             existing.OperationChecksum,
                             prepared.OperationChecksum,
                             StringComparison.Ordinal))
                {
                    throw new JournalCorruptionException(
                        "A recovery operation reuses a committed operation ID with different content.");
                }

                var status = await _store.CommitAsync(
                        prepared,
                        frame.Sequence,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (status == StoreCommitStatus.Conflict)
                {
                    throw new JournalCorruptionException(
                        "A recovery operation conflicts with committed content.");
                }

                confirmedThrough = frame.Sequence;
            }

            if (confirmedThrough > _journal.ConfirmedThrough)
            {
                await _journal.AppendCheckpointAsync(confirmedThrough, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeLock.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
