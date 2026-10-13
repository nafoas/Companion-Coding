using CompanionCore.Capture.Contracts;

namespace CompanionCore.Api;

/// <summary>One validated, structured interpretation, published at most once per operation.</summary>
public sealed record SemanticInterpretation(
    Guid OperationId,
    Guid TargetSessionId,
    string Summary,
    IReadOnlyList<SemanticObservation> Observations)
{
    /// <summary>Threats being fought in this look and how it is going (M3b); older answers carry none.</summary>
    public IReadOnlyList<SemanticEncounter> Encounters { get; init; } = [];
}

/// <summary>One threat being fought, and how it is going for the person playing.</summary>
public sealed record SemanticEncounter(string Threat, EncounterOutcome Outcome);

public enum EncounterOutcome
{
    Ongoing = 0,
    BossWon = 1,
    BossHurt = 2,
    BossLost = 3,
}

/// <summary>
/// One thing the eyes saw. <see cref="Kind"/> and <see cref="Danger"/> (M3a) let the subconscious
/// get used to things by kind, and never get used to danger; older answers carry neither.
/// </summary>
public sealed record SemanticObservation(
    AttentionRegionKind Region,
    string Label,
    double Confidence,
    ObservationKind Kind = ObservationKind.Other,
    bool Danger = false);

/// <summary>What kind of thing an observation is, as the eyes see it.</summary>
public enum ObservationKind
{
    Other = 0,
    Creature = 1,
    Character = 2,
    Item = 3,
    Place = 4,
    Text = 5,
    Interface = 6,
    Event = 7,
}
