using CompanionCore.Memory;

namespace CompanionCore.Api;

/// <summary>
/// Locally assembled continuity for exactly one request. It is rebuilt for every
/// operation from committed local memory and the local bridge journal; nothing remote
/// persists it, and no remote conversation or capsule identifier exists.
/// </summary>
public sealed record ResumePacket(
    Guid PacketId,
    DateTimeOffset BuiltAtUtc,
    string? SessionReference,
    string? ApplicationReference,
    IReadOnlyList<string> Subjects,
    IReadOnlyList<ResumePacketItem> Items,
    long BasisSequence,
    bool Truncated);

/// <summary>One committed local memory supplied as labeled context.</summary>
public sealed record ResumePacketItem(
    Guid RecordId,
    string SubjectKey,
    MemoryScope Scope,
    MemorySourceKind SourceKind,
    double Confidence,
    bool IsCurrent,
    DateTimeOffset CreatedAtUtc,
    string Recollection);

/// <summary>Caller-supplied local focus for one request.</summary>
public sealed record ResumeContext(IReadOnlyList<string> FocusSubjects, string? ApplicationReference = null)
{
    public static ResumeContext Empty { get; } = new(Array.Empty<string>());
}
