namespace CompanionCore.TargetAuth;

internal sealed record TargetPrivacyPause(
    bool HadActiveTarget,
    TargetSessionSnapshot Session,
    CompanionCore.Privacy.PrivacyPauseReceipt PrivacyReceipt);
