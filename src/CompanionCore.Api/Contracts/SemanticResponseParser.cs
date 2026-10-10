using System.Text;
using System.Text.Json;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Api;

public enum SemanticResponseInvalidReason
{
    None = 0,
    TooLarge = 1,
    Malformed = 2,
    UnsupportedSchemaVersion = 3,
    OperationMismatch = 4,
    OutOfBounds = 5,
    UnknownRegion = 6,
    CredentialEcho = 7,
}

internal sealed record ParsedSemanticResponse(
    string Summary,
    IReadOnlyList<SemanticObservation> Observations,
    IReadOnlyList<MemoryProposalWire> Proposals,
    long? InputUnits,
    long? OutputUnits);

/// <summary>
/// The one strict parser every provider's success reply passes through. Any violation
/// invalidates the whole response; nothing partial reaches memory or output.
/// </summary>
internal static class SemanticResponseParser
{
    internal static bool TryParse(
        string responseJson,
        Guid expectedOperationId,
        IReadOnlyCollection<AttentionRegionKind> sheetRegions,
        out ParsedSemanticResponse? parsed,
        out SemanticResponseInvalidReason reason)
    {
        parsed = null;
        if (responseJson is null
            || responseJson.Length > SemanticSchema.MaximumResponseBytes
            || Encoding.UTF8.GetByteCount(responseJson) > SemanticSchema.MaximumResponseBytes)
        {
            reason = SemanticResponseInvalidReason.TooLarge;
            return false;
        }

        SemanticResponseWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<SemanticResponseWire>(responseJson, SemanticSchema.StrictOptions);
        }
        catch (JsonException)
        {
            reason = SemanticResponseInvalidReason.Malformed;
            return false;
        }
        catch (NotSupportedException)
        {
            reason = SemanticResponseInvalidReason.Malformed;
            return false;
        }

        if (wire?.Interpretation is null)
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

        if (!IsBoundedText(wire.Interpretation.Summary, SemanticSchema.MaximumSummaryCharacters))
        {
            reason = SemanticResponseInvalidReason.OutOfBounds;
            return false;
        }

        var observationWires = wire.Interpretation.Observations ?? [];
        if (observationWires.Count > SemanticSchema.MaximumObservations)
        {
            reason = SemanticResponseInvalidReason.OutOfBounds;
            return false;
        }

        var observations = new List<SemanticObservation>(observationWires.Count);
        foreach (var observation in observationWires)
        {
            if (observation is null
                || !Enum.IsDefined(observation.Region)
                || !IsBoundedText(observation.Label, SemanticSchema.MaximumLabelCharacters)
                || !IsUnitInterval(observation.Confidence))
            {
                reason = SemanticResponseInvalidReason.OutOfBounds;
                return false;
            }

            if (!sheetRegions.Contains(observation.Region))
            {
                reason = SemanticResponseInvalidReason.UnknownRegion;
                return false;
            }

            observations.Add(new SemanticObservation(observation.Region, observation.Label, observation.Confidence));
        }

        var proposals = wire.MemoryProposals ?? [];
        if (proposals.Count > SemanticSchema.MaximumProposals || proposals.Any(proposal => proposal is null))
        {
            reason = SemanticResponseInvalidReason.OutOfBounds;
            return false;
        }

        if (wire.Usage is { } usage
            && (usage.InputUnits is < 0 or > SemanticSchema.MaximumUsageUnits
                || usage.OutputUnits is < 0 or > SemanticSchema.MaximumUsageUnits))
        {
            reason = SemanticResponseInvalidReason.OutOfBounds;
            return false;
        }

        parsed = new ParsedSemanticResponse(
            wire.Interpretation.Summary,
            observations.AsReadOnly(),
            proposals.AsReadOnly(),
            wire.Usage?.InputUnits,
            wire.Usage?.OutputUnits);
        reason = SemanticResponseInvalidReason.None;
        return true;
    }

    internal static bool IsUnitInterval(double value) => double.IsFinite(value) && value is >= 0 and <= 1;

    internal static bool IsBoundedText(string? value, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumCharacters)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (char.IsControl(character) && character is not '\n' and not '\t')
            {
                return false;
            }
        }

        return true;
    }
}
