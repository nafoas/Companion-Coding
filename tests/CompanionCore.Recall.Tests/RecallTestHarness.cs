using CompanionCore.Memory;
using CompanionCore.Privacy;

namespace CompanionCore.Recall.Tests;

/// <summary>One isolated synthetic test memory root; nothing here can resolve a real data root.</summary>
internal sealed class RecallTestHarness : IAsyncDisposable
{
    internal static readonly DateTimeOffset BaselineUtc = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private RecallTestHarness(string basePath, MemoryStoreLocation location)
    {
        BasePath = basePath;
        Location = location;
    }

    internal string BasePath { get; }

    internal MemoryStoreLocation Location { get; }

    internal RuntimePrivacyState Privacy { get; } = new();

    internal MemoryRepository Repository { get; private set; } = null!;

    internal static async Task<RecallTestHarness> CreateAsync()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "CompanionCore.Recall.Tests", Guid.NewGuid().ToString("N"));
        var harness = new RecallTestHarness(basePath, TestDataRootPolicy.Create(basePath, Guid.NewGuid()));
        harness.Repository = await MemoryRepository.OpenAsync(harness.Location, harness.Privacy);
        return harness;
    }

    internal async Task ReopenAsync()
    {
        await Repository.DisposeAsync();
        Repository = await MemoryRepository.OpenAsync(Location, Privacy);
    }

    internal async Task<WriteGateResult> CommitAsync(AppendMemoryProposal proposal)
    {
        var result = await Repository.WriteGate.SubmitAsync(proposal);
        Assert.Equal(WriteGateStatus.Committed, result.Status);
        return result;
    }

    internal async Task<RetrievedMemory> CommitOneAsync(AppendMemoryProposal proposal)
    {
        var result = await CommitAsync(proposal);
        var retrieved = await Repository.RetrieveAsync(new MemoryQuery { RecordIds = result.RecordIds });
        return Assert.Single(retrieved);
    }

    public async ValueTask DisposeAsync()
    {
        await Repository.DisposeAsync();
        if (Directory.Exists(BasePath))
        {
            Directory.Delete(BasePath, recursive: true);
        }
    }

    /// <summary>A neutral session entry as an earlier capture path would have appended it.</summary>
    internal static MemoryRecordDraft Entry(
        string text,
        string session,
        string? game,
        string? save,
        int minute,
        bool highlight = false,
        bool spoiler = false,
        MemoryScope scope = MemoryScope.Session,
        string? subject = null) =>
        new()
        {
            RecordId = Guid.NewGuid(),
            CreatedAtUtc = BaselineUtc.AddMinutes(minute),
            Scope = scope,
            SourceKind = MemorySourceKind.Observed,
            Confidence = 0.8,
            SubjectKey = subject ?? $"entry:{session}:{minute}",
            EntityReferences = ["synthetic.entity"],
            GameReference = game,
            SaveReference = save,
            SessionReference = session,
            VisibleRecollection = text,
            RetrievalMetadataJson = new RecallMetadata { Highlight = highlight, Spoiler = spoiler }.ToJson(),
        };

    internal static RetrievedMemory Retrieved(MemoryRecordDraft record, bool isCurrent = true, long sequence = 1) =>
        new(record, Guid.NewGuid(), sequence, new string('0', 64), isCurrent);
}
