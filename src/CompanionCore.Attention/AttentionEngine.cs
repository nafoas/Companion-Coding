using CompanionCore.Capture.Contracts;

namespace CompanionCore.Attention;

/// <summary>
/// Deterministic attention state machine for one target session. Time advances only
/// through event and <see cref="Advance"/> timestamps, so identical input streams always
/// produce identical updates. The engine emits typed abstract intents and never touches
/// capture, presentation, conversation, memory, or I/O.
/// </summary>
public sealed class AttentionEngine
{
    private const int MaximumDecaySteps = 1000;
    private const int MaximumTransitions = 1024;
    private const int MaximumHeldPerTopic = 16;

    private readonly object _gate = new();
    private readonly AttentionConfiguration _config;
    private readonly Dictionary<string, DateTimeOffset> _transitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<HeldEvidence>> _held = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Habituation> _habituation = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LocationEntry> _locations = new(StringComparer.Ordinal);
    private readonly HashSet<string> _escalationTopics = new(StringComparer.Ordinal);

    private double _score;
    private DateTimeOffset _now;
    private AttentionState _state = AttentionState.Noticing;
    private DateTimeOffset _stateSince;
    private DateTimeOffset _quietSince;
    private DateTimeOffset? _afterglowUntil;
    private DateTimeOffset _highEnteredAt;
    private double _episodePeak;
    private bool _urgentInEpisode;
    private string? _episodeLocation;
    private string? _lastTopic;
    private AttentionRegionKind _lastRegionKind = AttentionRegionKind.FullContext;
    private string? _currentLocation;

    public AttentionEngine(Guid targetSessionId, DateTimeOffset startUtc, AttentionConfiguration? configuration = null)
    {
        if (targetSessionId == Guid.Empty || startUtc == default)
        {
            throw new ArgumentException("An attention engine needs a target session and a start time.");
        }

        _config = configuration ?? AttentionConfiguration.Default;
        _config.Validate();
        TargetSessionId = targetSessionId;
        _now = startUtc;
        _stateSince = startUtc;
        _quietSince = startUtc;
    }

    public Guid TargetSessionId { get; }

