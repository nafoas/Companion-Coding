using System.Text.Json.Serialization;
using CompanionCore.Capture.Contracts;
using CompanionCore.Memory;

namespace CompanionCore.Api;

// Strict schema-v1 wire shapes. Unknown members, duplicate members, integer enums, and
// missing required members are all rejected by SemanticSchema.StrictOptions.

internal sealed class SemanticResponseWire
{
    [JsonRequired]
    public int SchemaVersion { get; set; }

    [JsonRequired]
    public Guid OperationId { get; set; }

    [JsonRequired]
    public InterpretationWire Interpretation { get; set; } = null!;

    public List<MemoryProposalWire>? MemoryProposals { get; set; }

    public UsageWire? Usage { get; set; }
}

internal sealed class InterpretationWire
{
    [JsonRequired]
    public string Summary { get; set; } = null!;

    public List<ObservationWire>? Observations { get; set; }
}

internal sealed class ObservationWire
{
    [JsonRequired]
    public AttentionRegionKind Region { get; set; }

    [JsonRequired]
    public string Label { get; set; } = null!;

    [JsonRequired]
    public double Confidence { get; set; }
}

/// <summary>
/// The schema can name non-append operations and a target record so that a remote
/// update/delete is representable, and therefore locally rejected, rather than merely
/// malformed. Only <see cref="SemanticSchema.AppendOperationName"/> is ever admitted.
/// </summary>
internal sealed class MemoryProposalWire
{
    [JsonRequired]
    public string Operation { get; set; } = null!;

    public Guid? TargetRecordId { get; set; }

    public MemoryScope? Scope { get; set; }

    public MemorySourceKind? SourceKind { get; set; }

    public double? Confidence { get; set; }

    public string? SubjectKey { get; set; }

    public List<string>? EntityReferences { get; set; }

    public string? Recollection { get; set; }

    public List<LinkWire>? Links { get; set; }
}

internal sealed class LinkWire
{
    [JsonRequired]
    public Guid TargetRecordId { get; set; }

    [JsonRequired]
    public MemoryLinkKind Kind { get; set; }
}

internal sealed class UsageWire
{
    [JsonRequired]
    public long InputUnits { get; set; }

    [JsonRequired]
    public long OutputUnits { get; set; }
}
