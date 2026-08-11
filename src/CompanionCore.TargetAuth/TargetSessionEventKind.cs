namespace CompanionCore.TargetAuth;

public enum TargetSessionEventKind
{
    DiscoveryReady,
    DiscoveryBlocked,
    DiscoveryFailed,
    ConsentRequired,
    Denied,
    StandingAuthorizationAvailable,
    AnotherTargetActive,
    Authorized,
    PrivacyPaused,
    PrivacyResumed,
    Resumed,
    TargetEnded,
    TargetUnavailable,
    Failed
}
