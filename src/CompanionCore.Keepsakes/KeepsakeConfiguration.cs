namespace CompanionCore.Keepsakes;

/// <summary>Provisional keepsake bounds; tuned once real measurements exist.</summary>
public sealed record KeepsakeConfiguration
{
    /// <summary>Photographs are rare: at least this long between camera actions.</summary>
    public TimeSpan MinimumInterval { get; init; } = TimeSpan.FromMinutes(5);

    public int MaximumActionsPerDay { get; init; } = 12;

    /// <summary>How long a shown camera action may wait for its frame.</summary>
    public TimeSpan ActionWindow { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>The saved image's longest edge.</summary>
    public int MaximumSavedEdge { get; init; } = 1280;

    public int MaximumSourceEdge { get; init; } = 8192;

    public int MaximumEncodedBytes { get; init; } = 16 * 1024 * 1024;

    public static KeepsakeConfiguration Default { get; } = new();

    internal void Validate()
    {
        var valid = MinimumInterval >= TimeSpan.Zero
            && MaximumActionsPerDay is >= 1 and <= 1000
            && ActionWindow > TimeSpan.Zero && ActionWindow <= TimeSpan.FromMinutes(5)
            && MaximumSavedEdge is >= 16 and <= 8192
            && MaximumSourceEdge is >= 16 and <= 16384
            && MaximumSavedEdge <= MaximumSourceEdge
            && MaximumEncodedBytes is >= 1024 and <= 64 * 1024 * 1024;
        if (!valid)
        {
            throw new ArgumentOutOfRangeException(nameof(KeepsakeConfiguration), "Keepsake configuration is invalid.");
        }
    }
}
