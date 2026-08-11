namespace CompanionCore.Privacy;

internal sealed record PrivacyPauseReceipt(
    long RevokedGeneration,
    long PausedGeneration,
    bool WasAlreadyPaused,
    Task AdmittedWorkDrained);
