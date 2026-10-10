using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CompanionCore.Keepsakes;
using CompanionCore.Memory;

namespace CompanionCore.Vault;

internal sealed record CompanionEntry(string Name, long Length, string Sha256);

internal sealed record CompanionManifest(int FormatVersion, Guid BackupId, DateTimeOffset CreatedAtUtc, IReadOnlyList<CompanionEntry> Entries)
{
    internal bool SameAs(CompanionManifest other) =>
        FormatVersion == other.FormatVersion
        && BackupId == other.BackupId
        && CreatedAtUtc == other.CreatedAtUtc
        && Entries.SequenceEqual(other.Entries);
}

/// <summary>A validated companion archive extracted into a task-owned directory.</summary>
internal sealed class ValidatedCompanion : IDisposable
{
    private readonly string _parent;

    internal ValidatedCompanion(CompanionManifest manifest, string parent, string directory)
    {
        Manifest = manifest;
        _parent = parent;
        Directory = directory;
    }

    internal CompanionManifest Manifest { get; }

    internal string Directory { get; }

    internal string? PathFor(string entryName) =>
        Manifest.Entries.Any(entry => entry.Name == entryName) ? Path.Combine(Directory, entryName) : null;

    internal CompanionEntry? Entry(string entryName) => Manifest.Entries.SingleOrDefault(entry => entry.Name == entryName);

    public void Dispose() => CompanionArchive.TryDeleteOwnedDirectory(_parent, Directory);
}

/// <summary>
/// Strict companion archive: a canonical manifest and its checksum, plus exactly the declared
/// keepsake and state entries, each length-bounded and digest-verified on extraction.
/// </summary>
internal static partial class CompanionArchive
{
    internal const int FormatVersion = 1;
    internal const string ManifestName = "companion-manifest-v1.json";
    internal const string ChecksumName = "companion-manifest-v1.sha256";
    internal const int MaximumDataEntries = 10_000;
    internal const long MaximumEntryBytes = 64L * 1024 * 1024;
    internal const long MaximumArchiveBytes = 8L * 1024 * 1024 * 1024;
    internal const int MaximumManifestBytes = 4 * 1024 * 1024;

    internal static string KeepsakeEntryName(Guid actionId) => $"keepsake-{actionId:N}.png";

    internal static string StateEntryName(string name) => $"state-{name}{VaultStateStore.Extension}";

    internal static bool ValidEntryName(string name) =>
        KeepsakeEntryPattern().IsMatch(name)
        || (name.StartsWith("state-", StringComparison.Ordinal)
            && name.EndsWith(VaultStateStore.Extension, StringComparison.Ordinal)
            && VaultStateStore.ValidName(name["state-".Length..^VaultStateStore.Extension.Length]));

