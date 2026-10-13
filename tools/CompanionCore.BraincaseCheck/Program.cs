using System.Globalization;
using CompanionCore.Braincase;
using CompanionCore.Memory;

// Builder Prince's first live test (LIVE-02). Without --live it only reports status and
// spends nothing. With --live it makes exactly one Claude call using a synthetic picture
// (never your screen), checked by Prince's own strict parser. With --turn-on, a passing check
// also switches Builder Prince's live calls on.
//
//   dotnet run --project tools/CompanionCore.BraincaseCheck                       (status only)
//   dotnet run --project tools/CompanionCore.BraincaseCheck -- --live             (one call, about a cent)
//   dotnet run --project tools/CompanionCore.BraincaseCheck -- --live --turn-on [--dollars-per-day=5]

var live = args.Contains("--live", StringComparer.OrdinalIgnoreCase);
var turnOn = args.Contains("--turn-on", StringComparer.OrdinalIgnoreCase);
var dollarsArgument = args.FirstOrDefault(argument => argument.StartsWith("--dollars-per-day=", StringComparison.OrdinalIgnoreCase));

var location = new DevelopmentDataRootPolicy(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create)).Resolve();
if (DataLayoutMigration.HasLegacyStores(location))
{
    // M1b: Builder Prince's data moves into the BrainCase, only while Prince is closed.
    if (OperatingSystem.IsWindows() && Mutex.TryOpenExisting("Local\\CompanionCore.Dev.SingleInstance", out var running))
    {
        running.Dispose();
        Console.WriteLine("Prince is running with his data in the old place. Close him first, then run the check again.");
        return 2;
    }

    var moved = DataLayoutMigration.Apply(location);
    Console.WriteLine($"Moved into the BrainCase: {string.Join(", ", moved.Moved)}");
}

var root = location.RootPath;
var (settings, issue) = BuilderBraincaseSettings.LoadOrCreate(root);
Console.WriteLine($"Builder Prince's settings: {Path.Combine(root, BuilderBraincaseSettings.FileName)}");
if (issue != BuilderBraincaseIssue.None)
{
    Console.WriteLine($"  The settings file could not be used ({issue}); live calls stay off until it is fixed or deleted.");
}

if (dollarsArgument is not null)
{
    if (!decimal.TryParse(dollarsArgument["--dollars-per-day=".Length..], NumberStyles.Number, CultureInfo.InvariantCulture, out var dollars)
        || dollars is <= 0 or > 1000)
    {
        Console.WriteLine("  --dollars-per-day must be a number between 0 and 1000.");
        return 2;
    }

    settings = settings with { DollarsPerDay = dollars };
}

Console.WriteLine($"  Model: {settings.Model} (effort {settings.Effort}); Glance: {(settings.GlanceEnabled ? settings.GlanceModel : "off")}");
Console.WriteLine($"  Bun Budget: ${settings.DollarsPerDay.ToString("0.00", CultureInfo.InvariantCulture)} per day (UTC)");
Console.WriteLine($"  Live calls: {(settings.LiveCalls ? "on" : "off")}");

var signedIn = AnthropicSignIn.IsAvailable();
Console.WriteLine($"Sign-in: {(signedIn ? "found" : "not found")}");
if (!signedIn)
{
    Console.WriteLine("  Run `ant auth login` once (or set ANTHROPIC_API_KEY), then run this again.");
    return 1;
}

if (!live)
{
    Console.WriteLine("Status only. Add --live to make one test call.");
    return 0;
}

Console.WriteLine("Making one Claude call with a synthetic picture...");
var result = await BraincaseLiveCheck.RunAsync(settings, CancellationToken.None);
Console.WriteLine($"Result: {result.Verdict} in {result.Latency.TotalSeconds:0.0} s");
switch (result.Verdict)
{
    case LiveCheckVerdict.Passed:
        Console.WriteLine($"  Summary: {result.Summary}");
        Console.WriteLine($"  Observations: {result.Observations}");
        Console.WriteLine($"  Usage: {result.InputUnits} input, {result.OutputUnits} output tokens (about ${result.EstimatedCost.ToString("0.0000", CultureInfo.InvariantCulture)})");
        if (result.GlanceLatencies.Count > 0)
        {
            var sorted = result.GlanceLatencies.Order().ToArray();
            var each = string.Join(", ", result.GlanceLatencies.Select(latency => $"{latency.TotalMilliseconds:0} ms"));
            Console.WriteLine($"  Glances ({settings.GlanceModel}): {each}");
            Console.WriteLine($"  Glance median: {sorted[sorted.Length / 2].TotalMilliseconds:0} ms; failed: {result.GlanceFailures}; cost about ${result.GlanceCost.ToString("0.0000", CultureInfo.InvariantCulture)}");
            foreach (var group in result.GlanceFailureReasons.GroupBy(reason => reason))
            {
                Console.WriteLine($"  Glance failed {group.Count()}x: {group.Key}");
            }
        }

        break;
    case LiveCheckVerdict.ProviderFailed:
        Console.WriteLine($"  The call failed: {result.FailureKind} {result.UnavailableReason}");
        if (result.FailureDetail is { } detail)
        {
            Console.WriteLine($"  The server said: {detail}");
            if (detail.Contains("credit balance", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  Add prepaid credits at console.anthropic.com > Billing, then run this again.");
            }
        }

        break;
    case LiveCheckVerdict.InvalidResponse:
        Console.WriteLine($"  The answer was refused by the strict parser: {result.InvalidReason}");
        break;
}

if (result.Verdict != LiveCheckVerdict.Passed)
{
    return 3;
}

if (turnOn)
{
    (settings with { LiveCalls = true }).Save(root);
    Console.WriteLine("Builder Prince's live calls are now ON. Set \"liveCalls\" to false in the settings file to turn them off.");
}

return 0;
