using Microsoft.Data.Sqlite;

namespace CompanionCore.Memory.Tests;

public sealed class MemoryStoreSchemaTests
{
    [Fact]
    public async Task NewStore_InitializesDurableVersionOneSchema()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();

        var diagnostics = await repository.Store.ReadDiagnosticsAsync(default);
        var counts = await repository.Store.ReadCountsAsync(default);

        Assert.Equal("wal", diagnostics.JournalMode, ignoreCase: true);
        Assert.Equal(2, diagnostics.SynchronousLevel);
        Assert.True(diagnostics.ForeignKeysEnabled);
        Assert.Equal(MemorySchema.CurrentVersion, diagnostics.SchemaVersion);
        Assert.Equal(8, diagnostics.TriggerCount);
        Assert.Equal((0L, 0L, 0L), counts);
        Assert.StartsWith(directory.Location.RootPath, repository.Store.DatabasePath);
        Assert.True(File.Exists(repository.Store.DatabasePath));
        Assert.True(File.Exists(directory.Location.JournalPath));
    }

    [Fact]
    public async Task DirectUpdatesAndDeletes_AreBlockedForEveryCommittedTable()
    {
        using var directory = new MemoryTestDirectory();
        string databasePath;
        await using (var repository = await directory.OpenRepositoryAsync())
        {
            var source = SyntheticMemory.Record(subjectKey: "synthetic.source");
            var linked = SyntheticMemory.Record(
                subjectKey: "synthetic.linked",
                links: [new MemoryLink(source.RecordId, MemoryLinkKind.Source)]);
            var result = await repository.WriteGate.SubmitAsync(
                SyntheticMemory.Proposal(source, linked));
            Assert.Equal(WriteGateStatus.Committed, result.Status);
            databasePath = repository.Store.DatabasePath;
        }

        var statements = new[]
        {
            "UPDATE schema_info SET schema_version = 1 WHERE singleton = 1;",
            "DELETE FROM schema_info WHERE singleton = 1;",
            "UPDATE append_operations SET operation_checksum = operation_checksum;",
            "DELETE FROM append_operations;",
            "UPDATE memory_records SET visible_recollection = visible_recollection;",
            "DELETE FROM memory_records;",
            "UPDATE memory_links SET link_kind = link_kind;",
            "DELETE FROM memory_links;",
        };

        await using var connection = SyntheticMemory.OpenConnection(databasePath);
        await connection.OpenAsync();
        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            await Assert.ThrowsAsync<SqliteException>(async () =>
                await command.ExecuteNonQueryAsync());
        }
    }

    [Fact]
    public async Task UnknownSchemaVersion_FailsClosedWithoutCreatingJournalOrChangingData()
    {
        using var directory = new MemoryTestDirectory();
        Directory.CreateDirectory(directory.Location.RootPath);
        await using (var connection = new SqliteConnection(
                         $"Data Source={directory.Location.DatabasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA user_version = 99;
                CREATE TABLE schema_info (
                    singleton INTEGER PRIMARY KEY,
                    schema_version INTEGER NOT NULL);
                INSERT INTO schema_info VALUES (1, 99);
                CREATE TABLE sentinel (value TEXT NOT NULL);
                INSERT INTO sentinel VALUES ('preserve-me');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<UnsupportedMemorySchemaException>(() =>
            MemoryRepository.OpenAsync(directory.Location));

        Assert.Equal(99, exception.ActualVersion);
        Assert.False(File.Exists(directory.Location.JournalPath));
        await using var verify = SyntheticMemory.OpenConnection(directory.Location.DatabasePath);
        await verify.OpenAsync();
        await using var verifyCommand = verify.CreateCommand();
        verifyCommand.CommandText = "SELECT value FROM sentinel;";
        Assert.Equal("preserve-me", (string?)await verifyCommand.ExecuteScalarAsync());
    }

    [Fact]
    public async Task IncompleteVersionOneSchema_IsRejectedRatherThanAutoRewritten()
    {
        using var directory = new MemoryTestDirectory();
        Directory.CreateDirectory(directory.Location.RootPath);
        await using (var connection = new SqliteConnection(
                         $"Data Source={directory.Location.DatabasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA user_version = 1;
                CREATE TABLE schema_info (
                    singleton INTEGER PRIMARY KEY,
                    schema_version INTEGER NOT NULL);
                INSERT INTO schema_info VALUES (1, 1);
                """;
            await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<MemoryIntegrityException>(() =>
            MemoryRepository.OpenAsync(directory.Location));
        Assert.False(File.Exists(directory.Location.JournalPath));
    }
}
