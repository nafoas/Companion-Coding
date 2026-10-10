using CompanionCore.Memory;

namespace CompanionCore.Api;

/// <summary>
/// Validated location of the local bridge journal: a sibling <c>Braincase</c> directory
/// of an already validated development or test memory location. There is no raw-path
/// constructor, so the bridge can never resolve the production root.
/// </summary>
public sealed class BraincaseStateLocation
{
    internal const string DirectoryName = "Braincase";
    internal const string JournalFileName = "braincase-journal-v1.jsonl";

    private BraincaseStateLocation(DataRootKind kind, string rootPath)
    {
        Kind = kind;
        RootPath = rootPath;
    }

    public DataRootKind Kind { get; }

    public string RootPath { get; }

    internal string JournalPath => Path.Combine(RootPath, JournalFileName);

    internal string CompactionTemporaryPath => Path.Combine(RootPath, "." + JournalFileName + ".tmp");

    internal string CorruptDirectoryPath => Path.Combine(RootPath, "corrupt-journal-v1");

    public static BraincaseStateLocation For(MemoryStoreLocation memoryLocation)
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
                "The bridge journal requires a validated development or test memory location.");
        }

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(memoryLocation.RootPath))
            ?? throw new DataRootViolationException("The memory location has no parent directory.");
        return new BraincaseStateLocation(memoryLocation.Kind, Path.Combine(parent, DirectoryName));
    }
}
