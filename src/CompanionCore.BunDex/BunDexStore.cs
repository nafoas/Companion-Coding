using System.Text;
using CompanionCore.Curtain;

namespace CompanionCore.BunDex;

/// <summary>
/// Prince's BunDex: his own folders and pages, built from the BrainCase ledger. The ledger is
/// written first and is authoritative; the files are his readable notebook. Pages are
/// append-only: no method rewrites, truncates, or deletes a page or folder. On open, every
/// folder and page is reconciled against the ledger, so a crash never leaves a partial page;
/// a page that differs in any other way is reported as altered and left untouched.
/// </summary>
public sealed partial class BunDexStore
{
    internal const int MaximumTextBytes = 64 * 1024;

    private readonly BunDexLocation _location;
    private readonly BunDexLedger _ledger;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, FolderState> _folders = [];
    private readonly Dictionary<Guid, PageState> _pages = [];
    private readonly Dictionary<BunDexRole, int> _declines = [];
    private readonly List<DateTimeOffset> _sessions = [];
    private readonly Dictionary<Guid, DateTimeOffset> _cameToMind = [];
    private readonly HashSet<Guid> _altered = [];

    private BunDexStore(BunDexLocation location, BunDexLedger ledger, TimeProvider time)
    {
        _location = location;
        _ledger = ledger;
        _time = time;
    }

    public BunDexLocation Location => _location;

    public BunDexHealth Health => _ledger.Damaged ? BunDexHealth.Damaged : BunDexHealth.Healthy;

    /// <summary>Test seam: runs after a ledger commit and before the files change (crash simulation).</summary>
    internal Action<LedgerKind>? AfterCommit { get; set; }

    public static BunDexStore Open(BunDexLocation location, out BunDexOpenReport report, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        var ledger = BunDexLedger.Open(location.LedgerRoot);
        var store = new BunDexStore(location, ledger, time ?? TimeProvider.System);
        foreach (var entry in ledger.Entries)
        {
            store.Apply(entry);
        }

        report = ledger.Damaged
            ? new BunDexOpenReport(BunDexHealth.Damaged, ledger.Entries.Count, ledger.TornTailDropped, 0, 0, 0, 0, [])
            : store.Reconcile(ledger.Entries.Count, ledger.TornTailDropped);
        return store;
    }

    // ---- writes: ledger first, then the files ------------------------------------------

    /// <param name="about">Prince's own line about what the place is for, if he wrote one.</param>
    public Guid CreateFolder(string name, Guid? parentId = null, string? about = null)
    {
        lock (_gate)
        {
            if (about is not null)
            {
                ValidateAbout(about);
            }

            if (parentId is { } parent && !_folders.ContainsKey(parent))
            {
                throw new BunDexException("That folder does not exist.");
            }

            if (Depth(parentId) + 1 > BunDexNames.MaximumDepth)
            {
                throw new BunDexException("Folders nest at most four deep.");
            }

            var safe = BunDexNames.MakeSafe(name) ?? throw new BunDexException("That name cannot be used.");
            var unique = UniqueFolderName(parentId, safe);
            var id = Guid.NewGuid();
            Commit(new LedgerEntry(0, Now(), LedgerKind.FolderCreated, FolderId: id, ParentId: parentId, Name: unique, Text: about));
            Directory.CreateDirectory(FolderPath(id));
            return id;
        }
    }

    public Guid CreatePage(Guid? folderId, string name, string text, PageTags tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        lock (_gate)
        {
            if (folderId is { } folder && !_folders.ContainsKey(folder))
            {
                throw new BunDexException("That folder does not exist.");
            }

            ValidateText(text);
            var safe = BunDexNames.MakeSafe(name) ?? throw new BunDexException("That name cannot be used.");
            var unique = UniquePageName(folderId, safe);
            var id = Guid.NewGuid();
            Commit(new LedgerEntry(0, Now(), LedgerKind.PageCreated, FolderId: folderId, Name: unique, PageId: id, Text: text, Tags: PageTagsWire.From(tags)));
            WriteNewFile(PagePath(id), Encoding.UTF8.GetBytes(text));
            return id;
        }
    }

