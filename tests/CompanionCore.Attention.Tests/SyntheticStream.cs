using CompanionCore.Capture.Contracts;

namespace CompanionCore.Attention.Tests;

/// <summary>Deterministic synthetic evidence. No clock, capture, or randomness.</summary>
internal sealed class SyntheticStream
{
    internal static readonly Guid Session = Guid.Parse("6f1d2c3b-0000-4000-8000-000000000008");
    internal static readonly DateTimeOffset Start = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    internal SyntheticStream(AttentionConfiguration? configuration = null)
    {
        Engine = new AttentionEngine(Session, Start, configuration);
    }

    internal AttentionEngine Engine { get; }

    internal DateTimeOffset Now { get; private set; } = Start;

    internal List<AttentionUpdate> Updates { get; } = [];

    internal IEnumerable<AttentionIntentKind> IntentKinds => Updates.SelectMany(update => update.Intents).Select(intent => intent.Kind);

    /// <summary>Fails promptly instead of hanging when a state-driven loop never ends.</summary>
    internal static void Guard(int iteration)
    {
        if (iteration > 10_000)
        {
            throw new Xunit.Sdk.XunitException("A state-driven test loop exceeded its iteration bound.");
        }
    }

    internal AttentionUpdate At(double seconds, InterestEvent interestEvent)
    {
        Now = Start + TimeSpan.FromSeconds(seconds);
        var update = Engine.Observe(interestEvent with { Timestamp = Now });
        Updates.Add(update);
        return update;
    }

    internal AttentionUpdate AdvanceTo(double seconds)
    {
        Now = Start + TimeSpan.FromSeconds(seconds);
        var update = Engine.Advance(Now);
        Updates.Add(update);
        return update;
    }

    internal static InterestEvent WeakMotion(string source = "region:center", string? topic = "motion.foliage") => new()
    {
        TargetSessionId = Session,
        Timestamp = Start,
        RegionKind = AttentionRegionKind.CenterEnvironment,
        Region = new NormalizedRegion(0.25, 0.25, 0.5, 0.5),
        Signals = new AttentionSignals(Novelty: 0.2, Change: 0.4, Salience: 0.1, Urgency: 0.05, Persistence: 0.1, Confidence: 0.4),
        TopicKey = topic,
        EvidenceSource = source,
    };

    internal static InterestEvent EnemyEvidence(string source, double confidence = 0.45) => new()
    {
        TargetSessionId = Session,
        Timestamp = Start,
        RegionKind = source == "region:upper-left" ? AttentionRegionKind.UpperLeft : AttentionRegionKind.CenterEnvironment,
        Signals = new AttentionSignals(Novelty: 0.7, Change: 0.7, Salience: 0.8, Urgency: 0.6, Persistence: 0.5, Confidence: confidence),
        TopicKey = "threat.enemy-nearby",
        EvidenceSource = source,
    };

    internal static InterestEvent Harmless(string topic = "npc.villager-walk", string? location = null) => new()
    {
        TargetSessionId = Session,
        Timestamp = Start,
        RegionKind = AttentionRegionKind.CenterEnvironment,
        Signals = new AttentionSignals(Novelty: 0.7, Change: 0.6, Salience: 0.6, Urgency: 0.1, Persistence: 0.3, Confidence: 0.9),
        TopicKey = topic,
        LocationKey = location,
        EvidenceSource = "region:center",
    };

    internal static InterestEvent Suspicious(string topic = "shadow.flicker") => new()
    {
        TargetSessionId = Session,
        Timestamp = Start,
        RegionKind = AttentionRegionKind.RightSide,
        Signals = new AttentionSignals(Novelty: 0.9, Change: 0.8, Salience: 0.9, Urgency: 0.5, Persistence: 0.5, Confidence: 0.9),
        TopicKey = topic,
        EvidenceSource = "region:right",
    };

    internal static InterestEvent UrgentDanger(string topic = "npc.villager-walk", string? location = null) => new()
    {
        TargetSessionId = Session,
        Timestamp = Start,
        RegionKind = AttentionRegionKind.UpperLeft,
        Signals = new AttentionSignals(Novelty: 0.1, Change: 0.8, Salience: 0.9, Urgency: 0.95, Persistence: 0.5, Confidence: 0.9),
        TopicKey = topic,
        LocationKey = location,
        EvidenceSource = "region:upper-left",
    };

    internal static InterestEvent Decisive(DecisiveReason reason = DecisiveReason.Boss, string topic = "encounter.boss") => new()
    {
        TargetSessionId = Session,
        Timestamp = Start,
        Kind = AttentionEventKind.Decisive,
        DecisiveReason = reason,
        RegionKind = AttentionRegionKind.CenterEnvironment,
        Signals = new AttentionSignals(Novelty: 0.9, Change: 0.9, Salience: 1, Urgency: 0.9, Persistence: 0.5, Confidence: 0.95),
        TopicKey = topic,
        EvidenceSource = "semantic:interpretation",
    };

    internal static InterestEvent Loading(string transition = "transition.loading.1") => new()
    {
        TargetSessionId = Session,
        Timestamp = Start,
        Kind = AttentionEventKind.GlobalTransition,
        TransitionKey = transition,
        Signals = new AttentionSignals(Novelty: 0.5, Change: 1, Salience: 0.5, Urgency: 0, Persistence: 0.2, Confidence: 0.95),
        TopicKey = "transition.loading",
        EvidenceSource = "region:full",
    };
}
