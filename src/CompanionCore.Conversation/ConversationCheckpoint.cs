using CompanionCore.Attention;

namespace CompanionCore.Conversation;

/// <summary>
/// Complete, serializable coordinator state so a locked or suspended thread, both banks,
/// and both clocks survive restart exactly. Validated before restore.
/// </summary>
public sealed record ConversationCheckpoint
{
    public int Version { get; init; }

    public Guid CoordinatorId { get; init; }

    public long IdCounter { get; init; }

    public DateTimeOffset Start { get; init; }

    public DateTimeOffset Now { get; init; }

    public AttentionState Attention { get; init; }

    public ConversationThread? Thread { get; init; }

    public List<ConversationSeed> GameSeeds { get; init; } = [];

    public List<ConversationSeed> InitiatedSeeds { get; init; } = [];

    public PendingOffer? Offer { get; init; }

    public List<AmbientExpression> Ambient { get; init; } = [];

    public Dictionary<string, int> Profile { get; init; } = new(StringComparer.Ordinal);

    public DateTimeOffset NextInitiatedCheck { get; init; }

    public DateTimeOffset? UrgentAt { get; init; }

    public DateTimeOffset? LastBrainFart { get; init; }

    public DateTimeOffset? LastGameCheckAt { get; init; }

    internal void Validate(ConversationConfiguration configuration)
    {
        configuration.Validate();
        var seeds = (GameSeeds ?? []).Concat(InitiatedSeeds ?? []).ToArray();
        var valid = Version == ConversationCoordinator.CheckpointVersion
            && CoordinatorId != Guid.Empty
            && IdCounter >= 0
            && Start != default && Now >= Start
            && Enum.IsDefined(Attention)
            && GameSeeds is not null && InitiatedSeeds is not null && Ambient is not null && Profile is not null
            && GameSeeds.Count <= configuration.MaximumSeedsPerBank
            && InitiatedSeeds.Count <= configuration.MaximumSeedsPerBank
            && GameSeeds.All(seed => seed?.Bank == SeedBank.Game)
            && InitiatedSeeds.All(seed => seed?.Bank == SeedBank.Initiated)
            && seeds.All(seed => seed.SeedId != Guid.Empty && double.IsFinite(seed.Value) && seed.Value is >= 0 and <= 1 && seed.Presentations >= 0 && Valid(seed.TopicKey))
            && seeds.Select(seed => seed.SeedId).Distinct().Count() == seeds.Length
            && (Thread is null || (Thread.ThreadId != Guid.Empty && Enum.IsDefined(Thread.Kind) && Enum.IsDefined(Thread.Status) && Valid(Thread.TopicKey) && Thread.SubstantiveTurns >= 0 && Thread.FollowUps >= 0))
            && (Offer is null || (Offer.Kind == OfferKind.Resume
                ? Thread is { Status: ThreadStatus.Suspended } && Offer.ThreadId == Thread.ThreadId
                : Offer.SeedId is { } seedId && seeds.Any(seed => seed.SeedId == seedId)))
            && Ambient.Count <= configuration.MaximumAmbientExpressions
            && Profile.All(pair => Valid(pair.Key) && pair.Value >= 0)
            && NextInitiatedCheck >= Start;
        if (!valid)
        {
            throw new ArgumentException("The conversation checkpoint is invalid.");
        }
    }

    private static bool Valid(string? topic) => !string.IsNullOrWhiteSpace(topic) && topic.Length <= 256;
}
