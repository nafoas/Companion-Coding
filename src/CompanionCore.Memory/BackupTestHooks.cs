namespace CompanionCore.Memory;

internal enum BackupTestPoint
{
    CutEstablished,
    SnapshotCopied,
    SourceValidated,
    CandidateBuilt,
    CandidateValidated,
    BeforeArchivePromotion,
    AfterArchivePromotion,
    BeforeJournalReplacement,
    AfterJournalReplacement,
}

/// <summary>
/// Friend-test-only deterministic pause/fault seam. It is internal, is never resolved by
/// the application, and does not add a shipping command or public maintenance surface.
/// </summary>
internal interface IBackupTestHook
{
    Task OnPointAsync(BackupTestPoint point, CancellationToken cancellationToken);
}
