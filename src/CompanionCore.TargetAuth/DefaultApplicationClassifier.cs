namespace CompanionCore.TargetAuth;

/// <summary>
/// Conservative filename-only classification for well-known sensitive application
/// families. A miss stays UnknownAsk; it never becomes standing authority.
/// </summary>
public static class DefaultApplicationClassifier
{
    private static readonly IReadOnlyDictionary<string, ApplicationCategory> ByFileName =
        new Dictionary<string, ApplicationCategory>(StringComparer.OrdinalIgnoreCase)
        {
            ["chrome.exe"] = ApplicationCategory.Browser,
            ["msedge.exe"] = ApplicationCategory.Browser,
            ["firefox.exe"] = ApplicationCategory.Browser,
            ["brave.exe"] = ApplicationCategory.Browser,
            ["opera.exe"] = ApplicationCategory.Browser,
            ["vivaldi.exe"] = ApplicationCategory.Browser,
            ["arc.exe"] = ApplicationCategory.Browser,
            ["discord.exe"] = ApplicationCategory.Messaging,
            ["slack.exe"] = ApplicationCategory.Messaging,
            ["teams.exe"] = ApplicationCategory.Messaging,
            ["ms-teams.exe"] = ApplicationCategory.Messaging,
            ["telegram.exe"] = ApplicationCategory.Messaging,
            ["whatsapp.exe"] = ApplicationCategory.Messaging,
            ["signal.exe"] = ApplicationCategory.Messaging,
            ["outlook.exe"] = ApplicationCategory.Email,
            ["olk.exe"] = ApplicationCategory.Email,
            ["thunderbird.exe"] = ApplicationCategory.Email,
            ["winword.exe"] = ApplicationCategory.WordProcessor,
            ["soffice.bin"] = ApplicationCategory.WordProcessor,
            ["libreoffice.exe"] = ApplicationCategory.WordProcessor,
            ["onedrive.exe"] = ApplicationCategory.CloudStorage,
            ["dropbox.exe"] = ApplicationCategory.CloudStorage,
            ["googledrivefs.exe"] = ApplicationCategory.CloudStorage,
            ["googledrivesync.exe"] = ApplicationCategory.CloudStorage,
            ["1password.exe"] = ApplicationCategory.PasswordManager,
            ["bitwarden.exe"] = ApplicationCategory.PasswordManager,
            ["keepass.exe"] = ApplicationCategory.PasswordManager,
            ["keepassxc.exe"] = ApplicationCategory.PasswordManager,
            ["protonpass.exe"] = ApplicationCategory.PasswordManager,
        };

    public static ApplicationCategory Classify(string executableFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableFileName);
        return ByFileName.TryGetValue(executableFileName, out var category)
            ? category
            : ApplicationCategory.Other;
    }

    public static bool IsSensitiveByDefault(ApplicationCategory category) =>
        category is ApplicationCategory.Browser
            or ApplicationCategory.SocialMedia
            or ApplicationCategory.WordProcessor
            or ApplicationCategory.CloudStorage
            or ApplicationCategory.Email
            or ApplicationCategory.Messaging
            or ApplicationCategory.PasswordManager
            or ApplicationCategory.Banking
            or ApplicationCategory.Tax
            or ApplicationCategory.Medical
            or ApplicationCategory.Government;
}
