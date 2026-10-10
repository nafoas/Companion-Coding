using System.Globalization;
using System.Text;
using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;
using CompanionCore.Conversation;
using CompanionCore.Watchbun;

namespace CompanionCore.Orchestration;

internal sealed record SemanticEvidence(
    IReadOnlyList<InterestEvent> Events,
    GameObservation? Observation,
    StructuredGameEvent? WatchbunEvent);

/// <summary>
/// Maps one validated semantic interpretation into typed evidence for attention,
/// conversation, and Watchbun. Labels are treated as opaque topics; the core attaches no
/// meaning or personality to their words.
/// </summary>
internal static class SemanticEvidenceMapper
{
    internal const int MaximumTopicCharacters = 64;
    internal const double MeaningfulChange = 0.25;

    internal static SemanticEvidence Map(
        SemanticInterpretation interpretation,
        AttentionSheetMetadata sheet,
        DateTimeOffset now,
        ISet<string> seenTopics)
    {
        ArgumentNullException.ThrowIfNull(interpretation);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(seenTopics);
        var change = Clamp(sheet.ChangeScore);
        var events = new List<InterestEvent>();
        var anyNovel = false;
        (string Topic, double Confidence)? strongest = null;
        foreach (var observation in interpretation.Observations)
        {
            var topic = Topic(observation.Label);
            var confidence = Clamp(observation.Confidence);
            var novel = seenTopics.Add(topic);
            anyNovel |= novel;
            var region = sheet.Regions.FirstOrDefault(candidate => candidate.Kind == observation.Region)?.NormalizedSource
                ?? new NormalizedRegion(0, 0, 1, 1);
            events.Add(new InterestEvent
            {
                TargetSessionId = interpretation.TargetSessionId,
                Timestamp = now,
                Region = region,
                RegionKind = observation.Region,
                Signals = new AttentionSignals(
                    Novelty: novel ? 0.8 : 0.2,
                    Change: change,
                    Salience: confidence,
                    Urgency: 0,
                    Persistence: novel ? 0.2 : 0.6,
                    Confidence: confidence),
                TopicKey = topic,
                EvidenceSource = $"semantic:{observation.Region}",
                EvidenceReferences = [interpretation.OperationId.ToString("N")],
            });
            if (strongest is null || confidence > strongest.Value.Confidence)
            {
                strongest = (topic, confidence);
            }
        }

        var gameObservation = strongest is { } best ? new GameObservation(best.Topic, best.Confidence) : null;
        var watchbunEvent = strongest is { } top
            ? new StructuredGameEvent(
                interpretation.TargetSessionId,
                anyNovel || change >= MeaningfulChange ? GameEventKind.Meaningful : GameEventKind.Change,
                top.Topic)
            : null;
        return new SemanticEvidence(events, gameObservation, watchbunEvent);
    }

    /// <summary>A bounded, lowercase, Watchbun-compatible key derived from opaque text.</summary>
    internal static string Topic(string? label)
    {
        var builder = new StringBuilder();
        foreach (var character in (label ?? string.Empty).Trim().Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (builder.Length == MaximumTopicCharacters)
            {
                break;
            }

            if (character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var topic = builder.ToString().Trim('-', '.', '_');
        return topic.Length == 0 ? "observation" : topic;
    }

    private static double Clamp(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
}
