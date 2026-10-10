namespace CompanionCore.Api;

/// <summary>
/// The single semantic-provider seam (architecture §7). The bridge depends only on this
/// contract; mock, replay, and the disabled real-provider shell all satisfy it, and every
/// success reply passes through the same strict parser and local allowlist.
/// </summary>
public interface ISemanticProvider
{
    /// <summary>Neutral provider identifier recorded as provenance (for example "mock").</summary>
    string ProviderName { get; }

    Task<ProviderReply> InterpretAsync(SemanticRequest request, CancellationToken cancellationToken);
}
