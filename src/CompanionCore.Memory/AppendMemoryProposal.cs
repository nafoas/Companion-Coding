namespace CompanionCore.Memory;

public sealed record AppendMemoryProposal : IAutomatedWriteProposal
{
    public AppendMemoryProposal(Guid localOperationId, IReadOnlyList<MemoryRecordDraft> records)
    {
        LocalOperationId = localOperationId;
        Records = SnapshotRecords(records);
    }

    public const string AllowlistedOperationName = "memory.append.v1";

    public Guid LocalOperationId { get; }

    public IReadOnlyList<MemoryRecordDraft> Records { get; }

    public string OperationName => AllowlistedOperationName;

    private static IReadOnlyList<MemoryRecordDraft> SnapshotRecords(
        IReadOnlyList<MemoryRecordDraft> records)
    {
        if (records is null)
        {
            throw new MemoryValidationException("The append record list is required.");
        }

        var snapshots = new MemoryRecordDraft[records.Count];
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record is null)
            {
                throw new MemoryValidationException("Append operations cannot contain a null record.");
            }

            var entities = record.EntityReferences is null
                ? null!
                : Array.AsReadOnly(record.EntityReferences.ToArray());
            var links = record.Links is null
                ? null!
                : Array.AsReadOnly(record.Links
                    .Select(link => link is null ? null! : link with { })
                    .ToArray());

            snapshots[index] = record with
            {
                EntityReferences = entities,
                Links = links,
            };
        }

        return Array.AsReadOnly(snapshots);
    }
}
