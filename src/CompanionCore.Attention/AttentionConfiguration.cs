namespace CompanionCore.Attention;

/// <summary>
/// Every attention number in one place. All values are provisional configuration,
/// recorded with test evidence, and tuned only after prototype calibration.
/// </summary>
public sealed record AttentionConfiguration
{
    // Signal blend weights.
    public double NoveltyWeight { get; init; } = 1.0;
    public double ChangeWeight { get; init; } = 0.6;
    public double SalienceWeight { get; init; } = 1.0;
    public double UrgencyWeight { get; init; } = 1.4;
    public double PersistenceWeight { get; init; } = 0.6;
    public double PersonalRelevanceWeight { get; init; } = 0.6;
    public double LoreRelevanceWeight { get; init; } = 0.4;

    /// <summary>Score added by one fully weighted, fully confident event.</summary>
    public double MaximumContribution { get; init; } = 45;

    // State thresholds with hysteresis (score is 0–100).
    public double EngagedEnter { get; init; } = 40;
    public double EngagedExit { get; init; } = 32;
    public double HighEnter { get; init; } = 70;
    public double HighExit { get; init; } = 60;
    public TimeSpan HighMinimumDwell { get; init; } = TimeSpan.FromSeconds(5);

    // Level-dependent decay: higher interest decays more slowly.
    public TimeSpan BaseHalfLife { get; init; } = TimeSpan.FromSeconds(8);
    public TimeSpan HalfLifePerScorePoint { get; init; } = TimeSpan.FromSeconds(0.25);

    // Urgency, weak evidence, and corroboration.
    public double UrgentThreshold { get; init; } = 0.7;
    public double WeakConfidence { get; init; } = 0.5;
    public double WeakContribution { get; init; } = 8;
    public TimeSpan CorroborationWindow { get; init; } = TimeSpan.FromSeconds(10);
    public int MinimumIndependentSources { get; init; } = 2;
    public double CorroborationBonus { get; init; } = 2.0;

    // Decisive bypass and global transitions.
    public double DecisiveMargin { get; init; } = 15;
    public TimeSpan TransitionDedupWindow { get; init; } = TimeSpan.FromSeconds(30);
    public double TransitionContributionCap { get; init; } = 10;

    // Habituation and false alarms.
    public double HabituationRate { get; init; } = 0.85;
    public double HabituationFloor { get; init; } = 0.2;
    public TimeSpan HabituationRecoveryHalfLife { get; init; } = TimeSpan.FromMinutes(10);
    public double FalseAlarmPenalty { get; init; } = 0.6;
    public double FalseAlarmScoreCeiling { get; init; } = 10;
    public int MaximumTopics { get; init; } = 512;

    // Location familiarity.
    public double HazardousFactor { get; init; } = 1.3;
    public double ClearedFactor { get; init; } = 0.7;
    public double SafeFactor { get; init; } = 0.5;
    public TimeSpan LocationClearedAfter { get; init; } = TimeSpan.FromMinutes(5);
    public int MaximumLocations { get; init; } = 256;

    // Adaptive Afterglow.
    public TimeSpan AfterglowBase { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan AfterglowPerPeakPoint { get; init; } = TimeSpan.FromSeconds(0.5);
    public double AfterglowPerHighSecond { get; init; } = 0.25;
    public TimeSpan AfterglowMaximum { get; init; } = TimeSpan.FromMinutes(3);

    public static AttentionConfiguration Default { get; } = new();

    internal void Validate()
    {
        double[] weights =
        [
            NoveltyWeight, ChangeWeight, SalienceWeight, UrgencyWeight, PersistenceWeight,
            PersonalRelevanceWeight, LoreRelevanceWeight,
        ];
        var valid = weights.All(weight => double.IsFinite(weight) && weight >= 0)
            && weights.Sum() > 0
            && InRange(MaximumContribution, 1, 100)
            && 0 < EngagedExit && EngagedExit < EngagedEnter
            && EngagedEnter < HighExit && HighExit < HighEnter && HighEnter <= 100
            && HighMinimumDwell >= TimeSpan.Zero
            && BaseHalfLife > TimeSpan.Zero && HalfLifePerScorePoint >= TimeSpan.Zero
            && InRange(UrgentThreshold, 0.01, 1)
            && InRange(WeakConfidence, 0, 1)
            && WeakContribution >= 0
            && CorroborationWindow > TimeSpan.Zero
            && MinimumIndependentSources is >= 2 and <= 16
            && InRange(CorroborationBonus, 1, 5)
            && InRange(DecisiveMargin, 0, 30)
            && TransitionDedupWindow > TimeSpan.Zero
            && InRange(TransitionContributionCap, 0, 100)
            && InRange(HabituationRate, 0.01, 1)
            && InRange(HabituationFloor, 0.01, 1)
            && HabituationRecoveryHalfLife > TimeSpan.Zero
            && InRange(FalseAlarmPenalty, 0.01, 1)
            && InRange(FalseAlarmScoreCeiling, 0, EngagedExit)
            && MaximumTopics is >= 1 and <= 100_000
            && InRange(HazardousFactor, 1, 5)
            && InRange(ClearedFactor, 0.01, 1)
            && InRange(SafeFactor, 0.01, 1)
            && LocationClearedAfter > TimeSpan.Zero
            && MaximumLocations is >= 1 and <= 100_000
            && AfterglowBase > TimeSpan.Zero
            && AfterglowPerPeakPoint >= TimeSpan.Zero
            && double.IsFinite(AfterglowPerHighSecond) && AfterglowPerHighSecond >= 0
            && AfterglowMaximum >= AfterglowBase;
        if (!valid)
        {
            throw new ArgumentOutOfRangeException(nameof(AttentionConfiguration), "Attention configuration is invalid.");
        }
    }

    private static bool InRange(double value, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum;
}
