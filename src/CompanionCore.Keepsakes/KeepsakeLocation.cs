using CompanionCore.Memory;

namespace CompanionCore.Keepsakes;

/// <summary>
/// Validated keepsake directory: a sibling <c>Keepsakes</c> directory of an already
/// validated development or test memory location. No raw-path constructor exists, so a
/// keepsake can never resolve the production root.
/// </summary>
public sealed class KeepsakeLocation
{
    internal const string DirectoryName = "Keepsakes";

    private KeepsakeLocation(DataRootKind kind, string rootPath, MemoryStoreLocation memoryLocation)
    {
        Kind = kind;
        RootPath = rootPath;
        MemoryLocation = memoryLocation;
    }

    public DataRootKind Kind { get; }

    public string RootPath { get; }

    internal MemoryStoreLocation MemoryLocation { get; }

    internal string PhotographPath(string sha256) => Path.Combine(RootPath, $"{sha256}.png");

    public static KeepsakeLocation For(MemoryStoreLocation memoryLocation)
    {
        ArgumentNullException.ThrowIfNull(memoryLocation);
        var expectedNamespace = memoryLocation.Kind switch
        {
            DataRootKind.Development => DevelopmentDataRootPolicy.DevelopmentApplicationNamespace,
            DataRootKind.Test => TestDataRootPolicy.TestApplicationNamespace,
            _ => null,
        };

        if (expectedNamespace is null
            || !string.Equals(memoryLocation.ApplicationNamespace, expectedNamespace, StringComparison.Ordinal))
        {
            throw new DataRootViolationException(
                "Keepsakes require a validated development or test memory location.");
        }

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(memoryLocation.RootPath))
            ?? throw new DataRootViolationException("The memory location has no parent directory.");
        return new KeepsakeLocation(memoryLocation.Kind, Path.Combine(parent, DirectoryName), memoryLocation);
    }
}
