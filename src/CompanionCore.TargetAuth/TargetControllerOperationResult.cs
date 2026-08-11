namespace CompanionCore.TargetAuth;

public sealed record TargetControllerOperationResult(
    bool Succeeded,
    TargetSessionEventKind EventKind,
    TargetAuthorizationResult? Authorization,
    bool CleanupComplete);
