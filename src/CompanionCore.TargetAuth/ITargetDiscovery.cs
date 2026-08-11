namespace CompanionCore.TargetAuth;

public interface ITargetDiscovery
{
    Task<IReadOnlyList<TargetCandidate>> DiscoverAsync(CancellationToken cancellationToken);

    Task<bool> IsStillValidAsync(TargetCandidate target, CancellationToken cancellationToken);
}
