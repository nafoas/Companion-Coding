namespace CompanionCore.TargetAuth;

internal sealed record AuthorizationPolicyLoadResult(
    bool IsValid,
    IReadOnlyDictionary<string, AuthorizationPolicyEntry> Entries);
