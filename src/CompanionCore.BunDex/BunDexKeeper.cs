using CompanionCore.Curtain;

namespace CompanionCore.BunDex;

/// <summary>
/// The subconscious side of keeping things (M1c: a Living Bun; M5a: he decides). A moment reaches
/// Prince only through his Metaconscious: what happened, where he is, real feelings he has, a few
/// of his own pages it reminds him of, and, after a while without writing, his own urge to
/// scribble. Nothing tells him how worth keeping it is, and nothing mentions not writing: whether
/// he writes, how much, and where, are his own little Bnuy choices. He sees his whole notebook, with
/// the lines he wrote about his places, and decides everything himself. Nothing here assigns a kind
/// of thing a home, or counts, names, or orders his folders. The BrainCase keeps hidden links from
/// whatever he writes to where it came from. His notebook is only ever reorganized by him: while he
/// writes, or when Boss invites it. Everything he writes passes the character validator first: a
/// leak is asked for again a few times, then nothing is written.
/// </summary>
public sealed class BunDexKeeper
{
    /// <summary>After this many moments without writing, he feels like scribbling (M5a); after <see cref="StrongUrgeAfter"/>, really.</summary>
    internal const int UrgeAfter = 3;

    internal const int StrongUrgeAfter = 6;

    private readonly BunDexStore _store;
    private readonly IBunDexWriter _writer;
    private readonly ICharacterValidator _validator;
    private readonly Action<CurtainLeakKind>? _diagnostics;

    public BunDexKeeper(BunDexStore store, IBunDexWriter writer, ICharacterValidator? validator = null, Action<CurtainLeakKind>? diagnostics = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _validator = validator ?? CurtainValidator.Instance;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// A moment reaches him (M5a). <paramref name="whatHappened"/> has already crossed into his
    /// Metaconscious. <paramref name="sources"/> are the hidden links the BrainCase keeps for
    /// whatever he writes; he never sees them. Whether he writes, and where, is his.
    /// </summary>
    public async Task<KeepOutcome> KeepAsync(BunDexRole role, PageTags tags, Sense whatHappened, CancellationToken cancellationToken = default, IReadOnlyList<string>? sources = null)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(whatHappened);
        if (tags.Role != role)
        {
            throw new ArgumentException("The page tags must carry the same role.", nameof(tags));
        }

        // His urge to scribble comes from his nature after a while without writing, never from this moment.
        var without = _store.MomentsWithoutWriting;
        var feeling = without >= StrongUrgeAfter ? KeepFeeling.Strong : without >= UrgeAfter ? KeepFeeling.Warm : KeepFeeling.Gentle;
        var cue = whatHappened.Quoted ?? whatHappened.Words;
        var (clear, bell, lookUp) = _store.ComeToMind(cue, tags.Where);

        // "Dat rings a bell... should look dis up to remembers more" (M4b, M4e): a little lookup,
        // when it matters to him or is on the tip of his tongue.
        var rummage = lookUp is { } faint ? _store.Look(cue, tags.Where, Membrane.WantsToRememberMore(), faint).Steps : [];
        var moment = new KeepMoment(whatHappened, Membrane.At(tags.Where), feeling, clear, bell is null ? [] : [bell], rummage);

        KeepResponse? response = null;
        for (var attempt = 0; attempt < BnuyVoice.MaximumAttempts; attempt++)
        {
            response = await _writer.RespondAsync(moment, _store.View(), cancellationToken).ConfigureAwait(false);
            if (response is null || response.Choice == KeepChoice.NotNow || Accepted(WordsOf(response)))
            {
                break;
            }

            response = null;
            if (attempt == BnuyVoice.MaximumAttempts - 1)
            {
                return new KeepOutcome(KeepOutcomeKind.Refused, null, false, "Nothing was written.");
            }
        }

        if (response is null || response.Choice == KeepChoice.NotNow)
        {
            _store.RecordDecline(role);
            return new KeepOutcome(KeepOutcomeKind.NotNow, null, false, null);
        }

        if (string.IsNullOrWhiteSpace(response.Page) || string.IsNullOrEmpty(response.Text))
        {
            return new KeepOutcome(KeepOutcomeKind.Refused, null, false, "Nothing was written.");
        }

        Guid pageId;
        bool newPlace;
        try
        {
            // Check what he wrote before making any place for it, so a refusal leaves no empty place.
            BunDexStore.ValidateText(response.Text);
            _ = BunDexNames.MakeSafe(response.Page) ?? throw new BunDexException("That name cannot be used.");
            if (response.AboutNewPlace is not null)
            {
                BunDexStore.ValidateAbout(response.AboutNewPlace);
            }

            Guid? folderId;
            (folderId, newPlace) = EnsureFolder(response.Folder ?? [], response.AboutNewPlace);
            var existing = _store.FindPage(folderId, response.Page);
            if (response.AddToExistingPage && existing is { } existingId)
            {
                _store.AddToPage(existingId, response.Text, sources);
                pageId = existingId;
            }
            else
            {
                pageId = _store.CreatePage(folderId, response.Page, response.Text, sources is { Count: > 0 } ? tags with { Sources = sources } : tags);
            }
        }
        catch (BunDexException exception)
        {
            return new KeepOutcome(KeepOutcomeKind.Refused, null, false, exception.Message);
        }

        var (tidied, refused) = ApplyTidy(response.Tidy);
        return new KeepOutcome(KeepOutcomeKind.Written, pageId, newPlace, null, tidied, refused);
    }

