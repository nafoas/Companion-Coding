using CompanionCore.Api;

namespace CompanionCore.Braincase;

/// <summary>Builder Prince's chosen Braincase: the provider and the bridge budget to run it under.</summary>
public sealed record BuilderBraincaseSelection(
    ISemanticProvider Provider,
    BridgeOptions? Bridge,
    bool LiveCallsEnabled,
    BuilderBraincaseIssue Issue)
{
    /// <summary>Whether Prince glances with a fast model between detailed looks (VIS-02).</summary>
    public bool GlanceEnabled { get; init; }
}

/// <summary>
/// Selects Builder Prince's Braincase from his settings file. Live Claude calls run only
/// when the settings turn them on for a known model, always under the dollars-per-day Bun
/// Budget. Otherwise the offline shell is used, which never makes a call.
/// </summary>
public static class BuilderBraincase
{
    public static BuilderBraincaseSelection Compose(string rootPath, ICredentialStore credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var (settings, issue) = BuilderBraincaseSettings.LoadOrCreate(rootPath);
        if (!settings.LiveCalls
            || AnthropicPricing.BudgetFor(settings.Model, settings.GlanceEnabled ? settings.GlanceModel : null, settings.DollarsPerDay) is not { } budget)
        {
            return new(new RealSemanticProviderShell(credentials), null, false, issue);
        }

        return new(
            new AnthropicSemanticProvider(credentials, settings.ToProviderOptions()),
            new BridgeOptions { DailyCostBudget = budget },
            true,
            issue)
        {
            GlanceEnabled = settings.GlanceEnabled,
        };
    }
}
