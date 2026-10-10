using CompanionCore.Memory;

namespace CompanionCore.Recall;

/// <summary>
/// Grouped evolving belief: the current understanding first, every other stance kept
/// (newest first). Unlinked concurrent stances stay in <see cref="Earlier"/> too, so nothing is hidden.
/// </summary>
public sealed record BeliefHistory(RetrievedMemory? Current, IReadOnlyList<RetrievedMemory> Earlier)
{
    public static BeliefHistory From(IReadOnlyList<RetrievedMemory> subjectRecords)
    {
        ArgumentNullException.ThrowIfNull(subjectRecords);
        var current = subjectRecords.FirstOrDefault(memory => memory.IsCurrent);
        var earlier = subjectRecords
            .Where(memory => !ReferenceEquals(memory, current))
            .OrderByDescending(memory => memory.Record.CreatedAtUtc)
            .ThenByDescending(memory => memory.JournalSequence)
            .ToArray();
        return new BeliefHistory(current, earlier);
    }
}
