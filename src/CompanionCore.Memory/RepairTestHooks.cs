namespace CompanionCore.Memory;

internal enum RepairTestPoint
{
    ArchiveValidated,
    JournalValidated,
    PreservationCompleted,
    BeforeMutation,
    AfterCompanionsMoved,
    AfterDatabaseReplacement,
    AfterJournalReplacement,
    BeforeRecoveryOpen,
    AfterRecoveryValidation,
}

internal interface IRepairTestHook
{
    Task OnPointAsync(RepairTestPoint point, CancellationToken cancellationToken);
}
