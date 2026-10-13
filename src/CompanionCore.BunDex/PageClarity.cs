namespace CompanionCore.BunDex;

/// <summary>How clearly a page of his comes to mind right now.</summary>
public enum Clarity
{
    /// <summary>It comes to mind with its details.</summary>
    Clear = 1,

    /// <summary>Only a feeling of familiarity, with no details: "dat rings a bell".</summary>
    RingsABell = 2,

    /// <summary>Not offered unprompted (not clairvoyant); still in his BunDex to look up.</summary>
    Faded = 3,
}

/// <summary>
/// Remembering like a living Bun (plan item 4, Boss-approved values). Clarity is measured from
/// when a page was last written or remembered, on two clocks, play sessions and calendar time,
/// each window lasting whichever is longer, so time away from the game never causes forgetting.
/// It decides only what comes to mind, never what exists: nothing is edited or deleted.
/// </summary>
internal static class PageClarity
{
    internal static readonly (int Sessions, TimeSpan Time) Short = (1, TimeSpan.FromDays(2));
    internal static readonly (int Sessions, TimeSpan Time) Medium = (5, TimeSpan.FromDays(21));
    internal static readonly (int Sessions, TimeSpan Time) Long = (10, TimeSpan.FromDays(60));

    /// <summary>The layer a page belongs to: its tag, or by default from the kind of thing it is.</summary>
    internal static MemoryLayer LayerOf(PageTags tags) => tags.Layer != MemoryLayer.Unspecified
        ? tags.Layer
        : tags.Role switch
        {
            BunDexRole.Cherished => MemoryLayer.Cherished,
            BunDexRole.MediumTerm => MemoryLayer.MediumTerm,
            BunDexRole.LongTerm or BunDexRole.Adventures or BunDexRole.Lore or BunDexRole.Opinions
                or BunDexRole.Relationship or BunDexRole.ScaryOrCool => MemoryLayer.LongTerm,
            _ => MemoryLayer.ShortTerm,
        };

    /// <param name="sessionsSince">Play sessions begun after the page was last written or remembered.</param>
    internal static Clarity Of(MemoryLayer layer, TimeSpan sinceTouched, int sessionsSince)
    {
        if (layer == MemoryLayer.Cherished)
        {
            return Clarity.Clear;
        }

        var window = layer switch
        {
            MemoryLayer.LongTerm => Long,
            MemoryLayer.MediumTerm => Medium,
            _ => Short,
        };

        if (Within(window, 1, sinceTouched, sessionsSince))
        {
            return Clarity.Clear;
        }

        // Long-term memories always still surface when relevant, as a bell.
        return layer == MemoryLayer.LongTerm || Within(window, 2, sinceTouched, sessionsSince) ? Clarity.RingsABell : Clarity.Faded;
    }

    /// <summary>Whichever clock is longer: still within either window.</summary>
    private static bool Within((int Sessions, TimeSpan Time) window, int multiple, TimeSpan sinceTouched, int sessionsSince) =>
        sessionsSince <= window.Sessions * multiple || sinceTouched <= window.Time * multiple;

    /// <summary>The recall-priority ladder: cherished, then long, medium, short.</summary>
    internal static int Rank(MemoryLayer layer) => layer switch
    {
        MemoryLayer.Cherished => 4,
        MemoryLayer.LongTerm => 3,
        MemoryLayer.MediumTerm => 2,
        _ => 1,
    };
}
