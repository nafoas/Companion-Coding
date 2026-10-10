using CompanionCore.Capture.Contracts;

namespace CompanionCore.Api;

/// <summary>One validated, structured interpretation, published at most once per operation.</summary>
public sealed record SemanticInterpretation(
    Guid OperationId,
    Guid TargetSessionId,
    string Summary,
    IReadOnlyList<SemanticObservation> Observations);

public sealed record SemanticObservation(AttentionRegionKind Region, string Label, double Confidence);
