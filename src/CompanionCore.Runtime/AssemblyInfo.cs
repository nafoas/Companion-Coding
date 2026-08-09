using System.Runtime.CompilerServices;

// The internal CompanionRuntime constructor may only be called from the application
// composition root (CompanionCore.App) or from tests exercising the state machine
// directly. No other assembly is granted access, so "only the composition root may
// construct a CompanionRuntime" is enforced by the compiler, not by convention.
[assembly: InternalsVisibleTo("CompanionCore.App")]
[assembly: InternalsVisibleTo("CompanionCore.Runtime.Tests")]
