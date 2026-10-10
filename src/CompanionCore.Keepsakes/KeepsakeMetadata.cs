using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CompanionCore.Keepsakes;

/// <summary>Invisible structured keepsake metadata stored with the photograph's memory record.</summary>
internal sealed record KeepsakeMetadata(
    Guid ActionId,
    string Sha256,
    long ByteLength,
    int Width,
    int Height,
    DateTimeOffset TakenAt,
    bool Deleted)
{
    internal string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("keepsake");
            writer.WriteString("actionId", ActionId.ToString("D"));
            writer.WriteNumber("byteLength", ByteLength);
            writer.WriteBoolean("deleted", Deleted);
            writer.WriteString("format", "png");
            writer.WriteNumber("height", Height);
            writer.WriteString("sha256", Sha256);
            writer.WriteString("takenAt", TakenAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteNumber("width", Width);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    internal static KeepsakeMetadata? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("keepsake", out var keepsake) || keepsake.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var sha = keepsake.GetProperty("sha256").GetString();
            if (!Guid.TryParseExact(keepsake.GetProperty("actionId").GetString(), "D", out var actionId)
                || !IsSha256(sha)
                || !DateTimeOffset.TryParseExact(keepsake.GetProperty("takenAt").GetString(), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var takenAt)
                || keepsake.GetProperty("format").GetString() != "png")
            {
                return null;
            }

            return new KeepsakeMetadata(
                actionId,
                sha!,
                keepsake.GetProperty("byteLength").GetInt64(),
                keepsake.GetProperty("width").GetInt32(),
                keepsake.GetProperty("height").GetInt32(),
                takenAt,
                keepsake.GetProperty("deleted").GetBoolean());
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    internal static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
