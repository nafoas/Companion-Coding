namespace CompanionCore.Memory;

/// <summary>
/// A validated capability identifying one non-production store. Construction is
/// internal so ordinary callers cannot turn an arbitrary path into an openable store.
/// </summary>
public sealed class MemoryStoreLocation
{
    internal MemoryStoreLocation(DataRootKind kind, string applicationNamespace, string rootPath)
    {
        Kind = kind;
        ApplicationNamespace = applicationNamespace;
        RootPath = Path.GetFullPath(rootPath);
    }

    public DataRootKind Kind { get; }

    public string ApplicationNamespace { get; }

    public string RootPath { get; }

    internal string DatabasePath => Path.Combine(RootPath, "memory-v1.db");

    internal string JournalPath => Path.Combine(RootPath, "session-journal-v1.bin");
}
