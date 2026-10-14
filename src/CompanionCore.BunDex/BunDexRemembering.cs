using System.Text;

namespace CompanionCore.BunDex;

/// <summary>
/// Remembering organically (M4e, Boss 2026-10-14). Words are weighed by how familiar they are to
/// him, learned from his own pages and from what his eyes saw, never thrown out for their length.
/// A memory's strength comes from his own history and the moment: how long ago, how much he wrote,
/// how often he remembered it, whether it is close to his heart or what he has been thinking
/// about, how strongly he felt, and where he is. Nothing here is random; his consciousness is only
/// ever given what this genuinely surfaces.
/// </summary>
public sealed partial class BunDexStore
{
    /// <summary>A word met this many times weighs half as much as a brand-new one.</summary>
    internal const double FamiliarAfter = 3;

    /// <summary>Everyday words are familiar from the start, the way any speaker knows them.</summary>
    internal const double EverydayFamiliarity = 300;

    /// <summary>How strongly his own cues must fit for a page to come to mind unprompted (one distinctive word and where he is, or a few distinctive words).</summary>
    internal const double ComeToMindAt = 1.0;

    /// <summary>Being where it happened is as strong a cue as a brand-new word.</summary>
    internal const double SamePlaceCue = 1.0;

    /// <summary>A brief note: writing more than this many words makes a page stay with him longer.</summary>
    internal const int BriefNote = 20;

    /// <summary>Below this, there is nothing distinctive to look for.</summary>
    internal const double MeaningfulCue = 0.5;

    private static readonly HashSet<string> Everyday = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "is", "are", "was", "were", "be", "been", "being", "am", "of", "to", "in", "on", "at", "for", "and", "or", "but",
        "with", "it", "its", "this", "that", "these", "those", "there", "here", "then", "than", "as", "by", "from", "into", "onto", "up",
        "down", "out", "off", "over", "under", "so", "very", "just", "only", "too", "also", "has", "have", "had", "do", "does", "did",
        "he", "she", "they", "them", "their", "his", "her", "him", "i", "me", "my", "we", "us", "our", "you", "your", "what", "when",
        "where", "who", "which", "how", "why", "not", "no", "yes", "if", "all", "some", "any", "one", "again", "still", "now", "s",
        "da", "dat", "dis", "dem", "dere", "wif", "an", "n",
    };

    private readonly Dictionary<string, int> _pagesWith = new(StringComparer.Ordinal);

    /// <summary>How many sightings a word was part of (the host connects his sighting record).</summary>
    public Func<string, int>? SeenInTheWorld { get; set; }

    /// <summary>How much a set of words matters to him, 0 to 1 (learned significance, M5b; his interests, M6).</summary>
    public Func<IReadOnlySet<string>, double>? MattersToHim { get; set; }

    /// <summary>Every word, of any length, lower case. Nothing is thrown out here.</summary>
    internal static HashSet<string> Words(string text)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        var current = new StringBuilder();
        foreach (var character in text.Append(' '))
        {
            if (char.IsLetter(character))
            {
                current.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (current.Length > 0)
            {
                words.Add(current.ToString());
            }

            current.Clear();
        }

        return words;
    }

    /// <summary>How much a word stands out to him: a familiar word is background, a rare one vivid.</summary>
    internal double Weight(string word)
    {
        var met = (Everyday.Contains(word) ? EverydayFamiliarity : 0)
            + _pagesWith.GetValueOrDefault(word)
            + (SeenInTheWorld?.Invoke(word) ?? 0);
        return 1 / (1 + (met / FamiliarAfter));
    }

    internal double Weight(IEnumerable<string> words) => words.Sum(Weight);

    private void Learn(PageState page, string text)
    {
        foreach (var word in Words(text))
        {
            if (page.Words.Add(word))
            {
                _pagesWith[word] = _pagesWith.GetValueOrDefault(word) + 1;
            }
        }
    }

    /// <summary>How strongly what is happening calls a page to mind: the distinctive words they share, and being where it happened.</summary>
    private double Likeness(PageState page, HashSet<string> words, string? where) =>
        Weight(words.Where(page.Words.Contains)) + (SamePlace(page, where) ? SamePlaceCue : 0);

    private static bool SamePlace(PageState page, string? where) =>
        where is not null && string.Equals(page.Tags.Where, where, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// How much longer than its layer's window a page stays with him (1 is the plain window):
    /// how much he wrote, how often he remembered it, whether it is close to his heart, how
    /// strongly he felt, and being back where it happened.
    /// </summary>
    private double Strength(PageState page, string? where)
    {
        var wrote = 1 + (0.5 * Math.Clamp((page.Words.Count - BriefNote) / 60.0, 0, 1)) + (0.25 * Math.Min(page.Additions, 4));
        var remembered = 1 + (0.25 * Math.Min(page.Recalls, 8));
        var heart = 1 + Care(page);
        var felt = page.Tags.Role == BunDexRole.ScaryOrCool ? 1.5 : 1;
        var place = SamePlace(page, where) ? 1.5 : 1;
        return wrote * remembered * heart * felt * place;
    }

    /// <summary>
    /// Whether he would realistically know it: how much it shares with what he holds dear (his
    /// cherished pages), what he has been thinking about (his thinking-about pages, and what he
    /// wrote this session), and what matters to him. 0 to 1.
    /// </summary>
    private double Care(PageState page)
    {
        var own = Weight(page.Words);
        if (own <= 0)
        {
            return 0;
        }

        var sessionBegan = _sessions.Count == 0 ? DateTimeOffset.MaxValue : _sessions[^1];
        var dear = new HashSet<string>(StringComparer.Ordinal);
        // A page he is thinking about, or wrote this session, is itself on his mind.
        foreach (var other in _pages.Values)
        {
            if (other.Tags.Role is BunDexRole.Cherished or BunDexRole.ThingsThinkingAbout || other.LastWrittenAt >= sessionBegan)
            {
                dear.UnionWith(other.Words);
            }
        }

        var close = Weight(page.Words.Where(dear.Contains)) / own;
        var matters = MattersToHim?.Invoke(page.Words) ?? 0;
        return Math.Clamp(Math.Max(close, matters), 0, 1);
    }

    /// <summary>How clearly a page comes to mind now, from its layer, both clocks, and its strength.</summary>
    private Clarity ClarityOf(PageState page, DateTimeOffset now, string? where) =>
        PageClarity.Of(PageClarity.LayerOf(page.Tags), now - page.LastTouched, SessionsSince(page.LastTouched), Strength(page, where));

    /// <summary>
    /// A bell worth a little lookup: it is close to his heart, he felt strongly about it, or it is
    /// on the tip of his tongue. Otherwise the bell stays a passing feeling.
    /// </summary>
    private bool WorthLookingUp(PageState page, DateTimeOffset now, string? where) =>
        Care(page) > 0
        || page.Tags.Role == BunDexRole.ScaryOrCool
        || PageClarity.OnTheTipOfHisTongue(PageClarity.LayerOf(page.Tags), now - page.LastTouched, SessionsSince(page.LastTouched), Strength(page, where));
}
