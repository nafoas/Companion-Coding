using CompanionCore.Api;

namespace CompanionCore.Sightings;

/// <summary>
/// Courage and trust in Da Boss (M3b), per game and per threat, kept by the subconscious and
/// felt by Prince only as feelings. Boss, 2026-10-13: "Trust should never go down — only up...
/// it should be unconditional." So by construction:
/// <list type="bullet">
/// <item>trust only grows, from Boss's wins against that kind of threat in that game; there is no
/// input that lowers it (not losses, getting hurt, silence, or time);</item>
/// <item>fear only eases, from safe exposure; nothing raises it again (not losses, danger, or time);</item>
/// <item>neither ever touches danger right now: a trusted threat that is dangerous stays fully
/// alert-worthy, and urgency and warnings are never involved.</item>
/// </list>
/// </summary>
internal sealed class Bravery
{
    internal const double TrustRate = 0.4;
    internal const double FearRate = 0.35;
    internal const double FearFloor = 0.2;
    internal const double BossHasGotItAt = 0.5;
    internal const double TrustCalm = 0.6;

    private readonly Dictionary<(string Game, string Threat), Courage> _threats = [];

    internal bool IsThreat(string game, string thing) => _threats.ContainsKey((game, Key(thing)));

    internal double Trust(string game, string threat) =>
        _threats.TryGetValue((game, Key(threat)), out var courage) ? 1 - (1 / (1 + (TrustRate * courage.Wins))) : 0;

    internal double Fear(string game, string threat) =>
        _threats.TryGetValue((game, Key(threat)), out var courage) ? Math.Max(FearFloor, 1 / (1 + (FearRate * courage.SafeExposures))) : 1;

    /// <summary>How pressing a threat feels with Boss handling it: calmer with trust, never when dangerous right now.</summary>
    internal double Calm(Sighting sighting, double novelty) =>
        sighting.Danger || !IsThreat(sighting.Game, sighting.Thing)
            ? novelty
            : novelty * (1 - (TrustCalm * Trust(sighting.Game, sighting.Thing)));

    /// <summary>The only ways in: each can only raise trust or ease fear.</summary>
    internal void Observe(Sighting sighting)
    {
        var key = (sighting.Game, Key(sighting.Thing));
        switch (sighting.Source)
        {
            case SightingSource.Encounter:
                var courage = Get(key);
                if (sighting.Outcome == EncounterOutcome.BossWon)
                {
                    courage.Wins++;
                    courage.SafeExposures++;
                }

                // Ongoing, hurt, and lost: a threat now, but nothing about Prince's trust or fear gets worse.
                break;
            case SightingSource.Observation when sighting.Danger:
                Get(key);
                break;
            case SightingSource.Observation when _threats.TryGetValue(key, out var known):
                known.SafeExposures++;
                break;
        }
    }

    private Courage Get((string, string) key)
    {
        if (!_threats.TryGetValue(key, out var courage))
        {
            _threats[key] = courage = new Courage();
        }

        return courage;
    }

    private static string Key(string thing) => thing.Trim().ToLowerInvariant();

    private sealed class Courage
    {
        public int Wins { get; set; }

        public int SafeExposures { get; set; }
    }
}
