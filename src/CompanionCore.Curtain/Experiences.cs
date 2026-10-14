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
    SawSoManyTimes = 10,
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

/// <summary>
/// A feeling he has about what is in front of him (M3b): how brave he feels, and that Boss has
/// got it covered. Only the membrane makes these, from levels it keeps to itself.
/// </summary>
public sealed class Feeling
{
    internal Feeling(string line)
    {
        Line = line;
    }

    public string Line { get; }

    public override string ToString() => Line;
}

/// <summary>How strongly something feels worth keeping. A feeling, never a number.</summary>
public enum KeepFeeling
{
    Gentle = 1,
    Warm = 2,
    Strong = 3,
}

/// <summary>One step of looking something up in his BunDex (M4b), as he lives it.</summary>
public enum LookStepKind
{
    /// <summary>"Okay... now how find dis...?"</summary>
    StartsLooking = 1,

    /// <summary>Something rang a bell, and he wants to remember more.</summary>
    WantsToRememberMore = 2,

    /// <summary>A place of his comes to mind as where it might be.</summary>
    Hunch = 3,

    /// <summary>He looked in a place, and it was not there.</summary>
    NotThere = 4,

    /// <summary>He found a page like it, but it is a different one.</summary>
    ADifferentOne = 5,

    /// <summary>He found it.</summary>
    Found = 6,

    /// <summary>He found a page he thinks is it, but he is not sure.</summary>
    ThinksItWas = 7,

    /// <summary>He gives up gently for now.</summary>
    CantFind = 8,

    /// <summary>Nothing came to mind at all: he doesn't think he wrote it down.</summary>
    NotWrittenDown = 9,
}

/// <summary>
/// One step of his rummage. A place is always one of his own folder names (null is the front of
/// his BunDex); a page is only ever one he actually found. Only the membrane makes these.
/// </summary>
public sealed class LookStep
{
    internal LookStep(LookStepKind kind, string? place = null, string? otherPage = null, Remembered? page = null)
    {
        Kind = kind;
        Place = place;
        OtherPage = otherPage;
        Page = page;
    }

    public LookStepKind Kind { get; }

    /// <summary>For a hunch or a place he looked in: his own name for it; null for the front of his BunDex.</summary>
    public string? Place { get; }

    /// <summary>For a different one: the name of the page he found instead.</summary>
    public string? OtherPage { get; }

    /// <summary>For found or thinks it was: the page, as remembering it.</summary>
    public Remembered? Page { get; }

    /// <summary>The step as he experiences it.</summary>
    public string Line => string.Join('\n', BnuyRequest.LinesOf(this).Select(line => string.Join(' ', line.Select(part => part.Text))));

    public override string ToString() => Line;
}
