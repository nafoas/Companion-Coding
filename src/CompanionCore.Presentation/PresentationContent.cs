namespace CompanionCore.Presentation;

/// <summary>
/// Opaque output of <see cref="IPersonalityAdapter"/>: a stable content key (for tests
/// and for Stage 13's later replacement to key off) plus an expression intent.
/// <see cref="IPresentationSink"/> renders this without interpreting it further.
/// </summary>
public sealed record PresentationContent(string ContentKey, ExpressionIntent Intent);
