using System.Buffers.Binary;
using System.Security.Cryptography;
using CompanionCore.Memory;

namespace CompanionCore.Recall;

public sealed record ConsolidationPlan(
    AppendMemoryProposal Proposal,
    IReadOnlyList<Guid> SummaryRecordIds,
    IReadOnlyList<Guid> HighlightRecordIds,
    int RoutineCount);

/// <summary>
/// Pure PRINCE mechanics that turn local state into append-only proposals. Nothing here
/// can update or delete: summaries and highlights link to their originals, statuses and
/// corrections supersede or correct by link, and every visible text is a neutral
/// placeholder until the Stage 13 voice is installed on Builder Prince.
/// </summary>
public static class ConsolidationPlanner
{
    public const int MaximumSourcesPerSummary = 256;

    /// <summary>Matches the accepted store's per-operation record bound.</summary>
    public const int MaximumRecordsPerConsolidation = 128;

    /// <summary>
    /// Plans one atomic session consolidation. Replaying the same operation ID and time is
    /// <c>AlreadyCommitted</c>; a re-plan at a different time under the same operation is a
    /// store conflict, so callers persist the operation's time with its intent.
    /// </summary>
    public static ConsolidationPlan PlanSessionSummary(
        Guid operationId,
        DateTimeOffset now,
        string session,
        string? game,
        string? save,
        IReadOnlyList<RetrievedMemory> originals)
    {
        ArgumentNullException.ThrowIfNull(originals);
        ArgumentException.ThrowIfNullOrWhiteSpace(session);
        var entries = originals
            .Select(memory => memory ?? throw new ArgumentException("An original is null.", nameof(originals)))
            .DistinctBy(memory => memory.Record.RecordId)
            .Where(memory => RecallMetadata.Parse(memory.Record.RetrievalMetadataJson).Kind
                is not RecallRecordKind.Summary and not RecallRecordKind.Highlight)
            .OrderBy(memory => memory.JournalSequence)
            .ThenBy(memory => memory.Record.RecordId)
            .ToArray();
        if (entries.Length == 0)
        {
            throw new ArgumentException("A session summary needs at least one original entry.", nameof(originals));
        }

        if (entries.Any(memory => !string.Equals(memory.Record.SessionReference, session, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Every consolidated original must belong to the session.", nameof(originals));
        }

        if (entries.Any(memory => (memory.Record.GameReference is not null && !string.Equals(memory.Record.GameReference, game, StringComparison.Ordinal))
                                  || (memory.Record.SaveReference is not null && !string.Equals(memory.Record.SaveReference, save, StringComparison.Ordinal))))
        {
            throw new ArgumentException("A consolidated original belongs to a different game or save.", nameof(originals));
        }

        var drafts = new List<MemoryRecordDraft>();
        var highlights = entries.Where(memory => RecallMetadata.Parse(memory.Record.RetrievalMetadataJson).Highlight).ToArray();
        var summaryScope = save is not null ? MemoryScope.Save : game is not null ? MemoryScope.Game : MemoryScope.General;
        var summaryIds = new List<Guid>();
        var chunks = entries.Chunk(MaximumSourcesPerSummary).ToArray();
        foreach (var chunk in chunks)
        {
            var id = DeriveRecordId(operationId, drafts.Count);
            summaryIds.Add(id);
            drafts.Add(new MemoryRecordDraft
            {
                RecordId = id,
                CreatedAtUtc = now.ToUniversalTime(),
                Scope = summaryScope,
                SourceKind = MemorySourceKind.Remembered,
                Confidence = 0.9,
                SubjectKey = RecallSubjects.Summary(session),
                GameReference = game,
                SaveReference = save,
                SessionReference = session,
                VisibleRecollection = $"[neutral session summary] {chunk.Length} entries consolidated; {highlights.Length} highlights preserved.",
                RetrievalMetadataJson = new RecallMetadata
                {
                    Kind = RecallRecordKind.Summary,
                    ConsolidatedCount = chunk.Length,
                    HighlightCount = highlights.Length,
                    Spoiler = chunk.Any(memory => RecallMetadata.Parse(memory.Record.RetrievalMetadataJson).Spoiler),
                }.ToJson(),
                Links = chunk.Select(memory => new MemoryLink(memory.Record.RecordId, MemoryLinkKind.Source)).ToArray(),
            });
        }

        // Highlights are preserved verbatim (close to the original), linked to their source.
        var highlightIds = new List<Guid>();
        foreach (var highlight in highlights)
        {
            var id = DeriveRecordId(operationId, drafts.Count);
            highlightIds.Add(id);
            var metadata = RecallMetadata.Parse(highlight.Record.RetrievalMetadataJson);
            drafts.Add(new MemoryRecordDraft
            {
                RecordId = id,
                CreatedAtUtc = now.ToUniversalTime(),
                Scope = game is not null ? MemoryScope.Game : MemoryScope.General,
                SourceKind = MemorySourceKind.Remembered,
                Confidence = highlight.Record.Confidence,
                SubjectKey = RecallSubjects.Highlight(highlight.Record.RecordId),
                EntityReferences = highlight.Record.EntityReferences,
                GameReference = game,
                SaveReference = save,
                SessionReference = session,
                VisibleRecollection = highlight.Record.VisibleRecollection,
                RetrievalMetadataJson = new RecallMetadata { Kind = RecallRecordKind.Highlight, Highlight = true, Spoiler = metadata.Spoiler }.ToJson(),
                Links = [new MemoryLink(highlight.Record.RecordId, MemoryLinkKind.Source)],
            });
        }

        if (drafts.Count > MaximumRecordsPerConsolidation)
        {
            // One atomic append per consolidation; the caller splits an oversized session.
            throw new ArgumentException("The consolidation exceeds one append operation; split the session.", nameof(originals));
        }

        return new ConsolidationPlan(
            new AppendMemoryProposal(operationId, drafts),
            summaryIds,
            highlightIds,
            entries.Length - highlights.Length);
    }

    public static AppendMemoryProposal PlanAdventureStatus(
        Guid operationId,
        DateTimeOffset now,
        string game,
        string save,
        AdventureStatus status,
        RetrievedMemory? previousStatus,
        MemorySourceKind sourceKind = MemorySourceKind.Observed)
    {
        var subject = RecallSubjects.Adventure(game, save);
        RequireSameSubject(previousStatus, subject);
        return Single(operationId, new MemoryRecordDraft
        {
            RecordId = DeriveRecordId(operationId, 0),
            CreatedAtUtc = now.ToUniversalTime(),
            Scope = MemoryScope.Save,
            SourceKind = sourceKind,
            Confidence = 1.0,
            SubjectKey = subject,
            GameReference = game,
            SaveReference = save,
            VisibleRecollection = $"[neutral adventure record] status: {status}.",
            RetrievalMetadataJson = new RecallMetadata { Kind = RecallRecordKind.Adventure, Adventure = status }.ToJson(),
            Links = previousStatus is null ? [] : [new MemoryLink(previousStatus.Record.RecordId, MemoryLinkKind.Supersedes)],
        });
    }

    /// <summary>A new-save or ending hypothesis: a guess awaiting Boss confirmation.</summary>
    public static AppendMemoryProposal PlanAdventureHypothesis(
        Guid operationId,
        DateTimeOffset now,
        string game,
        string save,
        AdventureHypothesis hypothesis) =>
        Single(operationId, new MemoryRecordDraft
        {
            RecordId = DeriveRecordId(operationId, 0),
            CreatedAtUtc = now.ToUniversalTime(),
            Scope = MemoryScope.Save,
            SourceKind = MemorySourceKind.Guess,
            Confidence = 0.5,
            SubjectKey = RecallSubjects.AdventureHypothesis(game, save),
            GameReference = game,
            SaveReference = save,
            VisibleRecollection = $"[neutral hypothesis] {hypothesis}; awaiting confirmation.",
            RetrievalMetadataJson = new RecallMetadata { Kind = RecallRecordKind.AdventureHypothesis, Hypothesis = hypothesis }.ToJson(),
        });

    /// <summary>Boss's answer to a hypothesis: a user correction that supersedes it.</summary>
    public static AppendMemoryProposal PlanHypothesisAnswer(
        Guid operationId,
        DateTimeOffset now,
        RetrievedMemory hypothesis,
        bool confirmed)
    {
        ArgumentNullException.ThrowIfNull(hypothesis);
        var metadata = RecallMetadata.Parse(hypothesis.Record.RetrievalMetadataJson);
        if (metadata.Kind != RecallRecordKind.AdventureHypothesis)
        {
            throw new ArgumentException("Only an adventure hypothesis can be answered.", nameof(hypothesis));
        }

        return Single(operationId, new MemoryRecordDraft
        {
            RecordId = DeriveRecordId(operationId, 0),
            CreatedAtUtc = now.ToUniversalTime(),
            Scope = hypothesis.Record.Scope,
            SourceKind = MemorySourceKind.UserCorrection,
            Confidence = 1.0,
            SubjectKey = hypothesis.Record.SubjectKey,
            GameReference = hypothesis.Record.GameReference,
            SaveReference = hypothesis.Record.SaveReference,
            VisibleRecollection = $"[neutral confirmation] {metadata.Hypothesis}: {(confirmed ? "confirmed" : "declined")}.",
            RetrievalMetadataJson = new RecallMetadata { Kind = RecallRecordKind.Correction, Hypothesis = metadata.Hypothesis }.ToJson(),
            Links = [new MemoryLink(hypothesis.Record.RecordId, MemoryLinkKind.Supersedes)],
        });
    }

    /// <summary>Lore with provenance; a replacement corrects the earlier theory without removing it.</summary>
    public static AppendMemoryProposal PlanLore(
        Guid operationId,
        DateTimeOffset now,
        string game,
        string topic,
        string recollection,
        LoreStatus status,
        RetrievedMemory? replaces = null,
        MemorySourceKind? sourceKind = null,
        bool spoiler = false,
        string? save = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (spoiler && string.IsNullOrWhiteSpace(save))
        {
            // Spoiler knowledge is attributed to the save where it was learned.
            throw new ArgumentException("Spoiler-flagged lore requires the save where it was learned.", nameof(save));
        }

        var subject = RecallSubjects.Lore(game, topic);
        RequireSameSubject(replaces, subject);
        return Single(operationId, new MemoryRecordDraft
        {
            RecordId = DeriveRecordId(operationId, 0),
            CreatedAtUtc = now.ToUniversalTime(),
            Scope = MemoryScope.Game,
            SourceKind = sourceKind ?? status switch
            {
                LoreStatus.Observed => MemorySourceKind.Observed,
                LoreStatus.Read => MemorySourceKind.Read,
                LoreStatus.Told => MemorySourceKind.Told,
                LoreStatus.Suspected => MemorySourceKind.Guess,
                _ => MemorySourceKind.Observed,
            },
            Confidence = status == LoreStatus.Suspected ? 0.4 : status == LoreStatus.Confirmed ? 0.95 : 0.75,
            SubjectKey = subject,
            GameReference = game,
            SaveReference = save,
            VisibleRecollection = recollection,
            RetrievalMetadataJson = new RecallMetadata { Kind = RecallRecordKind.Lore, Lore = status, Spoiler = spoiler }.ToJson(),
            Links = replaces is null ? [] : [new MemoryLink(replaces.Record.RecordId, MemoryLinkKind.Corrects)],
        });
    }

    /// <summary>An evolving opinion: appended and grouped by subject, superseding the prior stance.</summary>
    public static AppendMemoryProposal PlanOpinion(
        Guid operationId,
        DateTimeOffset now,
        string topic,
        string recollection,
        RetrievedMemory? previous = null)
    {
        var subject = RecallSubjects.Opinion(topic);
        RequireSameSubject(previous, subject);
        return Single(operationId, new MemoryRecordDraft
        {
            RecordId = DeriveRecordId(operationId, 0),
            CreatedAtUtc = now.ToUniversalTime(),
            Scope = MemoryScope.General,
            SourceKind = MemorySourceKind.Inferred,
            Confidence = 0.7,
            SubjectKey = subject,
            VisibleRecollection = recollection,
            RetrievalMetadataJson = new RecallMetadata { Kind = RecallRecordKind.Opinion }.ToJson(),
            Links = previous is null ? [] : [new MemoryLink(previous.Record.RecordId, MemoryLinkKind.Supersedes)],
        });
    }

    /// <summary>Boss's correction: the highest authority, appended against its target.</summary>
    public static AppendMemoryProposal PlanUserCorrection(
        Guid operationId,
        DateTimeOffset now,
        RetrievedMemory target,
        string recollection)
    {
        ArgumentNullException.ThrowIfNull(target);
        return Single(operationId, new MemoryRecordDraft
        {
            RecordId = DeriveRecordId(operationId, 0),
            CreatedAtUtc = now.ToUniversalTime(),
            Scope = target.Record.Scope,
            SourceKind = MemorySourceKind.UserCorrection,
            Confidence = 1.0,
            SubjectKey = target.Record.SubjectKey,
            GameReference = target.Record.GameReference,
            SaveReference = target.Record.SaveReference,
            SessionReference = target.Record.SessionReference,
            VisibleRecollection = recollection,
            RetrievalMetadataJson = new RecallMetadata { Kind = RecallRecordKind.Correction }.ToJson(),
            Links = [new MemoryLink(target.Record.RecordId, MemoryLinkKind.Corrects)],
        });
    }

    internal static Guid DeriveRecordId(Guid operationId, int index)
    {
        Span<byte> input = stackalloc byte[64];
        var prefix = "companion.recall.record.v1"u8;
        prefix.CopyTo(input);
        operationId.TryWriteBytes(input[prefix.Length..], bigEndian: true, out _);
        BinaryPrimitives.WriteInt32BigEndian(input[(prefix.Length + 16)..], index);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input[..(prefix.Length + 20)], hash);
        var bytes = hash[..16].ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private static AppendMemoryProposal Single(Guid operationId, MemoryRecordDraft draft) =>
        new(operationId, [draft]);

    private static void RequireSameSubject(RetrievedMemory? previous, string subject)
    {
        if (previous is not null && !string.Equals(previous.Record.SubjectKey, subject, StringComparison.Ordinal))
        {
            throw new ArgumentException("A superseded or corrected record must share the exact subject.");
        }
    }
}
