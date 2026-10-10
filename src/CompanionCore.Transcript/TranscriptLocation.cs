using CompanionCore.Memory;

namespace CompanionCore.Transcript;

/// <summary>
/// Validated transcript directory: a sibling <c>Transcripts</c> directory of an already
/// validated development or test memory location. No raw-path constructor exists, so a
/// transcript can never resolve the production root.
/// </summary>
public sealed class TranscriptLocation
{
    internal const string DirectoryName = "Transcripts";

    private TranscriptLocation(DataRootKind kind, string rootPath)
    {
        Kind = kind;
        RootPath = rootPath;
    }

    public DataRootKind Kind { get; }

    public string RootPath { get; }

    internal string CorruptDirectoryPath => Path.Combine(RootPath, "corrupt-v1");

    internal string SessionPath(Guid sessionId) => Path.Combine(RootPath, $"session-{sessionId:N}.jsonl");

    public static TranscriptLocation For(MemoryStoreLocation memoryLocation)
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
                "Session transcripts require a validated development or test memory location.");
        }

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(memoryLocation.RootPath))
            ?? throw new DataRootViolationException("The memory location has no parent directory.");
        return new TranscriptLocation(memoryLocation.Kind, Path.Combine(parent, DirectoryName));
    }
}
