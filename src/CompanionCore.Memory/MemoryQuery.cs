namespace CompanionCore.Memory;

/// <summary>
/// Bounded read-only memory query. At least one filter is required, so no caller can
/// enumerate the whole store, and every string is validated like a stored reference.
/// </summary>
public sealed record MemoryQuery
{
    public const int MaximumLimit = 1000;
    public const int MaximumRecordIds = 256;

    public string? GameReference { get; init; }

    public string? SaveReference { get; init; }

    public string? SessionReference { get; init; }

    public string? SubjectPrefix { get; init; }

    public IReadOnlyList<MemoryScope>? Scopes { get; init; }

    public IReadOnlyList<Guid>? RecordIds { get; init; }

    public int Limit { get; init; } = 200;

    internal void Validate()
    {
        var hasFilter = GameReference is not null
            || SaveReference is not null
            || SessionReference is not null
            || SubjectPrefix is not null
            || RecordIds is { Count: > 0 };
        if (!hasFilter
            || Limit is < 1 or > MaximumLimit
            || !ValidKey(GameReference)
            || !ValidKey(SaveReference)
            || !ValidKey(SessionReference)
            || !ValidKey(SubjectPrefix)
            || (Scopes is not null && Scopes.Any(scope => !Enum.IsDefined(scope)))
            || (RecordIds is not null && (RecordIds.Count > MaximumRecordIds || RecordIds.Any(id => id == Guid.Empty))))
        {
            throw new ArgumentException("The memory query is unbounded or invalid.");
        }
    }

    private static bool ValidKey(string? value) =>
        value is null || (!string.IsNullOrWhiteSpace(value) && value.Length <= MemoryProposalValidator.MaximumKeyCharacters);
}
