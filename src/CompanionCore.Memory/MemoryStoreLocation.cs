namespace CompanionCore.Memory;

/// <summary>
/// A validated capability identifying one non-production store. Construction is
/// internal so ordinary callers cannot turn an arbitrary path into an openable store.
/// </summary>
public sealed class MemoryStoreLocation
{
    internal MemoryStoreLocation(
        DataRootKind kind,
        string applicationNamespace,
        string rootPath,
        string databaseFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseFileName);
        if (!string.Equals(
                Path.GetFileName(databaseFileName),
                databaseFileName,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A memory database capability requires a plain file name.",
                nameof(databaseFileName));
        }

        Kind = kind;
        ApplicationNamespace = applicationNamespace;
        RootPath = Path.GetFullPath(rootPath);
        DatabasePath = Path.Combine(RootPath, databaseFileName);
    }

    public DataRootKind Kind { get; }

    public string ApplicationNamespace { get; }

    public string RootPath { get; }

    internal string DatabasePath { get; }

    internal string JournalPath => Path.Combine(RootPath, "session-journal-v1.bin");
}
