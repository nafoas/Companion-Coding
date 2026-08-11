namespace CompanionCore.TargetAuth;

public enum TargetAuthorizationStatus
{
    Authorized,
    ConsentRequired,
    Denied,
    PrivacyPaused,
    AnotherTargetActive,
    UnsupportedDisplayTopology,
    StaleTarget,
    Cancelled,
    Failed
}
