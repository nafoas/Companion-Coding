using CompanionCore.Runtime;
using CompanionCore.TargetAuth;

namespace CompanionCore.Presentation;

/// <summary>
/// The only <see cref="IPersonalityAdapter"/> implementation wired in during neutral-core
/// stages. This is a direct, literal implementation of architecture §6.2.1's normative
/// mapping table — deterministic, total, and independent of wall-clock time or any other
/// source of non-determinism. Prince's real adapter replaces this on resettable Builder
/// Prince during the Stage 13 personality phase; that is not Companion Awakening, and
/// nothing else in the core depends on which adapter is wired in.
/// </summary>
public sealed class NeutralPersonalityAdapter : IPersonalityAdapter
{
    public const string StartedKey = "lifecycle.started";
    public const string RecoveringKey = "lifecycle.recovering";
    public const string NappingKey = "lifecycle.napping";
    public const string WakingKey = "lifecycle.waking";
    public const string StoppedKey = "lifecycle.stopped";
    public const string UnknownKey = "lifecycle.unknown";
    public const string TargetDiscoveryReadyKey = "target.discovery-ready";
    public const string TargetDiscoveryBlockedKey = "target.discovery-blocked";
    public const string TargetDiscoveryFailedKey = "target.discovery-failed";
    public const string TargetConsentRequiredKey = "target.consent-required";
    public const string TargetDeniedKey = "target.denied";
    public const string StandingAuthorizationAvailableKey = "target.standing-available";
    public const string AnotherTargetActiveKey = "target.another-active";
    public const string TargetAuthorizedKey = "target.authorized";
    public const string TargetPrivacyPausedKey = "target.privacy-paused";
    public const string TargetPrivacyPausedNoTargetKey = "target.privacy-paused-no-target";
    public const string TargetPrivacyResumedKey = "target.privacy-resumed";
    public const string TargetResumedKey = "target.resumed";
    public const string TargetEndedKey = "target.ended";
    public const string TargetUnavailableKey = "target.unavailable";
    public const string TargetFailedKey = "target.failed";

    public PresentationContent Map(LifecycleTransitionResult transition)
    {
        // Deterministic fallback: any invalid transition — an unrecognized event, or a
        // recognized event attempted from an invalid prior state (e.g. Wake when not
        // napping) — renders the same neutral "unknown" content. The raw details behind
        // an invalid transition are diagnostics-only (see CompanionRuntime.Invalid) and
        // never reach this mapping's output.
        if (!transition.IsValid)
        {
            return new PresentationContent(UnknownKey, ExpressionIntent.None);
        }

        return transition.Event switch
        {
            LifecycleEvent.Start when transition.CheckpointRecovered =>
                new PresentationContent(RecoveringKey, ExpressionIntent.Recovering),
            LifecycleEvent.Start =>
                new PresentationContent(StartedKey, ExpressionIntent.None),
            LifecycleEvent.Nap =>
                new PresentationContent(NappingKey, ExpressionIntent.None),
            LifecycleEvent.Wake =>
                new PresentationContent(WakingKey, ExpressionIntent.None),
            LifecycleEvent.Stop =>
                new PresentationContent(StoppedKey, ExpressionIntent.None),
            // Total function: any event value this switch doesn't otherwise name (e.g. a
            // future enum member reaching this old build) still returns a defined,
            // renderable result rather than throwing.
            _ => new PresentationContent(UnknownKey, ExpressionIntent.None)
        };
    }

    public PresentationContent Map(TargetSessionEvent targetEvent)
    {
        ArgumentNullException.ThrowIfNull(targetEvent);
        var detail = targetEvent.Candidate?.NeutralDisplayLabel;
        return targetEvent.Kind switch
        {
            TargetSessionEventKind.DiscoveryReady =>
                new PresentationContent(TargetDiscoveryReadyKey, ExpressionIntent.None),
            TargetSessionEventKind.DiscoveryBlocked =>
                new PresentationContent(TargetDiscoveryBlockedKey, ExpressionIntent.PrivacyPaused),
            TargetSessionEventKind.DiscoveryFailed =>
                new PresentationContent(TargetDiscoveryFailedKey, ExpressionIntent.None),
            TargetSessionEventKind.ConsentRequired =>
                new PresentationContent(TargetConsentRequiredKey, ExpressionIntent.None, detail),
            TargetSessionEventKind.Denied =>
                new PresentationContent(TargetDeniedKey, ExpressionIntent.None, detail),
            TargetSessionEventKind.StandingAuthorizationAvailable =>
                new PresentationContent(StandingAuthorizationAvailableKey, ExpressionIntent.None, detail),
            TargetSessionEventKind.AnotherTargetActive =>
                new PresentationContent(AnotherTargetActiveKey, ExpressionIntent.None, detail),
            TargetSessionEventKind.Authorized =>
                new PresentationContent(TargetAuthorizedKey, ExpressionIntent.None, detail),
            TargetSessionEventKind.PrivacyPaused when detail is null =>
                new PresentationContent(TargetPrivacyPausedNoTargetKey, ExpressionIntent.PrivacyPaused),
            TargetSessionEventKind.PrivacyPaused =>
                new PresentationContent(TargetPrivacyPausedKey, ExpressionIntent.PrivacyPaused, detail),
            TargetSessionEventKind.PrivacyResumed =>
                new PresentationContent(TargetPrivacyResumedKey, ExpressionIntent.None),
            TargetSessionEventKind.Resumed =>
                new PresentationContent(TargetResumedKey, ExpressionIntent.None, detail),
            TargetSessionEventKind.TargetEnded =>
                new PresentationContent(TargetEndedKey, ExpressionIntent.None),
            TargetSessionEventKind.TargetUnavailable =>
                new PresentationContent(TargetUnavailableKey, ExpressionIntent.PrivacyPaused, detail),
            TargetSessionEventKind.Failed =>
                new PresentationContent(TargetFailedKey, ExpressionIntent.None, detail),
            _ => new PresentationContent(UnknownKey, ExpressionIntent.None)
        };
    }
}
