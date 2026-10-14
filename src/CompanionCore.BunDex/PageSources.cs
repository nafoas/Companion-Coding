using System.Globalization;

namespace CompanionCore.BunDex;

/// <summary>
/// The BrainCase's hidden source links (M5a): where a page came from. They live only in a page's
/// hidden tags and the ledger, never in anything Prince is given.
/// </summary>
public static class PageSources
{
    /// <summary>A memory record a page came from.</summary>
    public static string Memory(Guid recordId) => "memory:" + recordId.ToString("N", CultureInfo.InvariantCulture);

    /// <summary>The sightings of a pattern, up to the magnitude it grew into.</summary>
    public static string Sightings(string game, string thing, int count) =>
        "sightings:" + game + ":" + thing.Trim().ToLowerInvariant() + ":" + count.ToString(CultureInfo.InvariantCulture);
}
