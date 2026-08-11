namespace CompanionCore.TargetAuth;

public sealed record TargetDiscoveryResult(
    TargetDiscoveryStatus Status,
    int AttachedDisplayCount,
    IReadOnlyList<TargetCandidate> Candidates)
{
    public static TargetDiscoveryResult Unsupported(int displayCount) =>
        new(TargetDiscoveryStatus.UnsupportedDisplayTopology, displayCount, []);

    public static TargetDiscoveryResult Failed() =>
        new(TargetDiscoveryStatus.Failed, 0, []);
}
