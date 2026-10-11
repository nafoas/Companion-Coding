using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CompanionCore.Memory;

namespace CompanionCore.Vault;

/// <summary>The outcome of a single-file Da Bun Vault export.</summary>
public sealed record VaultExportReport(string Path, long Bytes, string Sha256, long MemoryArchiveBytes, long CompanionArchiveBytes);

/// <summary>
/// Single-file Da Bun Vault export (KEEP-02 D1). It wraps the current, validated memory and
/// companion archives, unchanged, with a checksummed manifest into one file at a path Boss
/// chooses outside the data root. It is written atomically, never overwrites an existing
/// file, and is verified after writing. It copies Prince's Vault for safekeeping; it never
/// creates a second Prince (import and migration remain deferred).
/// </summary>
internal static class VaultExport
{
    internal const int FormatVersion = 1;
    internal const string ManifestName = "da-bun-vault-v1.json";
    internal const string MemoryEntryName = "memory-vault-v1.zip";
    internal const string CompanionEntryName = "companion-vault-v1.zip";
    internal const long MaximumExportBytes = 8L * 1024 * 1024 * 1024;
    private const string CompanionValidationDirectoryName = ".companion-validation-v1";

    internal sealed record Manifest(int FormatVersion, DateTimeOffset CreatedUtc, IReadOnlyList<Entry> Entries);

    internal sealed record Entry(string Name, long Length, string Sha256);

    internal static async Task<VaultExportReport> ExportAsync(
        MemoryStoreLocation location,
        string destinationPath,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        var destination = RequireDestination(location, destinationPath);

        // Both archives must validate exactly as a repair would read them.
        await using (await MemoryBackupArchiveValidator.ValidateAsync(location, location.BackupArchivePath, cancellationToken).ConfigureAwait(false))
        {
        }

        using (await CompanionArchive.ValidateAsync(
                   location.CompanionArchivePath,
                   Path.Combine(location.RootPath, CompanionValidationDirectoryName),
                   expectedBackupId: null,
                   cancellationToken).ConfigureAwait(false))
        {
        }

        var memory = await File.ReadAllBytesAsync(location.BackupArchivePath, cancellationToken).ConfigureAwait(false);
        var companion = await File.ReadAllBytesAsync(location.CompanionArchivePath, cancellationToken).ConfigureAwait(false);
        var manifest = new Manifest(FormatVersion, now.ToUniversalTime(), [Describe(MemoryEntryName, memory), Describe(CompanionEntryName, companion)]);

        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                // The inner archives are already compressed; store them as they are.
                await WriteEntryAsync(archive, MemoryEntryName, memory, cancellationToken).ConfigureAwait(false);
                await WriteEntryAsync(archive, CompanionEntryName, companion, cancellationToken).ConfigureAwait(false);
                await WriteEntryAsync(archive, ManifestName, JsonSerializer.SerializeToUtf8Bytes(manifest), cancellationToken).ConfigureAwait(false);
            }

            using (var flush = new FileStream(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                flush.Flush(flushToDisk: true);
            }

            await VerifyAsync(temporary, cancellationToken).ConfigureAwait(false);

            // Never overwrite: an existing destination makes the move fail and is left untouched.
            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        var bytes = await File.ReadAllBytesAsync(destination, cancellationToken).ConfigureAwait(false);
        return new VaultExportReport(destination, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)), memory.Length, companion.Length);
    }

    /// <summary>Verifies an export file: exactly the three entries, each matching the manifest.</summary>
    internal static async Task<Manifest> VerifyAsync(string exportPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(exportPath) || new FileInfo(exportPath).Length is <= 0 or > MaximumExportBytes)
        {
            throw new BackupValidationException("The Vault export is missing or outside its bound.");
        }

        try
        {
            await using var stream = new FileStream(exportPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var names = archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
            if (!names.SequenceEqual(new[] { CompanionEntryName, ManifestName, MemoryEntryName }.Order(StringComparer.Ordinal)))
            {
                throw new BackupValidationException("The Vault export has unexpected entries.");
            }

            var manifest = JsonSerializer.Deserialize<Manifest>(await ReadEntryAsync(archive.GetEntry(ManifestName)!, 64 * 1024, cancellationToken).ConfigureAwait(false))
                ?? throw new BackupValidationException("The Vault export manifest is empty.");
            if (manifest.FormatVersion != FormatVersion
                || manifest.Entries is not { Count: 2 }
                || !manifest.Entries.Select(entry => entry.Name).Order(StringComparer.Ordinal).SequenceEqual(new[] { CompanionEntryName, MemoryEntryName }.Order(StringComparer.Ordinal)))
            {
                throw new BackupValidationException("The Vault export manifest is invalid.");
            }

            foreach (var declared in manifest.Entries)
            {
                var bytes = await ReadEntryAsync(archive.GetEntry(declared.Name)!, MaximumExportBytes, cancellationToken).ConfigureAwait(false);
                if (bytes.Length != declared.Length || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), declared.Sha256, StringComparison.Ordinal))
                {
                    throw new BackupValidationException("A Vault export entry does not match its manifest.");
                }
            }

            return manifest;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or IOException and not FileNotFoundException)
        {
            throw new BackupValidationException("The Vault export is corrupt.", exception);
        }
    }

    private static string RequireDestination(MemoryStoreLocation location, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (!Path.IsPathFullyQualified(destinationPath)
            || !string.Equals(Path.GetExtension(destinationPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The Vault export needs an absolute .zip path.", nameof(destinationPath));
        }

        var destination = Path.GetFullPath(destinationPath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(location.RootPath)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (destination.StartsWith(root, comparison))
        {
            throw new ArgumentException("The Vault export must be written outside Prince's data root.", nameof(destinationPath));
        }

        if (File.Exists(destination))
        {
            throw new IOException("The Vault export destination already exists; choose a new file.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        return destination;
    }

    private static Entry Describe(string name, byte[] bytes) =>
        new(name, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));

    private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] bytes, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var stream = entry.Open();
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, long maximum, CancellationToken cancellationToken)
    {
        if (entry.Length < 0 || entry.Length > maximum)
        {
            throw new BackupValidationException("A Vault export entry is outside its bound.");
        }

        await using var stream = entry.Open();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.Length == entry.Length
            ? buffer.ToArray()
            : throw new BackupValidationException("A Vault export entry length does not match.");
    }
}
