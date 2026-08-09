using System.Runtime.CompilerServices;

// CompanionCore.App needs internal access to CompanionRuntime.ClaimConstructionAuthority
// (its only way to obtain a runtime — the constructor itself is private, not internal,
// so this grant does not expose it). CompanionCore.Runtime.Tests needs internal access
// to LifecycleStateMachine, which carries the actual state-machine logic and none of
// CompanionRuntime's one-shot identity guarantee, so it can be constructed freely for
// exhaustive unit testing without fighting the production one-shot gate.
[assembly: InternalsVisibleTo("CompanionCore.App")]
[assembly: InternalsVisibleTo("CompanionCore.Runtime.Tests")]
