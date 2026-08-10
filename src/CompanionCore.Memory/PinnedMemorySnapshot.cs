using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CompanionCore.Memory;

/// <summary>
/// Owns a read transaction pinned while the serial writer is fenced. The writer may
/// resume after construction; SQLite's online-backup API continues to see this cut.
/// </summary>
internal sealed class PinnedMemorySnapshot : IAsyncDisposable
{
    private readonly SqliteConnection _sourceConnection;
    private readonly SqliteTransaction _readTransaction;
    private bool _disposed;

    private PinnedMemorySnapshot(
        SqliteConnection sourceConnection,
        SqliteTransaction readTransaction,
        long cutSequence)
    {
        _sourceConnection = sourceConnection;
        _readTransaction = readTransaction;
        CutSequence = cutSequence;
    }

    internal long CutSequence { get; }

    internal static async Task<PinnedMemorySnapshot> CreateAsync(
        string databasePath,
        long expectedCutSequence,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (expectedCutSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCutSequence));
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString());

        SqliteTransaction? transaction = null;
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            transaction = connection.BeginTransaction(deferred: true);

            await using var pinCommand = connection.CreateCommand();
            pinCommand.Transaction = transaction;
            pinCommand.CommandText =
                "SELECT COALESCE(MAX(journal_sequence), 0) FROM append_operations;";
            var observedCut = Convert.ToInt64(
                await pinCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                CultureInfo.InvariantCulture);
            if (observedCut != expectedCutSequence)
            {
                throw new MemoryIntegrityException(
                    "The SQLite read snapshot does not match the fenced journal cut.");
            }

            return new PinnedMemorySnapshot(connection, transaction, observedCut);
        }
        catch
        {
            transaction?.Dispose();
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal async Task CopyToAsync(
        string destinationDatabasePath,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDatabasePath);
        cancellationToken.ThrowIfCancellationRequested();

        var parent = Path.GetDirectoryName(destinationDatabasePath)
            ?? throw new ArgumentException(
                "The snapshot destination requires a parent directory.",
                nameof(destinationDatabasePath));
        Directory.CreateDirectory(parent);

        await using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
                     {
                         DataSource = destinationDatabasePath,
                         Mode = SqliteOpenMode.ReadWriteCreate,
                         Cache = SqliteCacheMode.Private,
                         Pooling = false,
                     }.ToString()))
        {
            await destination.OpenAsync(cancellationToken).ConfigureAwait(false);

            // BackupDatabase is synchronous and has no cancellation surface. Once it
            // starts, allow it to finish into task-owned staging; caller cancellation is
            // observed before any later archive promotion.
            _sourceConnection.BackupDatabase(destination);
        }

        await using var durableFile = new FileStream(
            destinationDatabasePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await durableFile.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        durableFile.Flush(flushToDisk: true);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _readTransaction.Dispose();
        await _sourceConnection.DisposeAsync().ConfigureAwait(false);
    }
}
