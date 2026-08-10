using System.Globalization;

namespace CompanionCore.Memory;

internal sealed partial class MemoryStore
{
    internal async Task<MemoryHealthReport> ValidateFullHealthAsync(
        long? expectedMaximumSequence,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        Guid[] operationIds;
        string[] subjectKeys;
        long maximumSequence;
        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await InitializeOrValidateSchemaAsync(
                    allowInitialization: false,
                    cancellationToken)
                .ConfigureAwait(false);
            await ValidateExactObjectsUnlockedAsync(cancellationToken).ConfigureAwait(false);
            await ValidateSqliteIntegrityUnlockedAsync(cancellationToken).ConfigureAwait(false);

            operationIds = await ReadOperationIdsUnlockedAsync(cancellationToken)
                .ConfigureAwait(false);
            subjectKeys = await ReadSubjectKeysUnlockedAsync(cancellationToken)
                .ConfigureAwait(false);
            maximumSequence = await ReadMaximumSequenceUnlockedAsync(cancellationToken)
                .ConfigureAwait(false);
            await ValidateContiguousSequencesUnlockedAsync(
                    operationIds.Length,
                    maximumSequence,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _accessLock.Release();
        }

        if (expectedMaximumSequence is not null
            && maximumSequence != expectedMaximumSequence.Value)
        {
            throw new MemoryIntegrityException(
                "The validated database does not end at its declared journal cut.");
        }

        foreach (var operationId in operationIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = await FindOperationAsync(operationId, cancellationToken).ConfigureAwait(false)
                ?? throw new MemoryIntegrityException(
                    $"Committed operation {operationId:D} disappeared during validation.");
        }

        long recordCount = 0;
        long linkCount = 0;
        foreach (var subjectKey in subjectKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var records = await RetrieveBySubjectAsync(subjectKey, cancellationToken)
                .ConfigureAwait(false);
            recordCount = checked(recordCount + records.Count);
            linkCount = checked(linkCount + records.Sum(record => record.Record.Links.Count));
        }

        var counts = await ReadCountsAsync(cancellationToken).ConfigureAwait(false);
        if (counts.Operations != operationIds.LongLength
            || counts.Records != recordCount
            || counts.Links != linkCount)
        {
            throw new MemoryIntegrityException(
                "The database ownership/link counts changed or are not represented by canonical operations.");
        }

        return new MemoryHealthReport(
            maximumSequence,
            counts.Operations,
            counts.Records,
            counts.Links);
    }

    private async Task ValidateExactObjectsUnlockedAsync(CancellationToken cancellationToken)
    {
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT name, type
            FROM sqlite_master
            WHERE name NOT LIKE 'sqlite_%'
              AND type IN ('table', 'index', 'trigger')
            ORDER BY name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.GetString(0);
            if (!actual.TryAdd(name, reader.GetString(1)))
            {
                throw new MemoryIntegrityException("The database contains duplicate schema objects.");
            }
        }

        if (actual.Count != MemoryStoreSchema.ExactUserObjects.Count
            || MemoryStoreSchema.ExactUserObjects.Any(expected =>
                !actual.TryGetValue(expected.Key, out var type)
                || !string.Equals(type, expected.Value, StringComparison.Ordinal)))
        {
            throw new MemoryIntegrityException(
                $"The database objects do not exactly match memory schema version {MemorySchema.CurrentVersion}.");
        }
    }

    private async Task ValidateSqliteIntegrityUnlockedAsync(CancellationToken cancellationToken)
    {
        var integrityRows = new List<string>();
        await using (var integrity = _connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            await using var reader = await integrity.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                integrityRows.Add(reader.GetString(0));
            }
        }

        if (integrityRows.Count != 1
            || !string.Equals(integrityRows[0], "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new MemoryIntegrityException("SQLite integrity_check did not report a healthy database.");
        }

        await using var foreignKeys = _connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_key_check;";
        await using var foreignKeyReader = await foreignKeys.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        if (await foreignKeyReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new MemoryIntegrityException("SQLite foreign_key_check found an invalid relationship.");
        }
    }

    private async Task<Guid[]> ReadOperationIdsUnlockedAsync(CancellationToken cancellationToken)
    {
        var operationIds = new List<Guid>();
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT operation_id, committed_at_utc
            FROM append_operations
            ORDER BY journal_sequence;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var operationIdText = reader.GetString(0);
            var committedAtText = reader.GetString(1);
            if (!Guid.TryParseExact(operationIdText, "D", out var operationId)
                || operationId == Guid.Empty
                || !string.Equals(
                    operationIdText,
                    operationId.ToString("D"),
                    StringComparison.Ordinal))
            {
                throw new MemoryIntegrityException("A committed operation ID is invalid.");
            }

            if (!DateTimeOffset.TryParseExact(
                    committedAtText,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var committedAtUtc)
                || committedAtUtc.Offset != TimeSpan.Zero
                || !string.Equals(
                    committedAtText,
                    committedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                throw new MemoryIntegrityException(
                    "A committed operation timestamp is not canonical UTC.");
            }

            operationIds.Add(operationId);
        }

        return operationIds.ToArray();
    }

    private async Task<string[]> ReadSubjectKeysUnlockedAsync(CancellationToken cancellationToken)
    {
        var subjectKeys = new List<string>();
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT subject_key FROM memory_records ORDER BY subject_key;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var subjectKey = reader.GetString(0);
            if (string.IsNullOrWhiteSpace(subjectKey)
                || subjectKey.Length > MemoryProposalValidator.MaximumKeyCharacters)
            {
                throw new MemoryIntegrityException("A committed subject key is invalid.");
            }

            subjectKeys.Add(subjectKey);
        }

        return subjectKeys.ToArray();
    }

    private async Task<long> ReadMaximumSequenceUnlockedAsync(CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(journal_sequence), 0) FROM append_operations;";
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private async Task ValidateContiguousSequencesUnlockedAsync(
        int operationCount,
        long maximumSequence,
        CancellationToken cancellationToken)
    {
        if (maximumSequence != operationCount)
        {
            throw new MemoryIntegrityException(
                "Committed journal sequences are not contiguous from sequence one.");
        }

        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM (
                SELECT journal_sequence,
                       ROW_NUMBER() OVER (ORDER BY journal_sequence) AS expected_sequence
                FROM append_operations
            )
            WHERE journal_sequence <> expected_sequence;
            """;
        var mismatchCount = Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        if (mismatchCount != 0)
        {
            throw new MemoryIntegrityException(
                "Committed journal sequences are duplicated, missing, or out of order.");
        }
    }
}

internal sealed record MemoryHealthReport(
    long MaximumJournalSequence,
    long OperationCount,
    long RecordCount,
    long LinkCount);
