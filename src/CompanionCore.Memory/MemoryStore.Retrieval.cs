using System.Globalization;
using System.Text.Json;

namespace CompanionCore.Memory;

internal sealed partial class MemoryStore
{
    internal async Task<IReadOnlyList<RetrievedMemory>> RetrieveBySubjectAsync(
        string subjectKey,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectKey);
        if (subjectKey.Length > MemoryProposalValidator.MaximumKeyCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(subjectKey));
        }

        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = new List<StoredRecordRow>();
            await using (var command = _connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT
                        r.record_id,
                        r.operation_id,
                        o.journal_sequence,
                        r.schema_version,
                        r.created_at_utc,
                        r.scope,
                        r.source_kind,
                        r.confidence,
                        r.subject_key,
                        r.entity_references_json,
                        r.application_reference,
                        r.game_reference,
                        r.save_reference,
                        r.session_reference,
                        r.visible_recollection,
                        r.retrieval_metadata_json,
                        r.record_checksum,
                        CASE WHEN EXISTS (
                            SELECT 1
                            FROM memory_links newer
                            WHERE newer.target_record_id = r.record_id
                              AND newer.link_kind IN (2, 3)
                        ) THEN 0 ELSE 1 END AS is_current,
                        CASE r.source_kind
                            WHEN 7 THEN 700
                            WHEN 8 THEN 600
                            WHEN 3 THEN 550
                            WHEN 1 THEN 500
                            WHEN 2 THEN 450
                            WHEN 4 THEN 400
                            WHEN 5 THEN 300
                            WHEN 6 THEN 100
                            ELSE 0
                        END AS source_rank
                    FROM memory_records r
                    INNER JOIN append_operations o ON o.operation_id = r.operation_id
                    WHERE r.subject_key = $subjectKey
                    ORDER BY
                        is_current DESC,
                        source_rank DESC,
                        r.confidence DESC,
                        r.created_at_utc DESC,
                        r.record_id ASC;
                    """;
                command.Parameters.AddWithValue("$subjectKey", subjectKey);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new StoredRecordRow(
                        RecordId: Guid.ParseExact(reader.GetString(0), "D"),
                        OperationId: Guid.ParseExact(reader.GetString(1), "D"),
                        JournalSequence: reader.GetInt64(2),
                        SchemaVersion: reader.GetInt32(3),
                        CreatedAtUtc: reader.GetString(4),
                        Scope: reader.GetInt32(5),
                        SourceKind: reader.GetInt32(6),
                        Confidence: reader.GetDouble(7),
                        SubjectKey: reader.GetString(8),
                        EntityReferencesJson: reader.GetString(9),
                        ApplicationReference: ReadNullableString(reader, 10),
                        GameReference: ReadNullableString(reader, 11),
                        SaveReference: ReadNullableString(reader, 12),
                        SessionReference: ReadNullableString(reader, 13),
                        VisibleRecollection: reader.GetString(14),
                        RetrievalMetadataJson: reader.GetString(15),
                        RecordChecksum: reader.GetString(16),
                        IsCurrent: reader.GetInt32(17) == 1));
                }
            }

            var operations = new Dictionary<Guid, StoredOperation>();
            foreach (var operationId in rows.Select(row => row.OperationId).Distinct())
            {
                operations[operationId] = await FindOperationUnlockedAsync(
                        operationId,
                        transaction: null,
                        cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new MemoryIntegrityException(
                        $"Retrieved record operation {operationId:D} is missing.");
            }

            var results = new List<RetrievedMemory>(rows.Count);
            foreach (var row in rows)
            {
                var links = await ReadLinksUnlockedAsync(row.RecordId, cancellationToken)
                    .ConfigureAwait(false);
                string[] entities;
                try
                {
                    entities = JsonSerializer.Deserialize<string[]>(row.EntityReferencesJson)
                        ?? throw new MemoryIntegrityException("Stored entity references are null.");
                }
                catch (JsonException exception)
                {
                    throw new MemoryIntegrityException(
                        $"Stored entity references are invalid JSON: {exception.Message}");
                }

                var record = new MemoryRecordDraft
                {
                    RecordId = row.RecordId,
                    SchemaVersion = row.SchemaVersion,
                    CreatedAtUtc = DateTimeOffset.ParseExact(
                        row.CreatedAtUtc,
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind),
                    Scope = (MemoryScope)row.Scope,
                    SourceKind = (MemorySourceKind)row.SourceKind,
                    Confidence = row.Confidence,
                    SubjectKey = row.SubjectKey,
                    EntityReferences = entities,
                    ApplicationReference = row.ApplicationReference,
                    GameReference = row.GameReference,
                    SaveReference = row.SaveReference,
                    SessionReference = row.SessionReference,
                    VisibleRecollection = row.VisibleRecollection,
                    RetrievalMetadataJson = row.RetrievalMetadataJson,
                    Links = links,
                };

                string actualChecksum;
                try
                {
                    actualChecksum = CanonicalMemorySerializer.ComputeRecordChecksum(row.OperationId, record);
                }
                catch (Exception exception) when (exception is JsonException or MemoryValidationException)
                {
                    throw new MemoryIntegrityException(
                        $"Stored record {row.RecordId:D} cannot be canonicalized: {exception.Message}");
                }

                if (!string.Equals(actualChecksum, row.RecordChecksum, StringComparison.Ordinal))
                {
                    throw new MemoryIntegrityException(
                        $"Stored record {row.RecordId:D} failed checksum validation.");
                }

                if (!operations[row.OperationId].RecordChecksums.TryGetValue(
                        row.RecordId,
                        out var canonicalRecordChecksum)
                    || !string.Equals(
                        actualChecksum,
                        canonicalRecordChecksum,
                        StringComparison.Ordinal))
                {
                    throw new MemoryIntegrityException(
                        $"Stored record {row.RecordId:D} differs from its checksummed operation payload.");
                }

                results.Add(new RetrievedMemory(
                    record,
                    row.OperationId,
                    row.JournalSequence,
                    row.RecordChecksum,
                    row.IsCurrent));
            }

            return results;
        }
        finally
        {
            _accessLock.Release();
        }
    }

    internal async Task<MemoryStoreDiagnostics> ReadDiagnosticsAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var journalMode = await ReadPragmaStringAsync("journal_mode", cancellationToken)
                .ConfigureAwait(false);
            var synchronous = await ReadPragmaIntAsync("synchronous", cancellationToken)
                .ConfigureAwait(false);
            var foreignKeys = await ReadPragmaIntAsync("foreign_keys", cancellationToken)
                .ConfigureAwait(false);
            var userVersion = await ReadPragmaIntAsync("user_version", cancellationToken)
                .ConfigureAwait(false);

            await using var triggerCommand = _connection.CreateCommand();
            triggerCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger';";
            var triggerCount = Convert.ToInt32(
                await triggerCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                CultureInfo.InvariantCulture);

            return new MemoryStoreDiagnostics(
                journalMode,
                synchronous,
                foreignKeys == 1,
                userVersion,
                triggerCount);
        }
        finally
        {
            _accessLock.Release();
        }
    }

    internal async Task<(long Operations, long Records, long Links)> ReadCountsAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (
                await ReadCountUnlockedAsync("append_operations", cancellationToken).ConfigureAwait(false),
                await ReadCountUnlockedAsync("memory_records", cancellationToken).ConfigureAwait(false),
                await ReadCountUnlockedAsync("memory_links", cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            _accessLock.Release();
        }
    }

    internal async Task<long> ReadMaximumJournalSequenceAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT COALESCE(MAX(journal_sequence), 0) FROM append_operations;";
            return Convert.ToInt64(
                await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                CultureInfo.InvariantCulture);
        }
        finally
        {
            _accessLock.Release();
        }
    }

    private async Task<IReadOnlyList<MemoryLink>> ReadLinksUnlockedAsync(
        Guid sourceRecordId,
        CancellationToken cancellationToken)
    {
        var links = new List<MemoryLink>();
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT target_record_id, link_kind
            FROM memory_links
            WHERE source_record_id = $sourceRecordId
            ORDER BY link_kind, target_record_id;
            """;
        command.Parameters.AddWithValue("$sourceRecordId", sourceRecordId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            links.Add(new MemoryLink(
                Guid.ParseExact(reader.GetString(0), "D"),
                (MemoryLinkKind)reader.GetInt32(1)));
        }

        return links;
    }

    private async Task<string> ReadPragmaStringAsync(
        string pragmaName,
        CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragmaName};";
        return Convert.ToString(
                   await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                   CultureInfo.InvariantCulture)
               ?? string.Empty;
    }

    private async Task<int> ReadPragmaIntAsync(
        string pragmaName,
        CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragmaName};";
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private async Task<long> ReadCountUnlockedAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private static string? ReadNullableString(Microsoft.Data.Sqlite.SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private sealed record StoredRecordRow(
        Guid RecordId,
        Guid OperationId,
        long JournalSequence,
        int SchemaVersion,
        string CreatedAtUtc,
        int Scope,
        int SourceKind,
        double Confidence,
        string SubjectKey,
        string EntityReferencesJson,
        string? ApplicationReference,
        string? GameReference,
        string? SaveReference,
        string? SessionReference,
        string VisibleRecollection,
        string RetrievalMetadataJson,
        string RecordChecksum,
        bool IsCurrent);
}

internal sealed record MemoryStoreDiagnostics(
    string JournalMode,
    int SynchronousLevel,
    bool ForeignKeysEnabled,
    int SchemaVersion,
    int TriggerCount);
