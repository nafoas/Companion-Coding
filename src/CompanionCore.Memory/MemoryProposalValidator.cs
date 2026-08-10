using System.Text;
using System.Text.Json;

namespace CompanionCore.Memory;

internal static class MemoryProposalValidator
{
    internal const int MaximumRecordsPerOperation = 128;
    internal const int MaximumLinksPerRecord = 256;
    internal const int MaximumEntitiesPerRecord = 64;
    internal const int MaximumKeyCharacters = 512;
    internal const int MaximumVisiblePayloadBytes = 16 * 1024;
    internal const int MaximumMetadataBytes = 64 * 1024;

    internal static PreparedAppendOperation Prepare(AppendMemoryProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (proposal.LocalOperationId == Guid.Empty)
        {
            throw new MemoryValidationException("A non-empty local operation ID is required.");
        }

        if (!string.Equals(
                proposal.OperationName,
                AppendMemoryProposal.AllowlistedOperationName,
                StringComparison.Ordinal))
        {
            throw new MemoryValidationException("The append proposal has an invalid operation name.");
        }

        if (proposal.Records is null || proposal.Records.Count is < 1 or > MaximumRecordsPerOperation)
        {
            throw new MemoryValidationException(
                $"An append operation must contain between 1 and {MaximumRecordsPerOperation} records.");
        }

        var recordIds = new HashSet<Guid>();
        var recordsById = new Dictionary<Guid, MemoryRecordDraft>();
        foreach (var record in proposal.Records)
        {
            ValidateRecord(record);
            if (!recordIds.Add(record.RecordId))
            {
                throw new MemoryValidationException("Record IDs must be unique within an append operation.");
            }

            recordsById.Add(record.RecordId, record);
        }

        foreach (var record in proposal.Records)
        {
            foreach (var link in record.Links)
            {
                if (recordsById.TryGetValue(link.TargetRecordId, out var localTarget)
                    && link.Kind is MemoryLinkKind.Corrects
                        or MemoryLinkKind.Supersedes
                        or MemoryLinkKind.RecursWith
                    && !string.Equals(record.SubjectKey, localTarget.SubjectKey, StringComparison.Ordinal))
                {
                    throw new MemoryValidationException(
                        "Correction, supersession, and recurrence links must stay within one exact subject.");
                }
            }
        }

        return CanonicalMemorySerializer.Prepare(proposal);
    }

    private static void ValidateRecord(MemoryRecordDraft record)
    {
        if (record is null)
        {
            throw new MemoryValidationException("Append operations cannot contain a null record.");
        }

        if (record.RecordId == Guid.Empty)
        {
            throw new MemoryValidationException("A non-empty record ID is required.");
        }

        if (record.SchemaVersion != MemorySchema.CurrentVersion)
        {
            throw new MemoryValidationException("The proposed record schema version is unsupported.");
        }

        if (record.CreatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new MemoryValidationException("Record timestamps must be normalized to UTC.");
        }

        if (!Enum.IsDefined(typeof(MemoryScope), record.Scope)
            || !Enum.IsDefined(typeof(MemorySourceKind), record.SourceKind))
        {
            throw new MemoryValidationException("Record scope and source kind must be recognized values.");
        }

        if (!double.IsFinite(record.Confidence) || record.Confidence is < 0 or > 1)
        {
            throw new MemoryValidationException("Confidence must be a finite value between zero and one.");
        }

        ValidateRequiredText(record.SubjectKey, nameof(record.SubjectKey), MaximumKeyCharacters);
        ValidateRequiredText(record.VisibleRecollection, nameof(record.VisibleRecollection), int.MaxValue);
        if (Encoding.UTF8.GetByteCount(record.VisibleRecollection) > MaximumVisiblePayloadBytes)
        {
            throw new MemoryValidationException("The visible recollection exceeds the bounded payload size.");
        }

        ValidateOptionalReference(record.ApplicationReference, nameof(record.ApplicationReference));
        ValidateOptionalReference(record.GameReference, nameof(record.GameReference));
        ValidateOptionalReference(record.SaveReference, nameof(record.SaveReference));
        ValidateOptionalReference(record.SessionReference, nameof(record.SessionReference));

        if (record.EntityReferences is null || record.EntityReferences.Count > MaximumEntitiesPerRecord)
        {
            throw new MemoryValidationException("The entity-reference list is missing or exceeds its bound.");
        }

        var entities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in record.EntityReferences)
        {
            ValidateRequiredText(entity, "entity reference", MaximumKeyCharacters);
            if (!entities.Add(entity))
            {
                throw new MemoryValidationException("Entity references must be unique within a record.");
            }
        }

        ValidateMetadata(record.RetrievalMetadataJson);

        if (record.Links is null || record.Links.Count > MaximumLinksPerRecord)
        {
            throw new MemoryValidationException("The link list is missing or exceeds its bound.");
        }

        var links = new HashSet<(Guid Target, MemoryLinkKind Kind)>();
        foreach (var link in record.Links)
        {
            if (link is null || link.TargetRecordId == Guid.Empty || link.TargetRecordId == record.RecordId)
            {
                throw new MemoryValidationException("Links require a distinct, non-empty target record ID.");
            }

            if (!Enum.IsDefined(typeof(MemoryLinkKind), link.Kind))
            {
                throw new MemoryValidationException("Links require a recognized relationship kind.");
            }

            if (!links.Add((link.TargetRecordId, link.Kind)))
            {
                throw new MemoryValidationException("Duplicate links are not canonical.");
            }
        }
    }

    private static void ValidateMetadata(string metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)
            || Encoding.UTF8.GetByteCount(metadataJson) > MaximumMetadataBytes)
        {
            throw new MemoryValidationException("Retrieval metadata is missing or exceeds its bound.");
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new MemoryValidationException("Retrieval metadata must be a JSON object.");
            }

            ValidateCanonicalJsonShape(document.RootElement);
        }
        catch (JsonException exception)
        {
            throw new MemoryValidationException($"Retrieval metadata is invalid JSON: {exception.Message}");
        }
    }

    private static void ValidateCanonicalJsonShape(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!propertyNames.Add(property.Name))
                {
                    throw new MemoryValidationException(
                        "Retrieval metadata cannot contain duplicate object-property names.");
                }

                ValidateCanonicalJsonShape(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ValidateCanonicalJsonShape(item);
            }
        }
    }

    private static void ValidateOptionalReference(string? value, string fieldName)
    {
        if (value is not null)
        {
            ValidateRequiredText(value, fieldName, MaximumKeyCharacters);
        }
    }

    private static void ValidateRequiredText(string value, string fieldName, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumCharacters)
        {
            throw new MemoryValidationException($"{fieldName} is missing or exceeds its bound.");
        }
    }
}
