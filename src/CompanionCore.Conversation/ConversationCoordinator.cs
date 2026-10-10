using System.Buffers.Binary;
using System.Security.Cryptography;
using CompanionCore.Attention;

namespace CompanionCore.Conversation;

/// <summary>
/// Owns exactly one Conversation Thread and the two Seed Banks. Deterministic: time comes
/// only from call timestamps, identifiers from a counter, and chance from an injected
/// source. Non-response is strictly neutral: the engagement profile only ever grows from
/// substantive engagement, and nothing records disappointment or preference loss.
/// </summary>
public sealed class ConversationCoordinator
{
    internal const int CheckpointVersion = 1;
    private const int MaximumTopicCharacters = 256;

    private readonly object _gate = new();
    private readonly ConversationConfiguration _config;
    private readonly IExpressionChance _chance;
    private readonly Guid _coordinatorId;
    private readonly List<ConversationSeed> _gameSeeds = [];
    private readonly List<ConversationSeed> _initiatedSeeds = [];
    private readonly List<AmbientExpression> _ambient = [];
    private readonly Dictionary<string, int> _profile = new(StringComparer.Ordinal);

    private long _idCounter;
    private DateTimeOffset _now;
    private DateTimeOffset _start;
    private AttentionState _attention = AttentionState.Noticing;
    private ConversationThread? _thread;
    private PendingOffer? _offer;
    private DateTimeOffset _nextInitiatedCheck;
    private DateTimeOffset? _urgentAt;
    private DateTimeOffset? _lastBrainFart;
    private DateTimeOffset? _lastGameCheckAt;

    public ConversationCoordinator(
        Guid coordinatorId,
        DateTimeOffset startUtc,
        IExpressionChance chance,
        ConversationConfiguration? configuration = null)
    {
        if (coordinatorId == Guid.Empty || startUtc == default)
        {
            throw new ArgumentException("A coordinator needs an identity and a start time.");
        }

        _chance = chance ?? throw new ArgumentNullException(nameof(chance));
        _config = configuration ?? ConversationConfiguration.Default;
        _config.Validate();
        _coordinatorId = coordinatorId;
        _now = startUtc;
        _start = startUtc;
        _nextInitiatedCheck = startUtc + _config.InitiatedClockOffset;
    }

