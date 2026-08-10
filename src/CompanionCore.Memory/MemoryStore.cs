using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CompanionCore.Memory;

internal sealed partial class MemoryStore : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _accessLock = new(1, 1);
    private bool _disposed;

    private MemoryStore(SqliteConnection connection, string databasePath)
    {
        _connection = connection;
        DatabasePath = databasePath;
    }

    internal string DatabasePath { get; }

    internal static async Task<MemoryStore> OpenAsync(
        MemoryStoreLocation location,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        ValidateLocationCapability(location);

        Directory.CreateDirectory(location.RootPath);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = location.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var store = new MemoryStore(connection, location.DatabasePath);
            await store.ConfigureSafetyPragmasAsync(cancellationToken).ConfigureAwait(false);
            await store.InitializeOrValidateSchemaAsync(cancellationToken).ConfigureAwait(false);
            await store.EnableWalAsync(cancellationToken).ConfigureAwait(false);
            return store;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal async Task<StoredOperation?> FindOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await FindOperationUnlockedAsync(operationId, transaction: null, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _accessLock.Release();
        }
    }

    internal async Task ValidateAppendAsync(
        PreparedAppendOperation operation,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ValidateAppendUnlockedAsync(operation, transaction: null, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _accessLock.Release();
        }
    }

    internal async Task<StoreCommitStatus> CommitAsync(
        PreparedAppendOperation operation,
        long journalSequence,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (journalSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(journalSequence));
        }

        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var transaction = _connection.BeginTransaction();
            var existing = await FindOperationUnlockedAsync(
                    operation.Proposal.LocalOperationId,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                return string.Equals(
                    existing.OperationChecksum,
                    operation.OperationChecksum,
                    StringComparison.Ordinal)
                    ? StoreCommitStatus.AlreadyCommitted
                    : StoreCommitStatus.Conflict;
            }

            await ValidateAppendUnlockedAsync(operation, transaction, cancellationToken)
                .ConfigureAwait(false);
            await InsertOperationAsync(operation, journalSequence, transaction, cancellationToken)
                .ConfigureAwait(false);
            await InsertRecordsAsync(operation, transaction, cancellationToken).ConfigureAwait(false);
            await InsertLinksAsync(operation, transaction, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return StoreCommitStatus.Committed;
        }
        finally
        {
            _accessLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _connection.DisposeAsync().ConfigureAwait(false);
        _accessLock.Dispose();
    }

    private static void ValidateLocationCapability(MemoryStoreLocation location)
    {
        var expectedNamespace = location.Kind switch
        {
            DataRootKind.Development => DevelopmentDataRootPolicy.DevelopmentApplicationNamespace,
            DataRootKind.Test => TestDataRootPolicy.TestApplicationNamespace,
            _ => throw new DataRootViolationException(
                "Task 2 can open only validated development or isolated test memory locations."),
        };

        if (!string.Equals(location.ApplicationNamespace, expectedNamespace, StringComparison.Ordinal))
        {
            throw new DataRootViolationException("The memory-location capability has an invalid namespace.");
        }
    }

    private async Task ConfigureSafetyPragmasAsync(CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            PRAGMA synchronous = FULL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnableWalAsync(CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        var mode = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw new MemoryIntegrityException("SQLite did not enter the required WAL journal mode.");
        }
    }

    private async Task InitializeOrValidateSchemaAsync(CancellationToken cancellationToken)
    {
        var hasSchemaInfo = await ObjectExistsAsync("schema_info", cancellationToken).ConfigureAwait(false);
        if (!hasSchemaInfo)
        {
            if (await CountUserObjectsAsync(cancellationToken).ConfigureAwait(false) != 0)
            {
                throw new MemoryIntegrityException(
                    "A non-empty database without recognized schema metadata cannot be initialized automatically.");
            }

            using var transaction = _connection.BeginTransaction();
            await using var createCommand = _connection.CreateCommand();
            createCommand.Transaction = transaction;
            createCommand.CommandText = MemoryStoreSchema.CreateVersionOne;
            await createCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            transaction.Commit();
        }

        var schemaVersion = await ReadSchemaVersionAsync(cancellationToken).ConfigureAwait(false);
        if (schemaVersion != MemorySchema.CurrentVersion)
        {
            throw new UnsupportedMemorySchemaException(schemaVersion);
        }

        await using (var userVersionCommand = _connection.CreateCommand())
        {
            userVersionCommand.CommandText = "PRAGMA user_version;";
            var userVersion = Convert.ToInt32(
                await userVersionCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                CultureInfo.InvariantCulture);
            if (userVersion != MemorySchema.CurrentVersion)
            {
                throw new MemoryIntegrityException("SQLite user_version disagrees with schema_info.");
            }
        }

        var actualObjects = new HashSet<string>(StringComparer.Ordinal);
        await using (var objectsCommand = _connection.CreateCommand())
        {
            objectsCommand.CommandText = """
                SELECT name
                FROM sqlite_master
                WHERE type IN ('table', 'trigger');
                """;
            await using var reader = await objectsCommand.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                actualObjects.Add(reader.GetString(0));
            }
        }

        var missing = MemoryStoreSchema.RequiredObjectNames
            .Where(required => !actualObjects.Contains(required))
            .ToArray();
        if (missing.Length != 0)
        {
            throw new MemoryIntegrityException(
                $"The memory schema is incomplete; missing objects: {string.Join(", ", missing)}.");
        }


        foreach (var (tableName, requiredColumns) in MemoryStoreSchema.RequiredColumns)
        {
            var actualColumns = new List<string>();
            await using var columnCommand = _connection.CreateCommand();
            columnCommand.CommandText = $"PRAGMA table_info({tableName});";
            await using var columnReader = await columnCommand.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await columnReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                actualColumns.Add(columnReader.GetString(1));
            }

            if (!actualColumns.SequenceEqual(requiredColumns, StringComparer.Ordinal))
            {
                throw new MemoryIntegrityException(
                    $"The {tableName} table does not match memory schema version {MemorySchema.CurrentVersion}.");
            }
        }
    }

    private async Task<bool> ObjectExistsAsync(string objectName, CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE name = $name;
            """;
        command.Parameters.AddWithValue("$name", objectName);
        var count = Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        return count == 1;
    }

    private async Task<long> CountUserObjectsAsync(CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE name NOT LIKE 'sqlite_%';
            """;
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private async Task<int> ReadSchemaVersionAsync(CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT schema_version FROM schema_info WHERE singleton = 1;";
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (value is null)
        {
            throw new MemoryIntegrityException("The memory schema version row is missing.");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private async Task<StoredOperation?> FindOperationUnlockedAsync(
        Guid operationId,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string? checksum = null;
        byte[]? canonicalPayload = null;
        await using (var command = _connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT operation_checksum, canonical_payload
                FROM append_operations
                WHERE operation_id = $operationId;
                """;
            command.Parameters.AddWithValue("$operationId", operationId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                checksum = reader.GetString(0);
                canonicalPayload = reader.GetValue(1) as byte[]
                    ?? throw new MemoryIntegrityException(
                        "A committed operation has an invalid canonical payload type.");
            }
        }

        if (checksum is null || canonicalPayload is null)
        {
            return null;
        }

        PreparedAppendOperation canonicalOperation;
        try
        {
            var parsed = MemoryPayloadParser.ParseOperation(canonicalPayload);
            canonicalOperation = MemoryProposalValidator.Prepare(parsed);
        }
        catch (Exception exception) when (
            exception is MemoryValidationException or JournalCorruptionException)
        {
            throw new MemoryIntegrityException(
                $"Committed operation {operationId:D} has an invalid canonical payload: {exception.Message}");
        }

        if (canonicalOperation.Proposal.LocalOperationId != operationId
            || !canonicalPayload.AsSpan().SequenceEqual(canonicalOperation.CanonicalPayload)
            || !string.Equals(checksum, canonicalOperation.OperationChecksum, StringComparison.Ordinal))
        {
            throw new MemoryIntegrityException(
                $"Committed operation {operationId:D} failed checksum or canonical-form validation.");
        }

        var recordIds = new List<Guid>();
        await using (var recordsCommand = _connection.CreateCommand())
        {
            recordsCommand.Transaction = transaction;
            recordsCommand.CommandText = """
                SELECT record_id
                FROM memory_records
                WHERE operation_id = $operationId
                ORDER BY record_id;
                """;
            recordsCommand.Parameters.AddWithValue("$operationId", operationId.ToString("D"));
            await using var reader = await recordsCommand.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                recordIds.Add(Guid.ParseExact(reader.GetString(0), "D"));
            }
        }

        var canonicalRecordIds = canonicalOperation.Proposal.Records
            .Select(record => record.RecordId)
            .ToHashSet();
        if (recordIds.Count != canonicalRecordIds.Count
            || recordIds.Any(recordId => !canonicalRecordIds.Contains(recordId)))
        {
            throw new MemoryIntegrityException(
                $"Committed operation {operationId:D} does not own its canonical record set.");
        }

        return new StoredOperation(checksum, recordIds, canonicalOperation.RecordChecksums);
    }

    private async Task ValidateAppendUnlockedAsync(
        PreparedAppendOperation operation,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var localRecords = operation.Proposal.Records.ToDictionary(record => record.RecordId);

        foreach (var record in operation.Proposal.Records)
        {
            if (await ReadSubjectAsync(record.RecordId, transaction, cancellationToken).ConfigureAwait(false)
                is not null)
            {
                throw new MemoryValidationException("A proposed record ID is already committed.");
            }
        }

        foreach (var record in operation.Proposal.Records)
        {
            foreach (var link in record.Links)
            {
                string targetSubject;
                if (localRecords.TryGetValue(link.TargetRecordId, out var localTarget))
                {
                    targetSubject = localTarget.SubjectKey;
                }
                else
                {
                    targetSubject = await ReadSubjectAsync(
                            link.TargetRecordId,
                            transaction,
                            cancellationToken)
                        .ConfigureAwait(false)
                        ?? throw new MemoryValidationException("A link target does not exist.");
                }

                if (link.Kind is MemoryLinkKind.Corrects
                        or MemoryLinkKind.Supersedes
                        or MemoryLinkKind.RecursWith
                    && !string.Equals(record.SubjectKey, targetSubject, StringComparison.Ordinal))
                {
                    throw new MemoryValidationException(
                        "Correction, supersession, and recurrence links must stay within one exact subject.");
                }
            }
        }
    }

    private async Task<string?> ReadSubjectAsync(
        Guid recordId,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT subject_key
            FROM memory_records
            WHERE record_id = $recordId;
            """;
        command.Parameters.AddWithValue("$recordId", recordId.ToString("D"));
        return (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertOperationAsync(
        PreparedAppendOperation operation,
        long journalSequence,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO append_operations (
                operation_id,
                operation_checksum,
                canonical_payload,
                committed_at_utc,
                journal_sequence)
            VALUES ($operationId, $checksum, $canonicalPayload, $committedAtUtc, $journalSequence);
            """;
        command.Parameters.AddWithValue(
            "$operationId",
            operation.Proposal.LocalOperationId.ToString("D"));
        command.Parameters.AddWithValue("$checksum", operation.OperationChecksum);
        command.Parameters.AddWithValue("$canonicalPayload", operation.CanonicalPayload);
        command.Parameters.AddWithValue(
            "$committedAtUtc",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$journalSequence", journalSequence);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertRecordsAsync(
        PreparedAppendOperation operation,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        foreach (var record in operation.Proposal.Records.OrderBy(record => record.RecordId))
        {
            await using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO memory_records (
                    record_id,
                    operation_id,
                    schema_version,
                    created_at_utc,
                    scope,
                    source_kind,
                    confidence,
                    subject_key,
                    entity_references_json,
                    application_reference,
                    game_reference,
                    save_reference,
                    session_reference,
                    visible_recollection,
                    retrieval_metadata_json,
                    record_checksum,
                    committed)
                VALUES (
                    $recordId,
                    $operationId,
                    $schemaVersion,
                    $createdAtUtc,
                    $scope,
                    $sourceKind,
                    $confidence,
                    $subjectKey,
                    $entityReferencesJson,
                    $applicationReference,
                    $gameReference,
                    $saveReference,
                    $sessionReference,
                    $visibleRecollection,
                    $retrievalMetadataJson,
                    $recordChecksum,
                    1);
                """;

            command.Parameters.AddWithValue("$recordId", record.RecordId.ToString("D"));
            command.Parameters.AddWithValue(
                "$operationId",
                operation.Proposal.LocalOperationId.ToString("D"));
            command.Parameters.AddWithValue("$schemaVersion", record.SchemaVersion);
            command.Parameters.AddWithValue(
                "$createdAtUtc",
                record.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$scope", (int)record.Scope);
            command.Parameters.AddWithValue("$sourceKind", (int)record.SourceKind);
            command.Parameters.AddWithValue("$confidence", record.Confidence);
            command.Parameters.AddWithValue("$subjectKey", record.SubjectKey);
            command.Parameters.AddWithValue(
                "$entityReferencesJson",
                JsonSerializer.Serialize(record.EntityReferences.OrderBy(value => value, StringComparer.Ordinal)));
            AddNullableParameter(command, "$applicationReference", record.ApplicationReference);
            AddNullableParameter(command, "$gameReference", record.GameReference);
            AddNullableParameter(command, "$saveReference", record.SaveReference);
            AddNullableParameter(command, "$sessionReference", record.SessionReference);
            command.Parameters.AddWithValue("$visibleRecollection", record.VisibleRecollection);

            using (var metadata = JsonDocument.Parse(record.RetrievalMetadataJson))
            {
                command.Parameters.AddWithValue(
                    "$retrievalMetadataJson",
                    CanonicalMemorySerializer.CanonicalizeJson(metadata.RootElement));
            }

            command.Parameters.AddWithValue(
                "$recordChecksum",
                operation.RecordChecksums[record.RecordId]);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task InsertLinksAsync(
        PreparedAppendOperation operation,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        foreach (var record in operation.Proposal.Records.OrderBy(record => record.RecordId))
        {
            foreach (var link in record.Links
                         .OrderBy(link => (int)link.Kind)
                         .ThenBy(link => link.TargetRecordId))
            {
                await using var command = _connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO memory_links (source_record_id, target_record_id, link_kind)
                    VALUES ($sourceRecordId, $targetRecordId, $linkKind);
                    """;
                command.Parameters.AddWithValue("$sourceRecordId", record.RecordId.ToString("D"));
                command.Parameters.AddWithValue("$targetRecordId", link.TargetRecordId.ToString("D"));
                command.Parameters.AddWithValue("$linkKind", (int)link.Kind);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void AddNullableParameter(SqliteCommand command, string name, string? value) =>
        command.Parameters.AddWithValue(name, (object?)value ?? DBNull.Value);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

internal sealed record StoredOperation(
    string OperationChecksum,
    IReadOnlyList<Guid> RecordIds,
    IReadOnlyDictionary<Guid, string> RecordChecksums);