    internal static async Task<CompanionManifest> BuildAsync(
        Guid backupId,
        DateTimeOffset createdAtUtc,
        KeepsakeLocation keepsakes,
        VaultStateStore state,
        string candidatePath,
        CancellationToken cancellationToken)
    {
        var sources = new List<(string Name, string Path)>();
        if (System.IO.Directory.Exists(keepsakes.RootPath))
        {
            foreach (var path in System.IO.Directory.EnumerateFiles(keepsakes.RootPath))
            {
                var file = Path.GetFileName(path);
                if (file.EndsWith(".png", StringComparison.Ordinal)
                    && Guid.TryParseExact(file[..^4], "N", out var actionId)
                    && file == KeepsakeRecords.FileName(actionId))
                {
                    sources.Add((KeepsakeEntryName(actionId), path));
                }
            }
        }

        foreach (var name in state.Names())
        {
            // A damaged state entry fails the backup so the previous Vault keeps its good copy.
            if ((await state.GetAsync(name, cancellationToken).ConfigureAwait(false)).Status != StateStatus.Verified)
            {
                throw new BackupValidationException($"State entry '{name}' is damaged; the previous Vault is kept.");
            }

            sources.Add((StateEntryName(name), state.PathFor(name)));
        }

        if (sources.Count > MaximumDataEntries)
        {
            throw new BackupValidationException("The companion archive has too many entries.");
        }

        var entries = new List<CompanionEntry>();
        var stamp = createdAtUtc.ToUniversalTime();
        await using (var archiveStream = new FileStream(candidatePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, path) in sources.OrderBy(source => source.Name, StringComparer.Ordinal))
                {
                    if (new FileInfo(path).Length > MaximumEntryBytes)
                    {
                        throw new BackupValidationException("A companion entry exceeds its bound.");
                    }

                    var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);

                    entries.Add(new CompanionEntry(name, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes))));
                    await AddAsync(archive, name, bytes, stamp, cancellationToken).ConfigureAwait(false);
                }

                var manifest = new CompanionManifest(FormatVersion, backupId, stamp, entries);
                var manifestBytes = Serialize(manifest);
                await AddAsync(archive, ManifestName, manifestBytes, stamp, cancellationToken).ConfigureAwait(false);
                await AddAsync(archive, ChecksumName, Encoding.ASCII.GetBytes(Convert.ToHexStringLower(SHA256.HashData(manifestBytes))), stamp, cancellationToken).ConfigureAwait(false);
            }

            await archiveStream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            archiveStream.Flush(flushToDisk: true);
        }

        return new CompanionManifest(FormatVersion, backupId, stamp, entries);
    }

    internal static async Task<ValidatedCompanion> ValidateAsync(
        string archivePath,
        string validationParent,
        Guid? expectedBackupId,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(archivePath))
        {
            throw new BackupValidationException("The companion archive does not exist.");
        }

        var length = new FileInfo(archivePath).Length;
        if (length <= 0 || length > MaximumArchiveBytes)
        {
            throw new BackupValidationException("The companion archive length is outside its bound.");
        }

        System.IO.Directory.CreateDirectory(validationParent);
        var directory = Path.Combine(validationParent, Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            CompanionManifest manifest;
            await using (var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous))
            using (var archive = OpenArchive(stream))
            {
                if (archive.Entries.Count > MaximumDataEntries + 2)
                {
                    throw new BackupValidationException("The companion archive has too many entries.");
                }

                var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName != entry.Name
                        || entry.FullName.Contains('/') || entry.FullName.Contains('\\')
                        || !entries.TryAdd(entry.FullName, entry))
                    {
                        throw new BackupValidationException("The companion archive contains a duplicate, directory, or traversal entry.");
                    }
                }

                if (!entries.TryGetValue(ManifestName, out var manifestEntry) || !entries.TryGetValue(ChecksumName, out var checksumEntry))
                {
                    throw new BackupValidationException("The companion manifest or checksum is missing.");
                }

                var manifestBytes = await ReadBoundedAsync(manifestEntry, MaximumManifestBytes, cancellationToken).ConfigureAwait(false);
                var checksum = await ReadBoundedAsync(checksumEntry, 64, cancellationToken).ConfigureAwait(false);
                var actual = Encoding.ASCII.GetBytes(Convert.ToHexStringLower(SHA256.HashData(manifestBytes)));
                if (checksum.Length != 64 || !CryptographicOperations.FixedTimeEquals(checksum, actual))
                {
                    throw new BackupValidationException("The companion manifest checksum is invalid.");
                }

                manifest = Parse(manifestBytes);
                if (expectedBackupId is { } expected && manifest.BackupId != expected)
                {
                    throw new BackupValidationException("The companion archive belongs to a different backup.");
                }

                var declared = manifest.Entries.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);
                var present = entries.Keys.Where(name => name is not ManifestName and not ChecksumName).ToHashSet(StringComparer.Ordinal);
                if (!declared.SetEquals(present))
                {
                    throw new BackupValidationException("The companion entries are not exactly the declared set.");
                }

                foreach (var declaredEntry in manifest.Entries)
                {
                    var bytes = await ReadBoundedAsync(entries[declaredEntry.Name], declaredEntry.Length, cancellationToken, allowEmpty: true).ConfigureAwait(false);
                    if (bytes.Length != declaredEntry.Length
                        || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), declaredEntry.Sha256, StringComparison.Ordinal))
                    {
                        throw new BackupValidationException($"Companion entry '{declaredEntry.Name}' failed verification.");
                    }

                    if (declaredEntry.Name.StartsWith("state-", StringComparison.Ordinal) && !VaultStateStore.TryDecode(bytes, out _))
                    {
                        throw new BackupValidationException($"Companion state entry '{declaredEntry.Name}' is not a valid state envelope.");
                    }

                    await File.WriteAllBytesAsync(Path.Combine(directory, declaredEntry.Name), bytes, cancellationToken).ConfigureAwait(false);
                }
            }

            return new ValidatedCompanion(manifest, validationParent, directory);
        }
        catch (InvalidDataException exception)
        {
            TryDeleteOwnedDirectory(validationParent, directory);
            throw new BackupValidationException("The companion archive is corrupt.", exception);
        }
        catch
        {
            TryDeleteOwnedDirectory(validationParent, directory);
            throw;
        }
    }

    internal static byte[] Serialize(CompanionManifest manifest)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", manifest.FormatVersion);
            writer.WriteString("backupId", manifest.BackupId.ToString("D"));
            writer.WriteString("createdAtUtc", manifest.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteStartArray("entries");
            foreach (var entry in manifest.Entries)
            {
                writer.WriteStartObject();
                writer.WriteString("name", entry.Name);
                writer.WriteNumber("length", entry.Length);
                writer.WriteString("sha256", entry.Sha256);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    internal static CompanionManifest Parse(byte[] bytes)
    {
        CompanionManifest manifest;
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            var entries = root.GetProperty("entries").EnumerateArray().Select(entry => new CompanionEntry(
                entry.GetProperty("name").GetString()!,
                entry.GetProperty("length").GetInt64(),
                entry.GetProperty("sha256").GetString()!)).ToArray();
            manifest = new CompanionManifest(
                root.GetProperty("formatVersion").GetInt32(),
                Guid.ParseExact(root.GetProperty("backupId").GetString()!, "D"),
                DateTimeOffset.ParseExact(root.GetProperty("createdAtUtc").GetString()!, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                entries);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentNullException)
        {
            throw new BackupValidationException("The companion manifest is malformed.", exception);
        }

        var names = manifest.Entries.Select(entry => entry.Name).ToArray();
        var valid = manifest.FormatVersion == FormatVersion
            && manifest.BackupId != Guid.Empty
            && manifest.CreatedAtUtc.Offset == TimeSpan.Zero
            && manifest.Entries.Count <= MaximumDataEntries
            && manifest.Entries.All(entry => ValidEntryName(entry.Name) && entry.Length is >= 0 and <= MaximumEntryBytes && KeepsakeMetadata.IsSha256(entry.Sha256))
            && names.SequenceEqual(names.Order(StringComparer.Ordinal))
            && names.Distinct(StringComparer.Ordinal).Count() == names.Length;
        if (!valid || !bytes.AsSpan().SequenceEqual(Serialize(manifest)))
        {
            throw new BackupValidationException("The companion manifest is invalid or not canonical.");
        }

        return manifest;
    }

    internal static void TryDeleteOwnedDirectory(string parent, string directory)
    {
        try
        {
            var full = Path.GetFullPath(directory);
            if (string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(parent), StringComparison.Ordinal) && System.IO.Directory.Exists(full))
            {
                System.IO.Directory.Delete(full, recursive: true);
            }
        }
        catch (IOException)
        {
            // Validation scratch only; a later attempt may clean it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }

    private static ZipArchive OpenArchive(Stream stream)
    {
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException exception)
        {
            throw new BackupValidationException("The companion archive is not a valid zip archive.", exception);
        }
    }

    private static async Task AddAsync(ZipArchive archive, string name, byte[] bytes, DateTimeOffset stamp, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = stamp;
        await using var stream = entry.Open();
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadBoundedAsync(ZipArchiveEntry entry, long maximumBytes, CancellationToken cancellationToken, bool allowEmpty = false)
    {
        if (entry.Length > maximumBytes || (!allowEmpty && entry.Length <= 0))
        {
            throw new BackupValidationException("A companion entry has an invalid length.");
        }

        using var buffer = new MemoryStream();
        await using (var stream = entry.Open())
        {
            var chunk = new byte[64 * 1024];
            long total = 0;
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > maximumBytes)
                {
                    throw new BackupValidationException("A companion entry expanded beyond its bound.");
                }

                buffer.Write(chunk, 0, read);
            }
        }

        return buffer.ToArray();
    }

    [GeneratedRegex("^keepsake-[0-9a-f]{32}\\.png$", RegexOptions.CultureInvariant)]
    private static partial Regex KeepsakeEntryPattern();
}
