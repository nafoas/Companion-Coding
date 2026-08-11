namespace CompanionCore.TargetAuth;

public sealed record TargetSessionEvent(
    TargetSessionEventKind Kind,
    TargetCandidate? Candidate,
    TargetAuthorizationStatus? AuthorizationStatus = null);
