using CompanionCore.Api;

namespace CompanionCore.Sightings;

public enum SightingSource
{
    /// <summary>Something the eyes noticed in a look.</summary>
    Observation = 1,

    /// <summary>Something the eyes proposed remembering.</summary>
    Proposal = 2,
}

/// <summary>
/// One sighting in the subconscious record: complete and factual. <see cref="Thing"/> is the
/// thing seen (an observation's topic, or a proposal's subject); <see cref="Words"/> are the
/// eyes' words. <see cref="BecameMemory"/> says whether it added something new enough to remember.
/// </summary>
public sealed record Sighting(
    DateTimeOffset At,
    string Game,
    string? Situation,
    string Thing,
    ObservationKind Kind,
    bool Danger,
    string Words,
    SightingSource Source,
    bool BecameMemory = false);

/// <summary>"Saw dis a few times": one growing impression of the same thing, never one memory per sighting.</summary>
public sealed record Impression(string Game, string Thing, int Count, DateTimeOffset First, DateTimeOffset Last, string LatestWords);

/// <summary>How an impression crosses the membrane: the gist, never the count.</summary>
public static class ImpressionSense
{
    public static Curtain.Sense? Of(Impression impression)
    {
        ArgumentNullException.ThrowIfNull(impression);
        return Curtain.Membrane.SawAgain(impression.LatestWords, impression.Count);
    }
}
