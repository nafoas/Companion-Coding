namespace CompanionCore.Memory;

/// <summary>
/// Creates an explicitly rooted, uniquely namespaced synthetic test location. There is
/// intentionally no parameterless or ambient application-data fallback.
/// </summary>
public static class TestDataRootPolicy
{
    public const string TestApplicationNamespace = "CompanionCore.Tests";
    public const string TestDatabaseFileName = "test-memory-v1.db";

    public static MemoryStoreLocation Create(string isolatedBasePath, Guid testRunId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isolatedBasePath);
        if (!Path.IsPathFullyQualified(isolatedBasePath))
        {
            throw new ArgumentException("The isolated test base must be an absolute path.", nameof(isolatedBasePath));
        }

        if (testRunId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty test-run identifier is required.", nameof(testRunId));
        }

        var root = Path.Combine(
            Path.GetFullPath(isolatedBasePath),
            TestApplicationNamespace,
            testRunId.ToString("N"),
            "Memory");

        return new MemoryStoreLocation(
            DataRootKind.Test,
            TestApplicationNamespace,
            root,
            TestDatabaseFileName);
    }
}
