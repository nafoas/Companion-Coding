using System.Text;
using System.Text.RegularExpressions;

namespace CompanionCore.Curtain;

/// <summary>
/// The intermediate layer between his subconscious and Da Bnuy (Boss, 2026-10-13: "all his
/// sensory information has an intermediate layer... the intermediate layer is what turns it
/// from raw data into 'Bnuy Saw' and 'Bnuy Remembers' and that's all he interprets").
/// Raw data (IDs, confidence numbers, regions, tags) stays below; it is the only maker of
/// what crosses. A raw input that cannot be made innocent is simply not experienced: it never
/// crosses half-cleaned, and he never notices the gap.
/// </summary>
public static partial class Membrane
{
    internal const int MaximumWords = 400;
    internal const int MaximumQuoted = 400;
    internal const int MaximumName = 80;
    internal const int MaximumGlimpse = 160;

    /// <summary>Confidence at or above this is simply "Prince saw".</summary>
    internal const double SureAt = 0.8;

    /// <summary>Confidence at or above this is "Prince thinks he saw"; below, he is guessing.</summary>
    internal const double ThinksAt = 0.5;

    /// <summary>What the eyes described, as Prince seeing it. Confidence becomes how sure he is, never a number.</summary>
    public static Sense? Saw(string description, double confidence = 1.0)
    {
        var words = Plain(description, MaximumWords);
        if (words is null)
        {
            return null;
        }

        var kind = confidence >= SureAt ? SenseKind.Saw : confidence >= ThinksAt ? SenseKind.ThinksHeSaw : SenseKind.GuessesHeSaw;
        return new Sense(kind, words, null);
    }

    /// <summary>At this many sightings it is "a few times"; at <see cref="LotsOfTimes"/>, "lots of times".</summary>
    internal const int AFewTimes = 3;

    internal const int LotsOfTimes = 10;

    /// <summary>
    /// One growing impression of the same thing, as the gist he registers ("saw dis a few
    /// times"): never a count, never one experience per sighting.
    /// </summary>
    public static Sense? SawAgain(string description, int times)
    {
        var words = Plain(description, MaximumWords);
        if (words is null || times < 1)
        {
            return null;
        }

        var kind = times switch
        {
            1 => SenseKind.Saw,
            < AFewTimes => SenseKind.SawAgain,
            < LotsOfTimes => SenseKind.SawAFewTimes,
            _ => SenseKind.SawLotsOfTimes,
        };
        return new Sense(kind, words, null);
    }

    /// <summary>Fear at or above these levels feels scared / a little scared / can look; below, brave (M3b J1).</summary>
    internal const double ScaredAt = 0.75;

    internal const double ALittleScaredAt = 0.45;

    internal const double CanLookAt = 0.3;

    /// <summary>
    /// How he feels about a threat in front of him: his courage, and, when he has seen Boss
    /// handle it enough, that Boss has got it covered. Levels stay below; only feelings cross.
    /// </summary>
    public static IReadOnlyList<Feeling> FeelingsAbout(double fear, bool bossHasGotIt)
    {
        var feelings = new List<Feeling>
        {
            new(fear >= ScaredAt ? BnuyTemplates.Scared
                : fear >= ALittleScaredAt ? BnuyTemplates.ALittleScared
                : fear >= CanLookAt ? BnuyTemplates.CanLook
                : BnuyTemplates.Brave),
        };
        if (bossHasGotIt)
        {
            feelings.Add(new Feeling(BnuyTemplates.BossHasGotIt));
        }

        return feelings;
    }

    /// <summary>
    /// A faded memory of his that fits what is happening: only the feeling of familiarity, with
    /// no details at all (M4a). To remember more, he would have to look it up.
    /// </summary>
    public static Feeling RingsABell() => new(BnuyTemplates.RingsABell);

    /// <summary>Text he read in his world, verbatim and quoted; <paramref name="on"/> is what it was on ("a sign"), if known.</summary>
    public static Sense? Read(string text, string? on = null)
    {
        var quoted = Quotable(text);
        if (quoted is null)
        {
            return null;
        }

        var where = on is null ? string.Empty : Plain(on, MaximumName) ?? string.Empty;
        return new Sense(SenseKind.Read, where, quoted);
    }

