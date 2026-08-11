using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed record WorkerHostOptions(string PipeName, string HandshakeNonce, bool UseSyntheticSource)
{
    internal const int MaximumPipeNameLength = 128;
    internal const int NonceLength = CaptureIpcProtocol.HandshakeNonceHexLength;

    internal static WorkerHostOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? pipeName = null;
        string? nonce = null;
        var synthetic = false;
        foreach (var argument in args)
        {
            if (argument.StartsWith("--pipe=", StringComparison.Ordinal))
            {
                pipeName = argument["--pipe=".Length..];
            }
            else if (argument.StartsWith("--nonce=", StringComparison.Ordinal))
            {
                nonce = argument["--nonce=".Length..];
            }
            else if (string.Equals(argument, "--synthetic-private-test-source", StringComparison.Ordinal))
            {
                synthetic = true;
            }
            else
            {
                throw new ArgumentException("Unsupported worker argument.", nameof(args));
            }
        }

        if (string.IsNullOrWhiteSpace(pipeName)
            || pipeName.Length > MaximumPipeNameLength
            || pipeName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("The worker pipe name is invalid.", nameof(args));
        }

        if (nonce is not { Length: NonceLength }
            || nonce.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("The worker handshake nonce is invalid.", nameof(args));
        }

        return new WorkerHostOptions(pipeName, nonce.ToUpperInvariant(), synthetic);
    }
}
