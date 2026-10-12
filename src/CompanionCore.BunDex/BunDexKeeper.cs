namespace CompanionCore.BunDex;

/// <summary>
/// The subconscious side of keeping things. When something is worth keeping, the moment
/// reaches Prince only as a feeling, plus the place in his own notebook where things like it go
/// (when he has one). He decides what to do. A place he makes for a kind of thing is quietly
/// registered as its home; a "not now" is quietly remembered, so the next such moment feels a
/// little stronger. Nothing is ever said to him, repeated, or insisted on.
/// </summary>
public sealed class BunDexKeeper
{
    internal const int StrongAfterDeclines = 2;

    private readonly BunDexStore _store;
    private readonly IBunDexWriter _writer;

    public BunDexKeeper(BunDexStore store, IBunDexWriter writer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public async Task<KeepOutcome> KeepAsync(BunDexRole role, PageTags tags, string whatHappened, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentException.ThrowIfNullOrWhiteSpace(whatHappened);
        if (tags.Role != role)
        {
            throw new ArgumentException("The page tags must carry the same role.", nameof(tags));
        }

        var home = _store.HomeFor(role);
        var declines = _store.Declines(role);
        var feeling = declines >= StrongAfterDeclines ? KeepFeeling.Strong : declines == 1 ? KeepFeeling.Warm : KeepFeeling.Gentle;
        var comesToMind = home is { } homeId ? FolderNames(homeId) : null;
        var moment = new KeepMoment(whatHappened, feeling, comesToMind);

        var response = await _writer.RespondAsync(moment, _store.View(), cancellationToken).ConfigureAwait(false);
        if (response is null || response.Choice == KeepChoice.NotNow)
        {
            _store.RecordDecline(role);
            return new KeepOutcome(KeepOutcomeKind.NotNow, null, false, null);
        }

        if (string.IsNullOrWhiteSpace(response.Page) || string.IsNullOrEmpty(response.Text))
        {
            return new KeepOutcome(KeepOutcomeKind.Refused, null, false, "Nothing was written.");
        }

        try
        {
            var (folderId, newPlace) = EnsureFolder(response.Folder ?? []);
            Guid pageId;
            var existing = _store.FindPage(folderId, response.Page);
            if (response.AddToExistingPage && existing is { } existingId)
            {
                _store.AddToPage(existingId, response.Text);
                pageId = existingId;
            }
            else
            {
                pageId = _store.CreatePage(folderId, response.Page, response.Text, tags);
            }

            // A place made (or first chosen) for a kind of thing becomes its home.
            if (folderId is { } chosen && chosen != home && (home is null || newPlace))
            {
                _store.RegisterHome(role, chosen);
            }

            return new KeepOutcome(KeepOutcomeKind.Written, pageId, newPlace, null);
        }
        catch (BunDexException exception)
        {
            return new KeepOutcome(KeepOutcomeKind.Refused, null, false, exception.Message);
        }
    }

    private (Guid? FolderId, bool NewPlace) EnsureFolder(IReadOnlyList<string> names)
    {
        Guid? current = null;
        var created = false;
        for (var index = 0; index < names.Count; index++)
        {
            var found = _store.FindFolder([.. names.Take(index + 1)]);
            if (found is { } id)
            {
                current = id;
                continue;
            }

            current = _store.CreateFolder(names[index], current);
            created = true;
        }

        return (current, created);
    }

    private IReadOnlyList<string> FolderNames(Guid folderId)
    {
        var relative = Path.GetRelativePath(_store.Location.BunDexRoot, _store.FolderPath(folderId));
        return relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    }
}
