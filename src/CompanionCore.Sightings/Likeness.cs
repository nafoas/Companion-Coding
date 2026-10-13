using System.Text;

namespace CompanionCore.Sightings;

/// <summary>
/// "Is this the same thing again?" Words are compared with numbers ignored, so a status readout
/// that only counts up ("counter 5/7", "counter 6/7") is the same sighting, while words that
/// differ in substance ("the soda is gone!") are a meaningful change.
/// </summary>
internal static class Likeness
{
    /// <summary>Word overlap at or above this is a repeat (M3a J2).</summary>
    internal const double SameAt = 0.6;

    private static readonly HashSet<string> Common = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "is", "are", "was", "were", "be", "been", "of", "to", "in", "on", "at", "for", "and", "or", "with",
        "it", "its", "this", "that", "there", "here", "now", "again", "still", "has", "have", "had", "as", "by", "from",
    };

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

            // Digits and punctuation end a word; numbers themselves are never part of the gist.
            if (current.Length > 0 && !Common.Contains(current.ToString()))
            {
                words.Add(current.ToString());
            }

            current.Clear();
        }

        return words;
    }

    internal static double Overlap(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 && right.Count == 0)
        {
            return 1;
        }

        var shared = left.Count(right.Contains);
        return (double)shared / (left.Count + right.Count - shared);
    }

    internal static bool Same(string left, string right) => Overlap(Words(left), Words(right)) >= SameAt;
}
