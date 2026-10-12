using System.Text;
using System.Text.RegularExpressions;

namespace CompanionCore.BunDex;

/// <summary>
/// Safety rails for the names Prince gives his folders and pages. Names are made safe, never
/// tidied: spelling, grammar, case, and mess are left exactly as he wrote them. Only names that
/// look like secrets are refused.
/// </summary>
internal static partial class BunDexNames
{
    internal const int MaximumNameLength = 64;
    internal const int MaximumDepth = 4;
    internal const string PageExtension = ".md";

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Returns a safe name, or null when nothing usable or a secret-like name remains.</summary>
    internal static string? MakeSafe(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || LooksSecret(name))
        {
            return null;
        }

        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (char.IsControl(character) || character is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|')
            {
                builder.Append(' ');
            }
            else
            {
                builder.Append(character);
            }
        }

        var cleaned = Spaces().Replace(builder.ToString(), " ").Trim().Trim('.', ' ');
        if (cleaned.Length > MaximumNameLength)
        {
            cleaned = cleaned[..MaximumNameLength].TrimEnd('.', ' ');
        }

        if (cleaned.Length == 0)
        {
            return null;
        }

        var stem = cleaned.Split('.')[0];
        if (ReservedDeviceNames.Contains(stem))
        {
            cleaned = "~" + cleaned;
        }

        return cleaned;
    }

    /// <summary>
    /// API keys and other credential-shaped strings never enter the BunDex: an <c>sk-</c> key, an
    /// "api key:" assignment, or a long token mixing upper case, lower case, and digits.
    /// Ordinary long words or repeated letters are not secrets.
    /// </summary>
    internal static bool LooksSecret(string text) => SecretPattern().IsMatch(text) || HasMixedLongToken(text);

    private const int LongTokenLength = 40;

    private static bool HasMixedLongToken(string text)
    {
        int length = 0;
        bool upper = false, lower = false, digit = false;
        foreach (var character in text.Append(' '))
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '+' or '/' or '_' or '-')
            {
                length++;
                upper |= char.IsAsciiLetterUpper(character);
                lower |= char.IsAsciiLetterLower(character);
                digit |= char.IsAsciiDigit(character);
                continue;
            }

            if (length >= LongTokenLength && upper && lower && digit)
            {
                return true;
            }

            length = 0;
            upper = lower = digit = false;
        }

        return false;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Spaces();

    [GeneratedRegex(@"sk-[A-Za-z0-9_\-]{8,}|(?i:api[_\- ]?key)\s*[:=]", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();
}
