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

    internal static IEnumerable<string> All() =>
        typeof(BnuyTemplates).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
}
