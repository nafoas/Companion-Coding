using CompanionCore.Curtain;

namespace CompanionCore.BunDex;

/// <summary>
/// The kinds of things a Bun keeps, as the BrainCase privately notes them after the fact. They
/// are a reference for remembering, never a blueprint: they never name, choose, count, or order
/// his folders, never decide where anything goes, and never reach Prince (M1c).
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

/// <summary>The memory layer a page belongs to (item 4): how long it stays clear (M4a). Unspecified falls back to its kind.</summary>
public enum MemoryLayer
{
    Unspecified = 0,
    ShortTerm = 1,
    MediumTerm = 2,
    LongTerm = 3,
    Cherished = 4,
}

/// <summary>
/// Invisible tags the BrainCase keeps for every page, whatever folder it sits in.
/// <see cref="Where"/> is where Prince was when it happened, as he perceived it.
/// </summary>
public sealed record PageTags(
    BunDexRole Role,
    MemoryLayer Layer,
    string? Game,
    string? Save,
    IReadOnlyList<string> Subjects,
    DateTimeOffset CreatedAtUtc,
    string? Where = null);

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

/// <summary>A folder in Prince's own notebook, by his own names, with the line he wrote about it (if any).</summary>
public sealed record FolderView(string Name, string? About, IReadOnlyList<FolderView> Folders, IReadOnlyList<string> Pages);

/// <summary>Prince's own view of his notebook: his folders, what he said they are for, and his page names.</summary>
public sealed record BunDexView(IReadOnlyList<FolderView> Folders, IReadOnlyList<string> Pages);

/// <summary>
/// A moment that feels worth keeping, as it crosses the membrane: what he sensed (with how he
/// knows it), where he is, how strongly it pulls, and a few of his own pages it reminds him of.
/// Only the keeper makes one, and only from membrane experiences. Where it goes is his choice.
/// </summary>
public sealed class KeepMoment
{
    internal KeepMoment(Sense whatHappened, Place? where, KeepFeeling feeling, IReadOnlyList<Remembered> comesToMind, IReadOnlyList<Feeling>? feelings = null, IReadOnlyList<LookStep>? rummage = null)
    {
        Rummage = rummage ?? [];
        WhatHappened = whatHappened;
        Where = where;
        Feeling = feeling;
        ComesToMind = comesToMind;
        Feelings = feelings ?? [];
    }

    public Sense WhatHappened { get; }

    public Place? Where { get; }

    public KeepFeeling Feeling { get; }

    public IReadOnlyList<Remembered> ComesToMind { get; }

    /// <summary>Other feelings in the moment, such as "something about this rings a bell" (M4a).</summary>
    public IReadOnlyList<Feeling> Feelings { get; }

    /// <summary>When something rang a bell: his little lookup to remember more (M4b).</summary>
    public IReadOnlyList<LookStep> Rummage { get; }

    /// <summary>The whole moment as one Bnuy request, with his notebook as he sees it.</summary>
    public BnuyRequest ToRequest(BunDexView notebook) =>
        BnuyRequest.Assemble(Where, [WhatHappened], ComesToMind, NotebookLines.Of(notebook), Feeling, feelings: Feelings, rummage: Rummage);
}

public enum KeepChoice
{
    /// <summary>Not now. Allowed: an inclination is a pull, not a command.</summary>
    NotNow = 1,
    Write = 2,
}

/// <summary>
/// One change Prince makes to his own notebook. <see cref="Folder"/> names a folder by its path
/// of his names; with <see cref="Page"/>, the page in it. <see cref="MoveInto"/> is where it goes
/// (a path of names, created if new; empty for the top; null to stay), and <see cref="Rename"/>
/// its new name. Pages and folders are moved and renamed, never removed.
/// </summary>
public sealed record TidyStep(IReadOnlyList<string> Folder, string? Page = null, IReadOnlyList<string>? MoveInto = null, string? Rename = null);

/// <summary>
/// What Prince does with the moment: where to write (a folder path in his own names, created if
/// new), a line about a new place if he wants one, the page name, whether to add to that page if
/// it exists, the text, and any tidying he feels like doing while he is there.
/// </summary>
public sealed record KeepResponse(
    KeepChoice Choice,
    IReadOnlyList<string>? Folder = null,
    string? Page = null,
    bool AddToExistingPage = false,
    string? Text = null,
    string? AboutNewPlace = null,
    IReadOnlyList<TidyStep>? Tidy = null)
{
    public static KeepResponse NotNow { get; } = new(KeepChoice.NotNow);
}

/// <summary>An invitation to tidy: Boss's own words, as Prince hears them.</summary>
public sealed class TidyMoment
{
    internal TidyMoment(Sense? invitation)
    {
        Invitation = invitation;
    }

    public Sense? Invitation { get; }

    /// <summary>The invitation as one Bnuy request, with his notebook as he sees it.</summary>
    public BnuyRequest ToRequest(BunDexView notebook) =>
        BnuyRequest.Assemble(null, Invitation is null ? [] : [Invitation], notebook: NotebookLines.Of(notebook), tidying: true);
}

internal static class NotebookLines
{
    internal static IReadOnlyList<NotebookLine> Of(BunDexView notebook)
    {
        var lines = new List<NotebookLine>();
        void Folder(FolderView folder, int depth)
        {
            lines.Add(new NotebookLine(depth, folder.Name, folder.About, IsPlace: true));
            foreach (var child in folder.Folders)
            {
                Folder(child, depth + 1);
            }

            lines.AddRange(folder.Pages.Select(page => new NotebookLine(depth + 1, page, null, IsPlace: false)));
        }

        foreach (var folder in notebook.Folders)
        {
            Folder(folder, 0);
        }

        lines.AddRange(notebook.Pages.Select(page => new NotebookLine(0, page, null, IsPlace: false)));
        return lines;
    }
}

public enum TidyChoice
{
    NotNow = 1,
    Tidy = 2,
}

public sealed record TidyResponse(TidyChoice Choice, IReadOnlyList<TidyStep>? Steps = null)
{
    public static TidyResponse NotNow { get; } = new(TidyChoice.NotNow);
}

/// <summary>Prince's side of his BunDex. The stand-in writer until Stage 13.</summary>
public interface IBunDexWriter
{
    ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken);

    /// <summary>Only ever asked when Boss invites it. Prince may say not now.</summary>
    ValueTask<TidyResponse> TidyAsync(TidyMoment moment, BunDexView notebook, CancellationToken cancellationToken);
}

public enum KeepOutcomeKind
{
    Written = 1,
    NotNow = 2,
    Refused = 3,
}

/// <summary>The subconscious's record of what happened when something felt worth keeping.</summary>
public sealed record KeepOutcome(KeepOutcomeKind Kind, Guid? PageId, bool NewPlace, string? RefusedReason, int Tidied = 0, int TidyRefused = 0);

/// <summary>What happened when Prince was invited to tidy.</summary>
public sealed record TidyOutcome(TidyChoice Choice, int Applied, int Refused);

public sealed class BunDexException(string message) : Exception(message);
