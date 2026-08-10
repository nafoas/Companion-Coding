using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CompanionCore.Memory;

internal static class CanonicalMemorySerializer
{
    internal static PreparedAppendOperation Prepare(AppendMemoryProposal proposal)
    {
        var payload = SerializeOperation(proposal);
        var normalizedProposal = MemoryPayloadParser.ParseOperation(payload);
        var recordChecksums = normalizedProposal.Records.ToDictionary(
            record => record.RecordId,
            record => ComputeRecordChecksum(normalizedProposal.LocalOperationId, record));

        return new PreparedAppendOperation(
            normalizedProposal,
            payload,
            ComputeChecksum(payload),
            recordChecksums);
    }

    internal static byte[] SerializeOperation(AppendMemoryProposal proposal)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("operationId", proposal.LocalOperationId.ToString("D"));
            writer.WriteString("operationName", AppendMemoryProposal.AllowlistedOperationName);
            writer.WritePropertyName("records");
            writer.WriteStartArray();
            foreach (var record in proposal.Records.OrderBy(record => record.RecordId))
            {
                WriteRecord(writer, record);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    internal static string ComputeRecordChecksum(Guid localOperationId, MemoryRecordDraft record)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("operationId", localOperationId.ToString("D"));
            writer.WritePropertyName("record");
            WriteRecord(writer, record);
            writer.WriteEndObject();
        }

        return ComputeChecksum(buffer.ToArray());
    }

    internal static string ComputeChecksum(ReadOnlySpan<byte> payload) =>
        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    internal static string CanonicalizeJson(JsonElement element)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalJson(writer, element);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteRecord(Utf8JsonWriter writer, MemoryRecordDraft record)
    {
        writer.WriteStartObject();
        writer.WriteString("recordId", record.RecordId.ToString("D"));
        writer.WriteNumber("schemaVersion", record.SchemaVersion);
        writer.WriteString(
            "createdAtUtc",
            record.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        writer.WriteNumber("scope", (int)record.Scope);
        writer.WriteNumber("sourceKind", (int)record.SourceKind);
        writer.WriteNumber("confidence", record.Confidence);
        writer.WriteString("subjectKey", record.SubjectKey);

        writer.WritePropertyName("entityReferences");
        writer.WriteStartArray();
        foreach (var entity in record.EntityReferences.OrderBy(entity => entity, StringComparer.Ordinal))
        {
            writer.WriteStringValue(entity);
        }

        writer.WriteEndArray();

        WriteNullableString(writer, "applicationReference", record.ApplicationReference);
        WriteNullableString(writer, "gameReference", record.GameReference);
        WriteNullableString(writer, "saveReference", record.SaveReference);
        WriteNullableString(writer, "sessionReference", record.SessionReference);
        writer.WriteString("visibleRecollection", record.VisibleRecollection);

        writer.WritePropertyName("retrievalMetadata");
        using (var metadata = JsonDocument.Parse(record.RetrievalMetadataJson))
        {
            WriteCanonicalJson(writer, metadata.RootElement);
        }

        writer.WritePropertyName("links");
        writer.WriteStartArray();
        foreach (var link in record.Links
                     .OrderBy(link => (int)link.Kind)
                     .ThenBy(link => link.TargetRecordId))
        {
            writer.WriteStartObject();
            writer.WriteString("targetRecordId", link.TargetRecordId.ToString("D"));
            writer.WriteNumber("kind", (int)link.Kind);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(propertyName);
        }
        else
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                throw new MemoryValidationException("Retrieval metadata contains an unsupported JSON value.");
        }
    }
}
