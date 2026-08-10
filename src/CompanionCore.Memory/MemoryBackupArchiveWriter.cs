using System.IO.Compression;
using System.Security.Cryptography;

namespace CompanionCore.Memory;

internal static class MemoryBackupArchiveWriter
{
    internal static async Task<MemoryBackupManifest> BuildAsync(
        string snapshotDatabasePath,
        string candidateArchivePath,
        Guid backupId,
        DateTimeOffset createdAtUtc,
        long cutSequence,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateArchivePath);
        if (!File.Exists(snapshotDatabasePath))
        {
            throw new BackupValidationException("The staged SQLite snapshot does not exist.");
        }

        var databaseLength = new FileInfo(snapshotDatabasePath).Length;
        if (databaseLength <= 0 || databaseLength > MemoryBackupFormat.MaximumDatabaseBytes)
        {
            throw new BackupValidationException("The staged SQLite snapshot length is outside its bound.");
        }

        string databaseDigest;
        await using (var database = new FileStream(
                         snapshotDatabasePath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read,
                         bufferSize: 64 * 1024,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            databaseDigest = Convert.ToHexString(
                    await SHA256.HashDataAsync(database, cancellationToken).ConfigureAwait(false))
                .ToLowerInvariant();
        }

        var manifest = new MemoryBackupManifest(
            MemoryBackupFormat.FormatVersion,
            MemorySchema.CurrentVersion,
            backupId,
            createdAtUtc.ToUniversalTime(),
            cutSequence,
            MemoryBackupFormat.DatabaseEntryName,
            databaseLength,
            databaseDigest);
        var manifestBytes = MemoryBackupManifestCodec.Serialize(manifest);
        var manifestChecksum = MemoryBackupManifestCodec.EncodeChecksum(
            MemoryBackupManifestCodec.ComputeSha256Hex(manifestBytes));

        await using var archiveStream = new FileStream(
            candidateArchivePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            await AddFileEntryAsync(
                    archive,
                    MemoryBackupFormat.DatabaseEntryName,
                    snapshotDatabasePath,
                    createdAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            await AddBytesEntryAsync(
                    archive,
                    MemoryBackupFormat.ManifestEntryName,
                    manifestBytes,
                    createdAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            await AddBytesEntryAsync(
                    archive,
                    MemoryBackupFormat.ManifestChecksumEntryName,
                    manifestChecksum,
                    createdAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await archiveStream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        archiveStream.Flush(flushToDisk: true);
        if (archiveStream.Length <= 0 || archiveStream.Length > MemoryBackupFormat.MaximumArchiveBytes)
        {
            throw new BackupValidationException("The staged backup archive length is outside its bound.");
        }

        return manifest;
    }

    private static async Task AddFileEntryAsync(
        ZipArchive archive,
        string entryName,
        string sourcePath,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = createdAtUtc;
        await using var entryStream = entry.Open();
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(entryStream, 64 * 1024, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddBytesEntryAsync(
        ZipArchive archive,
        string entryName,
        ReadOnlyMemory<byte> bytes,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = createdAtUtc;
        await using var entryStream = entry.Open();
        await entryStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }
}
