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
            [NeutralPersonalityAdapter.UnknownKey] = "Status unavailable.",
            [NeutralPersonalityAdapter.TargetDiscoveryReadyKey] = "Eligible application windows refreshed.",
            [NeutralPersonalityAdapter.TargetDiscoveryBlockedKey] = "Target authorization is paused because exactly one display could not be proven.",
            [NeutralPersonalityAdapter.TargetDiscoveryFailedKey] = "Eligible target discovery failed closed.",
            [NeutralPersonalityAdapter.TargetConsentRequiredKey] = "Permission is required before attaching to {target}.",
            [NeutralPersonalityAdapter.TargetDeniedKey] = "Target is denied by the authorization policy: {target}.",
            [NeutralPersonalityAdapter.StandingAuthorizationAvailableKey] = "Standing authorization is available for {target}.",
            [NeutralPersonalityAdapter.AnotherTargetActiveKey] = "Another target is already authorized: {target}. End it before authorizing a replacement.",
            [NeutralPersonalityAdapter.TargetAuthorizedKey] = "Authorized target: {target}.",
            [NeutralPersonalityAdapter.TargetPrivacyPausedKey] = "Privacy stop is active. Explicit resume is required for {target}.",
            [NeutralPersonalityAdapter.TargetPrivacyPausedNoTargetKey] = "Privacy stop is active. Explicit resume is required; no target is authorized.",
            [NeutralPersonalityAdapter.TargetPrivacyResumedKey] = "Privacy stop cleared. No target is authorized.",
            [NeutralPersonalityAdapter.TargetResumedKey] = "Authorized target resumed: {target}.",
            [NeutralPersonalityAdapter.TargetEndedKey] = "No target is authorized.",
            [NeutralPersonalityAdapter.TargetUnavailableKey] = "The authorized target is no longer available: {target}.",
            [NeutralPersonalityAdapter.TargetFailedKey] = "The target operation failed closed for {target}.",
            ["companion.session-started"] = "Target session started.",
            ["companion.session-ended"] = "Target session ended.",
            ["companion.attention"] = "Attention: {detail}.",
            ["companion.conversation"] = "Conversation: {detail}.",
            ["companion.watchbun"] = "Watchbun: {detail}.",
            ["companion.keepsake"] = "Keepsake: {detail}.",
            ["companion.braincase"] = "Braincase: {detail}.",
            ["companion.consolidated"] = "Session memories consolidated.",
            ["companion.vault-backed-up"] = "Vault backup complete.",
            ["companion.vault-backup-failed"] = "Vault backup failed; the previous backup is unchanged.",
            ["companion.privacy-paused"] = "Privacy stop: pending work cancelled.",
            ["companion.photograph-refused"] = "Photograph not taken: {detail}.",
            ["companion.recovering"] = "Recovering the previous watch.",
            ["companion.fault"] = "An internal error was contained.",
            ["companion.unknown"] = "Status unavailable."
        };

    public static string Resolve(PresentationContent content)
    {
        var template = ByContentKey.TryGetValue(content.ContentKey, out var text)
            ? text
            : FamilyTemplate(content.ContentKey) ?? ByContentKey[NeutralPersonalityAdapter.UnknownKey];
        return template.Replace(
            "{detail}",
            string.IsNullOrWhiteSpace(content.NeutralDetail) ? "update" : content.NeutralDetail,
            StringComparison.Ordinal).Replace(
            "{target}",
            string.IsNullOrWhiteSpace(content.NeutralDetail) ? "the selected target" : content.NeutralDetail,
            StringComparison.Ordinal);
    }

    /// <summary>Typed companion intents (<c>companion.&lt;kind&gt;.&lt;member&gt;</c>) share their family's template.</summary>
    private static string? FamilyTemplate(string key)
    {
        if (!key.StartsWith(NeutralPersonalityAdapter.CompanionPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var dot = key.IndexOf('.', NeutralPersonalityAdapter.CompanionPrefix.Length);
        return dot > 0 && ByContentKey.TryGetValue(key[..dot], out var family) ? family : null;
    }
}
