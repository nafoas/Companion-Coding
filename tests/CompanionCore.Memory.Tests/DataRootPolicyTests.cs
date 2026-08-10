using System.Reflection;

namespace CompanionCore.Memory.Tests;

public sealed class DataRootPolicyTests
{
    [Fact]
    public void DevelopmentPolicy_UsesFixedNamespaceDistinctFromProduction()
    {
        using var directory = new MemoryTestDirectory();
        var policy = new DevelopmentDataRootPolicy(directory.BasePath);

        var location = policy.Resolve();

        Assert.Equal(DataRootKind.Development, location.Kind);
        Assert.Equal("CompanionCore.Dev", location.ApplicationNamespace);
        Assert.Equal(
            Path.Combine(directory.BasePath, "CompanionCore.Dev", "Memory"),
            location.RootPath);
        Assert.NotEqual(policy.RecognizedProductionRoot, location.RootPath);
        Assert.Equal(
            DevelopmentDataRootPolicy.DevelopmentDatabaseFileName,
            Path.GetFileName(location.DatabasePath));
        Assert.NotEqual(policy.RecognizedProductionDatabasePath, location.DatabasePath);
        Assert.False(Directory.Exists(location.RootPath));
        Assert.False(Directory.Exists(policy.RecognizedProductionRoot));
        Assert.False(File.Exists(policy.RecognizedProductionDatabasePath));
    }

    [Fact]
    public void DevelopmentPolicy_RejectsProductionAndArbitraryOverridesBeforeCreation()
    {
        using var directory = new MemoryTestDirectory();
        var policy = new DevelopmentDataRootPolicy(directory.BasePath);
        var arbitraryRoot = Path.Combine(directory.BasePath, "arbitrary-memory");

        Assert.Throws<DataRootViolationException>(() =>
            policy.Resolve(policy.RecognizedProductionRoot));
        Assert.Throws<DataRootViolationException>(() => policy.Resolve(arbitraryRoot));

        Assert.False(Directory.Exists(policy.RecognizedProductionRoot));
        Assert.False(Directory.Exists(arbitraryRoot));
        Assert.False(Directory.Exists(Path.Combine(directory.BasePath, "CompanionCore.Dev")));
    }

    [Fact]
    public void TestPolicy_RequiresExplicitAbsoluteUniqueRoot()
    {
        using var directory = new MemoryTestDirectory();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        var first = TestDataRootPolicy.Create(directory.BasePath, firstId);
        var second = TestDataRootPolicy.Create(directory.BasePath, secondId);

        Assert.Equal(DataRootKind.Test, first.Kind);
        Assert.Equal("CompanionCore.Tests", first.ApplicationNamespace);
        Assert.Contains(firstId.ToString("N"), first.RootPath, StringComparison.Ordinal);
        Assert.NotEqual(first.RootPath, second.RootPath);
        Assert.Equal(TestDataRootPolicy.TestDatabaseFileName, Path.GetFileName(first.DatabasePath));
        Assert.False(Directory.Exists(first.RootPath));
        Assert.Throws<ArgumentException>(() => TestDataRootPolicy.Create("relative", Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => TestDataRootPolicy.Create(directory.BasePath, Guid.Empty));
    }

    [Fact]
    public void PublicOpeningSurface_RequiresValidatedCapabilityAndHasNoRawPath()
    {
        var publicConstructors = typeof(MemoryStoreLocation).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance);
        var open = Assert.Single(
            typeof(MemoryRepository).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == "OpenAsync");

        Assert.Empty(publicConstructors);
        Assert.Equal(typeof(MemoryStoreLocation), open.GetParameters()[0].ParameterType);
        Assert.DoesNotContain(open.GetParameters(), parameter => parameter.ParameterType == typeof(string));
        Assert.DoesNotContain(
            typeof(LocalWriteGate).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("update", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("delete", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("overwrite", StringComparison.OrdinalIgnoreCase));
    }
}
