using CompanionCore.Memory;

namespace CompanionCore.Api;

/// <summary>Read-only retrieval seam. The bridge never receives a write or maintenance surface.</summary>
public interface ILocalMemoryReader
{
    Task<IReadOnlyList<RetrievedMemory>> RetrieveBySubjectAsync(string subjectKey, CancellationToken cancellationToken);
}

/// <summary>Adapts the accepted repository's public read-only retrieval.</summary>
public sealed class MemoryRepositoryReader : ILocalMemoryReader
{
    private readonly MemoryRepository _repository;

    public MemoryRepositoryReader(MemoryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<IReadOnlyList<RetrievedMemory>> RetrieveBySubjectAsync(
        string subjectKey,
        CancellationToken cancellationToken) =>
        _repository.RetrieveBySubjectAsync(subjectKey, cancellationToken);
}
