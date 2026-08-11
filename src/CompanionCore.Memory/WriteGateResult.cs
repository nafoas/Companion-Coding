namespace CompanionCore.Memory;

public enum WriteGateStatus
{
    Committed = 1,
    AlreadyCommitted = 2,
    Rejected = 3,
    Conflict = 4,
}

public enum WriteGateRejectionReason
{
    None = 0,
    OperationNotAllowlisted = 1,
    InvalidProposal = 2,
    OperationConflict = 3,
    PrivacyPausedOrStale = 4,
}

public sealed record WriteGateResult(
    WriteGateStatus Status,
    WriteGateRejectionReason RejectionReason,
    IReadOnlyList<Guid> RecordIds)
{
    public bool IsAccepted => Status is WriteGateStatus.Committed or WriteGateStatus.AlreadyCommitted;

    internal static WriteGateResult Committed(IReadOnlyList<Guid> recordIds) =>
        new(WriteGateStatus.Committed, WriteGateRejectionReason.None, recordIds);

    internal static WriteGateResult AlreadyCommitted(IReadOnlyList<Guid> recordIds) =>
        new(WriteGateStatus.AlreadyCommitted, WriteGateRejectionReason.None, recordIds);

    internal static WriteGateResult Rejected(WriteGateRejectionReason reason) =>
        new(WriteGateStatus.Rejected, reason, Array.Empty<Guid>());

    internal static WriteGateResult Conflict() =>
        new(WriteGateStatus.Conflict, WriteGateRejectionReason.OperationConflict, Array.Empty<Guid>());
}
