namespace CompanionCore.TargetAuth;

public sealed record PrivacyStopResult(
    bool WasAlreadyPaused,
    bool HadActiveTarget,
    bool CleanupComplete,
    int ClearedMetadataCount);
