using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CompanionCore.Memory;

internal sealed record PreservedFile(string Name, long ByteLength, string Sha256);

internal sealed record DamagedSourceManifest(
    int FormatVersion,
    Guid RepairId,
    Guid BackupId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<PreservedFile> Files);

internal sealed class DamagedSourcePreservation
{
    private const int FormatVersion = 1;
    private const int MaximumManifestBytes = 32 * 1024;
    private const string ManifestFileName = "preservation-manifest-v1.json";
    private const string ManifestChecksumFileName = "preservation-manifest-v1.sha256";

    private DamagedSourcePreservation(
        string directoryPath,
        DamagedSourceManifest manifest)
    {
        DirectoryPath = directoryPath;
        Manifest = manifest;
    }

    internal string DirectoryPath { get; }

    internal DamagedSourceManifest Manifest { get; }

    internal static async Task<DamagedSourcePreservation> CreateAsync(
        MemoryStoreLocation location,
        Guid repairId,
        Guid backupId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (repairId == Guid.Empty || backupId == Guid.Empty)
        {
            throw new ArgumentException("Preservation requires non-empty repair and backup IDs.");
        }

        if (!File.Exists(location.DatabasePath) || !File.Exists(location.JournalPath))
        {
            throw new MemoryIntegrityException(
                "Repair requires both the damaged database and its complete live journal.");
        }

        Directory.CreateDirectory(location.DamagedPreservationDirectoryPath);
        var directory = Path.Combine(
            location.DamagedPreservationDirectoryPath,
            repairId.ToString("N"));
        if (Directory.Exists(directory))
        {
            throw new MemoryIntegrityException(
                "A damaged-source preservation bundle already uses this repair identifier.");
        }

        Directory.CreateDirectory(directory);

        try
        {
            var sourcePaths = new[]
                {
                    location.DatabasePath,
                    location.DatabasePath + "-wal",
                    location.DatabasePath + "-shm",
                    location.JournalPath,
                }
                .Where(File.Exists)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToArray();

            var files = new List<PreservedFile>(sourcePaths.Length);
            foreach (var sourcePath in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(sourcePath);
                var destinationPath = Path.Combine(directory, name);
                var (length, digest) = await CopyAndHashAsync(
                        sourcePath,
                        destinationPath,
                        cancellationToken)
                    .ConfigureAwait(false);
                files.Add(new PreservedFile(name, length, digest));
            }

            var manifest = new DamagedSourceManifest(
                FormatVersion,
                repairId,
                backupId,
                DateTimeOffset.UtcNow,
                files);
            var manifestBytes = Serialize(manifest);
            var manifestDigest = MemoryBackupManifestCodec.ComputeSha256Hex(manifestBytes);
            await WriteDurablyAsync(
                    Path.Combine(directory, ManifestFileName),
                    manifestBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            await WriteDurablyAsync(
                    Path.Combine(directory, ManifestChecksumFileName),
                    MemoryBackupManifestCodec.EncodeChecksum(manifestDigest),
                    cancellationToken)
                .ConfigureAwait(false);

            return await OpenAndValidateAsync(location, repairId.ToString("N"), cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            MemoryPathGuard.TryDeleteTaskOwnedDirectory(
                location.DamagedPreservationDirectoryPath,
                directory);
            throw;
        }
    }

    internal static async Task<DamagedSourcePreservation> OpenAndValidateAsync(
        MemoryStoreLocation location,
        string directoryName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (!string.Equals(Path.GetFileName(directoryName), directoryName, StringComparison.Ordinal))
        {
            throw new MemoryIntegrityException("The preservation directory name is invalid.");
        }

        var directory = MemoryPathGuard.RequireImmediateChild(
            location.DamagedPreservationDirectoryPath,
            Path.Combine(location.DamagedPreservationDirectoryPath, directoryName));
        var manifestPath = Path.Combine(directory, ManifestFileName);
        var checksumPath = Path.Combine(directory, ManifestChecksumFileName);
        if (!File.Exists(manifestPath) || !File.Exists(checksumPath))
        {
            throw new MemoryIntegrityException("The damaged-source preservation manifest is incomplete.");
        }

        var manifestBytes = await ReadBoundedAsync(
                manifestPath,
                MaximumManifestBytes,
                cancellationToken)
            .ConfigureAwait(false);
        var checksumBytes = await ReadBoundedAsync(checksumPath, 64, cancellationToken)
            .ConfigureAwait(false);
        var storedDigest = MemoryBackupManifestCodec.DecodeChecksum(checksumBytes);
        var actualDigest = MemoryBackupManifestCodec.ComputeSha256Hex(manifestBytes);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(storedDigest),
                Convert.FromHexString(actualDigest)))
        {
            throw new MemoryIntegrityException("The preservation manifest checksum is invalid.");
        }

        var manifest = ParseCanonical(manifestBytes);
        if (!string.Equals(manifest.RepairId.ToString("N"), directoryName, StringComparison.Ordinal))
        {
            throw new MemoryIntegrityException(
                "The preservation directory does not match its repair identifier.");
        }

        var allowedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.GetFileName(location.DatabasePath),
            Path.GetFileName(location.DatabasePath) + "-wal",
            Path.GetFileName(location.DatabasePath) + "-shm",
            Path.GetFileName(location.JournalPath),
        };
        if (manifest.Files.Count is < 2 or > 4
            || manifest.Files.Select(file => file.Name).Distinct(StringComparer.Ordinal).Count()
                != manifest.Files.Count
            || manifest.Files.Any(file => !allowedNames.Contains(file.Name))
            || !manifest.Files.Any(file => string.Equals(
                file.Name,
                Path.GetFileName(location.DatabasePath),
                StringComparison.Ordinal))
            || !manifest.Files.Any(file => string.Equals(
                file.Name,
                Path.GetFileName(location.JournalPath),
                StringComparison.Ordinal)))
        {
            throw new MemoryIntegrityException("The preservation file set is invalid.");
        }