    /// <summary>Adds text to the end of a page. Committed text is never changed.</summary>
    public void AddToPage(Guid pageId, string text)
    {
        lock (_gate)
        {
            var page = _pages.GetValueOrDefault(pageId) ?? throw new BunDexException("That page does not exist.");
            if (_altered.Contains(pageId))
            {
                throw new BunDexException("That page was changed outside the BunDex; it is left as it is.");
            }

            ValidateText(text);
            var path = PagePath(pageId);
            var expected = Encoding.UTF8.GetByteCount(page.Text.ToString());
            if (!File.Exists(path) || new FileInfo(path).Length != expected)
            {
                _altered.Add(pageId);
                throw new BunDexException("That page was changed outside the BunDex; it is left as it is.");
            }

            Commit(new LedgerEntry(0, Now(), LedgerKind.PageAdded, PageId: pageId, Text: text));
            AppendToFile(path, Encoding.UTF8.GetBytes(text));
        }
    }

    /// <summary>Moves and/or renames a folder. The ledger keeps where it was.</summary>
    public void MoveFolder(Guid folderId, Guid? newParentId, string? newName = null)
    {
        lock (_gate)
        {
            var folder = _folders.GetValueOrDefault(folderId) ?? throw new BunDexException("That folder does not exist.");
            if (newParentId is { } parent && (!_folders.ContainsKey(parent) || IsSelfOrDescendant(parent, folderId)))
            {
                throw new BunDexException("A folder cannot move there.");
            }

            if (Depth(newParentId) + 1 + SubtreeHeight(folderId) > BunDexNames.MaximumDepth)
            {
                throw new BunDexException("Folders nest at most four deep.");
            }

            var safe = newName is null ? folder.Name : BunDexNames.MakeSafe(newName) ?? throw new BunDexException("That name cannot be used.");
            var unique = UniqueFolderName(newParentId, safe, except: folderId);
            var from = FolderPath(folderId);
            Commit(new LedgerEntry(0, Now(), LedgerKind.FolderMoved, FolderId: folderId, ParentId: newParentId, Name: unique));
            MoveDirectory(from, FolderPath(folderId));
        }
    }

    /// <summary>Moves and/or renames a page. Its content is never touched.</summary>
    public void MovePage(Guid pageId, Guid? newFolderId, string? newName = null)
    {
        lock (_gate)
        {
            var page = _pages.GetValueOrDefault(pageId) ?? throw new BunDexException("That page does not exist.");
            if (newFolderId is { } folder && !_folders.ContainsKey(folder))
            {
                throw new BunDexException("That folder does not exist.");
            }

            var safe = newName is null ? page.Name : BunDexNames.MakeSafe(newName) ?? throw new BunDexException("That name cannot be used.");
            var unique = UniquePageName(newFolderId, safe, except: pageId);
            var from = PagePath(pageId);
            Commit(new LedgerEntry(0, Now(), LedgerKind.PageMoved, FolderId: newFolderId, Name: unique, PageId: pageId));
            MoveFile(from, PagePath(pageId));
        }
    }

    /// <summary>A play session began: the session clock for remembering (M4a).</summary>
    public void BeginSession()
    {
        lock (_gate)
        {
            Commit(new LedgerEntry(0, Now(), LedgerKind.SessionBegan));
        }
    }

    internal const int MaximumClear = 3;
    internal static readonly TimeSpan CameToMindCooldown = TimeSpan.FromMinutes(15);

