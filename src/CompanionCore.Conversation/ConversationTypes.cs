using CompanionCore.Attention;

namespace CompanionCore.Conversation;

/// <summary>Game conversations outrank initiated (Bun-initiated) conversations.</summary>
public enum ThreadKind
{
    Game = 1,
    Initiated = 2,
}

public enum SeedBank
{
    Game = 1,
    Initiated = 2,
}

public enum SeedOrigin
{
    Observation = 1,
    SettledConversation = 2,
    Afterglow = 3,
    InitiatedRoot = 4,
}

public enum ThreadStatus
{
    Active = 1,
    Suspended = 2,
}

public enum UserTurnKind
{
    Click = 1,
    YesNo = 2,
    Acknowledgement = 3,
    Substantive = 4,
}

public enum SettleReason
{
    Closed = 1,
    Finished = 2,
    Replaced = 3,
    Idle = 4,
    ResumeDeclined = 5,
    ResumeNotTaken = 6,
}

public enum AmbientKind
{
    Commentary = 1,
    PepTalk = 2,
    UrgentAlert = 3,
}

public enum OfferKind
{
    Opening = 1,
    Resume = 2,
}

public enum SeedRetirement
{
    PresentedWithoutResponse = 1,
    Evicted = 2,
    Rejected = 3,
}

public enum ConversationRefusal
{
    None = 0,
    Locked = 1,
    NoActiveThread = 2,
    UnknownReference = 3,
    FollowUpsSaturated = 4,
    NotAllowed = 5,
}

public enum ConversationIntentKind
{
    AmbientExpression = 1,
    OfferOpening = 2,
    ThreadStarted = 3,
    ThreadSettled = 4,
    HoldThought = 5,
    ResumeOffered = 6,
    ThreadResumed = 7,
    SeedRetired = 8,
    SeedBanked = 9,
    ThreadLocked = 10,
    ThreadUnlocked = 11,
    FollowUp = 12,
    BrainFart = 13,
}

/// <summary>One typed coordinator output. Wording belongs to the presentation adapter.</summary>
public sealed record ConversationIntent(
    ConversationIntentKind Kind,
    string? TopicKey = null,
    Guid? ThreadId = null,
    Guid? SeedId = null,
    Guid? ReferenceId = null,
    ThreadKind? ThreadKind = null,
    SeedBank? Bank = null,
    AmbientKind? Ambient = null,
    SettleReason? SettleReason = null,
    SeedRetirement? Retirement = null,
    bool AskFirst = false);

public sealed record ConversationSeed(
    Guid SeedId,
    SeedBank Bank,
    SeedOrigin Origin,
    string TopicKey,
    double Value,
    DateTimeOffset UpdatedAt,
    int Presentations,
    bool Sensitive,
    ConversationThread? ResumableThread);

public sealed record ConversationThread(
    Guid ThreadId,
    ThreadKind Kind,
    string TopicKey,
    ThreadStatus Status,
    bool Locked,
    bool Sensitive,
    int SubstantiveTurns,
    int FollowUps,
    DateTimeOffset StartedAt,
    DateTimeOffset LastActivity);

public sealed record PendingOffer(
    Guid OfferId,
    OfferKind Kind,
    Guid? SeedId,
    Guid? ThreadId,
    string TopicKey,
    DateTimeOffset ExpiresAt,
    bool AskFirst);

public sealed record AmbientExpression(Guid ExpressionId, AmbientKind Kind, string TopicKey, DateTimeOffset ExpiresAt);

public sealed record GameObservation(string TopicKey, double Significance, bool PlayerStruggling = false, bool Sensitive = false);

public sealed record UserTurn(UserTurnKind Kind, Guid? ReferenceId = null);

public sealed record ConversationSnapshot(
    ConversationThread? Thread,
    IReadOnlyList<ConversationSeed> GameSeeds,
    IReadOnlyList<ConversationSeed> InitiatedSeeds,
    PendingOffer? Offer,
    IReadOnlyDictionary<string, int> EngagementProfile,
    AttentionState Attention,
    DateTimeOffset AsOf);

public sealed record ConversationUpdate(
    IReadOnlyList<ConversationIntent> Intents,
    ConversationSnapshot Snapshot,
    ConversationRefusal Refusal = ConversationRefusal.None);

/// <summary>Deterministic chance source in [0, 1). Injected so every decision is reproducible.</summary>
public interface IExpressionChance
{
    double Next();
}

/// <summary>Seeded xorshift64* source: the same seed always yields the same sequence.</summary>
public sealed class SeededExpressionChance : IExpressionChance
{
    private ulong _state;

    public SeededExpressionChance(ulong seed)
    {
        _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public double Next()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return ((_state * 0x2545F4914F6CDD1DUL) >> 11) * (1.0 / (1UL << 53));
    }
}
