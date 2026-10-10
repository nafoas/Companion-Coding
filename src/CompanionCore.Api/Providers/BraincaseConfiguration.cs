namespace CompanionCore.Api;

public enum BraincaseProviderKind
{
    Real = 1,
    Mock = 2,
    Replay = 3,
    Disabled = 4,
}

public enum BraincaseConfigurationIssue
{
    None = 0,
    UnknownProvider = 1,
    ReplayDirectoryMissing = 2,
    ReplayFixturesInvalid = 3,
}

/// <summary>
/// Graceful provider selection. Parsing never throws: the default is the one primary
/// provider (neutrally unavailable until credentials and the final gate exist), and an
/// unknown value selects the disabled provider with a recorded issue.
/// </summary>
public sealed record BraincaseConfiguration(
    BraincaseProviderKind ProviderKind,
    string? ReplayFixtureDirectory,
    BraincaseConfigurationIssue Issue)
{
    public static BraincaseConfiguration Default { get; } =
        new(BraincaseProviderKind.Real, null, BraincaseConfigurationIssue.None);

    public static BraincaseConfiguration Parse(string? providerSetting, string? replayFixtureDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(providerSetting))
        {
            return Default;
        }

        var kind = providerSetting.Trim().ToLowerInvariant() switch
        {
            "real" => BraincaseProviderKind.Real,
            "mock" => BraincaseProviderKind.Mock,
            "replay" => BraincaseProviderKind.Replay,
            "disabled" => BraincaseProviderKind.Disabled,
            _ => (BraincaseProviderKind?)null,
        };

        if (kind is null)
        {
            return new(BraincaseProviderKind.Disabled, null, BraincaseConfigurationIssue.UnknownProvider);
        }

        if (kind == BraincaseProviderKind.Replay && string.IsNullOrWhiteSpace(replayFixtureDirectory))
        {
            return new(BraincaseProviderKind.Disabled, null, BraincaseConfigurationIssue.ReplayDirectoryMissing);
        }

        return new(
            kind.Value,
            kind == BraincaseProviderKind.Replay ? replayFixtureDirectory : null,
            BraincaseConfigurationIssue.None);
    }
}

public sealed record SemanticProviderSelection(ISemanticProvider Provider, BraincaseConfigurationIssue Issue);

/// <summary>Builds the configured provider; failures degrade to a neutral disabled provider.</summary>
public static class SemanticProviderFactory
{
    public static SemanticProviderSelection Create(BraincaseConfiguration configuration, ICredentialStore credentials)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(credentials);
        switch (configuration.ProviderKind)
        {
            case BraincaseProviderKind.Real:
                return new(new RealSemanticProviderShell(credentials), configuration.Issue);
            case BraincaseProviderKind.Mock:
                return new(new MockSemanticProvider(), configuration.Issue);
            case BraincaseProviderKind.Replay:
                try
                {
                    return new(ReplaySemanticProvider.Load(configuration.ReplayFixtureDirectory!), configuration.Issue);
                }
                catch (ReplayFixtureException)
                {
                    return new(
                        new DisabledSemanticProvider(ProviderUnavailableReason.FixturesUnavailable),
                        BraincaseConfigurationIssue.ReplayFixturesInvalid);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    return new(
                        new DisabledSemanticProvider(ProviderUnavailableReason.FixturesUnavailable),
                        BraincaseConfigurationIssue.ReplayFixturesInvalid);
                }

            default:
                return new(new DisabledSemanticProvider(), configuration.Issue);
        }
    }
}
