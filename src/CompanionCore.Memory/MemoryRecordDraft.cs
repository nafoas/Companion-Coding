namespace CompanionCore.Memory;

/// <summary>
/// Immutable content proposed for one append. Local operation ID, commit sequence, and
/// checksums are supplied by the enclosing append protocol rather than by callers.
/// </summary>
public sealed record MemoryRecordDraft
{
    public Guid RecordId { get; init; }

    public int SchemaVersion { get; init; } = MemorySchema.CurrentVersion;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public MemoryScope Scope { get; init; }

    public MemorySourceKind SourceKind { get; init; }

    public double Confidence { get; init; }

    public string SubjectKey { get; init; } = string.Empty;

    public IReadOnlyList<string> EntityReferences { get; init; } = Array.Empty<string>();

    public string? ApplicationReference { get; init; }

    public string? GameReference { get; init; }

    public string? SaveReference { get; init; }

    public string? SessionReference { get; init; }

    public string VisibleRecollection { get; init; } = string.Empty;

    public string RetrievalMetadataJson { get; init; } = "{}";

    public IReadOnlyList<MemoryLink> Links { get; init; } = Array.Empty<MemoryLink>();
}
