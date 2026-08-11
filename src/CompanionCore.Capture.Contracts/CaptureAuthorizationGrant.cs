namespace CompanionCore.Capture.Contracts;

/// <summary>
/// Sealed capability proving that <c>TargetAuthorizationService</c> approved one exact
/// window for one target session and privacy generation. Its constructor and issuer
/// are internal to the authorization assembly; ordinary callers cannot create a grant.
/// </summary>
public sealed class CaptureAuthorizationGrant
{
    private CaptureAuthorizationGrant(Guid targetSessionId, long generation, CaptureTargetIdentity target)
    {
        TargetSessionId = targetSessionId;
        Generation = generation;
        Target = target;
    }

    public Guid TargetSessionId { get; }

    public long Generation { get; }

    public CaptureTargetIdentity Target { get; }

    internal static CaptureAuthorizationGrant Issue(
        Guid targetSessionId,
        long generation,
        CaptureTargetIdentity target)
    {
        if (targetSessionId == Guid.Empty)
        {
            throw new ArgumentException("A target session ID is required.", nameof(targetSessionId));
        }

        if (generation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        ArgumentNullException.ThrowIfNull(target);
        return new CaptureAuthorizationGrant(targetSessionId, generation, target);
    }
}
