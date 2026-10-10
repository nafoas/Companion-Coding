using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CompanionCore.TargetAuth.Tests")]

// The orchestration composition point reads the issued grant; it cannot mint one.
[assembly: InternalsVisibleTo("CompanionCore.Orchestration")]
[assembly: InternalsVisibleTo("CompanionCore.Orchestration.Tests")]
