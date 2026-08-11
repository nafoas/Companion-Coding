namespace CompanionCore.Privacy;

public readonly record struct PrivacyStateSnapshot(long Generation, bool IsPaused, int ActiveAdmissionCount);
