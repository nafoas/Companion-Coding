namespace CompanionCore.Runtime.Diagnostics;

/// <summary>
/// The default, silent diagnostics sink. The composition root wires this in unless an
/// explicit development/test switch is set, so diagnostics are off by default as required.
/// </summary>
public sealed class NullDiagnosticsSink : IDiagnosticsSink
{
    public static readonly NullDiagnosticsSink Instance = new();

    private NullDiagnosticsSink()
    {
    }

    public void Log(string category, string message)
    {
        // Deliberately does nothing.
    }
}
