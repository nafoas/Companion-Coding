namespace CompanionCore.Conversation;

/// <summary>Provisional conversation numbers; calibrated only after prototype testing.</summary>
public sealed record ConversationConfiguration
{
    public int MaximumSeedsPerBank { get; init; } = 16;
    public double GameSeedThreshold { get; init; } = 0.5;
    public double SettledSeedValue { get; init; } = 0.8;
    public double AfterglowSeedValue { get; init; } = 0.9;
    public int MinimumSubstantiveTurns { get; init; } = 2;
    public int MaximumPresentations { get; init; } = 3;
    public int MaximumFollowUps { get; init; } = 3;

    public TimeSpan InitiatedClockInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan InitiatedClockOffset { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan OfferTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan AmbientPromotionWindow { get; init; } = TimeSpan.FromMinutes(2);
    public int MaximumAmbientExpressions { get; init; } = 8;
    public TimeSpan ThreadIdleTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan ResumeDelay { get; init; } = TimeSpan.FromSeconds(30);

    public double GameChanceNoticing { get; init; } = 0.1;
    public double GameChanceEngaged { get; init; } = 0.3;
    public double CommentaryChanceHigh { get; init; } = 0.8;
    public double GameChanceAfterglow { get; init; } = 0.5;
    public double InitiatedChanceNoticing { get; init; } = 0.25;
    public double InitiatedChanceEngaged { get; init; } = 0.1;

    public double BrainFartChance { get; init; } = 0.05;
    public TimeSpan BrainFartCooldown { get; init; } = TimeSpan.FromMinutes(10);

    public static ConversationConfiguration Default { get; } = new();

    internal void Validate()
    {
        static bool Probability(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
        var valid = MaximumSeedsPerBank is >= 1 and <= 1024
            && Probability(GameSeedThreshold) && Probability(SettledSeedValue) && Probability(AfterglowSeedValue)
            && MinimumSubstantiveTurns is >= 1 and <= 100
            && MaximumPresentations is >= 1 and <= 100
            && MaximumFollowUps is >= 0 and <= 100
            && InitiatedClockInterval > TimeSpan.Zero
            && InitiatedClockOffset >= TimeSpan.Zero && InitiatedClockOffset < InitiatedClockInterval
            && OfferTimeout > TimeSpan.Zero
            && AmbientPromotionWindow > TimeSpan.Zero
            && MaximumAmbientExpressions is >= 1 and <= 256
            && ThreadIdleTimeout > TimeSpan.Zero
            && ResumeDelay >= TimeSpan.Zero
            && Probability(GameChanceNoticing) && Probability(GameChanceEngaged) && Probability(CommentaryChanceHigh)
            && Probability(GameChanceAfterglow) && Probability(InitiatedChanceNoticing) && Probability(InitiatedChanceEngaged)
            && Probability(BrainFartChance)
            && BrainFartCooldown >= TimeSpan.Zero;
        if (!valid)
        {
            throw new ArgumentOutOfRangeException(nameof(ConversationConfiguration), "Conversation configuration is invalid.");
        }
    }
}
