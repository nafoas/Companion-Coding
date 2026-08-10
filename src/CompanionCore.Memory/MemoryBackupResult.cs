namespace CompanionCore.Memory;

internal sealed record MemoryBackupResult(
    Guid BackupId,
    long CutSequence,
    string ArchivePath,
    long ArchiveByteLength,
    string ArchiveSha256);