    /// <summary>Boss's own words, verbatim and quoted.</summary>
    public static Sense? BossSaid(string words)
    {
        var quoted = Quotable(words);
        return quoted is null ? null : new Sense(SenseKind.BossSaid, string.Empty, quoted);
    }

    /// <summary>Something from his memories, as remembering.</summary>
    public static Sense? Remembers(string recollection)
    {
        var words = Plain(recollection, MaximumWords);
        return words is null ? null : new Sense(SenseKind.Remembers, words, null);
    }

    /// <summary>One of his own pages coming to mind. His own words are trusted, but never with a claim about his workings.</summary>
    public static Remembered? Remember(IReadOnlyList<string> place, string page, string? where, string text)
    {
        ArgumentNullException.ThrowIfNull(place);
        var glimpse = Plain(Flatten(text), int.MaxValue, hisOwnWords: true);
        var name = Plain(page, MaximumName, hisOwnWords: true);
        if (glimpse is null || name is null || place.Any(folder => Plain(folder, MaximumName, hisOwnWords: true) is null))
        {
            return null;
        }

        if (glimpse.Length > MaximumGlimpse)
        {
            glimpse = glimpse[..MaximumGlimpse].TrimEnd() + "...";
        }

        return new Remembered([.. place.Select(folder => Plain(folder, MaximumName, hisOwnWords: true)!)], name, where is null ? null : At(where)?.Name, glimpse);
    }

    /// <summary>Where he is, as he perceives it.</summary>
    public static Place? At(string? name)
    {
        var plain = name is null ? null : Plain(name, MaximumName);
        return plain is null ? null : new Place(plain);
    }

    /// <summary>Text in quotes, with the quote marks themselves escaped so nothing can step outside them.</summary>
    internal static string InQuotes(string quoted) => BnuyTemplates.QuoteOpen + quoted + BnuyTemplates.QuoteClose;

    /// <summary>
    /// Plain words: control characters and machinery tokens removed, whitespace collapsed, the
    /// person playing named "Boss", bounded. Null when nothing innocent remains: a claim about
    /// his own workings, a voice addressing someone, an order, or nothing at all. His own words
    /// are his: he may talk to Boss or remind himself ("Remember dis Prince."), so only the
    /// self-claim and machinery rules apply to them.
    /// </summary>
    private static string? Plain(string? text, int maximum, bool hisOwnWords = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var cleaned = Collapse(CurtainRules.RemoveMachineTokens(StripControl(text)));
        cleaned = ThePlayer().Replace(cleaned, "Boss");
        cleaned = cleaned.Replace(BnuyTemplates.QuoteOpen, "‹", StringComparison.Ordinal).Replace(BnuyTemplates.QuoteClose, "›", StringComparison.Ordinal);
        if (cleaned.Length == 0
            || CurtainRules.FindSelfClaims(cleaned).Count > 0
            || (!hisOwnWords && (Addressing().IsMatch(cleaned) || CurtainRules.LooksLikeAnOrder(cleaned))))
        {
            return null;
        }

        return cleaned.Length <= maximum ? cleaned : cleaned[..maximum].TrimEnd();
    }

    /// <summary>Verbatim world text, bounded, with control characters removed and the quote marks escaped.</summary>
    private static string? Quotable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var quoted = Collapse(StripControl(text))
            .Replace(BnuyTemplates.QuoteOpen, "‹", StringComparison.Ordinal)
            .Replace(BnuyTemplates.QuoteClose, "›", StringComparison.Ordinal);
        if (quoted.Length == 0)
        {
            return null;
        }

        return quoted.Length <= MaximumQuoted ? quoted : quoted[..MaximumQuoted].TrimEnd() + "...";
    }

    private static string StripControl(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(char.IsControl(character) || char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.Format ? ' ' : character);
        }

        return builder.ToString();
    }

    private static string Collapse(string text) => Spaces().Replace(text, " ").Trim();

    private static string Flatten(string text) => Collapse(StripControl(text));

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\bthe (?:user|player)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ThePlayer();

    // A voice addressing someone is never part of what he plainly perceives; quoted text may contain it.
    [GeneratedRegex(@"\b(?:you|your|yours|yourself|you're|you've|you'll|you'd)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Addressing();
}
