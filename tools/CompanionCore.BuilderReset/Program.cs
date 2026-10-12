using CompanionCore.BuilderReset;

// Builder Prince's one-time reset (M1). Without --confirm it only shows what would happen.
//
//   dotnet run --project tools/CompanionCore.BuilderReset
//   dotnet run --project tools/CompanionCore.BuilderReset -- --confirm="<the exact Memory path it prints>"

var confirm = args.FirstOrDefault(argument => argument.StartsWith("--confirm=", StringComparison.OrdinalIgnoreCase))?["--confirm=".Length..];
var plan = BuilderResetPlan.For(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create));

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

if (OperatingSystem.IsWindows() && Mutex.TryOpenExisting("Local\\CompanionCore.Dev.SingleInstance", out var running))
{
    running.Dispose();
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
