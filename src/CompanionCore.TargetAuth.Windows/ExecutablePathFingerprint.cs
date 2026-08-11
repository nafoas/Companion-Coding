using System.Security.Cryptography;
using System.Text;

namespace CompanionCore.TargetAuth.Windows;

internal static class ExecutablePathFingerprint
{
    internal static string Create(string rawPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPath);
        var normalized = Path.GetFullPath(rawPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
