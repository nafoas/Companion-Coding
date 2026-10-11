using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Orchestration;

/// <summary>
/// Turns local Sniffs into attention evidence (VIS-01): Prince's ears perk the moment
/// something changes, without waiting for any cloud look. Three protections keep the reflex
/// calm rather than frantic:
/// <list type="bullet">
/// <item>Each grid cell habituates: a looping animation in one place fades to nothing while
/// it keeps looping, and recovers once it stops.</item>
/// <item>Each cell is forwarded at most once per <see cref="CellSpacing"/>.</item>
/// <item>Every event carries a score ceiling below Bnuy Mode: only understanding, or
/// decisive evidence, can enter it.</item>
/// </list>
/// Evidence carries no topic: habituation is this class's per-cell, quickly recovering
/// kind (a waterfall must not mute a whole region for many minutes), and the engine
/// corroborates weak evidence per region kind, so change spread across one region's cells
/// corroborates while one cell repeating itself never does. A scene cut is a global
/// transition, deduplicated and capped by the engine. Thread-safe; time comes from the
/// sniffs' own source timestamps.
/// </summary>
internal sealed class SniffReflex
{
    internal static readonly TimeSpan CellSpacing = TimeSpan.FromMilliseconds(200);
    internal static readonly TimeSpan HabituationRecoveryHalfLife = TimeSpan.FromSeconds(20);
    internal const double HabituationRate = 0.8;
    internal const double HabituationFloor = 0.05;
    /// <summary>A trigger-level change is trusted on first sight; a weaker one needs corroboration.</summary>
    internal const double TriggerConfidence = 0.8;

    internal const double Confidence = 0.5;
    internal const string SceneCutTransition = "sniff:scene-cut";

    private readonly object _gate = new();
    private readonly CellState[] _cells = new CellState[SniffSignal.GridColumns * SniffSignal.GridRows];
    private readonly double _ceiling;
    private long _forwarded;
    private long _suppressed;

    internal SniffReflex(AttentionConfiguration attention)
    {
        ArgumentNullException.ThrowIfNull(attention);

        // Just below the Bnuy Mode threshold: the reflex may bring him to the very edge.
        _ceiling = Math.Max(0, attention.HighEnter - 1);
        for (var cell = 0; cell < _cells.Length; cell++)
        {
            _cells[cell] = new CellState();
        }
    }

    internal long Forwarded => Interlocked.Read(ref _forwarded);

    internal long Suppressed => Interlocked.Read(ref _suppressed);

    /// <summary>The evidence for one sniff, or null when the cell is still inside its spacing.</summary>
    internal InterestEvent? Evaluate(SniffSignal sniff)
    {
        ArgumentNullException.ThrowIfNull(sniff);
        if (!sniff.IsProtocolSafe())
        {
            Interlocked.Increment(ref _suppressed);
            return null;
        }

        double familiarity;
        lock (_gate)
        {
            var cell = _cells[sniff.HotCell];
            var at = sniff.SourceTimestamp;
            if (cell.LastForwarded is { } last && at >= last && at - last < CellSpacing)
            {
                Interlocked.Increment(ref _suppressed);
                return null;
            }

            // Recover toward full novelty since the last exposure, then habituate.
            familiarity = cell.Factor;
            if (cell.LastForwarded is { } previous && at > previous)
            {
                var elapsed = (at - previous).TotalSeconds;
                familiarity = 1 - ((1 - familiarity) * Math.Pow(0.5, elapsed / HabituationRecoveryHalfLife.TotalSeconds));
            }

            cell.Factor = Math.Max(HabituationFloor, familiarity * HabituationRate);
            cell.LastForwarded = at;
        }

        Interlocked.Increment(ref _forwarded);
        var change = Math.Clamp(sniff.HotChange * 2, 0, 1) * familiarity;
        var spread = Math.Clamp(sniff.Motion + 0.2, 0, 1) * familiarity;
        var interest = new InterestEvent
        {
            TargetSessionId = sniff.TargetSessionId,
            Timestamp = sniff.SourceTimestamp,
            Region = sniff.HotRegion,
            RegionKind = sniff.HotRegionKind,
            Signals = new AttentionSignals(
                Novelty: 0.5 * familiarity,
                Change: change,
                Salience: spread,
                Urgency: 0,
                Persistence: 0.2,
                Confidence: sniff.IsTrigger ? TriggerConfidence : Confidence),
            EvidenceSource = $"sniff:cell:{sniff.HotCell}",
            ScoreCeiling = _ceiling,
        };

        return sniff.SceneCut
            ? interest with
            {
                Region = new NormalizedRegion(0, 0, 1, 1),
                RegionKind = AttentionRegionKind.FullContext,
                Kind = AttentionEventKind.GlobalTransition,
                TransitionKey = SceneCutTransition,
            }
            : interest;
    }

    internal void Reset()
    {
        lock (_gate)
        {
            foreach (var cell in _cells)
            {
                cell.Factor = 1;
                cell.LastForwarded = null;
            }
        }
    }

    private sealed class CellState
    {
        public double Factor { get; set; } = 1;

        public DateTimeOffset? LastForwarded { get; set; }
    }
}
