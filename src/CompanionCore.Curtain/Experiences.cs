namespace CompanionCore.Curtain;

/// <summary>How Prince knows something: his truthfulness categories, and nothing else.</summary>
public enum SenseKind
{
    Saw = 1,
    ThinksHeSaw = 2,
    GuessesHeSaw = 3,
    Read = 4,
    BossSaid = 5,
    Remembers = 6,
    SawAgain = 7,
    SawAFewTimes = 8,
    SawLotsOfTimes = 9,
}

/// <summary>
/// One thing Prince senses: "Prince saw...", "Prince read «...»", "Boss said «...»", "Prince
/// remembers...". Only the membrane makes these, from raw data it keeps to itself.
/// <see cref="Words"/> are plain description; <see cref="Quoted"/> is text he read or heard,
/// verbatim, which is only ever carried inside quotes as something in his world.
/// </summary>
public sealed class Sense
{
    internal Sense(SenseKind kind, string words, string? quoted)
    {
        Kind = kind;
        Words = words;
        Quoted = quoted;
    }

    public SenseKind Kind { get; }

    public string Words { get; }

    public string? Quoted { get; }

    /// <summary>The line as he experiences it.</summary>
    public string Line => string.Join(' ', BnuyRequest.PartsOf(this).Select(part => part.Text));

    public override string ToString() => Line;
}

/// <summary>One of his own pages coming to mind: where he put it, its name, where it happened, and the start of his words.</summary>
public sealed class Remembered
{
    internal Remembered(IReadOnlyList<string> place, string page, string? where, string glimpse)
    {
        Place = place;
        Page = page;
        Where = where;
        Glimpse = glimpse;
    }

    public IReadOnlyList<string> Place { get; }

    public string Page { get; }

    public string? Where { get; }

    public string Glimpse { get; }
}

/// <summary>Where he is, as he perceives it.</summary>
public sealed class Place
{
    internal Place(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public override string ToString() => Name;
}

/// <summary>How strongly something feels worth keeping. A feeling, never a number.</summary>
public enum KeepFeeling
{
    Gentle = 1,
    Warm = 2,
    Strong = 3,
}
