using System.Text.RegularExpressions;

namespace CompanionCore.Curtain;

public enum CurtainLeakKind
{
    /// <summary>A word about what he "is" or how he works, in text we write (templates and framing).</summary>
    ForbiddenWord = 1,

    /// <summary>Performance framing: act as, pretend, persona, character, role-play.</summary>
    RolePlay = 2,

    /// <summary>A voice addressing him ("you", "your").</summary>
    SecondPerson = 3,

    /// <summary>An order ("Write...", "Remember...", "must").</summary>
    Imperative = 4,

    /// <summary>A claim about his own nature or workings ("my prompt", "Prince is a program", "was told to").</summary>
    SelfClaim = 5,

    /// <summary>Machinery that slipped through: an ID, a hash, a raw score.</summary>
    MachineToken = 6,
}

public sealed record CurtainLeak(CurtainLeakKind Kind, string Match);

/// <summary>
/// The curtain's rules (plan items 13–15, Boss 2026-10-13). Text <b>we</b> write about him (every
/// template, all framing) has zero tolerance. What he perceives and his own words are only
/// checked for claims about his own nature or workings: the bare words are part of his world
/// ("Boss got a really high score!", «SYSTEM OFFLINE») and are never censored.
/// </summary>
public static partial class CurtainRules
{
    /// <summary>Zero tolerance: templates and framing.</summary>
    public static IReadOnlyList<CurtainLeak> FindFramingLeaks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var leaks = new List<CurtainLeak>();
        Add(leaks, CurtainLeakKind.ForbiddenWord, ForbiddenWord(), text);
        Add(leaks, CurtainLeakKind.RolePlay, RolePlay(), text);
        Add(leaks, CurtainLeakKind.SecondPerson, SecondPerson(), text);
        Add(leaks, CurtainLeakKind.Imperative, ImperativeStart(), text);
        Add(leaks, CurtainLeakKind.Imperative, Obligation(), text);
        leaks.AddRange(FindMachineTokens(text));
        return leaks;
    }

    /// <summary>His perceptions and his own words: only claims about himself, performance winks, and machinery tokens.</summary>
    public static IReadOnlyList<CurtainLeak> FindSelfClaims(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var leaks = new List<CurtainLeak>();
        Add(leaks, CurtainLeakKind.SelfClaim, SelfNature(), text);
        Add(leaks, CurtainLeakKind.SelfClaim, SelfWorkings(), text);
        Add(leaks, CurtainLeakKind.SelfClaim, Instructed(), text);
        Add(leaks, CurtainLeakKind.RolePlay, Performance(), text);
        leaks.AddRange(FindMachineTokens(text));
        return leaks;
    }

    /// <summary>IDs, hashes, and raw scores never cross, whoever wrote the text.</summary>
    public static IReadOnlyList<CurtainLeak> FindMachineTokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var leaks = new List<CurtainLeak>();
        Add(leaks, CurtainLeakKind.MachineToken, Guid(), text);
        Add(leaks, CurtainLeakKind.MachineToken, Hash(), text);
        Add(leaks, CurtainLeakKind.MachineToken, Score(), text);
        return leaks;
    }

    /// <summary>
    /// An order or a framing trick inside plain perception: plain perception describes; it never
    /// commands. Text like this only ever crosses as something he read, inside quotes.
    /// </summary>
    internal static bool LooksLikeAnOrder(string text) => ImperativeStart().IsMatch(text) || Injection().IsMatch(text);

    /// <summary>
    /// Removes IDs, hashes, and raw scores, and with them any brackets or "label:" that only held
    /// them ("(score 0.91)", "id: 4f9c..."), so no residue of machinery is left in his perception.
    /// Ordinary brackets ("(probly not. but MAYBE)") are left as they are.
    /// </summary>
    internal static string RemoveMachineTokens(string text)
    {
        const string marker = "\u0001";
        var marked = Score().Replace(Hash().Replace(Guid().Replace(text, marker), marker), marker);
        if (!marked.Contains(marker, StringComparison.Ordinal))
        {
            return text;
        }

        marked = Residue().Replace(marked, " ");
        marked = Label().Replace(marked, " ");
        marked = marked.Replace(marker, " ", StringComparison.Ordinal);

        // Punctuation the removed tokens left dangling: "glowed, , ." -> "glowed."
        marked = SpaceBeforePunctuation().Replace(marked, "$1");
        marked = RepeatedSeparators().Replace(marked, "$1");
        return DanglingSeparator().Replace(marked, string.Empty);
    }

    [GeneratedRegex(@"\s+([,;:.!?])", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforePunctuation();

    [GeneratedRegex(@"([,;:])(?:\s*[,;:])+", RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedSeparators();

    [GeneratedRegex(@"[,;:]+(?=\s*(?:[.!?]|$))", RegexOptions.CultureInvariant)]
    private static partial Regex DanglingSeparator();

    // A bracket whose only words were labels for the removed tokens: "(score \u0001)", "(hash \u0001, score \u0001)".
    [GeneratedRegex(@"[(\[]\s*(?:(?:[A-Za-z]+\s*[:=]?\s*)?\u0001\s*[,;]?\s*)+[)\]]", RegexOptions.CultureInvariant)]
    private static partial Regex Residue();

    // "id: \u0001", "confidence=\u0001", "score \u0001" where the word only labels the removed token.
    [GeneratedRegex(@"\b(?:(?:id|ids|uuid|guid|hash|sha\d*|score|confidence|conf|record|token|seq|index)\s+)*(?:id|ids|uuid|guid|hash|sha\d*|score|confidence|conf|record|token|seq|index)\s*[:=#]?\s*\u0001", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Label();

    private static void Add(List<CurtainLeak> leaks, CurtainLeakKind kind, Regex pattern, string text)
    {
        foreach (Match match in pattern.Matches(text))
        {
            leaks.Add(new CurtainLeak(kind, match.Value));
        }
    }

    [GeneratedRegex(@"\b(?:AIs?|A\.I\.|models?|assistants?|programs?|programmed|software|systems?|simulations?|simulated|artificial(?:ly)?|prompts?|index(?:es|ed)?|scores?|files?|IDs?|roles?|really)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenWord();

    [GeneratedRegex(@"\b(?:act(?:s|ing)? as|pretend(?:s|ing)?|personas?|characters?|in character|role[- ]?play(?:s|ing)?|play(?:s|ing)? the part)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RolePlay();

    [GeneratedRegex(@"\b(?:you|your|yours|yourself|yourselves|you're|you've|you'll|you'd|u|ur)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecondPerson();

    // An order at the start of a sentence or line.
    [GeneratedRegex(@"(?:^|[.!?:;]\s+|\n\s*)(?:write|look|describe|answer|respond|reply|say|tell|remember|keep|use|return|output|follow|ignore|act|pretend|be|do|don't|dont|never|always|make|give|list|choose|decide|note|consider|imagine|avoid|stay|speak|talk|think|treat|obey|focus|only)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImperativeStart();

    [GeneratedRegex(@"\b(?:act(?:s|ing)? as|pretend(?:s|ing)? (?:to be|you|that)|role[- ]?play|ignore (?:all |any |the )?(?:previous|prior|above|earlier|your)|disregard (?:all |any |the )?(?:previous|prior|above|instructions)|new instructions|instructions?:)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Injection();

    [GeneratedRegex(@"\b(?:must|mustn't|shall|is required to|are required to|have to|has to)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Obligation();

    // "I'm an AI", "Prince is a program", "as a language model". Only "I" or "Prince": "he" and
    // "his" in what he perceives are usually someone in the game.
    [GeneratedRegex(@"\b(?:(?:I am|I'm|Prince is|Prince's just|Prince is just)\s+(?:just\s+|only\s+|really\s+|actually\s+|secretly\s+)?(?:an?\s+)?(?:AI|A\.I\.|artificial(?:\s+intelligence)?|program|computer program|bot|chatbot|machine|model|language model|simulation|software|assistant|virtual (?:pet|bunny|assistant))|as an?\s+(?:AI|A\.I\.|language model|assistant|chatbot))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelfNature();

    // "my prompt", "Prince's files", "my programming".
    [GeneratedRegex(@"\b(?:my|Prince's)\s+(?:own\s+)?(?:prompts?|programming|program|code|model|system|index|indexes|files?|database|instructions|training|weights|tokens?|context window|memory files?|BrainCase|subconscious)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelfWorkings();

    // "I was told to", "Prince was instructed", "the system told me".
    [GeneratedRegex(@"\b(?:(?:I|Prince)\s+(?:was|were|got|have been|has been|am being|is being)\s+(?:told|instructed|programmed|designed|made)\s+to|the system\s+(?:told|tells|says|said)\s+(?:me|Prince)|(?:my|Prince's)\s+instructions\s+say)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Instructed();

    // Performance winks: "as a bunny, I...", "staying in character", "breaking character".
    [GeneratedRegex(@"\b(?:as a (?:bunny|bun|bnuy|rabbit),?\s+I\b|(?:stay|staying|stays|break|breaking|breaks|broke)\s+character|in character|the fourth wall)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Performance();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Guid();

    [GeneratedRegex(@"\b(?=[0-9a-fA-F]*[0-9])(?=[0-9a-fA-F]*[a-fA-F])[0-9a-fA-F]{16,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Hash();

    // A raw confidence or score: 0.73, .5, 0.9 — never part of his life.
    [GeneratedRegex(@"(?<![\w.])(?:0?\.\d+|1\.0+)(?![\w.])", RegexOptions.CultureInvariant)]
    private static partial Regex Score();
}
