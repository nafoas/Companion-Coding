using System.IO.Compression;
using System.Security.Cryptography;

namespace CompanionCore.Memory;

internal static class MemoryBackupArchiveValidator
{
    internal static async Task<ValidatedMemoryBackup> ValidateAsync(
        MemoryStoreLocation location,
        string archivePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        archivePath = MemoryPathGuard.RequireImmediateChild(location.BackupDirectoryPath, archivePath);
        if (!File.Exists(archivePath))
        {
            throw new BackupValidationException("The backup archive does not exist.");
        }

        var archiveLength = new FileInfo(archivePath).Length;
        if (archiveLength <= 0 || archiveLength > MemoryBackupFormat.MaximumArchiveBytes)
        {
            throw new BackupValidationException("The backup archive length is outside its bound.");
        }

        Directory.CreateDirectory(location.BackupValidationDirectoryPath);
        var validationDirectory = Path.Combine(
            location.BackupValidationDirectoryPath,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(validationDirectory);
        var extractedDatabasePath = Path.Combine(
            validationDirectory,
            MemoryBackupFormat.DatabaseEntryName);

        try
        {
            MemoryBackupManifest manifest;
            await using (var archiveStream = new FileStream(
                             archivePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             bufferSize: 64 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false))
            {
                if (archive.Entries.Count != MemoryBackupFormat.ExactEntryCount)
                {
                    throw new BackupValidationException("The backup archive entry count is invalid.");
                }

                var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
                foreach (var entry in archive.Entries)
                {
                    if (!entries.TryAdd(entry.FullName, entry)
                        || !string.Equals(entry.FullName, entry.Name, StringComparison.Ordinal)
                        || entry.FullName.Contains('/')
                        || entry.FullName.Contains('\\'))
                    {
                        throw new BackupValidationException(
                            "The backup archive contains a duplicate, directory, or traversal entry.");
                    }
                }

                var expectedNames = new[]
                {
                    MemoryBackupFormat.DatabaseEntryName,
                    MemoryBackupFormat.ManifestEntryName,
                    MemoryBackupFormat.ManifestChecksumEntryName,
                };
                if (entries.Count != expectedNames.Length
                    || expectedNames.Any(expected => !entries.ContainsKey(expected)))
                {
                    throw new BackupValidationException("The backup archive entries are not exact.");
                }

                var manifestBytes = await ReadBoundedEntryAsync(
                        entries[MemoryBackupFormat.ManifestEntryName],
                        MemoryBackupFormat.MaximumManifestBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
                var checksumBytes = await ReadBoundedEntryAsync(
                        entries[MemoryBackupFormat.ManifestChecksumEntryName],
                        maximumBytes: 64,
                        cancellationToken)
                    .ConfigureAwait(false);
                var storedManifestDigest = MemoryBackupManifestCodec.DecodeChecksum(checksumBytes);
                var actualManifestDigest = MemoryBackupManifestCodec.ComputeSha256Hex(manifestBytes);
                if (!CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(storedManifestDigest),
                        Convert.FromHexString(actualManifestDigest)))
                {
                    throw new BackupValidationException("The backup manifest checksum is invalid.");
                }

                manifest = MemoryBackupManifestCodec.ParseCanonical(manifestBytes);
                var databaseEntry = entries[MemoryBackupFormat.DatabaseEntryName];
                if (databaseEntry.Length != manifest.DatabaseByteLength
                    || databaseEntry.Length <= 0
                    || databaseEntry.Length > MemoryBackupFormat.MaximumDatabaseBytes)
                {
                    throw new BackupValidationException("The backup database entry length is invalid.");
                }

                await ExtractDatabaseAsync(
                        databaseEntry,
                        extractedDatabasePath,
                        manifest,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var snapshotLocation = new MemoryStoreLocation(
                location.Kind,
                location.ApplicationNamespace,
                validationDirectory,
                MemoryBackupFormat.DatabaseEntryName);
            await using (var snapshotStore = await MemoryStore.OpenExistingAsync(
                             snapshotLocation,
                             cancellationToken)
                         .ConfigureAwait(false))
            {
                _ = await snapshotStore.ValidateFullHealthAsync(
                        manifest.CutSequence,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return new ValidatedMemoryBackup(
                manifest,
                extractedDatabasePath,
                location.BackupValidationDirectoryPath,
                validationDirectory);
        }
        catch
        {
            MemoryPathGuard.TryDeleteTaskOwnedDirectory(
                location.BackupValidationDirectoryPath,
                validationDirectory);
            throw;
        }
    }

    private static async Task<byte[]> ReadBoundedEntryAsync(
        ZipArchiveEntry entry,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (entry.Length <= 0 || entry.Length > maximumBytes)
        {
            throw new BackupValidationException("A bounded backup metadata entry has an invalid length.");
        }

        var bytes = new byte[checked((int)entry.Length)];
        await using var stream = entry.Open();
        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new BackupValidationException("A backup metadata entry ended early.");
            }

            offset += read;
        }

        if (await stream.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false) != 0)
        {
            throw new BackupValidationException("A backup metadata entry exceeded its declared length.");
        }

        return bytes;
    }

    private static async Task ExtractDatabaseAsync(
        ZipArchiveEntry entry,
        string destinationPath,
        MemoryBackupManifest manifest,
        CancellationToken cancellationToken)
    {
        await using (var source = entry.Open())
        await using (var destination = new FileStream(
                         destinationPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 64 * 1024,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            var buffer = new byte[64 * 1024];
            long total = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total = checked(total + read);
                if (total > manifest.DatabaseByteLength
                    || total > MemoryBackupFormat.MaximumDatabaseBytes)
                {
                    throw new BackupValidationException("The backup database expanded beyond its bound.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }

            await destination.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            destination.Flush(flushToDisk: true);
            if (total != manifest.DatabaseByteLength)
            {
                throw new BackupValidationException("The extracted database length is invalid.");
            }
        }

        await using var extracted = new FileStream(
            destinationPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var digest = Convert.ToHexString(
                await SHA256.HashDataAsync(extracted, cancellationToken).ConfigureAwait(false))
            .ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(digest),
                Convert.FromHexString(manifest.DatabaseSha256)))
        {
            throw new BackupValidationException("The extracted database checksum is invalid.");
        }
    }
}
