using CompanionCore.Capture.Contracts;

namespace CompanionCore.TargetAuth;

public sealed record TargetSessionSnapshot
{
    internal TargetSessionSnapshot(
        TargetSessionPhase phase,
        Guid targetSessionId,
        long generation,
        TargetCandidate? candidate,
        TargetPolicy? policy,
        CaptureAuthorizationGrant? grant)
    {
        Phase = phase;
        TargetSessionId = targetSessionId;
        Generation = generation;
        Candidate = candidate;
        Policy = policy;
        Grant = grant;
    }

    public TargetSessionPhase Phase { get; internal init; }

    public Guid TargetSessionId { get; internal init; }

    public long Generation { get; internal init; }

    public TargetCandidate? Candidate { get; internal init; }

    public TargetPolicy? Policy { get; internal init; }

    internal CaptureAuthorizationGrant? Grant { get; init; }

    public static TargetSessionSnapshot None { get; } =
        new(TargetSessionPhase.None, Guid.Empty, 0, null, null, null);
}
