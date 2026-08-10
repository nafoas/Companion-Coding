namespace CompanionCore.Runtime.Diagnostics;

/// <summary>
/// Structured local diagnostics. Never networked, never wired to anything by default —
/// see <see cref="NullDiagnosticsSink"/>, which the composition root uses unless an
/// explicit development/test switch selects a real sink.
/// </summary>
public interface IDiagnosticsSink
{
    void Log(string category, string message);
}
