namespace CompanionCore.Presentation;

/// <summary>
/// The neutral placeholder strings behind each content key <see cref="NeutralPersonalityAdapter"/>
/// can produce. These are implementation detail, not an architectural decision — swap
/// them freely. What's normative is the content key each lifecycle event maps to
/// (<see cref="NeutralPersonalityAdapter"/>), not the literal text shown here.
/// </summary>
public static class PlaceholderStrings
{
    public static readonly IReadOnlyDictionary<string, string> ByContentKey =
        new Dictionary<string, string>
        {
            [NeutralPersonalityAdapter.StartedKey] = "Ready.",
            [NeutralPersonalityAdapter.RecoveringKey] = "Resuming from last checkpoint.",
            [NeutralPersonalityAdapter.NappingKey] = "Napping.",
            [NeutralPersonalityAdapter.WakingKey] = "Waking.",
            [NeutralPersonalityAdapter.StoppedKey] = "Stopped.",
            [NeutralPersonalityAdapter.UnknownKey] = "Status unavailable."
        };

    public static string Resolve(PresentationContent content) =>
        ByContentKey.TryGetValue(content.ContentKey, out var text) ? text : ByContentKey[NeutralPersonalityAdapter.UnknownKey];
}
