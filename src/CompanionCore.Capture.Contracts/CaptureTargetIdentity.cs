namespace CompanionCore.Capture.Contracts;

/// <summary>
/// Minimum title-free identity for one candidate application window. The path
/// fingerprint is deterministic locally but reveals no raw directory text.
/// </summary>
public sealed record CaptureTargetIdentity
{
    public const int Sha256HexLength = 64;

    public CaptureTargetIdentity(
        long windowId,
        int processId,
        string executableFileName,
        string executablePathFingerprint)
    {
        if (windowId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowId));
        }

        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        if (string.IsNullOrWhiteSpace(executableFileName)
            || executableFileName.Length > 260
            || executableFileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException(
                "The executable identity must contain only a bounded filename, never a raw path.",
                nameof(executableFileName));
        }

        if (!IsSha256Hex(executablePathFingerprint))
        {
            throw new ArgumentException(
                "The executable path fingerprint must be exactly one SHA-256 hexadecimal digest.",
                nameof(executablePathFingerprint));
        }

        WindowId = windowId;
        ProcessId = processId;
        ExecutableFileName = executableFileName;
        ExecutablePathFingerprint = executablePathFingerprint.ToUpperInvariant();
    }

    public long WindowId { get; }

    public int ProcessId { get; }

    public string ExecutableFileName { get; }

    public string ExecutablePathFingerprint { get; }

    public string NeutralDisplayLabel =>
        $"{ExecutableFileName} (PID {ProcessId}, window 0x{WindowId:X})";

    private static bool IsSha256Hex(string? value) =>
        value is { Length: Sha256HexLength }
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F');
}
