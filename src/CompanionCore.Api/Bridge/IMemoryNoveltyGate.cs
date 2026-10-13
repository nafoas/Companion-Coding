using CompanionCore.Memory;

namespace CompanionCore.Api;

/// <summary>
/// Between the safety allowlist and the write gate (M3a, plan item 1): seeing something is not
/// the same as remembering it. Every draft is recorded as a sighting; the gate returns only the
/// drafts that add something he does not already remember, in their original order. It runs
/// under the same privacy admission as the write it guards, and never edits a memory.
/// </summary>
public interface IMemoryNoveltyGate
{
    Task<IReadOnlyList<MemoryRecordDraft>> SiftAsync(
        IReadOnlyList<MemoryRecordDraft> drafts,
        ILocalMemoryReader memories,
        CancellationToken cancellationToken);
}
