namespace CompanionCore.Memory;

/// <summary>
/// Resolves the one fixed development namespace. A configured path override is an
/// explicit error rather than an escape hatch or fallback.
/// </summary>
public sealed class DevelopmentDataRootPolicy
{
    public const string DevelopmentApplicationNamespace = "CompanionCore.Dev";
    public const string ProductionApplicationNamespace = "CompanionCore";

    private readonly string _localApplicationDataBase;

    public DevelopmentDataRootPolicy(string localApplicationDataBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataBase);
        if (!Path.IsPathFullyQualified(localApplicationDataBase))
        {
            throw new ArgumentException("The application-data base must be an absolute path.", nameof(localApplicationDataBase));
        }

        _localApplicationDataBase = Path.GetFullPath(localApplicationDataBase);
    }

    public string RecognizedProductionRoot => Path.Combine(
        _localApplicationDataBase,
        ProductionApplicationNamespace,
        "Memory");

    public MemoryStoreLocation Resolve(string? configuredRootOverride = null)
    {
        if (configuredRootOverride is not null)
        {
            throw new DataRootViolationException(
                "Development memory uses its fixed application namespace; configured path overrides are not permitted.");
        }

        var root = Path.Combine(
            _localApplicationDataBase,
            DevelopmentApplicationNamespace,
            "Memory");

        if (PathsEqual(root, RecognizedProductionRoot))
        {
            throw new DataRootViolationException("The development and production memory roots must be distinct.");
        }

        return new MemoryStoreLocation(DataRootKind.Development, DevelopmentApplicationNamespace, root);
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            comparison);
    }
}
