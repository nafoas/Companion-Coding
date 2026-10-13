using CompanionCore.Api;
using CompanionCore.Memory;

namespace CompanionCore.Sightings;

/// <summary>
/// Memories are novel, not a log (plan item 1). Every proposal is recorded as a sighting; one
/// becomes a memory only if it adds something he does not already remember: the first time, or
/// a meaningful change. A repeat or near-duplicate (even with only its numbers changed) stays in
/// the record and grows the impression instead. Existing memories are only ever read.
/// </summary>
public sealed class SightingNoveltyGate : IMemoryNoveltyGate
{
    private readonly SightingStore _store;
    private readonly Func<string?> _situation;

    public SightingNoveltyGate(SightingStore store, Func<string?>? situation = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _situation = situation ?? (static () => null);
    }

    public async Task<IReadOnlyList<MemoryRecordDraft>> SiftAsync(IReadOnlyList<MemoryRecordDraft> drafts, ILocalMemoryReader memories, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(memories);
        var novel = new List<MemoryRecordDraft>();
        var known = new Dictionary<string, List<MemoryRecordDraft>>(StringComparer.Ordinal);
        foreach (var draft in drafts)
        {
            if (!known.TryGetValue(draft.SubjectKey, out var remembered))
            {
                var existing = await memories.RetrieveBySubjectAsync(draft.SubjectKey, cancellationToken).ConfigureAwait(false);
                known[draft.SubjectKey] = remembered = [.. existing.Where(memory => memory.IsCurrent).Select(memory => memory.Record)];
            }

            var repeat = remembered.Any(memory =>
                string.Equals(memory.ApplicationReference, draft.ApplicationReference, StringComparison.Ordinal)
                && Likeness.Same(memory.VisibleRecollection, draft.VisibleRecollection));
            _store.Record(new Sighting(
                draft.CreatedAtUtc,
                draft.ApplicationReference ?? "unknown",
                _situation(),
                draft.SubjectKey,
                ObservationKind.Other,
                Danger: false,
                draft.VisibleRecollection,
                SightingSource.Proposal,
                BecameMemory: !repeat));
            if (!repeat)
            {
                novel.Add(draft);
                remembered.Add(draft);
            }
        }

        return novel;
    }
}
