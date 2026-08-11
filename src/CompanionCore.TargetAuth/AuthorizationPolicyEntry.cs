using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth;

internal sealed record AuthorizationPolicyEntry(
    string ExecutablePathFingerprint,
    string ExecutableFileName,
    AuthorizationCategory AuthorizationCategory,
    TargetContentPolicy ContentPolicy);