    public ConversationSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return Snapshot();
            }
        }
    }

    /// <summary>Records the attention state; urgency clearing offers a suspended thread back.</summary>
    public ConversationUpdate ObserveAttention(AttentionState state, DateTimeOffset now)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            _attention = state;
            MaybeOfferResume(intents);
            return Update(intents);
        }
    }

    /// <summary>One meaningful semantic scan: the game-observation clock.</summary>
    public ConversationUpdate OnSemanticScan(GameObservation observation, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ValidateTopic(observation.TopicKey);
        ValidateProbability(observation.Significance);
        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            if (observation.Significance >= _config.GameSeedThreshold)
            {
                Bank(SeedBank.Game, SeedOrigin.Observation, observation.TopicKey, observation.Significance, observation.Sensitive, null, intents);
            }

            GameCheck(observation, intents);
            return Update(intents);
        }
    }

    /// <summary>Attention's Afterglow opening: always produces one contextual game opening.</summary>
    public ConversationUpdate OnAfterglowOpening(string topicKey, DateTimeOffset now)
    {
        ValidateTopic(topicKey);
        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            var seed = Bank(SeedBank.Game, SeedOrigin.Afterglow, topicKey, _config.AfterglowSeedValue, false, null, intents);
            if (seed is not null && _thread is null && _offer is null)
            {
                Offer(seed, intents);
            }

            return Update(intents);
        }
    }

    /// <summary>Adds an initiated-conversation seed (locally derived from roots/interests).</summary>
    public ConversationUpdate AddInitiatedSeed(string topicKey, double value, bool sensitive, DateTimeOffset now)
    {
        ValidateTopic(topicKey);
        ValidateProbability(value);
        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            Bank(SeedBank.Initiated, SeedOrigin.InitiatedRoot, topicKey, value, sensitive, null, intents);
            return Update(intents);
        }
    }

    /// <summary>Advances clocks: offer expiry, idle settling, resume offers, and the initiated clock.</summary>
    public ConversationUpdate Tick(DateTimeOffset now)
    {
        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            MaybeOfferResume(intents);
            if (_now >= _nextInitiatedCheck)
            {
                // Bounded catch-up: at most one check per tick, however long the gap.
                var behind = (_now - _nextInitiatedCheck).Ticks / _config.InitiatedClockInterval.Ticks;
                _nextInitiatedCheck += TimeSpan.FromTicks((behind + 1) * _config.InitiatedClockInterval.Ticks);
                InitiatedCheck(intents);
            }

            return Update(intents);
        }
    }

    /// <summary>An explicit user turn: answers an offer, promotes an ambient expression, or continues the thread.</summary>
    public ConversationUpdate OnUserTurn(UserTurn turn, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(turn);
        if (!Enum.IsDefined(turn.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(turn));
        }

        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            if (turn.ReferenceId is { } reference)
            {
                if (_offer is { } offer && offer.OfferId == reference)
                {
                    AcceptOffer(offer, intents);
                    Engage(turn.Kind);
                    return Update(intents);
                }

                var ambient = _ambient.FirstOrDefault(expression => expression.ExpressionId == reference);
                if (ambient is null)
                {
                    return Update(intents, ConversationRefusal.UnknownReference);
                }

                if (_thread is { Locked: true })
                {
                    return Update(intents, ConversationRefusal.Locked);
                }

                _ambient.Remove(ambient);
                if (_thread is not null)
                {
                    Settle(SettleReason.Replaced, intents);
                }

                StartThread(ThreadKind.Game, ambient.TopicKey, sensitive: false, intents);
                Engage(turn.Kind);
                return Update(intents);
            }

            if (_thread is not { Status: ThreadStatus.Active })
            {
                return Update(intents, ConversationRefusal.NoActiveThread);
            }

            Engage(turn.Kind);
            return Update(intents);
        }
    }

    /// <summary>Boss explicitly starts a game conversation.</summary>
    public ConversationUpdate StartGameConversation(string topicKey, bool sensitive, DateTimeOffset now)
    {
        ValidateTopic(topicKey);
        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            if (_thread is { Locked: true })
            {
                return Update(intents, ConversationRefusal.Locked);
            }

            if (_thread is not null)
            {
                Settle(SettleReason.Replaced, intents);
            }

            WithdrawOffer();
            StartThread(ThreadKind.Game, topicKey, sensitive, intents);
            return Update(intents);
        }
    }

    /// <summary>"Keep this thought": prevents replacement and automatic settling.</summary>
    public ConversationUpdate SetLock(bool locked, DateTimeOffset now)
    {
        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            if (_thread is null)
            {
                return Update(intents, ConversationRefusal.NoActiveThread);
            }

            if (_thread.Locked != locked)
            {
                _thread = _thread with { Locked = locked };
                intents.Add(new ConversationIntent(
                    locked ? ConversationIntentKind.ThreadLocked : ConversationIntentKind.ThreadUnlocked,
                    _thread.TopicKey,
                    _thread.ThreadId));
            }

            return Update(intents);
        }
    }

    /// <summary>Boss closes the thread; a finished conversation never becomes a seed.</summary>
    public ConversationUpdate Close(bool finished, DateTimeOffset now)
    {
        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            if (_thread is null)
            {
                return Update(intents, ConversationRefusal.NoActiveThread);
            }

            Settle(finished ? SettleReason.Finished : SettleReason.Closed, intents);
            return Update(intents);
        }
    }

    /// <summary>An urgent or overwhelming event.</summary>
    public ConversationUpdate OnUrgent(string topicKey, DateTimeOffset now)
    {
        ValidateTopic(topicKey);
        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            _urgentAt = _now;
            if (_thread is { Locked: false, Status: ThreadStatus.Active } thread)
            {
                _thread = thread with { Status = ThreadStatus.Suspended };
                WithdrawOffer();
                intents.Add(new ConversationIntent(ConversationIntentKind.HoldThought, thread.TopicKey, thread.ThreadId, ThreadKind: thread.Kind));
            }
            else
            {
                // A locked or absent thread is never overwritten; the alert is ambient.
                AddAmbient(AmbientKind.UrgentAlert, topicKey, intents);
            }

            return Update(intents);
        }
    }

    /// <summary>A Prince-initiated follow-up question; refused once curiosity is exhausted.</summary>
    public ConversationUpdate RequestFollowUp(DateTimeOffset now)
    {
        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            if (_thread is not { Status: ThreadStatus.Active } thread)
            {
                return Update(intents, ConversationRefusal.NoActiveThread);
            }

            if (thread.FollowUps >= _config.MaximumFollowUps)
            {
                return Update(intents, ConversationRefusal.FollowUpsSaturated);
            }

            _thread = thread with { FollowUps = thread.FollowUps + 1, LastActivity = _now };
            intents.Add(new ConversationIntent(ConversationIntentKind.FollowUp, thread.TopicKey, thread.ThreadId));
            return Update(intents);
        }
    }

    /// <summary>A rare relaxed associative detour. Never during urgency or sensitive discussion; never a seed.</summary>
    public ConversationUpdate TryBrainFart(DateTimeOffset now)
    {
        lock (_gate)
        {
            var intents = new List<ConversationIntent>();
            Advance(now, intents);
            var relaxed = _thread is { Status: ThreadStatus.Active, Sensitive: false }
                && _attention == AttentionState.Noticing
                && _urgentAt is null
                && (_lastBrainFart is null || _now - _lastBrainFart >= _config.BrainFartCooldown);
            if (!relaxed || _chance.Next() >= _config.BrainFartChance)
            {
                return Update(intents, ConversationRefusal.NotAllowed);
            }

            _lastBrainFart = _now;
            intents.Add(new ConversationIntent(ConversationIntentKind.BrainFart, _thread!.TopicKey, _thread.ThreadId));
            return Update(intents);
        }
    }

    public ConversationCheckpoint Checkpoint()
    {
        lock (_gate)
        {
            return new ConversationCheckpoint
            {
                Version = CheckpointVersion,
                CoordinatorId = _coordinatorId,
                IdCounter = _idCounter,
                Start = _start,
                Now = _now,
                Attention = _attention,
                Thread = _thread,
                GameSeeds = [.. _gameSeeds],
                InitiatedSeeds = [.. _initiatedSeeds],
                Offer = _offer,
                Ambient = [.. _ambient],
                Profile = new Dictionary<string, int>(_profile, StringComparer.Ordinal),
                NextInitiatedCheck = _nextInitiatedCheck,
                UrgentAt = _urgentAt,
                LastBrainFart = _lastBrainFart,
                LastGameCheckAt = _lastGameCheckAt,
            };
        }
    }

    public static ConversationCoordinator Restore(
        ConversationCheckpoint checkpoint,
        IExpressionChance chance,
        ConversationConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        checkpoint.Validate(configuration ?? ConversationConfiguration.Default);
        var coordinator = new ConversationCoordinator(checkpoint.CoordinatorId, checkpoint.Start, chance, configuration)
        {
            _idCounter = checkpoint.IdCounter,
            _now = checkpoint.Now,
            _attention = checkpoint.Attention,
            _thread = checkpoint.Thread,
            _offer = checkpoint.Offer,
            _nextInitiatedCheck = checkpoint.NextInitiatedCheck,
            _urgentAt = checkpoint.UrgentAt,
            _lastBrainFart = checkpoint.LastBrainFart,
            _lastGameCheckAt = checkpoint.LastGameCheckAt,
        };
        coordinator._gameSeeds.AddRange(checkpoint.GameSeeds);
        coordinator._initiatedSeeds.AddRange(checkpoint.InitiatedSeeds);
        coordinator._ambient.AddRange(checkpoint.Ambient);
        foreach (var pair in checkpoint.Profile)
        {
            coordinator._profile[pair.Key] = pair.Value;
        }

        return coordinator;
    }

    private void Advance(DateTimeOffset now, List<ConversationIntent> intents)
    {
        if (now == default)
        {
            throw new ArgumentException("A timestamp is required.", nameof(now));
        }

        if (now > _now)
        {
            _now = now;
        }

        _ambient.RemoveAll(expression => expression.ExpiresAt <= _now);
        if (_offer is { } offer && _now >= offer.ExpiresAt)
        {
            ExpireOffer(offer, intents);
        }

        if (_thread is { Status: ThreadStatus.Active, Locked: false } thread
            && _now - thread.LastActivity >= _config.ThreadIdleTimeout)
        {
            Settle(SettleReason.Idle, intents);
        }
    }

    private void GameCheck(GameObservation observation, List<ConversationIntent> intents)
    {
        // A game check claims the shared expression gate for this instant, spoken or not.
        _lastGameCheckAt = _now;
        switch (_attention)
        {
            case AttentionState.HighAttention:
                // Frequent event-driven support; ambient never creates a thread.
                if (_chance.Next() < _config.CommentaryChanceHigh)
                {
                    AddAmbient(observation.PlayerStruggling ? AmbientKind.PepTalk : AmbientKind.Commentary, observation.TopicKey, intents);
                }

                return;

            default:
                if (_thread is not null || _offer is not null)
                {
                    return;
                }

                var chance = _attention switch
                {
                    AttentionState.Engaged => _config.GameChanceEngaged,
                    AttentionState.Afterglow => _config.GameChanceAfterglow,
                    _ => _config.GameChanceNoticing,
                };
                var seed = Best(_gameSeeds);
                if (seed is not null && _chance.Next() < chance)
                {
                    Offer(seed, intents);
                }

                return;
        }
    }

    private void InitiatedCheck(List<ConversationIntent> intents)
    {
        // Initiated openings never compete with a thread, a pending offer, high attention,
        // Afterglow, or a game check at this same instant (game context is favored).
        if (_thread is not null
            || _offer is not null
            || _attention is AttentionState.HighAttention or AttentionState.Afterglow
            || _lastGameCheckAt == _now)
        {
            return;
        }

        var seed = Best(_initiatedSeeds);
        var chance = _attention == AttentionState.Engaged ? _config.InitiatedChanceEngaged : _config.InitiatedChanceNoticing;
        if (seed is not null && _chance.Next() < chance)
        {
            Offer(seed, intents);
        }
    }

    private void MaybeOfferResume(List<ConversationIntent> intents)
    {
        if (_thread is not { Status: ThreadStatus.Suspended } thread
            || _offer is not null
            || _attention is AttentionState.HighAttention or AttentionState.Afterglow
            || _urgentAt is { } urgentAt && _now - urgentAt < _config.ResumeDelay)
        {
            return;
        }

        _urgentAt = null;
        _offer = new PendingOffer(NextId(), OfferKind.Resume, null, thread.ThreadId, thread.TopicKey, _now + _config.OfferTimeout, thread.Sensitive);
        intents.Add(new ConversationIntent(
            ConversationIntentKind.ResumeOffered,
            thread.TopicKey,
            thread.ThreadId,
            ReferenceId: _offer.OfferId,
            ThreadKind: thread.Kind,
            AskFirst: thread.Sensitive));
    }

    private void Offer(ConversationSeed seed, List<ConversationIntent> intents)
    {
        var updated = seed with { Presentations = seed.Presentations + 1 };
        Replace(seed, updated);
        _offer = new PendingOffer(NextId(), OfferKind.Opening, seed.SeedId, null, seed.TopicKey, _now + _config.OfferTimeout, seed.Sensitive);
        intents.Add(new ConversationIntent(
            ConversationIntentKind.OfferOpening,
            seed.TopicKey,
            SeedId: seed.SeedId,
            ReferenceId: _offer.OfferId,
            Bank: seed.Bank,
            AskFirst: seed.Sensitive));
    }

    private void AcceptOffer(PendingOffer offer, List<ConversationIntent> intents)
    {
        _offer = null;
        if (offer.Kind == OfferKind.Resume)
        {
            if (_thread is { Status: ThreadStatus.Suspended } suspended && suspended.ThreadId == offer.ThreadId)
            {
                _thread = suspended with { Status = ThreadStatus.Active, LastActivity = _now };
                intents.Add(new ConversationIntent(ConversationIntentKind.ThreadResumed, suspended.TopicKey, suspended.ThreadId, ThreadKind: suspended.Kind));
            }

            return;
        }

        var seed = FindSeed(offer.SeedId!.Value);
        if (seed is null)
        {
            return;
        }

        RemoveSeed(seed);
        if (seed.ResumableThread is { } resumable)
        {
            _thread = resumable with { Status = ThreadStatus.Active, Locked = false, LastActivity = _now };
            intents.Add(new ConversationIntent(ConversationIntentKind.ThreadResumed, resumable.TopicKey, resumable.ThreadId, ThreadKind: resumable.Kind));
            return;
        }

        StartThread(seed.Bank == SeedBank.Game ? ThreadKind.Game : ThreadKind.Initiated, seed.TopicKey, seed.Sensitive, intents);
    }

    private void ExpireOffer(PendingOffer offer, List<ConversationIntent> intents)
    {
        _offer = null;
        if (offer.Kind == OfferKind.Resume)
        {
            // An unanswered resume offer settles the thread quietly; silence is neutral.
            if (_thread is { Status: ThreadStatus.Suspended } suspended && suspended.ThreadId == offer.ThreadId)
            {
                Settle(SettleReason.ResumeNotTaken, intents);
            }

            return;
        }

        // Non-response: one neutral presentation; after the limit the seed leaves the pool.
        if (FindSeed(offer.SeedId!.Value) is { } seed && seed.Presentations >= _config.MaximumPresentations)
        {
            RemoveSeed(seed);
            intents.Add(new ConversationIntent(
                ConversationIntentKind.SeedRetired,
                seed.TopicKey,
                SeedId: seed.SeedId,
                Bank: seed.Bank,
                Retirement: SeedRetirement.PresentedWithoutResponse));
        }
    }

    private void WithdrawOffer()
    {
        _offer = null;
    }

    private void StartThread(ThreadKind kind, string topic, bool sensitive, List<ConversationIntent> intents)
    {
        _thread = new ConversationThread(NextId(), kind, topic, ThreadStatus.Active, false, sensitive, 0, 0, _now, _now);
        intents.Add(new ConversationIntent(ConversationIntentKind.ThreadStarted, topic, _thread.ThreadId, ThreadKind: kind));
    }

    private void Engage(UserTurnKind kind)
    {
        if (_thread is not { Status: ThreadStatus.Active } thread)
        {
            return;
        }

        if (kind == UserTurnKind.Substantive)
        {
            _thread = thread with { SubstantiveTurns = thread.SubstantiveTurns + 1, LastActivity = _now };
            _profile[thread.TopicKey] = _profile.GetValueOrDefault(thread.TopicKey) + 1;
        }
        else
        {
            _thread = thread with { LastActivity = _now };
        }
    }

    private void Settle(SettleReason reason, List<ConversationIntent> intents)
    {
        var thread = _thread!;
        _thread = null;
        _urgentAt = null;
        if (_offer is { Kind: OfferKind.Resume })
        {
            _offer = null;
        }

        intents.Add(new ConversationIntent(ConversationIntentKind.ThreadSettled, thread.TopicKey, thread.ThreadId, ThreadKind: thread.Kind, SettleReason: reason));
        if (reason != SettleReason.Finished && thread.SubstantiveTurns >= _config.MinimumSubstantiveTurns)
        {
            var seed = Bank(
                thread.Kind == ThreadKind.Game ? SeedBank.Game : SeedBank.Initiated,
                SeedOrigin.SettledConversation,
                thread.TopicKey,
                _config.SettledSeedValue,
                thread.Sensitive,
                thread with { Status = ThreadStatus.Suspended, Locked = false },
                intents);

            // An unanswered resume offer was itself one neutral presentation.
            if (seed is not null && reason == SettleReason.ResumeNotTaken)
            {
                Replace(seed, seed with { Presentations = seed.Presentations + 1 });
            }
        }
    }

    private ConversationSeed? Bank(
        SeedBank bank,
        SeedOrigin origin,
        string topic,
        double value,
        bool sensitive,
        ConversationThread? resumable,
        List<ConversationIntent> intents)
    {
        var pool = bank == SeedBank.Game ? _gameSeeds : _initiatedSeeds;
        var existing = pool.FirstOrDefault(seed => string.Equals(seed.TopicKey, topic, StringComparison.Ordinal));
        if (existing is not null)
        {
            // Deduplicated by topic: keep the higher value and any resumable thread.
            var merged = existing with
            {
                Value = Math.Max(existing.Value, value),
                UpdatedAt = _now,
                Sensitive = existing.Sensitive || sensitive,
                ResumableThread = resumable ?? existing.ResumableThread,
                Origin = resumable is not null ? origin : existing.Origin,
            };
            Replace(existing, merged);
            return merged;
        }

        if (pool.Count >= _config.MaximumSeedsPerBank)
        {
            var weakest = pool.OrderBy(seed => seed.Value).ThenBy(seed => seed.UpdatedAt).First();
            if (weakest.Value >= value)
            {
                intents.Add(new ConversationIntent(ConversationIntentKind.SeedRetired, topic, Bank: bank, Retirement: SeedRetirement.Rejected));
                return null;
            }

            pool.Remove(weakest);
            if (_offer is { } offer && offer.SeedId == weakest.SeedId)
            {
                _offer = null;
            }

            intents.Add(new ConversationIntent(ConversationIntentKind.SeedRetired, weakest.TopicKey, SeedId: weakest.SeedId, Bank: bank, Retirement: SeedRetirement.Evicted));
        }

        var seed = new ConversationSeed(NextId(), bank, origin, topic, value, _now, 0, sensitive, resumable);
        pool.Add(seed);
        intents.Add(new ConversationIntent(ConversationIntentKind.SeedBanked, topic, SeedId: seed.SeedId, Bank: bank));
        return seed;
    }

    private void AddAmbient(AmbientKind kind, string topic, List<ConversationIntent> intents)
    {
        var expression = new AmbientExpression(NextId(), kind, topic, _now + _config.AmbientPromotionWindow);
        _ambient.Add(expression);
        if (_ambient.Count > _config.MaximumAmbientExpressions)
        {
            _ambient.RemoveAt(0);
        }

        intents.Add(new ConversationIntent(ConversationIntentKind.AmbientExpression, topic, ReferenceId: expression.ExpressionId, Ambient: kind));
    }

    /// <summary>
    /// Highest-value candidate. A conversation that settled at this very instant is never
    /// offered straight back.
    /// </summary>
    private ConversationSeed? Best(List<ConversationSeed> pool) =>
        pool.Where(seed => !(seed.Origin == SeedOrigin.SettledConversation && seed.UpdatedAt == _now))
            .OrderByDescending(seed => seed.Value)
            .ThenBy(seed => seed.Presentations)
            .ThenBy(seed => seed.UpdatedAt)
            .ThenBy(seed => seed.SeedId)
            .FirstOrDefault();

    private ConversationSeed? FindSeed(Guid seedId) =>
        _gameSeeds.FirstOrDefault(seed => seed.SeedId == seedId)
        ?? _initiatedSeeds.FirstOrDefault(seed => seed.SeedId == seedId);

    private void Replace(ConversationSeed existing, ConversationSeed updated)
    {
        var pool = existing.Bank == SeedBank.Game ? _gameSeeds : _initiatedSeeds;
        pool[pool.IndexOf(existing)] = updated;
    }

    private void RemoveSeed(ConversationSeed seed)
    {
        (seed.Bank == SeedBank.Game ? _gameSeeds : _initiatedSeeds).RemoveAll(item => item.SeedId == seed.SeedId);
    }

    /// <summary>Deterministic identifiers derived from the coordinator identity and a counter.</summary>
    private Guid NextId()
    {
        _idCounter++;
        Span<byte> input = stackalloc byte[24];
        _coordinatorId.TryWriteBytes(input, bigEndian: true, out _);
        BinaryPrimitives.WriteInt64BigEndian(input[16..], _idCounter);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        var bytes = hash[..16].ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private ConversationUpdate Update(List<ConversationIntent> intents, ConversationRefusal refusal = ConversationRefusal.None) =>
        new(intents, Snapshot(), refusal);

    private ConversationSnapshot Snapshot() =>
        new(
            _thread,
            [.. _gameSeeds],
            [.. _initiatedSeeds],
            _offer,
            new Dictionary<string, int>(_profile, StringComparer.Ordinal),
            _attention,
            _now);

    internal static void ValidateTopic(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic) || topic.Length > MaximumTopicCharacters)
        {
            throw new ArgumentException("Topic keys must be non-blank and bounded.", nameof(topic));
        }
    }

    private static void ValidateProbability(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Values must be finite and within [0, 1].");
        }
    }
}
