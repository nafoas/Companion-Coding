namespace CompanionCore.BunDex;

/// <summary>
/// A plain, deterministic stand-in for Prince's side of the BunDex until Stage 13 gives him his
/// own voice. It writes where things like this go when that comes to mind; otherwise it makes a
/// plainly named new place. It can be told to decline, to prove "not now" is respected.
/// </summary>
public sealed class StandInWriter : IBunDexWriter
{
    private readonly Func<KeepMoment, IReadOnlyList<string>> _newPlace;
    private readonly Func<KeepMoment, bool> _declineWhen;

    public StandInWriter(Func<KeepMoment, IReadOnlyList<string>>? newPlace = null, Func<KeepMoment, bool>? declineWhen = null)
    {
        _newPlace = newPlace ?? (moment => [FirstWords(moment.WhatHappened, 2) + " stuffs"]);
        _declineWhen = declineWhen ?? (_ => false);
    }

    public List<KeepMoment> Moments { get; } = [];

    public ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moment);
        Moments.Add(moment);
        if (_declineWhen(moment))
        {
            return ValueTask.FromResult(KeepResponse.NotNow);
        }

        var folder = moment.ComesToMind ?? _newPlace(moment);
        return ValueTask.FromResult(new KeepResponse(KeepChoice.Write, folder, FirstWords(moment.WhatHappened, 4), Text: moment.WhatHappened));
    }

    private static string FirstWords(string text, int count) =>
        string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(count));
}
