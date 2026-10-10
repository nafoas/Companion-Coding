namespace CompanionCore.Watchbun;

/// <summary>Provisional Watchbun bounds; calibrated in Stage 11.</summary>
public sealed record WatchbunConfiguration
{
    public TimeSpan QuietThreshold { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan SecondQuietThreshold { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan DefaultTaskLifetime { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan MaximumTaskLifetime { get; init; } = TimeSpan.FromHours(4);

    public int MaximumWatchTasks { get; init; } = 8;

    public TimeSpan AlertWindow { get; init; } = TimeSpan.FromMinutes(1);

    public int MaximumAlertsPerWindow { get; init; } = 6;

    public static WatchbunConfiguration Default { get; } = new();

    internal void Validate()
    {
        var valid = QuietThreshold > TimeSpan.Zero
            && SecondQuietThreshold > TimeSpan.Zero
            && DefaultTaskLifetime > TimeSpan.Zero
            && MaximumTaskLifetime >= DefaultTaskLifetime
            && MaximumTaskLifetime <= TimeSpan.FromDays(1)
            && MaximumWatchTasks is >= 1 and <= 64
            && AlertWindow > TimeSpan.Zero
            && MaximumAlertsPerWindow is >= 1 and <= 1000;
        if (!valid)
        {
            throw new ArgumentOutOfRangeException(nameof(WatchbunConfiguration), "Watchbun configuration is invalid.");
        }
    }
}
