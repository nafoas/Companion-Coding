namespace CompanionCore.Recall;

/// <summary>Stable subject keys that group related records (beliefs, lore, adventures, seeds).</summary>
public static class RecallSubjects
{
    public static string Adventure(string game, string save) => $"adventure:{Key(game)}:{Key(save)}";

    public static string AdventureHypothesis(string game, string save) => $"adventure-hypothesis:{Key(game)}:{Key(save)}";

    public static string Lore(string game, string topic) => $"lore:{Key(game)}:{Key(topic)}";

    public static string Opinion(string topic) => $"opinion:{Key(topic)}";

    public static string Summary(string session) => $"summary:{Key(session)}";

    public static string Highlight(Guid original) => $"highlight:{original:N}";

    public static string Seed(string rootId) => $"seed:{Key(rootId)}";

    private static string Key(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 120 || value.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("Subject components must be short, non-blank, and colon-free.", nameof(value));
        }

        return value;
    }
}
