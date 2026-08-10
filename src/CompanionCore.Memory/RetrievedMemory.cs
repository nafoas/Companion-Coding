namespace CompanionCore.Memory;

public sealed record RetrievedMemory(
    MemoryRecordDraft Record,
    Guid LocalOperationId,
    long JournalSequence,
    string RecordChecksum,
    bool IsCurrent);
