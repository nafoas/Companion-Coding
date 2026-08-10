namespace CompanionCore.Memory;

internal sealed record MemoryRepairResult(
    Guid RepairId,
    Guid BackupId,
    long ArchiveCutSequence,
    long RecoveredThroughSequence,
    long OperationCount,
    long RecordCount,
    long LinkCount,
    string DamagedSourceDirectory);
