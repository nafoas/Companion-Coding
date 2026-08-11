namespace CompanionCore.TargetAuth;

public sealed record TargetInvitation(
    TargetCandidate Candidate,
    TargetPolicy Policy,
    TargetInvitationDisposition Disposition);
