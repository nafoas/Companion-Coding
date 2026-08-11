using CompanionCore.Capture.Contracts;
using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth;

/// <summary>
/// Final synchronous boundary before metadata can reach any presentation, semantic,
/// journal, or memory consumer. Generation admission is leased across the downstream
/// callback so privacy stop either precedes admission or waits for already-admitted
/// work to finish; a late post-stop frame cannot cross.
/// </summary>
internal sealed class AuthorizedFrameAdmissionGate
{
    private readonly TargetAuthorizationService _authorization;
    private readonly RuntimePrivacyState _privacyState;
    private readonly LocalPrivacyGuard _privacyGuard;

    internal AuthorizedFrameAdmissionGate(
        TargetAuthorizationService authorization,
        RuntimePrivacyState privacyState,
        LocalPrivacyGuard privacyGuard)
    {
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _privacyState = privacyState ?? throw new ArgumentNullException(nameof(privacyState));
        _privacyGuard = privacyGuard ?? throw new ArgumentNullException(nameof(privacyGuard));
    }

    internal FrameAdmissionResult TryAdmit(
        CaptureFrameMetadata frame,
        PrivacyAssessment assessment,
        Action<CaptureFrameMetadata> downstream)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(downstream);

        if (!_privacyState.TryAcquireAdmissionLease(frame.Generation, out var lease))
        {
            return new FrameAdmissionResult(FrameAdmissionStatus.StaleOrUnauthorized, null);
        }

        using (lease)
        {
            if (!_authorization.TryGetCurrentPolicy(frame, out var policy)
                || policy is null)
            {
                return new FrameAdmissionResult(FrameAdmissionStatus.StaleOrUnauthorized, null);
            }

            var privacyDecision = _privacyGuard.Evaluate(policy.ContentPolicy, assessment);
            if (privacyDecision is PrivacyGuardDecision.RejectedSensitive
                or PrivacyGuardDecision.RejectedUnavailable)
            {
                return new FrameAdmissionResult(FrameAdmissionStatus.PrivacyRejected, privacyDecision);
            }

            downstream(frame);
            return new FrameAdmissionResult(FrameAdmissionStatus.Admitted, privacyDecision);
        }
    }
}
