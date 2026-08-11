using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth;

public sealed record TargetPolicy(
    AuthorizationCategory AuthorizationCategory,
    TargetContentPolicy ContentPolicy);
