using CompanionCore.BuilderReset;

// Builder Prince's one-time reset (M1). Without --confirm it only shows what would happen.
//
//   dotnet run --project tools/CompanionCore.BuilderReset
//   dotnet run --project tools/CompanionCore.BuilderReset -- --confirm="<the exact Memory path it prints>"

var confirm = args.FirstOrDefault(argument => argument.StartsWith("--confirm=", StringComparison.OrdinalIgnoreCase))?["--confirm=".Length..];
var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
var running = false;
if (OperatingSystem.IsWindows() && Mutex.TryOpenExisting("Local\\CompanionCore.Dev.SingleInstance", out var instance))
{
    instance.Dispose();
    running = true;
}
var location = new CompanionCore.Memory.DevelopmentDataRootPolicy(localApplicationData).Resolve();
if (CompanionCore.Memory.DataLayoutMigration.HasLegacyStores(location))
{
    // M1b: the old layout moves into the BrainCase first (a move, never a delete).
    if (running)
    {
        Console.WriteLine("Prince is running with his data in the old place. Close him first; nothing was changed.");
        return 2;
    }

    Console.WriteLine($"Moved into the BrainCase: {string.Join(", ", CompanionCore.Memory.DataLayoutMigration.Apply(location).Moved)}");
}

var plan = BuilderResetPlan.For(localApplicationData);

Console.WriteLine("Builder Prince reset (development data only; Companion Prince is never touched).");
Console.WriteLine($"Development memory folder: {plan.MemoryRoot}");
Console.WriteLine();
Console.WriteLine(plan.Remove.Count == 0 ? "Nothing to remove: Builder Prince is already fresh." : "Will be removed:");
foreach (var path in plan.Remove)
{
    Console.WriteLine($"  - {path}");
}

Console.WriteLine("Kept:");
foreach (var path in plan.Keep)
{
    Console.WriteLine($"  + {path}");
}

if (plan.Keep.Count == 0)
{
    Console.WriteLine("  (nothing to keep yet)");
}

if (confirm is null)
{
    Console.WriteLine();
    Console.WriteLine("Nothing was changed. To reset, close Prince first, then run again with:");
    Console.WriteLine($"  --confirm=\"{plan.MemoryRoot}\"");
    return 0;
}

if (running)
{
    Console.WriteLine();
    Console.WriteLine("Prince is running. Close him first; nothing was changed.");
    return 2;
}

if (!plan.Confirms(confirm))
{
    Console.WriteLine();
    Console.WriteLine("The confirmation did not match the exact path above; nothing was changed.");
    return 3;
}

var removed = plan.Execute(confirm);
Console.WriteLine();
Console.WriteLine($"Done: removed {removed} item(s). Builder Prince starts fresh next time.");
return 0;
