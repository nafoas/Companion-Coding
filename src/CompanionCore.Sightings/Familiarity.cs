namespace CompanionCore.Sightings;

/// <summary>
/// Getting used to things (plan item 12): each sighting counts for a little less, by thing and
/// more gently by kind, per game. Familiarity fades with time, so rare recurrences feel new
/// again; a familiar thing in a new situation feels fresh; and danger never fades below a high
/// floor. This only ever lowers interest: it never deletes a sighting, edits a memory, or
/// touches urgency.
/// </summary>
internal sealed class Familiarity
{
    internal const double Peak = 0.9;
    internal const double ThingRate = 0.6;
    internal const double KindRate = 0.1;
    internal const double DangerFloor = 0.5;
    internal const double Floor = 0.05;
    internal const double NewSituationShare = 0.5;
    internal static readonly TimeSpan ThingHalfLife = TimeSpan.FromDays(3);
    internal static readonly TimeSpan KindHalfLife = TimeSpan.FromDays(7);

    private readonly Dictionary<(string Game, string Thing), Exposure> _things = [];
    private readonly Dictionary<(string Game, Api.ObservationKind Kind), Exposure> _kinds = [];

    /// <summary>How new this sighting feels, before it adds to familiarity.</summary>
    internal double NoveltyOf(Sighting sighting)
    {
        var thing = _things.GetValueOrDefault((sighting.Game, Key(sighting.Thing)));
        var kind = _kinds.GetValueOrDefault((sighting.Game, sighting.Kind));
        var thingCount = thing?.At(sighting.At, ThingHalfLife) ?? 0;
        if (thing is not null && !string.Equals(thing.Situation, sighting.Situation, StringComparison.OrdinalIgnoreCase))
        {
            thingCount *= NewSituationShare;
        }

        var kindCount = kind?.At(sighting.At, KindHalfLife) ?? 0;
        var novelty = Peak / (1 + (ThingRate * thingCount)) * (0.5 + (0.5 / (1 + (KindRate * kindCount))));
        return Math.Max(sighting.Danger ? DangerFloor : Floor, novelty);
    }

    /// <summary>Adds the sighting to familiarity. Returns how new it felt.</summary>
    internal double Observe(Sighting sighting)
    {
        var novelty = NoveltyOf(sighting);
        Add(_things, (sighting.Game, Key(sighting.Thing)), sighting, ThingHalfLife);
        Add(_kinds, (sighting.Game, sighting.Kind), sighting, KindHalfLife);
        return novelty;
    }

    private static void Add<TKey>(Dictionary<TKey, Exposure> map, TKey key, Sighting sighting, TimeSpan halfLife)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var exposure))
        {
            map[key] = new Exposure(1, sighting.At, sighting.Situation);
            return;
        }

        exposure.Count = exposure.At(sighting.At, halfLife) + 1;
        exposure.Last = sighting.At > exposure.Last ? sighting.At : exposure.Last;
        exposure.Situation = sighting.Situation;
    }

    private static string Key(string thing) => thing.Trim().ToLowerInvariant();

    private sealed class Exposure(double count, DateTimeOffset last, string? situation)
    {
        public double Count { get; set; } = count;

        public DateTimeOffset Last { get; set; } = last;

        public string? Situation { get; set; } = situation;

        /// <summary>The effective count at a moment, faded by time since the last exposure.</summary>
        public double At(DateTimeOffset now, TimeSpan halfLife)
        {
            var elapsed = Math.Max(0, (now - Last).TotalSeconds);
            return Count * Math.Pow(0.5, elapsed / halfLife.TotalSeconds);
        }
    }
}
