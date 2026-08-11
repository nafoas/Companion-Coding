using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth;

internal sealed record FrameAdmissionResult(
    FrameAdmissionStatus Status,
    PrivacyGuardDecision? PrivacyDecision);
