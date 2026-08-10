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

    internal string BackupDirectoryPath => Path.Combine(RootPath, "backups-v1");

    internal string BackupArchivePath => Path.Combine(BackupDirectoryPath, "memory-vault-v1.zip");

    internal string BackupStagingDirectoryPath => Path.Combine(RootPath, ".backup-staging-v1");

    internal string BackupValidationDirectoryPath => Path.Combine(RootPath, ".backup-validation-v1");

    internal string RepairStagingDirectoryPath => Path.Combine(RootPath, ".repair-staging-v1");

    internal string MaintenanceLockPath => Path.Combine(RootPath, "memory-maintenance-v1.lock");

    internal string RepairMarkerPath => Path.Combine(RootPath, "repair-state-v1.bin");

    internal string RepairMarkerTemporaryPath => Path.Combine(RootPath, ".repair-state-v1.bin.tmp");

    internal string DamagedPreservationDirectoryPath => Path.Combine(RootPath, "damaged-memory-v1");
}
