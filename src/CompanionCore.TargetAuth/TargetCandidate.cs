using CompanionCore.Capture.Contracts;

namespace CompanionCore.TargetAuth;

/// <summary>
/// An eligible title-free application-window candidate. Discovery adapters may not add
/// document text, command lines, raw paths, pixels, or foreground metadata here.
/// </summary>
public sealed record TargetCandidate(
    CaptureTargetIdentity Identity,
    ApplicationCategory ApplicationCategory)
{
    public string NeutralDisplayLabel => Identity.NeutralDisplayLabel;
}
