namespace CompanionCore.BunDex;

/// <summary>
/// The kinds of things a Bun needs somewhere to keep. These are subconscious labels: they
/// live only in the BrainCase and never name a folder or reach Prince.
/// </summary>
public enum BunDexRole
{
    ThingsThinkingAbout = 1,
    ShortTerm = 2,
    MediumTerm = 3,
    LongTerm = 4,
    Cherished = 5,
    Lore = 6,
    Opinions = 7,
    Adventures = 8,
    ScaryOrCool = 9,
    Relationship = 10,
    LittleRequests = 11,
}

/// <summary>The memory layer a page belongs to (item 4). Used by later steps; stored now.</summary>
public enum MemoryLayer
{
    Unspecified = 0,
    ShortTerm = 1,
    MediumTerm = 2,
    LongTerm = 3,
    Cherished = 4,
}

/// <summary>Invisible tags the BrainCase keeps for every page, whatever folder it sits in.</summary>
public sealed record PageTags(
    BunDexRole Role,
    MemoryLayer Layer,
    string? Game,
    string? Save,
    IReadOnlyList<string> Subjects,
    DateTimeOffset CreatedAtUtc);

/// <summary>A page as the BrainCase indexes it. Never shown to Prince.</summary>
public sealed record IndexedPage(Guid PageId, string RelativePath, PageTags Tags);

public enum BunDexHealth
{
    Healthy = 1,

    /// <summary>The ledger failed verification: the BunDex is read-only until repaired.</summary>
    Damaged = 2,
}

/// <summary>What opening (reconciling) the BunDex found and fixed.</summary>
public sealed record BunDexOpenReport(
    BunDexHealth Health,
    long LedgerEntries,
    bool TornTailDropped,
    int FoldersRestored,
    int PagesRestored,
    int PagesCompleted,
    int MovesCompleted,
    IReadOnlyList<string> AlteredPages);

// ---- The writer boundary. Everything below is what Prince's side sees and says; it carries
// ---- only his own notebook and the moment, never roles, IDs, tags, scores, or registrations.

/// <summary>How strongly something feels worth keeping. A feeling, never a number.</summary>
public enum KeepFeeling
{
    Gentle = 1,
    Warm = 2,
    Strong = 3,
}

/// <summary>A folder in Prince's own notebook, by his own names.</summary>
public sealed record FolderView(string Name, IReadOnlyList<FolderView> Folders, IReadOnlyList<string> Pages);

/// <summary>Prince's own view of his notebook: just his folders and page names.</summary>
public sealed record BunDexView(IReadOnlyList<FolderView> Folders, IReadOnlyList<string> Pages);

/// <summary>
/// A moment that feels worth keeping. <see cref="ComesToMind"/> is the place in his own
/// notebook where things like this go, when he has one; otherwise it is null.
/// </summary>
public sealed record KeepMoment(string WhatHappened, KeepFeeling Feeling, IReadOnlyList<string>? ComesToMind);

public enum KeepChoice
{
    /// <summary>Not now. Allowed: an inclination is a pull, not a command.</summary>
    NotNow = 1,
    Write = 2,
}

/// <summary>
/// What Prince does with the moment: where to write (a folder path in his own names,
/// created if new), the page name, whether to add to that page if it exists, and the text.
/// </summary>
public sealed record KeepResponse(
    KeepChoice Choice,
    IReadOnlyList<string>? Folder = null,
    string? Page = null,
    bool AddToExistingPage = false,
    string? Text = null)
{
    public static KeepResponse NotNow { get; } = new(KeepChoice.NotNow);
}

/// <summary>Prince's side of keeping something. The stand-in writer until Stage 13.</summary>
public interface IBunDexWriter
{
    ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken);
}

public enum KeepOutcomeKind
{
    Written = 1,
    NotNow = 2,
    Refused = 3,
}

/// <summary>The subconscious's record of what happened when something felt worth keeping.</summary>
public sealed record KeepOutcome(KeepOutcomeKind Kind, Guid? PageId, bool NewPlace, string? RefusedReason);

public sealed class BunDexException(string message) : Exception(message);
