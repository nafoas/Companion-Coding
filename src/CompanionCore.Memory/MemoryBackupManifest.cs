using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CompanionCore.Memory;

internal sealed record MemoryBackupManifest(
    int FormatVersion,
    int MemorySchemaVersion,
    Guid BackupId,
    DateTimeOffset CreatedAtUtc,
    long CutSequence,
    string DatabaseEntryName,
    long DatabaseByteLength,
    string DatabaseSha256);

internal static class MemoryBackupManifestCodec
{
    private static readonly string[] ExactPropertyNames =
    [
        "formatVersion",
        "memorySchemaVersion",
        "backupId",
        "createdAtUtc",
        "cutSequence",
        "databaseEntryName",
        "databaseByteLength",
        "databaseSha256",
    ];

    internal static byte[] Serialize(MemoryBackupManifest manifest)
    {
        ValidateValues(manifest);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               {
                   Indented = false,
                   SkipValidation = false,
               }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", manifest.FormatVersion);
            writer.WriteNumber("memorySchemaVersion", manifest.MemorySchemaVersion);
            writer.WriteString("backupId", manifest.BackupId.ToString("D"));
            writer.WriteString(
                "createdAtUtc",
                manifest.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteNumber("cutSequence", manifest.CutSequence);
            writer.WriteString("databaseEntryName", manifest.DatabaseEntryName);
            writer.WriteNumber("databaseByteLength", manifest.DatabaseByteLength);
            writer.WriteString("databaseSha256", manifest.DatabaseSha256);
            writer.WriteEndObject();
        }

        var bytes = stream.ToArray();
        if (bytes.Length > MemoryBackupFormat.MaximumManifestBytes)
        {
            throw new BackupValidationException("The canonical backup manifest exceeds its bound.");
        }

        return bytes;
    }

    internal static MemoryBackupManifest ParseCanonical(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Length > MemoryBackupFormat.MaximumManifestBytes)
        {
            throw new BackupValidationException("The backup manifest length is invalid.");
        }

        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new BackupValidationException("The backup manifest root must be an object.");
            }

            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != ExactPropertyNames.Length
                || properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count()
                    != properties.Length
                || !properties.Select(property => property.Name)
                    .SequenceEqual(ExactPropertyNames, StringComparer.Ordinal))
            {
                throw new BackupValidationException(
                    "The backup manifest properties are missing, duplicated, extra, or non-canonical.");
            }

            var backupIdText = properties[2].Value.GetString();
            var createdAtText = properties[3].Value.GetString();
            var manifest = new MemoryBackupManifest(
                properties[0].Value.GetInt32(),
                properties[1].Value.GetInt32(),
                Guid.TryParseExact(backupIdText, "D", out var backupId)
                    ? backupId
                    : throw new BackupValidationException("The backup ID is invalid."),
                DateTimeOffset.TryParseExact(
                    createdAtText,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var createdAtUtc)
                    ? createdAtUtc
                    : throw new BackupValidationException("The backup creation time is invalid."),
                properties[4].Value.GetInt64(),
                properties[5].Value.GetString()
                    ?? throw new BackupValidationException("The database entry name is missing."),
                properties[6].Value.GetInt64(),
                properties[7].Value.GetString()
                    ?? throw new BackupValidationException("The database digest is missing."));
            ValidateValues(manifest);

            var canonical = Serialize(manifest);
            if (!bytes.Span.SequenceEqual(canonical))
            {
                throw new BackupValidationException("The backup manifest is not in canonical form.");
            }

            return manifest;
        }
        catch (BackupValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or FormatException or InvalidOperationException or OverflowException)
        {
            throw new BackupValidationException("The backup manifest is malformed.", exception);
        }
    }

    internal static string ComputeSha256Hex(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static byte[] EncodeChecksum(string lowercaseHexDigest)
    {
        ValidateSha256(lowercaseHexDigest, "manifest");
        return Encoding.ASCII.GetBytes(lowercaseHexDigest);
    }

    internal static string DecodeChecksum(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 64)
        {
            throw new BackupValidationException("The manifest checksum entry has an invalid length or encoding.");
        }

        foreach (var value in bytes)
        {
            if (value > 0x7f)
            {
                throw new BackupValidationException(
                    "The manifest checksum entry has an invalid length or encoding.");
            }
        }

        var digest = Encoding.ASCII.GetString(bytes);
        ValidateSha256(digest, "manifest");
        return digest;
    }

    private static void ValidateValues(MemoryBackupManifest manifest)
    {
        if (manifest.FormatVersion != MemoryBackupFormat.FormatVersion)
        {
            throw new BackupValidationException("The backup format version is unsupported.");
        }

        if (manifest.MemorySchemaVersion != MemorySchema.CurrentVersion)
        {
            throw new BackupValidationException("The backup memory schema version is unsupported.");
        }

        if (manifest.BackupId == Guid.Empty)
        {
            throw new BackupValidationException("The backup ID must not be empty.");
        }

        if (manifest.CreatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new BackupValidationException("The backup creation time must be UTC.");
        }

        if (manifest.CutSequence < 0)
        {
            throw new BackupValidationException("The backup cut sequence is invalid.");
        }

        if (!string.Equals(
                manifest.DatabaseEntryName,
                MemoryBackupFormat.DatabaseEntryName,
                StringComparison.Ordinal))
        {
            throw new BackupValidationException("The backup database entry name is invalid.");
        }

        if (manifest.DatabaseByteLength <= 0
            || manifest.DatabaseByteLength > MemoryBackupFormat.MaximumDatabaseBytes)
        {
            throw new BackupValidationException("The backup database length is outside its bound.");
        }

        ValidateSha256(manifest.DatabaseSha256, "database");
    }

    private static void ValidateSha256(string digest, string subject)
    {
        if (digest.Length != 64
            || digest.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new BackupValidationException(
                $"The {subject} SHA-256 digest is not canonical lowercase hexadecimal.");
        }
    }
}
