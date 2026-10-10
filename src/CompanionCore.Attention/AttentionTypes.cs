using CompanionCore.Capture.Contracts;

namespace CompanionCore.Attention;

public enum AttentionState
{
    Noticing = 1,
    Engaged = 2,
    HighAttention = 3,
    Afterglow = 4,
}

public enum AttentionEventKind
{
    Routine = 1,
    GlobalTransition = 2,
    Decisive = 3,
}

public enum DecisiveReason
{
    None = 0,
    Boss = 1,
    Miniboss = 2,
    LargeEnemyGroup = 3,
    MajorDeath = 4,
    Credits = 5,
    RareAchievement = 6,
    WatchTaskComplete = 7,
    ExplicitLookRequest = 8,
    ExceptionalCuriosity = 9,
}

public enum LocationFamiliarity
{
    Unknown = 0,
    Hazardous = 1,
    Cleared = 2,
    Safe = 3,
}

public enum AttentionEventDisposition
{
    Applied = 1,
    HeldForCorroboration = 2,
    Corroborated = 3,
    DuplicateTransition = 4,
    WrongSession = 5,
}

public enum AttentionIntentKind
{
    Observing = 1,
    Investigating = 2,
    Urgent = 3,
    HighAttentionStarted = 4,
    HighAttentionEnded = 5,
    AfterglowOpening = 6,
    AllClear = 7,
    ReturnedToNoticing = 8,
}

/// <summary>Normalized evidence inputs. Every value is finite and within [0, 1].</summary>
public sealed record AttentionSignals(
    double Novelty,
    double Change,
    double Salience,
    double Urgency,
    double Persistence,
    double Confidence,
    double PersonalRelevance = 0,
    double LoreRelevance = 0)
{
    internal void Validate()
    {
        foreach (var value in new[]
                 {
                     Novelty, Change, Salience, Urgency, Persistence, Confidence, PersonalRelevance, LoreRelevance,
                 })
        {
            if (!double.IsFinite(value) || value is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(AttentionSignals), "Attention signals must be finite values in [0, 1].");
            }
        }
    }
}

/// <summary>
/// One typed piece of evidence. <see cref="EvidenceSource"/> identifies an independent
/// source (a region, a semantic operation, an explicit request) for corroboration;
/// <see cref="TransitionKey"/> identifies a global transition for deduplication.
/// </summary>
public sealed record InterestEvent
{
    internal const int MaximumKeyCharacters = 256;
    internal const int MaximumEvidenceReferences = 16;

    public Guid TargetSessionId { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public NormalizedRegion Region { get; init; } = new(0, 0, 1, 1);

    public AttentionRegionKind RegionKind { get; init; } = AttentionRegionKind.FullContext;

    public required AttentionSignals Signals { get; init; }

    public AttentionEventKind Kind { get; init; } = AttentionEventKind.Routine;

    public DecisiveReason DecisiveReason { get; init; }

    public string? TopicKey { get; init; }

    public string? LocationKey { get; init; }

    public string? TransitionKey { get; init; }

    public string EvidenceSource { get; init; } = "unspecified";

    public IReadOnlyList<string> EvidenceReferences { get; init; } = Array.Empty<string>();

    internal void Validate()
    {
        if (TargetSessionId == Guid.Empty || Timestamp == default || Signals is null)
        {
            throw new ArgumentException("An interest event needs a session, a timestamp, and signals.");
        }

        Signals.Validate();
        if (!Region.IsValid || !Enum.IsDefined(RegionKind) || !Enum.IsDefined(Kind) || !Enum.IsDefined(DecisiveReason))
        {
            throw new ArgumentException("An interest event has an invalid region or kind.");
        }

        if ((Kind == AttentionEventKind.Decisive) != (DecisiveReason != DecisiveReason.None))
        {
            throw new ArgumentException("A decisive reason is required exactly for decisive events.");
        }

        if (Kind == AttentionEventKind.GlobalTransition && string.IsNullOrWhiteSpace(TransitionKey))
        {
            throw new ArgumentException("A global transition requires a transition identity.");
        }

        ValidateKey(TopicKey, allowNull: true);
        ValidateKey(LocationKey, allowNull: true);
        ValidateKey(TransitionKey, allowNull: true);
        ValidateKey(EvidenceSource, allowNull: false);
        if (EvidenceReferences is null || EvidenceReferences.Count > MaximumEvidenceReferences)
        {
            throw new ArgumentException("Evidence references are missing or exceed their bound.");
        }

        foreach (var reference in EvidenceReferences)
        {
            ValidateKey(reference, allowNull: false);
        }
    }

    private static void ValidateKey(string? value, bool allowNull)
    {
        if (value is null ? !allowNull : string.IsNullOrWhiteSpace(value) || value.Length > MaximumKeyCharacters)
        {
            throw new ArgumentException("Attention keys must be non-blank and bounded.");
        }
    }
}

public sealed record AttentionIntent(
    AttentionIntentKind Kind,
    string? TopicKey = null,
    IReadOnlyList<AttentionRegionKind>? Regions = null);

public sealed record AttentionSnapshot(
    Guid TargetSessionId,
    AttentionState State,
    double Score,
    DateTimeOffset AsOf,
    DateTimeOffset StateSince,
    DateTimeOffset? AfterglowUntil,
    bool SuppressUnrelatedInitiatedConversations);

/// <summary>The attention-event record: the evidence plus the post-event state.</summary>
public sealed record AttentionEventRecord(
    Guid TargetSessionId,
    DateTimeOffset Timestamp,
    NormalizedRegion Region,
    AttentionRegionKind RegionKind,
    AttentionSignals Signals,
    string? TopicKey,
    string EvidenceSource,
    IReadOnlyList<string> EvidenceReferences,
    string? TransitionKey,
    AttentionEventDisposition Disposition,
    double Contribution,
    AttentionState StateAfter);

public sealed record AttentionUpdate(
    AttentionSnapshot Snapshot,
    AttentionState? PreviousState,
    IReadOnlyList<AttentionIntent> Intents,
    AttentionEventRecord? Record)
{
    public bool Transitioned => PreviousState is { } previous && previous != Snapshot.State;
}
