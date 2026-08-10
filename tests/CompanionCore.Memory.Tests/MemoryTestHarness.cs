using Microsoft.Data.Sqlite;

namespace CompanionCore.Memory.Tests;

internal sealed class MemoryTestDirectory : IDisposable
{
    internal MemoryTestDirectory()
    {
        BasePath = Path.Combine(
            Path.GetTempPath(),
            "CompanionCore.Memory.Tests",
            Guid.NewGuid().ToString("N"));
        Location = TestDataRootPolicy.Create(BasePath, Guid.NewGuid());
    }

    internal string BasePath { get; }

    internal MemoryStoreLocation Location { get; }

    internal Task<MemoryRepository> OpenRepositoryAsync() =>
        MemoryRepository.OpenAsync(Location);

    public void Dispose()
    {
        if (Directory.Exists(BasePath))
        {
            Directory.Delete(BasePath, recursive: true);
        }
    }
}

internal static class SyntheticMemory
{
    internal static readonly DateTimeOffset BaselineUtc =
        new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    internal static MemoryRecordDraft Record(
        Guid? recordId = null,
        string subjectKey = "synthetic.subject",
        DateTimeOffset? createdAtUtc = null,
        MemoryScope scope = MemoryScope.General,
        MemorySourceKind sourceKind = MemorySourceKind.Observed,
        double confidence = 0.75,
        string visibleRecollection = "A neutral synthetic recollection.",
        IReadOnlyList<MemoryLink>? links = null) =>
        new()
        {
            RecordId = recordId ?? Guid.NewGuid(),
            CreatedAtUtc = createdAtUtc ?? BaselineUtc,
            Scope = scope,
            SourceKind = sourceKind,
            Confidence = confidence,
            SubjectKey = subjectKey,
            EntityReferences = ["synthetic.entity"],
            ApplicationReference = "synthetic.application",
            GameReference = "synthetic.game",
            SaveReference = "synthetic.save",
            SessionReference = "synthetic.session",
            VisibleRecollection = visibleRecollection,
            RetrievalMetadataJson = "{\"category\":\"synthetic\",\"priority\":2}",
            Links = links ?? Array.Empty<MemoryLink>(),
        };

    internal static AppendMemoryProposal Proposal(params MemoryRecordDraft[] records) =>
        new(Guid.NewGuid(), records);

    internal static async Task ExecuteSqlAsync(string databasePath, string sql)
    {
        await using var connection = OpenConnection(databasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    internal static SqliteConnection OpenConnection(string databasePath) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
}
