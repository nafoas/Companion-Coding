using CompanionCore.Curtain;

namespace CompanionCore.BunDex;

/// <summary>
/// A plain, deterministic stand-in for Prince's side of the BunDex until Stage 13 gives him his
/// own voice. It writes beside a page of his that came to mind from the same place he is now;
/// otherwise it makes a plainly named new place. It can be told to decline, and to tidy when
/// invited, to prove both are his choice.
/// </summary>
public sealed class StandInWriter : IBunDexWriter
{
    private readonly Func<KeepMoment, IReadOnlyList<string>> _newPlace;
    private readonly Func<KeepMoment, bool> _declineWhen;
    private readonly Func<TidyMoment, BunDexView, TidyResponse> _tidy;

    public StandInWriter(
        Func<KeepMoment, IReadOnlyList<string>>? newPlace = null,
        Func<KeepMoment, bool>? declineWhen = null,
        Func<TidyMoment, BunDexView, TidyResponse>? tidy = null)
    {
        _newPlace = newPlace ?? (moment => [(moment.Where?.Name ?? FirstWords(Plain(moment), 2)) + " stuffs"]);
        _declineWhen = declineWhen ?? (_ => false);
        _tidy = tidy ?? ((_, _) => TidyResponse.NotNow);
    }

    public List<KeepMoment> Moments { get; } = [];

    public List<TidyMoment> Invitations { get; } = [];

    public ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moment);
        Moments.Add(moment);
        if (_declineWhen(moment))
        {
            return ValueTask.FromResult(KeepResponse.NotNow);
        }

        var familiar = moment.ComesToMind.FirstOrDefault(recollection => recollection.Where is not null && string.Equals(recollection.Where, moment.Where?.Name, StringComparison.OrdinalIgnoreCase));
        var folder = familiar?.Place ?? _newPlace(moment);
        return ValueTask.FromResult(new KeepResponse(KeepChoice.Write, folder, FirstWords(Plain(moment), 4), Text: Plain(moment)));
    }

    public ValueTask<TidyResponse> TidyAsync(TidyMoment moment, BunDexView notebook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moment);
        Invitations.Add(moment);
        return ValueTask.FromResult(_tidy(moment, notebook));
    }

    /// <summary>What happened, in plain words (a stand-in has no voice of its own).</summary>
    public static string Plain(KeepMoment moment) => moment.WhatHappened.Quoted ?? moment.WhatHappened.Words;

    private static string FirstWords(string text, int count) =>
        string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(count));
}
