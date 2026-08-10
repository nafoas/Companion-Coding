namespace CompanionCore.Memory;

/// <summary>
/// Owns the one local committed store and recovery journal for a validated non-production
/// location. It exposes append proposals through LocalWriteGate and read-only retrieval.
/// </summary>
public sealed class MemoryRepository : IAsyncDisposable
{
    private readonly MemoryStore _store;
    private readonly SessionJournal _journal;
    private readonly MemoryCommitCoordinator _coordinator;
    private bool _disposed;

    private MemoryRepository(
        MemoryStore store,
        SessionJournal journal,
        MemoryCommitCoordinator coordinator)
    {
        _store = store;
        _journal = journal;
        _coordinator = coordinator;
        WriteGate = new LocalWriteGate(coordinator);
    }

    public LocalWriteGate WriteGate { get; }

    internal MemoryStore Store => _store;

    internal SessionJournal Journal => _journal;

    internal MemoryCommitCoordinator Coordinator => _coordinator;

    public static async Task<MemoryRepository> OpenAsync(
        MemoryStoreLocation location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        var store = await MemoryStore.OpenAsync(location, cancellationToken).ConfigureAwait(false);
        SessionJournal? journal = null;
        MemoryCommitCoordinator? coordinator = null;
        try
        {
            journal = await SessionJournal.OpenAsync(location.JournalPath, cancellationToken)
                .ConfigureAwait(false);
            coordinator = new MemoryCommitCoordinator(store, journal);
            await coordinator.RecoverAsync(cancellationToken).ConfigureAwait(false);
            return new MemoryRepository(store, journal, coordinator);
        }
        catch
        {
            coordinator?.Dispose();
            if (journal is not null)
            {
                await journal.DisposeAsync().ConfigureAwait(false);
            }

            await store.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task<IReadOnlyList<RetrievedMemory>> RetrieveBySubjectAsync(
        string subjectKey,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _store.RetrieveBySubjectAsync(subjectKey, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _coordinator.Dispose();
        await _journal.DisposeAsync().ConfigureAwait(false);
        await _store.DisposeAsync().ConfigureAwait(false);
    }
}
