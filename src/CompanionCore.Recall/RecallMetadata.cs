using System.Text;
using System.Text.Json;

namespace CompanionCore.Recall;

public enum RecallRecordKind
{
    Entry = 1,
    Summary = 2,
    Highlight = 3,
    Adventure = 4,
    AdventureHypothesis = 5,
    Lore = 6,
    Opinion = 7,
    Correction = 8,
    Seed = 9,
}

public enum AdventureStatus
{
    Active = 1,
    Paused = 2,
    Finished = 3,
}

public enum AdventureHypothesis
{
    NewSave = 1,
    Ending = 2,
}

public enum LoreStatus
{
    Observed = 1,
    Read = 2,
    Told = 3,
    Suspected = 4,
    Confirmed = 5,
}

/// <summary>
/// Structured, invisible retrieval metadata. Kept as a small canonical JSON object so
/// mechanics never interpret the visible (placeholder) prose.
/// </summary>
public sealed record RecallMetadata
{
    public RecallRecordKind Kind { get; init; } = RecallRecordKind.Entry;

    public bool Highlight { get; init; }

    public bool Spoiler { get; init; }

    public AdventureStatus? Adventure { get; init; }

    public AdventureHypothesis? Hypothesis { get; init; }

    public LoreStatus? Lore { get; init; }

    public string? RootId { get; init; }

    public string? RootsFingerprint { get; init; }

    public int? ConsolidatedCount { get; init; }

    public int? HighlightCount { get; init; }

    public string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("recall");
            if (Adventure is { } adventure)
            {
                writer.WriteString("adventure", Name(adventure));
            }

            if (ConsolidatedCount is { } consolidated)
            {
                writer.WriteNumber("consolidatedCount", consolidated);
            }

            writer.WriteBoolean("highlight", Highlight);
            if (HighlightCount is { } highlights)
            {
                writer.WriteNumber("highlightCount", highlights);
            }

            if (Hypothesis is { } hypothesis)
            {
                writer.WriteString("hypothesis", Name(hypothesis));
            }

            writer.WriteString("kind", Name(Kind));
            if (Lore is { } lore)
            {
                writer.WriteString("lore", Name(lore));
            }

            if (RootId is not null)
            {
                writer.WriteString("rootId", RootId);
            }

            if (RootsFingerprint is not null)
            {
                writer.WriteString("rootsFingerprint", RootsFingerprint);
            }

            writer.WriteBoolean("spoiler", Spoiler);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Reads recall metadata; records written by other paths parse as plain entries.</summary>
    public static RecallMetadata Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RecallMetadata();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("recall", out var recall) || recall.ValueKind != JsonValueKind.Object)
            {
                return new RecallMetadata();
            }

            return new RecallMetadata
            {
                Kind = Enum<RecallRecordKind>(recall, "kind") ?? RecallRecordKind.Entry,
                Highlight = recall.TryGetProperty("highlight", out var highlight) && highlight.ValueKind == JsonValueKind.True,
                Spoiler = recall.TryGetProperty("spoiler", out var spoiler) && spoiler.ValueKind == JsonValueKind.True,
                Adventure = Enum<AdventureStatus>(recall, "adventure"),
                Hypothesis = Enum<AdventureHypothesis>(recall, "hypothesis"),
                Lore = Enum<LoreStatus>(recall, "lore"),
                RootId = recall.TryGetProperty("rootId", out var root) && root.ValueKind == JsonValueKind.String ? root.GetString() : null,
                RootsFingerprint = recall.TryGetProperty("rootsFingerprint", out var fingerprint) && fingerprint.ValueKind == JsonValueKind.String ? fingerprint.GetString() : null,
                ConsolidatedCount = recall.TryGetProperty("consolidatedCount", out var consolidated) && consolidated.TryGetInt32(out var count) ? count : null,
                HighlightCount = recall.TryGetProperty("highlightCount", out var highlights) && highlights.TryGetInt32(out var highlightCount) ? highlightCount : null,
            };
        }
        catch (JsonException)
        {
            return new RecallMetadata();
        }
    }

    private static string Name<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static T? Enum<T>(JsonElement element, string property) where T : struct, Enum
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        foreach (var candidate in System.Enum.GetValues<T>())
        {
            if (string.Equals(Name(candidate), value.GetString(), StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }
}