    public AttentionSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return Snapshot();
            }
        }
    }

    internal int TrackedTopicCount
    {
        get
        {
            lock (_gate)
            {
                return _habituation.Count;
            }
        }
    }

    internal int TrackedLocationCount
    {
        get
        {
            lock (_gate)
            {
                return _locations.Count;
            }
        }
    }

    internal double HabituationOf(string topic)
    {
        lock (_gate)
        {
            return CurrentHabituation(topic, _now);
        }
    }

    public LocationFamiliarity FamiliarityOf(string locationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationKey);
        lock (_gate)
        {
            return _locations.TryGetValue(locationKey, out var entry) ? entry.Familiarity : LocationFamiliarity.Unknown;
        }
    }

    /// <summary>Applies one piece of evidence.</summary>
    public AttentionUpdate Observe(InterestEvent interestEvent)
    {
        ArgumentNullException.ThrowIfNull(interestEvent);
        interestEvent.Validate();
        lock (_gate)
        {
            var previous = _state;
            var intents = new List<AttentionIntent>();
            if (interestEvent.TargetSessionId != TargetSessionId)
            {
                return new AttentionUpdate(
                    Snapshot(),
                    previous,
                    intents,
                    Record(interestEvent, AttentionEventDisposition.WrongSession, 0));
            }

            // Late evidence is applied at the current time; time never moves backwards.
            AdvanceTo(Max(interestEvent.Timestamp, _now), intents);
            var now = _now;
            var urgent = interestEvent.Signals.Urgency >= _config.UrgentThreshold;
            var location = interestEvent.LocationKey ?? _currentLocation;
            if (interestEvent.LocationKey is { } locationKey)
            {
                TouchLocation(locationKey, now);
                if (!string.Equals(_currentLocation, locationKey, StringComparison.Ordinal))
                {
                    _currentLocation = locationKey;
                    _locations[locationKey].CurrentSince = now;
                }
            }

            if (urgent && location is not null && _locations.TryGetValue(location, out var urgentLocation))
            {
                urgentLocation.LastUrgent = now;
            }

            double contribution;
            AttentionEventDisposition disposition;
            switch (interestEvent.Kind)
            {
                case AttentionEventKind.Decisive:
                    var target = Math.Min(100, _config.HighEnter + _config.DecisiveMargin);
                    contribution = Math.Max(0, target - _score);
                    disposition = AttentionEventDisposition.Applied;
                    break;

                case AttentionEventKind.GlobalTransition:
                    PruneTransitions(now);
                    if (_transitions.ContainsKey(interestEvent.TransitionKey!))
                    {
                        contribution = 0;
                        disposition = AttentionEventDisposition.DuplicateTransition;
                    }
                    else
                    {
                        _transitions[interestEvent.TransitionKey!] = now;
                        contribution = Math.Min(_config.TransitionContributionCap, BaseContribution(interestEvent.Signals));
                        disposition = AttentionEventDisposition.Applied;
                    }

                    break;

                default:
                    contribution = BaseContribution(interestEvent.Signals) * Factor(interestEvent, urgent, location, now);
                    var weak = !urgent
                        && (interestEvent.Signals.Confidence < _config.WeakConfidence
                            || contribution < _config.WeakContribution);
                    if (weak)
                    {
                        (contribution, disposition) = HoldOrCorroborate(interestEvent, contribution, now);
                    }
                    else
                    {
                        disposition = AttentionEventDisposition.Applied;
                    }

                    // Every harmless exposure habituates, whether or not it moved the score.
                    if (!urgent && interestEvent.TopicKey is { } topic)
                    {
                        Habituate(topic, now);
                    }

                    break;
            }

            if (contribution > 0)
            {
                _score = Math.Min(100, _score + contribution);
                _lastRegionKind = interestEvent.RegionKind;
                if (interestEvent.TopicKey is { } escalationTopic)
                {
                    _escalationTopics.Add(escalationTopic);
                    _lastTopic = escalationTopic;
                }

                if (urgent)
                {
                    _urgentInEpisode = true;
                    _episodeLocation = location ?? _episodeLocation;
                    intents.Add(new AttentionIntent(AttentionIntentKind.Urgent, interestEvent.TopicKey, [interestEvent.RegionKind]));
                }
                else if (_state == AttentionState.Noticing)
                {
                    intents.Add(new AttentionIntent(AttentionIntentKind.Observing, interestEvent.TopicKey, [interestEvent.RegionKind]));
                }
            }

            Evaluate(intents);
            return new AttentionUpdate(Snapshot(), previous, intents, Record(interestEvent, disposition, contribution));
        }
    }

    /// <summary>Advances time, applying decay, dwell, Afterglow expiry, and location learning.</summary>
    public AttentionUpdate Advance(DateTimeOffset now)
    {
        lock (_gate)
        {
            var previous = _state;
            var intents = new List<AttentionIntent>();
            AdvanceTo(Max(now, _now), intents);
            return new AttentionUpdate(Snapshot(), previous, intents, null);
        }
    }

    /// <summary>
    /// "All clear": ends a false alarm promptly without Afterglow and gently lowers only
    /// the triggering topics' local profile. Urgent evidence is never habituated.
    /// </summary>
    public AttentionUpdate CorrectFalseAlarm(DateTimeOffset now)
    {
        lock (_gate)
        {
            var previous = _state;
            var intents = new List<AttentionIntent>();
            AdvanceTo(Max(now, _now), intents);
            if (_state == AttentionState.Noticing)
            {
                return new AttentionUpdate(Snapshot(), previous, intents, null);
            }

            foreach (var topic in _escalationTopics)
            {
                var entry = GetOrCreateHabituation(topic, _now);
                entry.Factor = Math.Max(_config.HabituationFloor, entry.Factor * _config.FalseAlarmPenalty);
            }

            _held.Clear();
            _score = Math.Min(_score, _config.FalseAlarmScoreCeiling);
            intents.Add(new AttentionIntent(AttentionIntentKind.AllClear, _lastTopic));
            EnterNoticing(intents, announce: false);
            return new AttentionUpdate(Snapshot(), previous, intents, null);
        }
    }

    /// <summary>Explicit familiarity (for example from a semantic "safe town" reading); learning never overrides it.</summary>
    public void SetLocationFamiliarity(string locationKey, LocationFamiliarity familiarity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationKey);
        if (locationKey.Length > InterestEvent.MaximumKeyCharacters || !Enum.IsDefined(familiarity))
        {
            throw new ArgumentException("Invalid location familiarity.");
        }

        lock (_gate)
        {
            var entry = TouchLocation(locationKey, _now);
            entry.Familiarity = familiarity;
            entry.Explicit = true;
        }
    }

    /// <summary>
    /// Integrates decay in bounded steps and evaluates state after each step, so dwell,
    /// exit, Afterglow expiry, and location learning happen when the score actually
    /// crosses rather than only at the end of a long gap.
    /// </summary>
    private void AdvanceTo(DateTimeOffset target, List<AttentionIntent> intents)
    {
        if (target > _now)
        {
            var start = _now;
            var totalTicks = (target - start).Ticks;
            var steps = (int)Math.Clamp(Math.Ceiling((target - start).TotalSeconds), 1, MaximumDecaySteps);
            for (var index = 1; index <= steps; index++)
            {
                var stepEnd = index == steps
                    ? target
                    : start + TimeSpan.FromTicks(totalTicks * index / steps);
                DecayStep(stepEnd - _now);
                _now = stepEnd;
                Evaluate(intents);
                LearnClearedLocation();
            }

            return;
        }

        Evaluate(intents);
        LearnClearedLocation();
    }

    private void DecayStep(TimeSpan step)
    {
        if (_score <= 0 || step <= TimeSpan.Zero)
        {
            return;
        }

        var halfLife = _config.BaseHalfLife.TotalSeconds + (_config.HalfLifePerScorePoint.TotalSeconds * _score);
        _score *= Math.Pow(0.5, step.TotalSeconds / halfLife);
        if (_score < 0.01)
        {
            _score = 0;
        }
    }

    private void Evaluate(List<AttentionIntent> intents)
    {
        switch (_state)
        {
            case AttentionState.Noticing:
                if (_score >= _config.HighEnter)
                {
                    EnterHigh(intents);
                }
                else if (_score >= _config.EngagedEnter)
                {
                    SetState(AttentionState.Engaged);
                    intents.Add(new AttentionIntent(AttentionIntentKind.Investigating, _lastTopic, [_lastRegionKind]));
                }

                break;

            case AttentionState.Engaged:
                if (_score >= _config.HighEnter)
                {
                    EnterHigh(intents);
                }
                else if (_score < _config.EngagedExit)
                {
                    EnterNoticing(intents, announce: true);
                }

                break;

            case AttentionState.HighAttention:
                _episodePeak = Math.Max(_episodePeak, _score);
                if (_score < _config.HighExit && _now - _highEnteredAt >= _config.HighMinimumDwell)
                {
                    ExitHigh(intents);
                }

                break;

            case AttentionState.Afterglow:
                if (_score >= _config.HighEnter)
                {
                    EnterHigh(intents);
                }
                else if (_afterglowUntil is { } until && _now >= until)
                {
                    _afterglowUntil = null;
                    if (_score >= _config.EngagedExit)
                    {
                        SetState(AttentionState.Engaged);
                    }
                    else
                    {
                        EnterNoticing(intents, announce: true);
                    }
                }

                break;
        }
    }

    private void EnterHigh(List<AttentionIntent> intents)
    {
        SetState(AttentionState.HighAttention);
        _afterglowUntil = null;
        _highEnteredAt = _now;
        _episodePeak = _score;
        AttentionRegionKind[] focus = _lastRegionKind == AttentionRegionKind.FullContext
            ? [AttentionRegionKind.FullContext]
            : [AttentionRegionKind.FullContext, _lastRegionKind];
        intents.Add(new AttentionIntent(AttentionIntentKind.HighAttentionStarted, _lastTopic, focus));
    }

    private void ExitHigh(List<AttentionIntent> intents)
    {
        var dwellSeconds = (_now - _highEnteredAt).TotalSeconds;
        var afterglow = _config.AfterglowBase
            + (_config.AfterglowPerPeakPoint * _episodePeak)
            + TimeSpan.FromSeconds(_config.AfterglowPerHighSecond * dwellSeconds);
        if (afterglow > _config.AfterglowMaximum)
        {
            afterglow = _config.AfterglowMaximum;
        }

        if (_urgentInEpisode && _episodeLocation is { } location && _locations.TryGetValue(location, out var entry) && !entry.Explicit)
        {
            entry.Familiarity = LocationFamiliarity.Hazardous;
        }

        SetState(AttentionState.Afterglow);
        _afterglowUntil = _now + afterglow;
        intents.Add(new AttentionIntent(AttentionIntentKind.HighAttentionEnded, _lastTopic));
        intents.Add(new AttentionIntent(AttentionIntentKind.AfterglowOpening, _lastTopic));
        _urgentInEpisode = false;
        _episodeLocation = null;
    }

    private void EnterNoticing(List<AttentionIntent> intents, bool announce)
    {
        SetState(AttentionState.Noticing);
        _afterglowUntil = null;
        _quietSince = _now;
        _escalationTopics.Clear();
        _urgentInEpisode = false;
        _episodeLocation = null;
        if (announce)
        {
            intents.Add(new AttentionIntent(AttentionIntentKind.ReturnedToNoticing));
        }
    }

    private void SetState(AttentionState state)
    {
        _state = state;
        _stateSince = _now;
    }

    private void LearnClearedLocation()
    {
        if (_state != AttentionState.Noticing
            || _currentLocation is null
            || !_locations.TryGetValue(_currentLocation, out var entry)
            || entry.Explicit
            || entry.Familiarity is LocationFamiliarity.Cleared or LocationFamiliarity.Safe)
        {
            return;
        }

        var quietFrom = Max(_quietSince, entry.CurrentSince);
        if (entry.LastUrgent is { } lastUrgent)
        {
            quietFrom = Max(quietFrom, lastUrgent);
        }

        if (_now - quietFrom >= _config.LocationClearedAfter)
        {
            entry.Familiarity = LocationFamiliarity.Cleared;
        }
    }

    private double BaseContribution(AttentionSignals signals)
    {
        var weightSum = _config.NoveltyWeight + _config.ChangeWeight + _config.SalienceWeight + _config.UrgencyWeight
            + _config.PersistenceWeight + _config.PersonalRelevanceWeight + _config.LoreRelevanceWeight;
        var blend = ((_config.NoveltyWeight * signals.Novelty)
            + (_config.ChangeWeight * signals.Change)
            + (_config.SalienceWeight * signals.Salience)
            + (_config.UrgencyWeight * signals.Urgency)
            + (_config.PersistenceWeight * signals.Persistence)
            + (_config.PersonalRelevanceWeight * signals.PersonalRelevance)
            + (_config.LoreRelevanceWeight * signals.LoreRelevance)) / weightSum;
        return blend * _config.MaximumContribution * signals.Confidence;
    }

    private double Factor(InterestEvent interestEvent, bool urgent, string? location, DateTimeOffset now)
    {
        var familiarity = location is not null && _locations.TryGetValue(location, out var entry)
            ? entry.Familiarity
            : LocationFamiliarity.Unknown;
        var locationFactor = familiarity switch
        {
            LocationFamiliarity.Hazardous => _config.HazardousFactor,
            LocationFamiliarity.Cleared => _config.ClearedFactor,
            LocationFamiliarity.Safe => _config.SafeFactor,
            _ => 1.0,
        };

        // Familiarity never suppresses urgent danger; hazardous ground may only raise it.
        if (urgent)
        {
            return Math.Max(1.0, locationFactor);
        }

        var habituation = interestEvent.TopicKey is { } topic ? CurrentHabituation(topic, now) : 1.0;
        return habituation * locationFactor;
    }

    private (double Contribution, AttentionEventDisposition Disposition) HoldOrCorroborate(
        InterestEvent interestEvent,
        double contribution,
        DateTimeOffset now)
    {
        var key = interestEvent.TopicKey ?? $"region:{interestEvent.RegionKind}";
        if (!_held.TryGetValue(key, out var held))
        {
            if (_held.Count >= _config.MaximumTopics)
            {
                var oldest = _held.MinBy(pair => pair.Value.Count == 0 ? DateTimeOffset.MinValue : pair.Value[^1].At).Key;
                _held.Remove(oldest);
            }

            held = [];
            _held[key] = held;
        }

        held.RemoveAll(item => now - item.At > _config.CorroborationWindow);
        held.Add(new HeldEvidence(interestEvent.EvidenceSource, contribution, now));
        if (held.Count > MaximumHeldPerTopic)
        {
            held.RemoveRange(0, held.Count - MaximumHeldPerTopic);
        }

        var sources = held.Select(item => item.Source).Distinct(StringComparer.Ordinal).Count();
        if (sources < _config.MinimumIndependentSources)
        {
            return (0, AttentionEventDisposition.HeldForCorroboration);
        }

        var corroborated = held.Sum(item => item.Contribution) * _config.CorroborationBonus;
        _held.Remove(key);
        return (corroborated, AttentionEventDisposition.Corroborated);
    }

    private void PruneTransitions(DateTimeOffset now)
    {
        foreach (var key in _transitions.Where(pair => now - pair.Value >= _config.TransitionDedupWindow).Select(pair => pair.Key).ToArray())
        {
            _transitions.Remove(key);
        }

        while (_transitions.Count >= MaximumTransitions)
        {
            _transitions.Remove(_transitions.MinBy(pair => pair.Value).Key);
        }
    }

    private double CurrentHabituation(string topic, DateTimeOffset now) =>
        _habituation.TryGetValue(topic, out var entry) ? Recovered(entry, now) : 1.0;

    private double Recovered(Habituation entry, DateTimeOffset now)
    {
        var elapsed = Math.Max(0, (now - entry.Updated).TotalSeconds);
        return 1 - ((1 - entry.Factor) * Math.Pow(0.5, elapsed / _config.HabituationRecoveryHalfLife.TotalSeconds));
    }

    private void Habituate(string topic, DateTimeOffset now)
    {
        var entry = GetOrCreateHabituation(topic, now);
        entry.Factor = Math.Max(_config.HabituationFloor, entry.Factor * _config.HabituationRate);
    }

    private Habituation GetOrCreateHabituation(string topic, DateTimeOffset now)
    {
        if (_habituation.TryGetValue(topic, out var entry))
        {
            entry.Factor = Recovered(entry, now);
            entry.Updated = now;
            return entry;
        }

        if (_habituation.Count >= _config.MaximumTopics)
        {
            _habituation.Remove(_habituation.MinBy(pair => pair.Value.Updated).Key);
        }

        entry = new Habituation { Factor = 1.0, Updated = now };
        _habituation[topic] = entry;
        return entry;
    }

    private LocationEntry TouchLocation(string locationKey, DateTimeOffset now)
    {
        if (_locations.TryGetValue(locationKey, out var entry))
        {
            entry.LastSeen = now;
            return entry;
        }

        if (_locations.Count >= _config.MaximumLocations)
        {
            var evict = _locations
                .Where(pair => !string.Equals(pair.Key, _currentLocation, StringComparison.Ordinal))
                .MinBy(pair => pair.Value.LastSeen)
                .Key;
            if (evict is not null)
            {
                _locations.Remove(evict);
            }
        }

        entry = new LocationEntry { LastSeen = now, CurrentSince = now };
        _locations[locationKey] = entry;
        return entry;
    }

    private AttentionSnapshot Snapshot() =>
        new(
            TargetSessionId,
            _state,
            Math.Round(_score, 6),
            _now,
            _stateSince,
            _afterglowUntil,
            _state is AttentionState.HighAttention or AttentionState.Afterglow);

    private AttentionEventRecord Record(
        InterestEvent interestEvent,
        AttentionEventDisposition disposition,
        double contribution) =>
        new(
            interestEvent.TargetSessionId,
            interestEvent.Timestamp,
            interestEvent.Region,
            interestEvent.RegionKind,
            interestEvent.Signals,
            interestEvent.TopicKey,
            interestEvent.EvidenceSource,
            interestEvent.EvidenceReferences.ToArray(),
            interestEvent.TransitionKey,
            disposition,
            Math.Round(contribution, 6),
            _state);

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left >= right ? left : right;

    private sealed record HeldEvidence(string Source, double Contribution, DateTimeOffset At);

    private sealed class Habituation
    {
        public double Factor { get; set; }

        public DateTimeOffset Updated { get; set; }
    }

    private sealed class LocationEntry
    {
        public LocationFamiliarity Familiarity { get; set; }

        public bool Explicit { get; set; }

        public DateTimeOffset LastSeen { get; set; }

        public DateTimeOffset CurrentSince { get; set; }

        public DateTimeOffset? LastUrgent { get; set; }
    }
}
