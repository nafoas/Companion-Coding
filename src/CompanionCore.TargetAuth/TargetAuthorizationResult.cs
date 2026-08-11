using CompanionCore.Capture.Contracts;

namespace CompanionCore.TargetAuth;

public sealed record TargetAuthorizationResult
{
    internal TargetAuthorizationResult(
        TargetAuthorizationStatus status,
        TargetCandidate candidate,
        TargetPolicy policy,
        bool shouldPrompt,
        CaptureAuthorizationGrant? grant)
    {
        Status = status;
        Candidate = candidate;
        Policy = policy;
        ShouldPrompt = shouldPrompt;
        Grant = grant;
    }

    public TargetAuthorizationStatus Status { get; }

    public TargetCandidate Candidate { get; }

    public TargetPolicy Policy { get; }

    public bool ShouldPrompt { get; }

    internal CaptureAuthorizationGrant? Grant { get; }

    public bool IsAuthorized => Status == TargetAuthorizationStatus.Authorized && Grant is not null;
}
