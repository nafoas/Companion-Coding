using CompanionCore.Api;
using CompanionCore.Memory;

namespace CompanionCore.Sightings.Tests;

/// <summary>One isolated synthetic test root. Nothing here can resolve a development or production root.</summary>
internal sealed class SightingHarness : IDisposable
{
    internal static readonly DateTimeOffset T0 = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    internal SightingHarness()
    {
        BasePath = Path.Combine(Path.GetTempPath(), "CompanionCore.Sightings.Tests", Guid.NewGuid().ToString("N"));
        MemoryLocation = TestDataRootPolicy.Create(BasePath, Guid.NewGuid());
        Location = SightingLocation.For(MemoryLocation);
    }

    internal string BasePath { get; }

    internal MemoryStoreLocation MemoryLocation { get; }

    internal SightingLocation Location { get; }

    internal SightingStore Open() => SightingStore.Open(Location, out _);

    internal SightingStore Open(out SightingOpenReport report) => SightingStore.Open(Location, out report);

    internal static Sighting Seen(string thing, DateTimeOffset at, string game = "Hollow House", ObservationKind kind = ObservationKind.Creature, bool danger = false, string? situation = null, string? words = null) =>
        new(at, game, situation, thing, kind, danger, words ?? thing, SightingSource.Observation);

    internal static MemoryRecordDraft Draft(string subject, string recollection, string game = "Hollow House", DateTimeOffset? at = null) => new()
    {
        RecordId = Guid.NewGuid(),
        CreatedAtUtc = at ?? T0,
        Scope = MemoryScope.General,
        SourceKind = MemorySourceKind.Observed,
        Confidence = 0.8,
        SubjectKey = subject,
        ApplicationReference = game,
        VisibleRecollection = recollection,
        RetrievalMetadataJson = "{}",
    };

    public void Dispose()
    {
        try
        {
            Directory.Delete(BasePath, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>A memory store stand-in: what the gate lets through is "committed" here.</summary>
internal sealed class Remembered : ILocalMemoryReader
{
    internal List<MemoryRecordDraft> Memories { get; } = [];

    public Task<IReadOnlyList<RetrievedMemory>> RetrieveBySubjectAsync(string subjectKey, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RetrievedMemory>>([.. Memories.Where(memory => memory.SubjectKey == subjectKey).Select(memory => new RetrievedMemory(memory, Guid.Empty, 0, "", true))]);

    internal async Task<int> SiftAsync(IMemoryNoveltyGate gate, params MemoryRecordDraft[] drafts)
    {
        var novel = await gate.SiftAsync(drafts, this, CancellationToken.None);
        Memories.AddRange(novel);
        return novel.Count;
    }
}
