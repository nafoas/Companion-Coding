using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Api;

/// <summary>
/// One Glance (VIS-02): Prince's quick, peripheral look. It says how interesting the
/// screen is, where it changed, whether to look closer, and when to glance again. It is
/// attention evidence only: a Glance carries no memory proposal and is never shown as text.
/// </summary>
public sealed record GlanceReading(
    Guid OperationId,
    Guid TargetSessionId,
    double Interest,
    double Confidence,
    AttentionRegionKind ChangedRegion,
    string Label,
    bool Escalate,
    TimeSpan NextLook);

/// <summary>Bounds of the strict Glance schema.</summary>
public static class GlanceSchema
{
    internal const int MaximumResponseBytes = 4 * 1024;
    internal const int MaximumLabelCharacters = 64;
    internal const int MinimumNextLookSeconds = 1;
    internal const int MaximumNextLookSeconds = 120;
}

internal sealed class GlanceResponseWire
{
    [JsonRequired]
    public int SchemaVersion { get; set; }

    [JsonRequired]
    public Guid OperationId { get; set; }

    [JsonRequired]
    public GlanceWire Glance { get; set; } = null!;

    public UsageWire? Usage { get; set; }
}

internal sealed class GlanceWire
{
    [JsonRequired]
    public double Interest { get; set; }

    [JsonRequired]
    public double Confidence { get; set; }

    [JsonRequired]
    public AttentionRegionKind ChangedRegion { get; set; }

    [JsonRequired]
    public string Label { get; set; } = null!;

    [JsonRequired]
    public bool Escalate { get; set; }

    [JsonRequired]
    public int NextLookSeconds { get; set; }
}

internal sealed record ParsedGlance(
    double Interest,
    double Confidence,
    AttentionRegionKind ChangedRegion,
    string Label,
    bool Escalate,
    TimeSpan NextLook,
    long? InputUnits,
    long? OutputUnits);

/// <summary>
/// The strict Glance parser. Any violation invalidates the whole Glance; unknown members,
/// including any attempt at a memory proposal, are rejected by the strict options.
/// </summary>
internal static class GlanceResponseParser
{
    internal static bool TryParse(
        string responseJson,
        Guid expectedOperationId,
        IReadOnlyCollection<AttentionRegionKind> sheetRegions,
        out ParsedGlance? parsed,
        out SemanticResponseInvalidReason reason)
    {
        parsed = null;
        if (responseJson is null
            || responseJson.Length > GlanceSchema.MaximumResponseBytes
            || Encoding.UTF8.GetByteCount(responseJson) > GlanceSchema.MaximumResponseBytes)
        {
            reason = SemanticResponseInvalidReason.TooLarge;
            return false;
        }

        GlanceResponseWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<GlanceResponseWire>(responseJson, SemanticSchema.StrictOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            reason = SemanticResponseInvalidReason.Malformed;
            return false;
        }

        if (wire?.Glance is not { } glance)
        {
            reason = SemanticResponseInvalidReason.Malformed;
            return false;
        }

        if (wire.SchemaVersion != SemanticSchema.Version)
        {
            reason = SemanticResponseInvalidReason.UnsupportedSchemaVersion;
            return false;
        }

        if (wire.OperationId != expectedOperationId)
        {
            reason = SemanticResponseInvalidReason.OperationMismatch;
            return false;
        }

        if (!SemanticResponseParser.IsUnitInterval(glance.Interest)
            || !SemanticResponseParser.IsUnitInterval(glance.Confidence)
            || !Enum.IsDefined(glance.ChangedRegion)
            || !SemanticResponseParser.IsBoundedText(glance.Label, GlanceSchema.MaximumLabelCharacters)
            || glance.Label.Contains('\n')
            || glance.NextLookSeconds is < GlanceSchema.MinimumNextLookSeconds or > GlanceSchema.MaximumNextLookSeconds
            || (wire.Usage is { } usage
                && (usage.InputUnits is < 0 or > SemanticSchema.MaximumUsageUnits
                    || usage.OutputUnits is < 0 or > SemanticSchema.MaximumUsageUnits)))
        {
            reason = SemanticResponseInvalidReason.OutOfBounds;
            return false;
        }

        if (!sheetRegions.Contains(glance.ChangedRegion))
        {
            reason = SemanticResponseInvalidReason.UnknownRegion;
            return false;
        }

        parsed = new ParsedGlance(
            glance.Interest,
            glance.Confidence,
            glance.ChangedRegion,
            glance.Label,
            glance.Escalate,
            TimeSpan.FromSeconds(glance.NextLookSeconds),
            wire.Usage?.InputUnits,
            wire.Usage?.OutputUnits);
        reason = SemanticResponseInvalidReason.None;
        return true;
    }
}
