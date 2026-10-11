using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Orchestration;

/// <summary>
/// Turns Text Sniffs (VIS-03) into attention evidence. New or changed words are strong,
/// trusted evidence: a dialogue line matters more than a waterfall's pixels. Vanished words
/// are weaker evidence. Like the Sniff reflex, each region habituates (a HUD clock that keeps
/// ticking fades to nothing while it ticks, and recovers once it stops), and every event
/// carries a score ceiling below Bnuy Mode. New, still-novel words bring the next Glance
/// forward at once. Thread-safe; time comes from the signals' own source timestamps.
/// </summary>
internal sealed class TextReflex
{
    internal static readonly TimeSpan HabituationRecoveryHalfLife = TimeSpan.FromSeconds(20);
    internal const double HabituationRate = 0.7;
    internal const double HabituationFloor = 0.05;

    /// <summary>New words this novel (or more) ask for an immediate Glance.</summary>
    internal const double GlanceFamiliarity = 0.5;

    internal const double NewTextConfidence = 0.8;
    internal const double VanishedConfidence = 0.5;

    private readonly object _gate = new();
    private readonly Dictionary<AttentionRegionKind, RegionState> _regions = [];
    private readonly double _ceiling;
    private long _forwarded;
    private long _suppressed;

    internal TextReflex(AttentionConfiguration attention)
    {
        ArgumentNullException.ThrowIfNull(attention);
        _ceiling = Math.Max(0, attention.HighEnter - 1);
    }

    internal long Forwarded => Interlocked.Read(ref _forwarded);

    internal long Suppressed => Interlocked.Read(ref _suppressed);

    /// <summary>The evidence for one Text Sniff, and whether it asks for a Glance now.</summary>
    internal (InterestEvent? Interest, bool GlanceNow) Evaluate(TextSniffSignal text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.IsProtocolSafe() || !text.TextChanged)
        {
            Interlocked.Increment(ref _suppressed);
            return (null, false);
        }

        double familiarity;
        lock (_gate)
        {
            if (!_regions.TryGetValue(text.RegionKind, out var region))
            {
                region = new RegionState();
                _regions[text.RegionKind] = region;
            }

            var at = text.SourceTimestamp;
            familiarity = region.Factor;
            if (region.LastSeen is { } previous && at > previous)
            {
                var elapsed = (at - previous).TotalSeconds;
                familiarity = 1 - ((1 - familiarity) * Math.Pow(0.5, elapsed / HabituationRecoveryHalfLife.TotalSeconds));
            }

            region.Factor = Math.Max(HabituationFloor, familiarity * HabituationRate);
            region.LastSeen = at;
        }

        Interlocked.Increment(ref _forwarded);
        var dialogue = text.RegionKind == AttentionRegionKind.LowerDialogueInventory;
        var signals = text.NewText
            ? new AttentionSignals(
                Novelty: 0.8 * familiarity,
                Change: 0.6 * familiarity,
                Salience: (dialogue ? 0.7 : 0.4) * familiarity,
                Urgency: 0,
                Persistence: 0.3,
                Confidence: NewTextConfidence)
            : new AttentionSignals(
                Novelty: 0.2 * familiarity,
                Change: 0.4 * familiarity,
                Salience: 0.2 * familiarity,
                Urgency: 0,
                Persistence: 0.1,
                Confidence: VanishedConfidence);
        var interest = new InterestEvent
        {
            TargetSessionId = text.TargetSessionId,
            Timestamp = text.SourceTimestamp,
            Region = text.Region,
            RegionKind = text.RegionKind,
            Signals = signals,
            EvidenceSource = $"text:{text.RegionKind}",
            ScoreCeiling = _ceiling,
        };
        return (interest, text.NewText && familiarity >= GlanceFamiliarity);
    }

    internal void Reset()
    {
        lock (_gate)
        {
            _regions.Clear();
        }
    }

    private sealed class RegionState
    {
        public double Factor { get; set; } = 1;

        public DateTimeOffset? LastSeen { get; set; }
    }
}
