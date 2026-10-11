using CompanionCore.Api;

namespace CompanionCore.Braincase;

/// <summary>
/// US dollars per million tokens (input, output) from the Claude API price list, used only to
/// turn Boss's dollars-per-day Bun Budget into the bridge's unit prices. An unknown model has
/// no budget, so live calls stay off for it.
/// </summary>
public static class AnthropicPricing
{
    private static readonly Dictionary<string, (decimal Input, decimal Output)> PerMillion = new(StringComparer.Ordinal)
    {
        ["claude-opus-5-5"] = (4m, 20m),
        ["claude-sonnet-5-5"] = (2m, 10m),
        ["claude-haiku-5-5"] = (0.10m, 0.50m),
        ["claude-opus-4-8"] = (5m, 25m),
    };

    public static bool IsKnown(string model) => PerMillion.ContainsKey(model);

    /// <summary>
    /// The bridge budget for <paramref name="model"/>. The default model's refusal fallback bills
    /// at the fallback model's rates, so it is budgeted at the higher of the two.
    /// </summary>
    public static DailyCostBudget? BudgetFor(string model, decimal dollarsPerDay)
    {
        if (!PerMillion.TryGetValue(model, out var price) || dollarsPerDay <= 0)
        {
            return null;
        }

        if (string.Equals(model, AnthropicProviderOptions.DefaultModel, StringComparison.Ordinal))
        {
            var fallback = PerMillion[AnthropicSemanticProvider.FallbackModel];
            price = (Math.Max(price.Input, fallback.Input), Math.Max(price.Output, fallback.Output));
        }

        return new DailyCostBudget(dollarsPerDay, price.Input, price.Output);
    }

    /// <summary>
    /// The budget for both lanes (VIS-02): the Inspect model's prices, plus the Glance model's
    /// prices for Glances. One dollars-per-day limit covers both. Null when either model is unknown.
    /// </summary>
    public static DailyCostBudget? BudgetFor(string model, string? glanceModel, decimal dollarsPerDay)
    {
        if (BudgetFor(model, dollarsPerDay) is not { } budget)
        {
            return null;
        }

        if (string.IsNullOrEmpty(glanceModel))
        {
            return budget;
        }

        return PerMillion.TryGetValue(glanceModel, out var glance)
            ? budget with { GlanceInputCostPerMillionUnits = glance.Input, GlanceOutputCostPerMillionUnits = glance.Output }
            : null;
    }
}
