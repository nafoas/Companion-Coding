namespace CompanionCore.Curtain;

/// <summary>
/// Every fixed word of every Bnuy request lives here, and nowhere else, so the build-time
/// check can hold all of it to zero tolerance. It is plain description of his own life, from
/// inside him, in his own self-reference ("Prince"). Stage 13 writes his nature and his voice.
/// </summary>
internal static class BnuyTemplates
{
    /// <summary>His nature's slot. A neutral placeholder until Stage 13 writes who he is.</summary>
    internal const string Nature = "Prince is Prince.";

    internal const string Where = "Where Prince is: {0}.";
    internal const string Saw = "Prince saw: {0}.";
    internal const string ThinksHeSaw = "Prince thinks he saw: {0}.";
    internal const string GuessesHeSaw = "Prince is guessing, but maybe he saw: {0}.";
    internal const string Read = "Prince read:";
    internal const string ReadOn = "Prince read on {0}:";
    internal const string BossSaid = "Boss said:";
    internal const string Remembers = "Prince remembers: {0}.";
    internal const string SawAgain = "Prince saw this again: {0}.";
    internal const string SawAFewTimes = "Prince saw this a few times: {0}.";
    internal const string SawLotsOfTimes = "Prince has seen this lots of times: {0}.";
    internal const string SawSoManyTimes = "Prince has seen this so, so many times: {0}.";
    internal const string ComesToMind = "Comes to mind, from Prince's own pages:";
    internal const string Page = "\"{0}\" in {1}{2}: {3}";
    internal const string PageFrom = ", from {0}";
    internal const string TopOfNotebook = "the front of his BunDex";
    internal const string PlaceSeparator = " / ";
    internal const string Notebook = "Prince's BunDex:";
    internal const string EmptyNotebook = "Prince's BunDex is still empty.";
    internal const string NotebookPlace = "{0}{1}/{2}";
    internal const string NotebookAbout = " (Prince wrote: {0})";
    internal const string NotebookPage = "{0}{1}";
    internal const string Indent = "  ";

    internal const string QuoteOpen = "«";
    internal const string QuoteClose = "»";

    internal const string KeepGentle = "Prince feels this might be worth keeping.";
    internal const string KeepWarm = "Prince feels this is worth keeping.";
    internal const string KeepStrong = "Prince feels this is very much worth keeping.";
    internal const string TidyFeeling = "Prince looks over his BunDex.";

    internal const string Scared = "Prince feels scared of this.";
    internal const string ALittleScared = "Prince feels a little scared of this.";
    internal const string CanLook = "Prince is still a little scared, but he can look now.";
    internal const string Brave = "Prince feels brave around this.";
    internal const string BossHasGotIt = "Prince feels Boss has got this covered.";
    internal const string RingsABell = "Something about this rings a bell.";

    // The lookup (M4b): neutral placeholders until Stage 13 writes his voice.
    internal const string StartsLooking = "Prince starts looking through his BunDex: now how to find this?";
    internal const string WantsToRememberMore = "Prince wants to remember more, so he starts looking it up.";
    internal const string Hunch = "Prince thinks: maybe it is in {0}... maybe look there?";
    internal const string HunchFront = "Prince thinks: maybe it is at the front of his BunDex... maybe look there?";
    internal const string NotThere = "Prince looks in {0}, but it is not there.";
    internal const string NotAtFront = "Prince looks at the front of his BunDex, but it is not there.";
    internal const string ADifferentOne = "Prince finds {0}, but that is a different one.";
    internal const string FoundIt = "Prince finds it!";
    internal const string ThinksItWas = "Prince thinks it was this one, but he is not sure:";
    internal const string CantFind = "Prince can't find it right now.";
    internal const string NotWrittenDown = "Prince doesn't think he wrote this down.";

    internal static IEnumerable<string> All() =>
        typeof(BnuyTemplates).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
}
