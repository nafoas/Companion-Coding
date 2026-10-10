using CompanionCore.Attention;

namespace CompanionCore.Conversation.Tests;

/// <summary>Scripted chance: queued values first, then a constant. Counts every draw.</summary>
internal sealed class ScriptedChance(double fallback = 0.0) : IExpressionChance
{
    private readonly Queue<double> _queue = new();

    internal int Draws { get; private set; }

    internal double Fallback { get; set; } = fallback;

    internal void Enqueue(params double[] values)
    {
        foreach (var value in values)
        {
            _queue.Enqueue(value);
        }
    }

    public double Next()
    {
        Draws++;
        return _queue.Count > 0 ? _queue.Dequeue() : Fallback;
    }
}

internal sealed class ConversationHarness
{
    internal static readonly Guid Identity = Guid.Parse("0c0ffee0-0000-4000-8000-000000000009");
    internal static readonly DateTimeOffset Start = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    internal ConversationHarness(ConversationConfiguration? configuration = null, IExpressionChance? chance = null)
    {
        Chance = chance as ScriptedChance ?? new ScriptedChance();
        Coordinator = new ConversationCoordinator(Identity, Start, chance ?? Chance, configuration);
    }

    internal ScriptedChance Chance { get; }

    internal ConversationCoordinator Coordinator { get; set; }

    internal List<ConversationIntent> Intents { get; } = [];

    internal static DateTimeOffset At(double seconds) => Start + TimeSpan.FromSeconds(seconds);

    internal ConversationUpdate Record(ConversationUpdate update)
    {
        Intents.AddRange(update.Intents);
        return update;
    }

    internal ConversationIntent Single(ConversationUpdate update, ConversationIntentKind kind) =>
        Assert.Single(update.Intents, intent => intent.Kind == kind);

    /// <summary>Offers and accepts an initiated seed, producing an active initiated thread.</summary>
    internal ConversationThread StartInitiatedThread(string topic = "interest.clouds", double seconds = 15, bool sensitive = false)
    {
        Record(Coordinator.AddInitiatedSeed(topic, 0.7, sensitive, At(seconds - 1)));
        var tick = Record(Coordinator.Tick(At(seconds)));
        var offer = Single(tick, ConversationIntentKind.OfferOpening);
        var accepted = Record(Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive, offer.ReferenceId), At(seconds + 1)));
        Single(accepted, ConversationIntentKind.ThreadStarted);
        return accepted.Snapshot.Thread!;
    }
}
