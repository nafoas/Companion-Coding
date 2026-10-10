namespace CompanionCore.Memory;

using CompanionCore.Privacy;

/// <summary>
/// Owns the one local committed store and recovery journal for a validated non-production
/// location. It exposes append proposals through LocalWriteGate and read-only retrieval.
/// </summary>
public sealed class MemoryRepository : IAsyncDisposable
{
    private readonly MemoryStore _store;
    private readonly SessionJournal _journal;
    private readonly MemoryCommitCoordinator _coordinator;
    private readonly MemoryStoreLocation _location;
    private readonly MemoryRepositoryLease _lease;
    private readonly bool _ownsLease;
    private readonly SemaphoreSlim _backupLock = new(1, 1);
    private bool _disposed;

    private MemoryRepository(
        MemoryStore store,
        SessionJournal journal,
        MemoryCommitCoordinator coordinator,
        RuntimePrivacyState privacyState,
        MemoryStoreLocation location,
        MemoryRepositoryLease lease,
        bool ownsLease)
    {
        _store = store;
        _journal = journal;
        _coordinator = coordinator;
        _location = location;
        _lease = lease;
        _ownsLease = ownsLease;
        WriteGate = new LocalWriteGate(coordinator, privacyState);
    }

    public LocalWriteGate WriteGate { get; }

    internal MemoryStore Store => _store;

    internal SessionJournal Journal => _journal;

    internal MemoryCommitCoordinator Coordinator => _coordinator;

    internal MemoryStoreLocation Location => _location;

    public static async Task<MemoryRepository> OpenAsync(
        MemoryStoreLocation location,
        RuntimePrivacyState privacyState,
        CancellationToken cancellationToken = default) =>
        await OpenCoreAsync(
                location,
                privacyState,
                existingLease: null,
                ownsExistingLease: true,
                allowRepairMarker: false,
                cancellationToken)
            .ConfigureAwait(false);

    internal static async Task<MemoryRepository> OpenForMaintenanceValidationAsync(
        MemoryStoreLocation location,
        MemoryRepositoryLease lease,
        CancellationToken cancellationToken) =>
        await OpenCoreAsync(
                location,
                new RuntimePrivacyState(),
                lease,
                ownsExistingLease: false,
                allowRepairMarker: true,
                cancellationToken)
            .ConfigureAwait(false);

    private static async Task<MemoryRepository> OpenCoreAsync(
        MemoryStoreLocation location,
        RuntimePrivacyState privacyState,
        MemoryRepositoryLease? existingLease,
        bool ownsExistingLease,
        bool allowRepairMarker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(privacyState);
        var lease = existingLease ?? MemoryRepositoryLease.Acquire(location);
        MemoryStore? store = null;
        SessionJournal? journal = null;
        MemoryCommitCoordinator? coordinator = null;
        try
        {
            if (!allowRepairMarker && File.Exists(location.RepairMarkerPath))
            {
                throw new MemoryIntegrityException(
                    "An interrupted repair marker blocks ordinary repository startup.");
            }

            store = await MemoryStore.OpenAsync(location, cancellationToken).ConfigureAwait(false);
            journal = await SessionJournal.OpenAsync(location.JournalPath, cancellationToken)
                .ConfigureAwait(false);
            coordinator = new MemoryCommitCoordinator(store, journal);
            await coordinator.RecoverAsync(cancellationToken).ConfigureAwait(false);
            return new MemoryRepository(
                store,
                journal,
                coordinator,
                privacyState,
                location,
                lease,
                ownsExistingLease);
        }
        catch
        {
            try
            {
                coordinator?.Dispose();
            }
            finally
            {
                try
                {
                    if (journal is not null)
                    {
                        await journal.DisposeAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    try
                    {
                        if (store is not null)
                        {
                            await store.DisposeAsync().ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        if (ownsExistingLease)
                        {
                            lease.Dispose();
                        }
                    }
                }
            }

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

    /// <summary>
    /// Read-only keyset page of records whose subject starts with <paramref name="subjectPrefix"/>,
    /// ordered by record ID, strictly after <paramref name="afterRecordId"/>. Repeating with the
    /// last returned ID enumerates every matching record exactly once.
    /// </summary>
    public Task<IReadOnlyList<RetrievedMemory>> RetrieveSubjectPrefixPageAsync(
        string subjectPrefix,
        Guid? afterRecordId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _store.RetrieveSubjectPrefixPageAsync(subjectPrefix, afterRecordId, limit, cancellationToken);
    }

    /// <summary>Bounded read-only query; see <see cref="MemoryQuery"/>.</summary>
    public Task<IReadOnlyList<RetrievedMemory>> RetrieveAsync(
        MemoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _store.RetrieveAsync(query, cancellationToken);
    }

    internal Task<MemoryBackupResult> CreateBackupAsync(
        IBackupTestHook? testHook = null,
        CancellationToken cancellationToken = default) =>
        CreateBackupCoreAsync(companion: null, testHook, cancellationToken);

    /// <summary>Creates the memory archive together with the Vault companion archive.</summary>
    internal Task<MemoryBackupResult> CreateBackupAsync(
        IVaultCompanion companion,
        IBackupTestHook? testHook = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(companion);
        return CreateBackupCoreAsync(companion, testHook, cancellationToken);
    }

    private async Task<MemoryBackupResult> CreateBackupCoreAsync(
        IVaultCompanion? companion,
        IBackupTestHook? testHook,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _backupLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            return await new MemoryBackupService(this, companion)
                .CreateAsync(testHook, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _backupLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _coordinator.Dispose();
        }
        finally
        {
            try
            {
                await _journal.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await _store.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        _backupLock.Dispose();
                    }
                    finally
                    {
                        if (_ownsLease)
                        {
                            _lease.Dispose();
                        }
                    }
                }
            }
        }
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
