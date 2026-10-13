using CompanionCore.Memory;

namespace CompanionCore.Sightings;

/// <summary>
/// The sighting record's home: <c>Braincase\sightings-v1</c> of a validated development or test
/// location. There is no raw-path constructor, so it can never resolve the production root.
/// </summary>
public sealed class SightingLocation
{
    internal const string DirectoryName = "sightings-v1";

    private SightingLocation(DataRootKind kind, string rootPath)
    {
        Kind = kind;
        RootPath = rootPath;
    }

    public DataRootKind Kind { get; }

    public string RootPath { get; }

    public static SightingLocation For(MemoryStoreLocation memoryLocation)
    {
        ArgumentNullException.ThrowIfNull(memoryLocation);
        var expected = memoryLocation.Kind switch
        {
            DataRootKind.Development => DevelopmentDataRootPolicy.DevelopmentApplicationNamespace,
            DataRootKind.Test => TestDataRootPolicy.TestApplicationNamespace,
            _ => null,
        };

        if (expected is null || !string.Equals(memoryLocation.ApplicationNamespace, expected, StringComparison.Ordinal))
        {
            throw new DataRootViolationException("The sighting record requires a validated development or test memory location.");
        }

        return new SightingLocation(memoryLocation.Kind, Path.Combine(memoryLocation.BraincaseRootPath, DirectoryName));
    }
}
