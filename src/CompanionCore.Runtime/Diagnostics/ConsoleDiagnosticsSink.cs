namespace CompanionCore.Runtime.Diagnostics;

/// <summary>
/// A minimal, local, non-networked diagnostics sink for development/test use. The
/// composition root only constructs this when the explicit diagnostics switch is set;
/// nothing in this assembly wires it in by default.
/// </summary>
public sealed class ConsoleDiagnosticsSink : IDiagnosticsSink
{
    public void Log(string category, string message)
    {
        Console.Error.WriteLine($"[{DateTimeOffset.UtcNow:O}] [{category}] {message}");
    }
}
