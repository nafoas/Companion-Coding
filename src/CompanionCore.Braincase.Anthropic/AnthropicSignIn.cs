namespace CompanionCore.Braincase;

/// <summary>
/// Detects a sign-in the official SDK will resolve on its own, without Prince ever holding
/// the secret: ANTHROPIC_API_KEY or ANTHROPIC_AUTH_TOKEN, a selected ANTHROPIC_PROFILE, or a
/// profile saved by <c>ant auth login</c> (Windows: %APPDATA%\Anthropic; elsewhere
/// ~/.config/anthropic; or ANTHROPIC_CONFIG_DIR).
/// </summary>
public static class AnthropicSignIn
{
    public static bool IsAvailable() =>
        IsAvailable(Environment.GetEnvironmentVariable, HasSavedProfile);

    internal static bool IsAvailable(Func<string, string?> environment, Func<string, bool> hasSavedProfile)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(hasSavedProfile);
        if (!string.IsNullOrWhiteSpace(environment("ANTHROPIC_API_KEY"))
            || !string.IsNullOrWhiteSpace(environment("ANTHROPIC_AUTH_TOKEN"))
            || !string.IsNullOrWhiteSpace(environment("ANTHROPIC_PROFILE")))
        {
            return true;
        }

        return ConfigDirectory(environment) is { } directory && hasSavedProfile(directory);
    }

    internal static string? ConfigDirectory(Func<string, string?> environment)
    {
        if (environment("ANTHROPIC_CONFIG_DIR") is { Length: > 0 } configured)
        {
            return configured;
        }

        if (OperatingSystem.IsWindows())
        {
            return environment("APPDATA") is { Length: > 0 } appData ? Path.Combine(appData, "Anthropic") : null;
        }

        var home = environment("HOME");
        return string.IsNullOrEmpty(home) ? null : Path.Combine(home, ".config", "anthropic");
    }

    private static bool HasSavedProfile(string configDirectory)
    {
        try
        {
            var credentials = Path.Combine(configDirectory, "credentials");
            return Directory.Exists(credentials) && Directory.EnumerateFiles(credentials, "*.json").Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
