using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Orchestration;

/// <summary>
/// Turns Scene Sniffs (VIS-04) into attention evidence. The local vision model reports when
/// the screen becomes a different scene. A change is whole-screen evidence that scales with
/// how far the scene moved and carries a <c>scene:&lt;label&gt;</c> topic, so the engine
/// habituates to a scene he keeps returning to. Every event stays below Bnuy Mode. A full
/// shift brings the next Glance forward, unless he has just seen that kind of scene several
/// times (each label habituates here too, recovering over <see cref="HabituationRecoveryHalfLife"/>).
/// Thread-safe; time comes from the signals' own source timestamps.
/// </summary>
internal sealed class SceneReflex
{
    internal static readonly TimeSpan HabituationRecoveryHalfLife = TimeSpan.FromSeconds(60);
    internal const double HabituationRate = 0.7;
    internal const double HabituationFloor = 0.05;

    /// <summary>A shift into a scene this novel (or more) asks for an immediate Glance.</summary>
    internal const double GlanceFamiliarity = 0.5;

    internal const double ShiftConfidence = 0.75;
    internal const double ChangeConfidence = 0.5;
    internal const string EvidenceSource = "scene";

    private readonly object _gate = new();
    private readonly Dictionary<int, LabelState> _labels = [];
    private readonly double _ceiling;
    private long _forwarded;
    private long _suppressed;

    internal SceneReflex(AttentionConfiguration attention)
    {
        ArgumentNullException.ThrowIfNull(attention);
        _ceiling = Math.Max(0, attention.HighEnter - 1);
    }

    internal long Forwarded => Interlocked.Read(ref _forwarded);

    internal long Suppressed => Interlocked.Read(ref _suppressed);

    /// <summary>The situation he is in: the last scene the Scene Sniff shifted to, if any (M3a).</summary>
    internal string? Label { get; private set; }

    /// <summary>The evidence for one Scene Sniff, and whether it asks for a Glance now.</summary>
    internal (InterestEvent? Interest, bool GlanceNow) Evaluate(SceneSniffSignal scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!scene.IsProtocolSafe())
        {
            Interlocked.Increment(ref _suppressed);
            return (null, false);
        }

        double familiarity;
        lock (_gate)
        {
            if (!_labels.TryGetValue(scene.Label, out var state))
            {
                state = new LabelState();
                _labels[scene.Label] = state;
            }

            var at = scene.SourceTimestamp;
            familiarity = state.Factor;
            if (state.LastSeen is { } previous && at > previous)
            {
                var elapsed = (at - previous).TotalSeconds;
                familiarity = 1 - ((1 - familiarity) * Math.Pow(0.5, elapsed / HabituationRecoveryHalfLife.TotalSeconds));
            }

            if (scene.Shift)
            {
                state.Factor = Math.Max(HabituationFloor, familiarity * HabituationRate);
                state.LastSeen = at;
                Label = SceneVocabulary.KeyOf(scene.Label);
            }
        }

        Interlocked.Increment(ref _forwarded);
        var moved = Math.Clamp(scene.Novelty * 2, 0, 1);
        var interest = new InterestEvent
        {
            TargetSessionId = scene.TargetSessionId,
            Timestamp = scene.SourceTimestamp,
            Region = new NormalizedRegion(0, 0, 1, 1),
            RegionKind = AttentionRegionKind.FullContext,
            Signals = new AttentionSignals(
                Novelty: moved,
                Change: moved,
                Salience: scene.Shift ? 0.6 : 0.3,
                Urgency: 0,
                Persistence: 0.3,
                Confidence: scene.Shift ? ShiftConfidence : ChangeConfidence),
            TopicKey = SceneVocabulary.KeyOf(scene.Label) is { } key ? $"scene:{key}" : null,
            EvidenceSource = EvidenceSource,
            ScoreCeiling = _ceiling,
        };
        return (interest, scene.Shift && familiarity >= GlanceFamiliarity);
    }

    internal void Reset()
    {
        lock (_gate)
        {
            _labels.Clear();
            Label = null;
        }
    }

    private sealed class LabelState
    {
        public double Factor { get; set; } = 1;

        public DateTimeOffset? LastSeen { get; set; }
    }
}
