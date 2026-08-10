using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace CompanionCore.Memory;

internal sealed record RepairStateMarker(
    int FormatVersion,
    Guid RepairId,
    Guid BackupId,
    string PreservationDirectoryName,
    DateTimeOffset MutationStartedAtUtc);

internal static class RepairStateMarkerCodec
{
    private const int FormatVersion = 1;
    private const int HeaderLength = 4 + sizeof(int) + sizeof(int);
    private const int ChecksumLength = 32;
    private const int MaximumPayloadLength = 16 * 1024;
    private static readonly byte[] Magic = [(byte)'C', (byte)'C', (byte)'R', (byte)'M'];

    internal static async Task WriteAsync(
        MemoryStoreLocation location,
        RepairStateMarker marker,
        CancellationToken cancellationToken)
    {
        Validate(marker);
        var payload = Serialize(marker);
        var bytes = new byte[HeaderLength + payload.Length + ChecksumLength];
        Magic.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), FormatVersion);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), payload.Length);
        payload.CopyTo(bytes.AsSpan(HeaderLength));
        SHA256.HashData(bytes.AsSpan(0, HeaderLength + payload.Length))
            .CopyTo(bytes.AsSpan(HeaderLength + payload.Length));

        if (File.Exists(location.RepairMarkerTemporaryPath))
        {
            File.Delete(location.RepairMarkerTemporaryPath);
        }

        await using (var stream = new FileStream(
                         location.RepairMarkerTemporaryPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 4096,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(location.RepairMarkerPath))
        {
            throw new MemoryIntegrityException("A repair marker already exists.");
        }

        File.Move(location.RepairMarkerTemporaryPath, location.RepairMarkerPath);
    }

    internal static async Task<RepairStateMarker> ReadAsync(
        MemoryStoreLocation location,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(location.RepairMarkerPath))
        {
            throw new MemoryIntegrityException("The interrupted repair marker is missing.");
        }

        var fileLength = new FileInfo(location.RepairMarkerPath).Length;
        if (fileLength < HeaderLength + ChecksumLength
            || fileLength > HeaderLength + MaximumPayloadLength + ChecksumLength)
        {
            throw new MemoryIntegrityException("The repair marker length is invalid.");
        }

        var bytes = await File.ReadAllBytesAsync(location.RepairMarkerPath, cancellationToken)
            .ConfigureAwait(false);
        if (!bytes.AsSpan(0, 4).SequenceEqual(Magic)
            || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)) != FormatVersion)
        {
            throw new MemoryIntegrityException("The repair marker header is invalid.");
        }

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8));
        if (payloadLength <= 0
            || payloadLength > MaximumPayloadLength
            || bytes.Length != HeaderLength + payloadLength + ChecksumLength)
        {
            throw new MemoryIntegrityException("The repair marker payload length is invalid.");
        }

        var actualChecksum = SHA256.HashData(bytes.AsSpan(0, HeaderLength + payloadLength));
        if (!CryptographicOperations.FixedTimeEquals(
                actualChecksum,
                bytes.AsSpan(HeaderLength + payloadLength, ChecksumLength)))
        {
            throw new MemoryIntegrityException("The repair marker checksum is invalid.");
        }

        return ParseCanonical(bytes.AsMemory(HeaderLength, payloadLength));
    }

    internal static void Delete(MemoryStoreLocation location)
    {
        if (File.Exists(location.RepairMarkerTemporaryPath))
        {
            File.Delete(location.RepairMarkerTemporaryPath);
        }

        // The authoritative marker is removed last. If temporary cleanup fails,
        // ordinary startup remains safely blocked and rollback can still retry.
        if (File.Exists(location.RepairMarkerPath))
        {
            File.Delete(location.RepairMarkerPath);
        }
    }

    private static byte[] Serialize(RepairStateMarker marker)
    {
        Validate(marker);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", marker.FormatVersion);
            writer.WriteString("repairId", marker.RepairId.ToString("D"));
            writer.WriteString("backupId", marker.BackupId.ToString("D"));
            writer.WriteString("preservationDirectoryName", marker.PreservationDirectoryName);
            writer.WriteString(
                "mutationStartedAtUtc",
                marker.MutationStartedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static RepairStateMarker ParseCanonical(ReadOnlyMemory<byte> payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 4,
            });
            var properties = document.RootElement.EnumerateObject().ToArray();
            var names = new[]
            {
                "formatVersion",
                "repairId",
                "backupId",
                "preservationDirectoryName",
                "mutationStartedAtUtc",
            };
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || properties.Length != names.Length
                || !properties.Select(property => property.Name)
                    .SequenceEqual(names, StringComparer.Ordinal)
                || properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count()
                    != properties.Length)
            {
                throw new MemoryIntegrityException("The repair marker shape is invalid.");
            }

            var repairText = properties[1].Value.GetString();
            var backupText = properties[2].Value.GetString();
            var timeText = properties[4].Value.GetString();
            var marker = new RepairStateMarker(
                properties[0].Value.GetInt32(),
                Guid.TryParseExact(repairText, "D", out var repairId)
                    ? repairId
                    : throw new MemoryIntegrityException("The repair marker ID is invalid."),
                Guid.TryParseExact(backupText, "D", out var backupId)
                    ? backupId
                    : throw new MemoryIntegrityException("The repair marker backup ID is invalid."),
                properties[3].Value.GetString()
                    ?? throw new MemoryIntegrityException(
                        "The repair marker preservation directory is missing."),
                DateTimeOffset.TryParseExact(
                    timeText,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var mutationStartedAtUtc)
                    ? mutationStartedAtUtc
                    : throw new MemoryIntegrityException("The repair marker time is invalid."));
            Validate(marker);
            if (!payload.Span.SequenceEqual(Serialize(marker)))
            {
                throw new MemoryIntegrityException("The repair marker is not canonical.");
            }

            return marker;
        }
        catch (MemoryIntegrityException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or FormatException or InvalidOperationException or OverflowException)
        {
            throw new MemoryIntegrityException($"The repair marker is malformed: {exception.Message}");
        }
    }

    private static void Validate(RepairStateMarker marker)
    {
        if (marker.FormatVersion != FormatVersion
            || marker.RepairId == Guid.Empty
            || marker.BackupId == Guid.Empty
            || marker.MutationStartedAtUtc.Offset != TimeSpan.Zero
            || !string.Equals(
                Path.GetFileName(marker.PreservationDirectoryName),
                marker.PreservationDirectoryName,
                StringComparison.Ordinal)
            || !string.Equals(
                marker.PreservationDirectoryName,
                marker.RepairId.ToString("N"),
                StringComparison.Ordinal))
        {
            throw new MemoryIntegrityException("The repair marker values are invalid.");
        }
    }
}
