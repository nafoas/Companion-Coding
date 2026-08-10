using System.Globalization;
using System.Text.Json;

namespace CompanionCore.Memory;

internal static class MemoryPayloadParser
{
    internal static AppendMemoryProposal ParseOperation(ReadOnlyMemory<byte> payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            RequireKind(root, JsonValueKind.Object, "operation payload");

            var operationId = ParseGuid(GetRequired(root, "operationId"), "operationId");
            var operationName = GetRequired(root, "operationName").GetString();
            if (!string.Equals(
                    operationName,
                    AppendMemoryProposal.AllowlistedOperationName,
                    StringComparison.Ordinal))
            {
                throw new MemoryValidationException("The journal operation name is not allowlisted.");
            }

            var recordsElement = GetRequired(root, "records");
            RequireKind(recordsElement, JsonValueKind.Array, "records");
            var records = recordsElement.EnumerateArray().Select(ParseRecord).ToArray();
            return new AppendMemoryProposal(operationId, records);
        }
        catch (JsonException exception)
        {
            throw new JournalCorruptionException($"A journal operation contains invalid JSON: {exception.Message}");
        }
        catch (FormatException exception)
        {
            throw new JournalCorruptionException($"A journal operation contains invalid formatted data: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            throw new JournalCorruptionException($"A journal operation contains an invalid JSON value: {exception.Message}");
        }
    }

    private static MemoryRecordDraft ParseRecord(JsonElement element)
    {
        RequireKind(element, JsonValueKind.Object, "memory record");

        var entityElement = GetRequired(element, "entityReferences");
        RequireKind(entityElement, JsonValueKind.Array, "entityReferences");
        var entities = entityElement.EnumerateArray()
            .Select(item => item.GetString() ?? throw new MemoryValidationException("Entity references must be strings."))
            .ToArray();

        var linksElement = GetRequired(element, "links");
        RequireKind(linksElement, JsonValueKind.Array, "links");
        var links = linksElement.EnumerateArray().Select(ParseLink).ToArray();

        var metadata = GetRequired(element, "retrievalMetadata");
        RequireKind(metadata, JsonValueKind.Object, "retrievalMetadata");

        return new MemoryRecordDraft
        {
            RecordId = ParseGuid(GetRequired(element, "recordId"), "recordId"),
            SchemaVersion = GetRequired(element, "schemaVersion").GetInt32(),
            CreatedAtUtc = DateTimeOffset.ParseExact(
                GetRequired(element, "createdAtUtc").GetString()
                    ?? throw new MemoryValidationException("createdAtUtc must be a string."),
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            Scope = (MemoryScope)GetRequired(element, "scope").GetInt32(),
            SourceKind = (MemorySourceKind)GetRequired(element, "sourceKind").GetInt32(),
            Confidence = GetRequired(element, "confidence").GetDouble(),
            SubjectKey = GetRequired(element, "subjectKey").GetString()
                ?? throw new MemoryValidationException("Subject key must be a string."),
            EntityReferences = entities,
            ApplicationReference = GetOptionalString(element, "applicationReference"),
            GameReference = GetOptionalString(element, "gameReference"),
            SaveReference = GetOptionalString(element, "saveReference"),
            SessionReference = GetOptionalString(element, "sessionReference"),
            VisibleRecollection = GetRequired(element, "visibleRecollection").GetString()
                ?? throw new MemoryValidationException("Visible recollection must be a string."),
            RetrievalMetadataJson = CanonicalMemorySerializer.CanonicalizeJson(metadata),
            Links = links,
        };
    }

    private static MemoryLink ParseLink(JsonElement element)
    {
        RequireKind(element, JsonValueKind.Object, "memory link");
        return new MemoryLink(
            ParseGuid(GetRequired(element, "targetRecordId"), "targetRecordId"),
            (MemoryLinkKind)GetRequired(element, "kind").GetInt32());
    }

    private static string? GetOptionalString(JsonElement parent, string propertyName)
    {
        var value = GetRequired(parent, propertyName);
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => throw new MemoryValidationException($"{propertyName} must be a string or null."),
        };
    }

    private static JsonElement GetRequired(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            throw new MemoryValidationException($"The required {propertyName} field is missing.");
        }

        return value;
    }

    private static Guid ParseGuid(JsonElement element, string fieldName)
    {
        var value = element.GetString();
        if (!Guid.TryParseExact(value, "D", out var parsed))
        {
            throw new MemoryValidationException($"{fieldName} is not a canonical GUID.");
        }

        return parsed;
    }

    private static void RequireKind(JsonElement element, JsonValueKind expected, string fieldName)
    {
        if (element.ValueKind != expected)
        {
            throw new MemoryValidationException($"{fieldName} has the wrong JSON shape.");
        }
    }
}