    /// <summary>
    /// What comes to mind (M4a, plan items 4 and 7i): his own similar pages that are still clear,
    /// at most a few, ranked by likeness, then the ladder, then recency; and, for a fitting page that
    /// has faded to familiarity, only the feeling that it rings a bell. Faded pages never come to
    /// mind unprompted. A page that just came to mind won't again for a while. Everything that comes
    /// to mind is remembered, which restarts its clock (append-only).
    /// </summary>
    internal (IReadOnlyList<Remembered> Clear, Feeling? Bell, Guid? BellPage) ComeToMind(string cue, string? where)
    {
        var words = Words(cue);
        lock (_gate)
        {
            var now = Now();
            var candidates = _pages.Values
                .Where(page => !(_cameToMind.TryGetValue(page.Id, out var last) && now - last < CameToMindCooldown))
                .Select(page => (Page: page, Score: Likeness(page, words, where), Layer: PageClarity.LayerOf(page.Tags)))
                .Where(candidate => candidate.Score >= 2)
                .Select(candidate => (candidate.Page, candidate.Score, candidate.Layer, Clarity: PageClarity.Of(candidate.Layer, now - candidate.Page.LastTouched, SessionsSince(candidate.Page.LastTouched))))
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => PageClarity.Rank(candidate.Layer))
                .ThenByDescending(candidate => candidate.Page.LastTouched)
                .ToList();

            var clear = new List<Remembered>();
            // Faded pages are neither: they never come to mind unprompted.
            foreach (var candidate in candidates.Where(candidate => candidate.Clarity == Clarity.Clear))
            {
                if (clear.Count == MaximumClear)
                {
                    break;
                }

                if (Membrane.Remember(FolderNames(candidate.Page.FolderId), candidate.Page.Name, candidate.Page.Tags.Where, candidate.Page.Text.ToString()) is { } remembered)
                {
                    clear.Add(remembered);
                    Remembering(candidate.Page.Id, now);
                }
            }

            Feeling? bell = null;
            Guid? bellPage = null;
            if (candidates.FirstOrDefault(candidate => candidate.Clarity == Clarity.RingsABell) is { Page: not null } faint)
            {
                bell = Membrane.RingsABell();
                bellPage = faint.Page.Id;
                Remembering(faint.Page.Id, now);
            }

            return (clear, bell, bellPage);
        }
    }

    private void Remembering(Guid pageId, DateTimeOffset now)
    {
        _cameToMind[pageId] = now;

        // A read-only (damaged) ledger still lets him remember; only the refresh goes unrecorded.
        if (!_ledger.Damaged)
        {
            Commit(new LedgerEntry(0, now, LedgerKind.PageRecalled, PageId: pageId));
        }
    }

    private int SessionsSince(DateTimeOffset at) => _sessions.Count(session => session > at);

    /// <summary>How many play sessions the session clock has counted.</summary>
    internal int SessionsPlayed
    {
        get
        {
            lock (_gate)
            {
                return _sessions.Count;
            }
        }
    }

    /// <summary>How clearly a page would come to mind right now (for the subconscious and tests).</summary>
    internal Clarity ClarityOf(Guid pageId)
    {
        lock (_gate)
        {
            var page = _pages.GetValueOrDefault(pageId) ?? throw new BunDexException("That page does not exist.");
            return PageClarity.Of(PageClarity.LayerOf(page.Tags), Now() - page.LastTouched, SessionsSince(page.LastTouched));
        }
    }

    internal void RecordDecline(BunDexRole role)
    {
        lock (_gate)
        {
            Commit(new LedgerEntry(0, Now(), LedgerKind.KeepDeclined, Role: role));
        }
    }

    // ---- reads ---------------------------------------------------------------------------

    /// <summary>"Not now"s for this kind of thing since he last kept one.</summary>
    internal int Declines(BunDexRole role)
    {
        lock (_gate)
        {
            return _declines.GetValueOrDefault(role);
        }
    }

    /// <summary>Pages found by their tags, wherever Prince filed them.</summary>
    public IReadOnlyList<IndexedPage> FindPages(Func<PageTags, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        lock (_gate)
        {
            return [.. _pages.Values
                .Where(page => predicate(page.Tags))
                .OrderBy(page => page.Tags.CreatedAtUtc)
                .Select(page => new IndexedPage(page.Id, RelativePagePath(page.Id), page.Tags))];
        }
    }

    public string PageText(Guid pageId)
    {
        lock (_gate)
        {
            return (_pages.GetValueOrDefault(pageId) ?? throw new BunDexException("That page does not exist.")).Text.ToString();
        }
    }

    public string PagePath(Guid pageId)
    {
        lock (_gate)
        {
            return Path.Combine(_location.BunDexRoot, RelativePagePath(pageId));
        }
    }

    public string FolderPath(Guid folderId)
    {
        lock (_gate)
        {
            return Path.Combine(_location.BunDexRoot, RelativeFolderPath(folderId));
        }
    }

    /// <summary>Prince's own view of his notebook: folder and page names only.</summary>
    public BunDexView View()
    {
        lock (_gate)
        {
            return new BunDexView(FoldersUnder(null), PagesIn(null));
        }
    }

    private static int Likeness(PageState page, HashSet<string> words, string? where)
    {
        var shared = Words(page.Text.ToString()).Count(words.Contains);
        var sameWhere = where is not null && string.Equals(page.Tags.Where, where, StringComparison.OrdinalIgnoreCase);
        return shared + (sameWhere ? 2 : 0);
    }

    private static readonly HashSet<string> CommonWords = new(StringComparer.Ordinal)
    {
        "that", "this", "with", "from", "they", "them", "then", "there", "their", "what", "when", "were", "have", "been", "into", "only",
        "very", "just", "about", "after", "before", "said", "says", "boss", "prince", "da", "dat", "dis", "dem", "dere", "wif", "even",
    };

    private static HashSet<string> Words(string text)
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

            if (current.Length >= 4 && !CommonWords.Contains(current.ToString()))
            {
                words.Add(current.ToString());
            }

            current.Clear();
        }

        return words;
    }

    private IReadOnlyList<string> FolderNames(Guid? folderId)
    {
        var names = new List<string>();
        for (var current = folderId; current is { } id; current = _folders[id].ParentId)
        {
            names.Add(_folders[id].Name);
        }

        names.Reverse();
        return names;
    }

    internal Guid? ParentOf(Guid folderId)
    {
        lock (_gate)
        {
            return (_folders.GetValueOrDefault(folderId) ?? throw new BunDexException("That folder does not exist.")).ParentId;
        }
    }

    internal Guid? FolderOf(Guid pageId)
    {
        lock (_gate)
        {
            return (_pages.GetValueOrDefault(pageId) ?? throw new BunDexException("That page does not exist.")).FolderId;
        }
    }

    /// <summary>Finds a folder by a path of his own folder names (case-insensitive), or null.</summary>
    internal Guid? FindFolder(IReadOnlyList<string> names)
    {
        lock (_gate)
        {
            Guid? current = null;
            foreach (var name in names)
            {
                var safe = BunDexNames.MakeSafe(name);
                var next = _folders.Values.FirstOrDefault(folder => folder.ParentId == current && string.Equals(folder.Name, safe, StringComparison.OrdinalIgnoreCase));
                if (next is null)
                {
                    return null;
                }

                current = next.Id;
            }

            return current;
        }
    }

    internal Guid? FindPage(Guid? folderId, string name)
    {
        lock (_gate)
        {
            var safe = BunDexNames.MakeSafe(name);
            return _pages.Values.FirstOrDefault(page => page.FolderId == folderId && string.Equals(page.Name, safe, StringComparison.OrdinalIgnoreCase))?.Id;
        }
    }

    // ---- replay and reconcile ------------------------------------------------------------

    private void Apply(LedgerEntry entry)
    {
        switch (entry.Kind)
        {
            case LedgerKind.FolderCreated:
                _folders[entry.FolderId!.Value] = new FolderState(entry.FolderId.Value, entry.ParentId, entry.Name!) { About = entry.Text };
                break;
            case LedgerKind.PageCreated:
                var created = new PageState(entry.PageId!.Value, entry.FolderId, entry.Name!, entry.Tags!.ToTags(), new StringBuilder(entry.Text)) { LastWrittenAt = entry.At, LastTouched = entry.At };
                _pages[created.Id] = created;
                _declines.Remove(created.Tags.Role);
                break;
            case LedgerKind.PageAdded:
                var added = _pages[entry.PageId!.Value];
                added.Text.Append(entry.Text);
                added.LastWrittenAt = entry.At;
                added.LastTouched = entry.At;
                _declines.Remove(added.Tags.Role);
                break;
            case LedgerKind.FolderMoved:
                var folder = _folders[entry.FolderId!.Value];
                folder.PreviousPaths.Add(RelativeFolderPath(folder.Id));
                folder.ParentId = entry.ParentId;
                folder.Name = entry.Name!;
                break;
            case LedgerKind.PageMoved:
                var page = _pages[entry.PageId!.Value];
                page.PreviousPaths.Add(RelativePagePath(page.Id));
                page.FolderId = entry.FolderId;
                page.Name = entry.Name!;
                break;
            case LedgerKind.RoleRegistered:
                // Retired (M1c): a kind of thing never has a home Prince did not choose each time.
                break;
            case LedgerKind.KeepDeclined:
                _declines[entry.Role!.Value] = _declines.GetValueOrDefault(entry.Role.Value) + 1;
                break;
            case LedgerKind.SessionBegan:
                _sessions.Add(entry.At);
                break;
            case LedgerKind.PageRecalled when _pages.TryGetValue(entry.PageId!.Value, out var recalled):
                recalled.LastTouched = entry.At > recalled.LastTouched ? entry.At : recalled.LastTouched;
                break;
        }
    }

    private BunDexOpenReport Reconcile(long entries, bool torn)
    {
        int foldersRestored = 0, pagesRestored = 0, pagesCompleted = 0, moves = 0;
        var altered = new List<string>();
        Directory.CreateDirectory(_location.BunDexRoot);

        foreach (var folder in _folders.Values.OrderBy(folder => Depth(folder.Id)))
        {
            var path = FolderPath(folder.Id);
            if (Directory.Exists(path))
            {
                continue;
            }

            var previous = folder.PreviousPaths
                .Select(relative => Path.Combine(_location.BunDexRoot, relative))
                .LastOrDefault(Directory.Exists);
            if (previous is not null)
            {
                MoveDirectory(previous, path);
                moves++;
            }
            else
            {
                Directory.CreateDirectory(path);
                foldersRestored++;
            }
        }

        foreach (var page in _pages.Values)
        {
            var path = PagePath(page.Id);
            var expected = Encoding.UTF8.GetBytes(page.Text.ToString());
            if (!File.Exists(path))
            {
                var previous = page.PreviousPaths
                    .Select(relative => Path.Combine(_location.BunDexRoot, relative))
                    .LastOrDefault(File.Exists);
                if (previous is not null)
                {
                    MoveFile(previous, path);
                    moves++;
                }
                else
                {
                    WriteNewFile(path, expected);
                    pagesRestored++;
                    continue;
                }
            }

            var actual = File.ReadAllBytes(path);
            if (actual.AsSpan().SequenceEqual(expected))
            {
                continue;
            }

            if (actual.Length < expected.Length && expected.AsSpan(0, actual.Length).SequenceEqual(actual))
            {
                AppendToFile(path, expected.AsSpan(actual.Length).ToArray());
                pagesCompleted++;
                continue;
            }

            _altered.Add(page.Id);
            altered.Add(RelativePagePath(page.Id));
        }

        return new BunDexOpenReport(BunDexHealth.Healthy, entries, torn, foldersRestored, pagesRestored, pagesCompleted, moves, altered);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private void Commit(LedgerEntry draft)
    {
        var entry = _ledger.Append(draft);
        Apply(entry);
        AfterCommit?.Invoke(entry.Kind);
    }

    private DateTimeOffset Now() => _time.GetUtcNow();

    internal static void ValidateText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new BunDexException("A page needs something written on it.");
        }

        if (Encoding.UTF8.GetByteCount(text) > MaximumTextBytes)
        {
            throw new BunDexException("That is too much to write at once.");
        }

        if (BunDexNames.LooksSecret(text))
        {
            throw new BunDexException("Secrets never go in the BunDex.");
        }
    }

    internal const int MaximumAboutLength = 200;

    internal static void ValidateAbout(string about)
    {
        if (string.IsNullOrWhiteSpace(about) || about.Length > MaximumAboutLength)
        {
            throw new BunDexException("A line about a place is short.");
        }

        if (BunDexNames.LooksSecret(about))
        {
            throw new BunDexException("Secrets never go in the BunDex.");
        }
    }

    private int Depth(Guid? folderId)
    {
        var depth = 0;
        for (var current = folderId; current is { } id; current = _folders[id].ParentId)
        {
            depth++;
        }

        return depth;
    }

    private int SubtreeHeight(Guid folderId)
    {
        var children = _folders.Values.Where(folder => folder.ParentId == folderId).ToList();
        return children.Count == 0 ? 0 : 1 + children.Max(child => SubtreeHeight(child.Id));
    }

    private bool IsSelfOrDescendant(Guid candidate, Guid ancestor)
    {
        for (Guid? current = candidate; current is { } id; current = _folders[id].ParentId)
        {
            if (id == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private string RelativeFolderPath(Guid? folderId)
    {
        var names = new List<string>();
        for (var current = folderId; current is { } id; current = _folders[id].ParentId)
        {
            names.Add(_folders[id].Name);
        }

        names.Reverse();
        return names.Count == 0 ? string.Empty : Path.Combine([.. names]);
    }

    private string RelativePagePath(Guid pageId)
    {
        var page = _pages[pageId];
        return Path.Combine(RelativeFolderPath(page.FolderId), page.Name + BunDexNames.PageExtension);
    }

    private string UniqueFolderName(Guid? parentId, string name, Guid? except = null)
    {
        var parentPath = Path.Combine(_location.BunDexRoot, RelativeFolderPath(parentId));
        bool Taken(string candidate) =>
            _folders.Values.Any(folder => folder.Id != except && folder.ParentId == parentId && string.Equals(folder.Name, candidate, StringComparison.OrdinalIgnoreCase))
            || (Directory.Exists(Path.Combine(parentPath, candidate)) && !(except is { } id && string.Equals(_folders[id].Name, candidate, StringComparison.OrdinalIgnoreCase) && _folders[id].ParentId == parentId))
            || File.Exists(Path.Combine(parentPath, candidate));
        return Suffixed(name, Taken);
    }

    private string UniquePageName(Guid? folderId, string name, Guid? except = null)
    {
        var folderPath = Path.Combine(_location.BunDexRoot, RelativeFolderPath(folderId));
        bool Taken(string candidate) =>
            _pages.Values.Any(page => page.Id != except && page.FolderId == folderId && string.Equals(page.Name, candidate, StringComparison.OrdinalIgnoreCase))
            || (File.Exists(Path.Combine(folderPath, candidate + BunDexNames.PageExtension)) && !(except is { } id && string.Equals(_pages[id].Name, candidate, StringComparison.OrdinalIgnoreCase) && _pages[id].FolderId == folderId));
        return Suffixed(name, Taken);
    }

    private static string Suffixed(string name, Func<string, bool> taken)
    {
        if (!taken(name))
        {
            return name;
        }

        for (var number = 2; ; number++)
        {
            var suffix = $" ({number})";
            var stem = name.Length + suffix.Length > BunDexNames.MaximumNameLength ? name[..(BunDexNames.MaximumNameLength - suffix.Length)] : name;
            if (!taken(stem + suffix))
            {
                return stem + suffix;
            }
        }
    }

    private IReadOnlyList<FolderView> FoldersUnder(Guid? parentId) =>
        [.. _folders.Values
            .Where(folder => folder.ParentId == parentId)
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .Select(folder => new FolderView(folder.Name, folder.About, FoldersUnder(folder.Id), PagesIn(folder.Id)))];

    private IReadOnlyList<string> PagesIn(Guid? folderId) =>
        [.. _pages.Values.Where(page => page.FolderId == folderId).Select(page => page.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];

    private void WriteNewFile(string path, byte[] bytes)
    {
        EnsureInside(path);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".~{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private void AppendToFile(string path, byte[] bytes)
    {
        EnsureInside(path);
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private void MoveDirectory(string from, string to)
    {
        EnsureInside(from);
        EnsureInside(to);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(to))!);
        if (Directory.Exists(from) && !Directory.Exists(to))
        {
            Directory.Move(from, to);
        }
    }

    private void MoveFile(string from, string to)
    {
        EnsureInside(from);
        EnsureInside(to);
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        if (File.Exists(from) && !File.Exists(to))
        {
            File.Move(from, to, overwrite: false);
        }
    }

    private void EnsureInside(string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_location.BunDexRoot)) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(root, StringComparison.Ordinal))
        {
            throw new BunDexException("Writes stay inside the BunDex.");
        }
    }

    private sealed class FolderState(Guid id, Guid? parentId, string name)
    {
        public Guid Id { get; } = id;

        public Guid? ParentId { get; set; } = parentId;

        public string Name { get; set; } = name;

        public string? About { get; init; }

        public List<string> PreviousPaths { get; } = [];
    }

    private sealed class PageState(Guid id, Guid? folderId, string name, PageTags tags, StringBuilder text)
    {
        public Guid Id { get; } = id;

        public Guid? FolderId { get; set; } = folderId;

        public string Name { get; set; } = name;

        public PageTags Tags { get; } = tags;

        public DateTimeOffset LastWrittenAt { get; set; }

        /// <summary>When it was last written or remembered: where its clocks start (M4a).</summary>
        public DateTimeOffset LastTouched { get; set; }

        public StringBuilder Text { get; } = text;

        public List<string> PreviousPaths { get; } = [];
    }
}
