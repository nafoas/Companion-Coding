namespace CompanionCore.Memory;

internal sealed record PreparedAppendOperation(
    AppendMemoryProposal Proposal,
    byte[] CanonicalPayload,
    string OperationChecksum,
    IReadOnlyDictionary<Guid, string> RecordChecksums);

internal enum StoreCommitStatus
{
    Committed = 1,
    AlreadyCommitted = 2,
    Conflict = 3,
}
