using CompanionCore.BuilderReset;
using CompanionCore.Memory;

namespace CompanionCore.BunDex.Tests;

/// <summary>M1: the one-time Builder Prince reset touches only his development data, and only when confirmed.</summary>
public sealed class BuilderResetTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "CompanionCore.BuilderReset.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void TheReset_ShowsItsPlan_RefusesWithoutTheExactPath_ThenRemovesOnlyBuilderData_AndIsIdempotent()
    {
        var dev = Path.Combine(_base, DevelopmentDataRootPolicy.DevelopmentApplicationNamespace);
        var memory = Path.Combine(dev, "Memory");
        Write(Path.Combine(memory, DevelopmentDataRootPolicy.DevelopmentDatabaseFileName), "memories");
        Write(Path.Combine(memory, "backups-v1", "memory-vault-v1.zip"), "vault");
        Write(Path.Combine(memory, BuilderResetPlan.SettingsFileName), "{\"liveCalls\": true}");
        foreach (var name in BuilderResetPlan.RemovedSiblings)
        {
            Write(Path.Combine(dev, name, "data.bin"), name);
        }

        Write(Path.Combine(dev, "TargetAuthorization", "policy.json"), "allowed");
        Write(Path.Combine(dev, "Calibration", "sample.json"), "numbers");
        var production = Path.Combine(_base, DevelopmentDataRootPolicy.ProductionApplicationNamespace, "Memory", DevelopmentDataRootPolicy.ProductionDatabaseFileName);
        Write(production, "Companion Prince");

        var plan = BuilderResetPlan.For(_base);

        Assert.Equal(memory, plan.MemoryRoot);
        Assert.Equal(2 + BuilderResetPlan.RemovedSiblings.Length, plan.Remove.Count);
        Assert.Contains(Path.Combine(memory, BuilderResetPlan.SettingsFileName), plan.Keep);
        Assert.All(plan.Remove, path => Assert.StartsWith(dev + Path.DirectorySeparatorChar, path, StringComparison.Ordinal));

        // Nothing happens without the exact path.
        Assert.False(plan.Confirms(null));
        Assert.False(plan.Confirms(dev));
        Assert.False(plan.Confirms(Path.Combine(memory, "sub")));
        Assert.Throws<InvalidOperationException>(() => plan.Execute("yes"));
        Assert.True(File.Exists(Path.Combine(memory, DevelopmentDataRootPolicy.DevelopmentDatabaseFileName)));

        Assert.Equal(plan.Remove.Count, plan.Execute($"\"{memory}{Path.DirectorySeparatorChar}\""));

        Assert.False(File.Exists(Path.Combine(memory, DevelopmentDataRootPolicy.DevelopmentDatabaseFileName)));
        Assert.False(Directory.Exists(Path.Combine(memory, "backups-v1")));
        Assert.All(BuilderResetPlan.RemovedSiblings, name => Assert.False(Directory.Exists(Path.Combine(dev, name))));
        Assert.Equal("{\"liveCalls\": true}", File.ReadAllText(Path.Combine(memory, BuilderResetPlan.SettingsFileName)));
        Assert.Equal("allowed", File.ReadAllText(Path.Combine(dev, "TargetAuthorization", "policy.json")));
        Assert.Equal("numbers", File.ReadAllText(Path.Combine(dev, "Calibration", "sample.json")));
        Assert.Equal("Companion Prince", File.ReadAllText(production));

        // Running it again finds nothing more to remove.
        var again = BuilderResetPlan.For(_base);
        Assert.Empty(again.Remove);
        Assert.Equal(0, again.Execute(memory));
        Assert.Equal("Companion Prince", File.ReadAllText(production));
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_base, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
