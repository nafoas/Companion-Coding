namespace CompanionCore.Memory;

internal static class MemoryBackupFormat
{
    internal const int FormatVersion = 1;
    internal const string DatabaseEntryName = "memory-snapshot-v1.db";
    internal const string ManifestEntryName = "manifest-v1.json";
    internal const string ManifestChecksumEntryName = "manifest-v1.sha256";
    internal const int ExactEntryCount = 3;
    internal const int MaximumManifestBytes = 16 * 1024;
    internal const long MaximumDatabaseBytes = 8L * 1024 * 1024 * 1024;
    internal const long MaximumArchiveBytes = 8L * 1024 * 1024 * 1024;
}