        var expectedDirectoryEntries = manifest.Files
            .Select(file => file.Name)
            .Append(ManifestFileName)
            .Append(ManifestChecksumFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var actualDirectoryEntries = Directory.EnumerateFileSystemEntries(directory)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actualDirectoryEntries.SequenceEqual(
                expectedDirectoryEntries,
                StringComparer.Ordinal))
        {
            throw new MemoryIntegrityException(
                "The preservation directory contains an unexpected or missing entry.");
        }

        foreach (var file in manifest.Files)
        {
            var path = Path.Combine(directory, file.Name);
            if (!File.Exists(path) || new FileInfo(path).Length != file.ByteLength)
            {
                throw new MemoryIntegrityException("A preserved damaged-source file is missing or truncated.");
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var digest = Convert.ToHexString(
                    await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false))
                .ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(digest),
                    Convert.FromHexString(file.Sha256)))
            {
                throw new MemoryIntegrityException("A preserved damaged-source checksum is invalid.");
            }
        }

        return new DamagedSourcePreservation(directory, manifest);
    }

    private static byte[] Serialize(DamagedSourceManifest manifest)
    {
        ValidateManifest(manifest);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", manifest.FormatVersion);
            writer.WriteString("repairId", manifest.RepairId.ToString("D"));
            writer.WriteString("backupId", manifest.BackupId.ToString("D"));
            writer.WriteString(
                "createdAtUtc",
                manifest.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteStartArray("files");
            foreach (var file in manifest.Files.OrderBy(file => file.Name, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("name", file.Name);
                writer.WriteNumber("byteLength", file.ByteLength);
                writer.WriteString("sha256", file.Sha256);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var bytes = stream.ToArray();
        if (bytes.Length > MaximumManifestBytes)
        {
            throw new MemoryIntegrityException("The preservation manifest exceeds its bound.");
        }

        return bytes;
    }

    private static DamagedSourceManifest ParseCanonical(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });
            var root = document.RootElement;
            var properties = root.EnumerateObject().ToArray();
            var exactProperties = new[]
            {
                "formatVersion", "repairId", "backupId", "createdAtUtc", "files",
            };
            if (root.ValueKind != JsonValueKind.Object
                || properties.Length != exactProperties.Length
                || !properties.Select(property => property.Name)
                    .SequenceEqual(exactProperties, StringComparer.Ordinal)
                || properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count()
                    != properties.Length
                || properties[4].Value.ValueKind != JsonValueKind.Array)
            {
                throw new MemoryIntegrityException("The preservation manifest shape is invalid.");
            }

            var files = new List<PreservedFile>();
            foreach (var element in properties[4].Value.EnumerateArray())
            {
                var fileProperties = element.EnumerateObject().ToArray();
                if (element.ValueKind != JsonValueKind.Object
                    || fileProperties.Length != 3
                    || !fileProperties.Select(property => property.Name)
                        .SequenceEqual(new[] { "name", "byteLength", "sha256" }, StringComparer.Ordinal)
                    || fileProperties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count()
                        != fileProperties.Length)
                {
                    throw new MemoryIntegrityException("A preservation file entry is invalid.");
                }

                files.Add(new PreservedFile(
                    fileProperties[0].Value.GetString()
                        ?? throw new MemoryIntegrityException("A preservation filename is missing."),
                    fileProperties[1].Value.GetInt64(),
                    fileProperties[2].Value.GetString()
                        ?? throw new MemoryIntegrityException("A preservation checksum is missing.")));
            }

            var repairIdText = properties[1].Value.GetString();
            var backupIdText = properties[2].Value.GetString();
            var createdAtText = properties[3].Value.GetString();
            var manifest = new DamagedSourceManifest(
                properties[0].Value.GetInt32(),
                Guid.TryParseExact(repairIdText, "D", out var repairId)
                    ? repairId
                    : throw new MemoryIntegrityException("The preservation repair ID is invalid."),
                Guid.TryParseExact(backupIdText, "D", out var backupId)
                    ? backupId
                    : throw new MemoryIntegrityException("The preservation backup ID is invalid."),
                DateTimeOffset.TryParseExact(
                    createdAtText,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var createdAtUtc)
                    ? createdAtUtc
                    : throw new MemoryIntegrityException("The preservation time is invalid."),
                files);
            ValidateManifest(manifest);
            if (!bytes.Span.SequenceEqual(Serialize(manifest)))
            {
                throw new MemoryIntegrityException("The preservation manifest is not canonical.");
            }

            return manifest;
        }
        catch (MemoryIntegrityException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or FormatException or InvalidOperationException or OverflowException)
        {
            throw new MemoryIntegrityException(
                $"The preservation manifest is malformed: {exception.Message}");
        }
    }

    private static void ValidateManifest(DamagedSourceManifest manifest)
    {
        if (manifest.FormatVersion != FormatVersion
            || manifest.RepairId == Guid.Empty
            || manifest.BackupId == Guid.Empty
            || manifest.CreatedAtUtc.Offset != TimeSpan.Zero
            || manifest.Files.Count is < 2 or > 4)
        {
            throw new MemoryIntegrityException("The preservation manifest values are invalid.");
        }

        string? priorName = null;
        foreach (var file in manifest.Files.OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(file.Name)
                || !string.Equals(Path.GetFileName(file.Name), file.Name, StringComparison.Ordinal)
                || file.ByteLength < 0
                || file.Sha256.Length != 64
                || file.Sha256.Any(character =>
                    character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                || string.Equals(priorName, file.Name, StringComparison.Ordinal))
            {
                throw new MemoryIntegrityException("A preservation file value is invalid.");
            }

            priorName = file.Name;
        }
    }

    private static async Task<(long Length, string Digest)> CopyAndHashAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using (var source = new FileStream(
                         sourcePath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read,
                         bufferSize: 64 * 1024,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (var destination = new FileStream(
                         destinationPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 64 * 1024,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await source.CopyToAsync(destination, 64 * 1024, cancellationToken)
                .ConfigureAwait(false);
            await destination.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            destination.Flush(flushToDisk: true);
        }

        var length = new FileInfo(destinationPath).Length;
        await using var copied = new FileStream(
            destinationPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var digest = Convert.ToHexString(
                await SHA256.HashDataAsync(copied, cancellationToken).ConfigureAwait(false))
            .ToLowerInvariant();
        return (length, digest);
    }

    private static async Task WriteDurablyAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static async Task<byte[]> ReadBoundedAsync(
        string path,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        var length = new FileInfo(path).Length;
        if (length <= 0 || length > maximumBytes)
        {
            throw new MemoryIntegrityException("A preservation metadata file has an invalid length.");
        }

        return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }
}
