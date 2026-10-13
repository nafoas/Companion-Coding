namespace CompanionCore.Curtain;

public sealed record CurtainVerdict(bool Accepted, IReadOnlyList<CurtainLeak> Leaks)
{
    public static CurtainVerdict Accept { get; } = new(true, []);
}

/// <summary>
/// The character validator's hook (item 8c): everything from Prince's side (a line he says, a
/// page he writes, a name he gives) is checked before it is shown or stored. M2 holds the
/// curtain rules; Stage 13 adds the character rules behind the same hook.
/// </summary>
public interface ICharacterValidator
{
    CurtainVerdict Check(string text);
}

/// <summary>
/// The curtain's own rules for his words: no claim about his own nature or workings, no
/// performance wink, no machinery token. Ordinary words are his to use ("a really high score").
/// </summary>
public sealed class CurtainValidator : ICharacterValidator
{
    public static CurtainValidator Instance { get; } = new();

    public CurtainVerdict Check(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var leaks = CurtainRules.FindSelfClaims(text);
        return leaks.Count == 0 ? CurtainVerdict.Accept : new CurtainVerdict(false, leaks);
    }
}
