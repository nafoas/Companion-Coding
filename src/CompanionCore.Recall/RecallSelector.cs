using System.Text;
using CompanionCore.Memory;

namespace CompanionCore.Recall;

public sealed record RecallContext(
    string? Game,
    string? Save,
    string? Session,
    IReadOnlyList<string> FocusSubjects,
    int MaximumRecords = 32,
    int MaximumRecollectionBytes = 24 * 1024);

public sealed record RecallSelection(
    IReadOnlyList<RetrievedMemory> Items,
    int SpoilersSuppressed,
    int OutOfScope,
    bool Truncated,
    int Resting = 0);

/// <summary>
/// Local relevant-context selection with spoiler suppression. Session memories need the
/// same session, save memories the same save, and spoiler-flagged knowledge is offered only
/// within the save where it was learned. Ranking keeps current, corrected
/// understanding first; nothing is ever removed from the store.
/// </summary>
public static class RecallSelector
{
    public static RecallSelection Select(IEnumerable<RetrievedMemory> candidates, RecallContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);
        if (context.MaximumRecords < 1 || context.MaximumRecollectionBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(context));
        }

        var focus = new HashSet<string>(context.FocusSubjects ?? [], StringComparer.Ordinal);
        var spoilers = 0;
        var outOfScope = 0;
        var eligible = new List<(RetrievedMemory Memory, double Score)>();
        foreach (var memory in candidates.DistinctBy(memory => memory.Record.RecordId))
        {
            var record = memory.Record;
            var metadata = RecallMetadata.Parse(record.RetrievalMetadataJson);
            var fromOtherSave = record.SaveReference is not null && !string.Equals(record.SaveReference, context.Save, StringComparison.Ordinal);
            var inScope = record.Scope switch
            {
                MemoryScope.Session => record.SessionReference is not null && string.Equals(record.SessionReference, context.Session, StringComparison.Ordinal),
                MemoryScope.Save => !fromOtherSave && record.SaveReference is not null && string.Equals(record.GameReference, context.Game, StringComparison.Ordinal),
                MemoryScope.Game => record.GameReference is not null && string.Equals(record.GameReference, context.Game, StringComparison.Ordinal),
                _ => true,
            };
            if (!inScope)
            {
                outOfScope++;
                continue;
            }

            // Spoiler knowledge reaches only the save it was learned in; unattributed spoilers stay suppressed.
            if (metadata.Spoiler && (fromOtherSave || record.SaveReference is null))
            {
                spoilers++;
                continue;
            }

            eligible.Add((memory, Score(memory, metadata, focus)));
        }

        var (selected, truncated) = Bounded(eligible, context.MaximumRecords, context.MaximumRecollectionBytes);
        return new RecallSelection(selected, spoilers, outOfScope, truncated);
    }

    /// <summary>
    /// What can still come up about one adventure at its stage (M4c, plan item 7h). Treasured
    /// Episodes (highlights) always; adventure context (its status and session summaries) until
    /// it is only its record; session detail only while active or freshly finished. The rest
    /// is resting, counted, and still in the store.
    /// </summary>
    public static RecallSelection SelectAdventure(
        IEnumerable<RetrievedMemory> candidates,
        string game,
        string save,
        AdventureStage stage,
        int maximumRecords = 32,
        int maximumRecollectionBytes = 24 * 1024)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(save);
        if (maximumRecords < 1 || maximumRecollectionBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRecords));
        }

        var resting = 0;
        var outOfScope = 0;
        var eligible = new List<(RetrievedMemory Memory, double Score)>();
        foreach (var memory in candidates.DistinctBy(memory => memory.Record.RecordId))
        {
            var record = memory.Record;
            var metadata = RecallMetadata.Parse(record.RetrievalMetadataJson);
            if (!string.Equals(record.GameReference, game, StringComparison.Ordinal) || !string.Equals(record.SaveReference, save, StringComparison.Ordinal)
                || metadata.Kind is RecallRecordKind.AdventureHypothesis)
            {
                outOfScope++;
                continue;
            }

            // Treasured Episodes are the preserved highlights; a highlighted original is session detail.
            var offered = metadata.Kind == RecallRecordKind.Highlight
                || stage is AdventureStage.Active or AdventureStage.FreshlyFinished
                || metadata.Kind == RecallRecordKind.Adventure
                || (stage == AdventureStage.Remembered && metadata.Kind == RecallRecordKind.Summary);
            if (!offered)
            {
                resting++;
                continue;
            }

            eligible.Add((memory, Score(memory, metadata, [])));
        }

        var (selected, truncated) = Bounded(eligible, maximumRecords, maximumRecollectionBytes);
        return new RecallSelection(selected, 0, outOfScope, truncated, resting);
    }

    private static (List<RetrievedMemory> Selected, bool Truncated) Bounded(List<(RetrievedMemory Memory, double Score)> eligible, int maximumRecords, int maximumBytes)
    {
        var selected = new List<RetrievedMemory>();
        var bytes = 0;
        foreach (var (memory, _) in eligible
                     .OrderByDescending(item => item.Score)
                     .ThenByDescending(item => item.Memory.Record.CreatedAtUtc)
                     .ThenBy(item => item.Memory.Record.RecordId))
        {
            var size = Encoding.UTF8.GetByteCount(memory.Record.VisibleRecollection);
            if (selected.Count == maximumRecords || bytes + size > maximumBytes)
            {
                return (selected, true);
            }

            bytes += size;
            selected.Add(memory);
        }

        return (selected, false);
    }

    /// <summary>Below the smallest authority gap (5 points), so confidence never reorders sources.</summary>
    internal const double ConfidenceWeight = 4.0;

    internal static int SourceRank(MemorySourceKind kind) => kind switch
    {
        MemorySourceKind.UserCorrection => 700,
        MemorySourceKind.Integration => 600,
        MemorySourceKind.Told => 550,
        MemorySourceKind.Observed => 500,
        MemorySourceKind.Read => 450,
        MemorySourceKind.Remembered => 400,
        MemorySourceKind.Inferred => 300,
        MemorySourceKind.Guess => 100,
        _ => 0,
    };

    /// <summary>
    /// Tiered score: current understanding first, then a current user correction above every
    /// other source, then relevance bonuses, then strict source authority. Confidence only
    /// breaks ties inside one authority rank (its weight stays below the smallest rank gap).
    /// </summary>
    private static double Score(RetrievedMemory memory, RecallMetadata metadata, HashSet<string> focus)
    {
        var score = memory.IsCurrent ? 10_000.0 : 0.0;
        if (memory.IsCurrent && memory.Record.SourceKind == MemorySourceKind.UserCorrection)
        {
            score += 5_000;
        }

        if (focus.Contains(memory.Record.SubjectKey))
        {
            score += 200;
        }

        // The ladder (plan item 7h): Treasured Episodes, then adventure context, then session detail.
        if (metadata.Highlight)
        {
            score += 300;
        }

        if (metadata.Kind is RecallRecordKind.Summary or RecallRecordKind.Adventure && memory.IsCurrent)
        {
            score += 150;
        }

        return score + (SourceRank(memory.Record.SourceKind) / 10.0) + (memory.Record.Confidence * ConfidenceWeight);
    }
}

/// <summary>Gathers candidates locally through bounded read-only queries, then selects.</summary>
public static class RecallService
{
    public const int CandidateLimit = 500;

    public static async Task<RecallSelection> RecallAsync(
        MemoryRepository repository,
        RecallContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(context);
        var candidates = new List<RetrievedMemory>();
        if (context.Game is not null)
        {
            candidates.AddRange(await repository.RetrieveAsync(new MemoryQuery { GameReference = context.Game, Limit = CandidateLimit }, cancellationToken).ConfigureAwait(false));
        }

        if (context.Session is not null)
        {
            candidates.AddRange(await repository.RetrieveAsync(new MemoryQuery { SessionReference = context.Session, Limit = CandidateLimit }, cancellationToken).ConfigureAwait(false));
        }

        foreach (var subject in (context.FocusSubjects ?? []).Distinct(StringComparer.Ordinal).Take(16))
        {
            candidates.AddRange(await repository.RetrieveBySubjectAsync(subject, cancellationToken).ConfigureAwait(false));
        }

        return RecallSelector.Select(candidates, context);
    }
}
