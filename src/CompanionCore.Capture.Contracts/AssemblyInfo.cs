using System.Runtime.CompilerServices;

// Only the target-authorization authority may issue a capture grant. Capture workers
// consume grants but cannot mint one, and ordinary app/runtime code cannot construct
// an authorization capability directly.
[assembly: InternalsVisibleTo("CompanionCore.TargetAuth")]
[assembly: InternalsVisibleTo("CompanionCore.Capture.Tests")]
[assembly: InternalsVisibleTo("CompanionCore.Capture.Worker.Tests")]