    /// <summary>
    /// Boss suggests some tidying ("maybe we should do some spring cleaning?"). Prince looks over
    /// his notebook and moves or renames what he wants, or says not now.
    /// </summary>
    public async Task<TidyOutcome> InviteTidyingAsync(string? invitation, CancellationToken cancellationToken = default)
    {
        var moment = new TidyMoment(invitation is null ? null : Membrane.BossSaid(invitation));
        TidyResponse? response = null;
        for (var attempt = 0; attempt < BnuyVoice.MaximumAttempts; attempt++)
        {
            response = await _writer.TidyAsync(moment, _store.View(), cancellationToken).ConfigureAwait(false);
            if (response is null || response.Choice == TidyChoice.NotNow || Accepted(WordsOf(response.Steps)))
            {
                break;
            }

            response = null;
        }

        if (response is null || response.Choice == TidyChoice.NotNow)
        {
            return new TidyOutcome(TidyChoice.NotNow, 0, 0);
        }

        var (applied, refused) = ApplyTidy(response.Steps);
        return new TidyOutcome(TidyChoice.Tidy, applied, refused);
    }

    /// <summary>Applies his own steps one by one; a step that cannot be done is skipped, never forced.</summary>
    private (int Applied, int Refused) ApplyTidy(IReadOnlyList<TidyStep>? steps)
    {
        int applied = 0, refused = 0;
        foreach (var step in steps ?? [])
        {
            try
            {
                if (step.MoveInto is null && step.Rename is null)
                {
                    throw new BunDexException("Nothing to change.");
                }

                Guid? folderId = step.Folder.Count == 0 ? null : _store.FindFolder(step.Folder) ?? throw new BunDexException("That folder does not exist.");
                if (step.Page is { } page)
                {
                    var pageId = _store.FindPage(folderId, page) ?? throw new BunDexException("That page does not exist.");
                    var into = step.MoveInto is null ? _store.FolderOf(pageId) : EnsureFolder(step.MoveInto, null).FolderId;
                    _store.MovePage(pageId, into, step.Rename);
                }
                else
                {
                    var folder = folderId ?? throw new BunDexException("The top of the notebook cannot move.");
                    var into = step.MoveInto is null ? _store.ParentOf(folder) : EnsureFolder(step.MoveInto, null).FolderId;
                    _store.MoveFolder(folder, into, step.Rename);
                }

                applied++;
            }
            catch (BunDexException)
            {
                refused++;
            }
        }

        return (applied, refused);
    }

    /// <summary>His words pass the validator, or nothing is written. Only Boss's diagnostics learn why, by kind.</summary>
    private bool Accepted(IEnumerable<string> words)
    {
        var accepted = true;
        foreach (var text in words)
        {
            var verdict = _validator.Check(text);
            if (!verdict.Accepted)
            {
                accepted = false;
                foreach (var leak in verdict.Leaks)
                {
                    _diagnostics?.Invoke(leak.Kind);
                }
            }
        }

        return accepted;
    }

    private static IEnumerable<string> WordsOf(KeepResponse response) =>
        new[] { response.Page, response.Text, response.AboutNewPlace }
            .Concat(response.Folder ?? [])
            .Concat(WordsOf(response.Tidy))
            .OfType<string>();

    private static IEnumerable<string> WordsOf(IReadOnlyList<TidyStep>? steps) =>
        (steps ?? []).SelectMany(step => new[] { step.Rename }.Concat(step.MoveInto ?? [])).OfType<string>();

    private (Guid? FolderId, bool NewPlace) EnsureFolder(IReadOnlyList<string> names, string? aboutNewPlace)
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

            // His line about the place belongs to the place he is making for it: the last one.
            current = _store.CreateFolder(names[index], current, index == names.Count - 1 ? aboutNewPlace : null);
            created = true;
        }

        return (current, created);
    }
}
