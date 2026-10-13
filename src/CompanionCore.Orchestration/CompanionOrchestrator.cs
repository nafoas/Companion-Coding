using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;
using CompanionCore.Conversation;
using CompanionCore.Keepsakes;
using CompanionCore.Memory;
using CompanionCore.Privacy;
using CompanionCore.Recall;
using CompanionCore.Sightings;
using CompanionCore.TargetAuth;
using CompanionCore.Transcript;
using CompanionCore.Vault;
using CompanionCore.Watchbun;

namespace CompanionCore.Orchestration;

/// <summary>Everything the orchestrator composes. The host owns their lifetimes.</summary>
public sealed record CompanionComponents(
    MemoryRepository Repository,
    ApiBridge Bridge,
    TargetSessionController Controller,
    RuntimePrivacyState Privacy,
    KeepsakeLocation Keepsakes,
    TranscriptLocation Transcripts,
    VaultStateStore State,
    IPlatformSignals? Platform,
    TimeProvider Time)
{
    /// <summary>The subconscious sighting record (M3a); without it, novelty is the old first-time flag.</summary>
    public SightingStore? Sightings { get; init; }

    /// <summary>His BunDex, whose remembering counts play sessions (M4a).</summary>
    public BunDex.BunDexStore? BunDex { get; init; }
}

public sealed record OrchestratorSnapshot(
    Guid? TargetSessionId,
    WatchbunSnapshot? Watchbun,
    ConversationSnapshot Conversation,
    AttentionSnapshot? Attention,
    bool PhotographPending,
    bool BridgeInFlight,
    IReadOnlyList<string> UnconsolidatedSessions,
    long Faults,
    Guid ConversationLineage)
{
    /// <summary>The local Sniff reflex: counts and arrival latency (VIS-01).</summary>
    public SniffDiagnostics Sniff { get; init; } = SniffDiagnostics.Empty;

    /// <summary>A Glance is in flight (VIS-02).</summary>
    public bool GlanceInFlight { get; init; }

    /// <summary>One closer look is waiting (VIS-02).</summary>
    public bool InspectQueued { get; init; }

    /// <summary>Attention sheets that reached the orchestrator for the current target (FIX-07).</summary>
    public long SheetsReceived { get; init; }

    /// <summary>Glances and Inspects sent to the Braincase (FIX-07).</summary>
    public long GlancesSent { get; init; }

    public long InspectsSent { get; init; }

    /// <summary>The kind of the most recent Glance or Inspect outcome, if any (FIX-07).</summary>
    public BridgeOutcomeKind? LastLookOutcome { get; init; }
}

/// <summary>
/// The Sniff reflex at a glance. Latency is measured from the worker's frame timestamp to
/// the moment the reflex applied the sniff (same machine, same wall clock).
/// </summary>
public sealed record SniffDiagnostics(
    long Received,
    long Forwarded,
    long Suppressed,
    TimeSpan LastLatency,
    TimeSpan MaximumLatency,
    TimeSpan Percentile95Latency)
{
    public static SniffDiagnostics Empty { get; } = new(0, 0, 0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

    /// <summary>Text Sniffs received and forwarded as evidence (VIS-03).</summary>
    public long TextReceived { get; init; }

    public long TextForwarded { get; init; }

    /// <summary>Scene Sniffs received and forwarded as evidence (VIS-04).</summary>
    public long SceneReceived { get; init; }

    public long SceneForwarded { get; init; }
}

/// <summary>
/// The one composition of every accepted subsystem. A single-consumer mailbox serializes
/// target events, sheets, ticks, platform signals, structured game events, and Boss
/// commands, so no component is ever re-entered or raced. Each subsystem keeps its own
/// invariants: capture stays target-only and RAM-only, memory stays append-only through
/// the local write gate, privacy revocation still fences every result, and nothing here
/// adds wording or personality.
/// </summary>
public sealed class CompanionOrchestrator : IAsyncDisposable
{
    internal const string ConversationStateName = "conversation-checkpoint";
    internal const string WatchbunStateName = "watchbun-checkpoint";
    internal const string SessionsStateName = "watchbun-sessions";
    internal const string ConsolidationStateName = "consolidation-pending";
    internal const string NeutralUrgentObservation = "[neutral urgent observation]";
    internal const string DefaultSave = "default";
    internal const string UnreadableSuffix = ".unreadable";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly CompanionComponents _c;
    private readonly OrchestratorOptions _options;
    private readonly KeepsakeCamera _camera;
    private readonly KeepsakeStore _keepsakeStore;
    private readonly Channel<WorkItem> _mailbox = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly List<string> _watchbunSessions = [];
    private readonly IExpressionChance _chance;
    private ConversationCoordinator _conversation = null!;
    private WatchbunEngine? _watchbun;
    private ActiveSession? _session;
    private CameraAction? _pendingPhoto;
    private bool _photographRequested;
    private Task? _loop;
    private bool _bridgeInFlight;
    private BridgeOutcomeKind? _lastBraincase;
    private DateTimeOffset? _lastSemanticLook;
    private DateTimeOffset _lastPersist;
    private long _faults;
    private int _disposed;
    private readonly object _publishGate = new();
    private readonly LatencyWindow _sniffLatency = new(256);
    private long _sniffsReceived;
    private long _textSniffsReceived;
    private long _sceneSniffsReceived;
    private bool _sniffTriggerPending;
    private bool _glanceInFlight;
    private long _sheetsReceived;
    private long _glancesSent;
    private long _inspectsSent;
    private BridgeOutcomeKind? _lastLookOutcome;
    private bool _inspectWanted;
    private DateTimeOffset? _nextGlanceAt;
    private DateTimeOffset? _lastGlanceAt;

    /// <summary>The agreed adaptive-cadence bounds: never more often than 2 s, never longer than 30 s.</summary>
    internal static readonly TimeSpan MinimumGlanceInterval = TimeSpan.FromSeconds(2);

    internal static readonly TimeSpan MaximumGlanceInterval = TimeSpan.FromSeconds(30);

    /// <summary>The Design BunDex: interest above 0.70 warrants a closer look.</summary>
    internal const double GlanceEscalationInterest = 0.7;
    private DateTimeOffset _lastCadenceAssert;
    internal static readonly TimeSpan CadenceReassertInterval = TimeSpan.FromSeconds(10);

    public CompanionOrchestrator(CompanionComponents components, OrchestratorOptions? options = null)
    {
        _c = components ?? throw new ArgumentNullException(nameof(components));
        _options = options ?? new OrchestratorOptions();
        _options.Validate();
        _chance = new SeededExpressionChance(_options.ExpressionSeed);
        _camera = new KeepsakeCamera(_c.Repository, _c.Privacy, _c.Keepsakes, _options.Keepsakes);
        _keepsakeStore = new KeepsakeStore(_c.Repository, _c.Keepsakes);
    }

    public event EventHandler<CompanionNotice>? Notice;

    public Exception? LastFault { get; private set; }

    public KeepsakeStore Keepsakes => _keepsakeStore;

    /// <summary>Restores persisted state, replays an interrupted consolidation, and starts the mailbox.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is not null)
        {
            throw new InvalidOperationException("The orchestrator is already started.");
        }

        var now = _c.Time.GetUtcNow();
        _conversation = await RestoreConversationAsync(now, cancellationToken).ConfigureAwait(false);
        _watchbun = await RestoreWatchbunAsync(now, cancellationToken).ConfigureAwait(false);
        _watchbunSessions.AddRange(await ReadJsonAsync<string[]>(SessionsStateName, cancellationToken).ConfigureAwait(false) ?? []);
        _lastPersist = now;

        _c.Controller.SessionEvent += OnSessionEvent;
        _c.Controller.AttentionSheetAvailable += OnSheetAvailable;
        _c.Controller.SniffAvailable += OnSniffAvailable;
        _c.Controller.TextSniffAvailable += OnTextSniffAvailable;
        _c.Controller.SceneSniffAvailable += OnSceneSniffAvailable;
        if (_c.Platform is { } platform)
        {
            platform.ForegroundChanged += OnForeground;
            platform.InputObserved += OnInput;
            platform.TargetExited += OnTargetExited;
            platform.TargetLaunched += OnTargetLaunched;
            platform.Suspended += OnSuspended;
            platform.Resumed += OnResumed;
        }

        _loop = Task.Run(LoopAsync);
        await InvokeAsync(async () =>
        {
            if (_watchbun is { } restored && restored.Current.Phase != WatchbunPhase.Closed)
            {
                Publish(new CompanionNotice(CompanionNoticeKind.Recovering, Watchbun: null, Key: restored.Current.Phase.ToString()));
                _c.Platform?.WatchForRelaunch(restored.Current.BoundTarget);
            }

            await ReplayPendingConsolidationAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public Task<OrchestratorSnapshot> GetSnapshotAsync() =>
        InvokeAsync(() => Task.FromResult(new OrchestratorSnapshot(
            _session?.Grant.TargetSessionId,
            _watchbun?.Current,
            _conversation.Current,
            _session?.Attention.Current,
            _pendingPhoto is not null,
            _bridgeInFlight,
            [.. _watchbunSessions],
            Interlocked.Read(ref _faults),
            _conversation.Checkpoint().CoordinatorId)
        {
            Sniff = SniffSnapshot(),
            GlanceInFlight = _glanceInFlight,
            InspectQueued = _inspectWanted,
            SheetsReceived = _sheetsReceived,
            GlancesSent = _glancesSent,
            InspectsSent = _inspectsSent,
            LastLookOutcome = _lastLookOutcome,
        }));

    private SniffDiagnostics SniffSnapshot()
    {
        var reflex = Volatile.Read(ref _session)?.Reflex;
        var (last, maximum, p95) = _sniffLatency.Snapshot();
        return new SniffDiagnostics(
            Interlocked.Read(ref _sniffsReceived),
            reflex?.Forwarded ?? 0,
            reflex?.Suppressed ?? 0,
            last,
            maximum,
            p95)
        {
            TextReceived = Interlocked.Read(ref _textSniffsReceived),
            TextForwarded = Volatile.Read(ref _session)?.Text.Forwarded ?? 0,
            SceneReceived = Interlocked.Read(ref _sceneSniffsReceived),
            SceneForwarded = Volatile.Read(ref _session)?.Scene.Forwarded ?? 0,
        };
    }

    public long Faults => Interlocked.Read(ref _faults);

    /// <summary>Runs <paramref name="work"/> on the mailbox (tests: occupy it to prove the reflex never waits).</summary>
    internal Task InvokeOnMailboxAsync(Func<Task> work) => InvokeAsync(work);

    /// <summary>
    /// Single-file Da Bun Vault export: a fresh Vault backup, then one verified file at
    /// <paramref name="destinationPath"/>. Serialized with every other Vault operation.
    /// </summary>
    internal Task<VaultExportReport> ExportVaultAsync(MemoryStoreLocation location, string destinationPath) =>
        InvokeAsync(async () =>
        {
            await DaBunVault.BackUpAsync(_c.Repository, _c.Keepsakes, _c.State, _shutdown.Token).ConfigureAwait(false);
            Publish(new CompanionNotice(CompanionNoticeKind.VaultBackedUp));
            return await VaultExport.ExportAsync(location, destinationPath, Now(), _shutdown.Token).ConfigureAwait(false);
        });

    public Task TickAsync() => InvokeAsync(TickCoreAsync);

    public Task<WatchbunRefusal> AnswerQuietCheckAsync(QuietAnswer answer) =>
        InvokeAsync(async () => await ApplyWatchbunAsync(engine => engine.AnswerQuietCheck(answer, Now())).ConfigureAwait(false));

    public Task<WatchbunRefusal> DecideAfterExitAsync(ExitDecision decision) =>
        InvokeAsync(async () => await ApplyWatchbunAsync(engine => engine.Decide(decision, Now())).ConfigureAwait(false));

    public Task<WatchbunRefusal> AddWatchTaskAsync(string eventKey, TimeSpan? lifetime = null) =>
        InvokeAsync(async () => await ApplyWatchbunAsync(engine => engine.AddWatchTask(eventKey, lifetime, Now())).ConfigureAwait(false));

    public Task<WatchbunRefusal> CancelWatchTaskAsync(Guid taskId) =>
        InvokeAsync(async () => await ApplyWatchbunAsync(engine => engine.CancelWatchTask(taskId, Now())).ConfigureAwait(false));

    /// <summary>Feeds one structured game-event line from the synthetic adapter for the active session.</summary>
    public Task<AdapterVerdict> SubmitGameEventLineAsync(string line) =>
        InvokeAsync(() => GameEventCoreAsync(line));

    public Task<ConversationRefusal> SubmitUserTurnAsync(UserTurn turn) =>
        InvokeAsync(() =>
        {
            var update = _conversation.OnUserTurn(turn, Now());
            HandleConversation(update);
            return Task.FromResult(update.Refusal);
        });

    public Task<ConversationRefusal> SetConversationLockAsync(bool locked) =>
        InvokeAsync(() =>
        {
            var update = _conversation.SetLock(locked, Now());
            HandleConversation(update);
            return Task.FromResult(update.Refusal);
        });

    /// <summary>
    /// Shows the camera action and asks the capture worker for one native-resolution
    /// photograph of the authorized target. If the worker cannot take one, the next
    /// current sheet's full-context region becomes the photograph instead.
    /// </summary>
    public Task<KeepsakeRefusal> TakePhotographAsync() =>
        InvokeAsync(async () =>
        {
            if (_session is not { } session)
            {
                return KeepsakeRefusal.UnknownAction;
            }

            var begun = _camera.BeginCameraAction(session.Grant, Now());
            foreach (var intent in begun.Intents)
            {
                Publish(new CompanionNotice(CompanionNoticeKind.Keepsake, Keepsake: intent));
            }

            if (begun.Action is { } action)
            {
                _pendingPhoto = action;
                _photographRequested = false;
                try
                {
                    await _c.Controller.RequestPhotographAsync(_shutdown.Token).ConfigureAwait(false);
                    _photographRequested = true;
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
                {
                    // Degraded but honest: the photograph comes from the sheet resolution.
                }
            }

            return begun.Refusal;
        });

    /// <summary>Boss's explicit photograph deletion; the record stays and a superseding note is appended.</summary>
    public Task<DeletionStatus> DeletePhotographAsync(Guid photographId) =>
        InvokeAsync(async () =>
        {
            var result = await _keepsakeStore
                .DeleteAsync(photographId, KeepsakeDeletionAuthority.ForExplicitLocalUserIntent(), Now(), _shutdown.Token)
                .ConfigureAwait(false);
            foreach (var intent in result.Intents)
            {
                Publish(new CompanionNotice(CompanionNoticeKind.Keepsake, Keepsake: intent));
            }

            return result.Status;
        });

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _c.Controller.SessionEvent -= OnSessionEvent;
        _c.Controller.AttentionSheetAvailable -= OnSheetAvailable;
        _c.Controller.SniffAvailable -= OnSniffAvailable;
        _c.Controller.TextSniffAvailable -= OnTextSniffAvailable;
        _c.Controller.SceneSniffAvailable -= OnSceneSniffAvailable;
        if (_c.Platform is { } platform)
        {
            platform.ForegroundChanged -= OnForeground;
            platform.InputObserved -= OnInput;
            platform.TargetExited -= OnTargetExited;
            platform.TargetLaunched -= OnTargetLaunched;
            platform.Suspended -= OnSuspended;
            platform.Resumed -= OnResumed;
        }

        if (_loop is not null)
        {
            try
            {
                await InvokeAsync(async () =>
                {
                    await PersistAsync().ConfigureAwait(false);

                    // The transcript stays un-ended on shutdown; reopening records the restart.
                    _session?.Transcript.Dispose();
                    _session = null;
                }).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or ObjectDisposedException)
            {
                RecordFault(exception);
            }

            _mailbox.Writer.TryComplete();
            await _loop.ConfigureAwait(false);
        }

        await _shutdown.CancelAsync().ConfigureAwait(false);
        _shutdown.Dispose();
    }

    // ---- mailbox ----------------------------------------------------------------------

    private sealed record WorkItem(Func<Task> Work);

    private async Task LoopAsync()
    {
        await foreach (var item in _mailbox.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await item.Work().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                RecordFault(exception);
            }
        }
    }

    private void Post(Func<Task> work)
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            _mailbox.Writer.TryWrite(new WorkItem(work));
        }
    }

    private Task InvokeAsync(Func<Task> work) => InvokeAsync(async () =>
    {
        await work().ConfigureAwait(false);
        return true;
    });

    private Task<T> InvokeAsync<T>(Func<Task<T>> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_loop is null || !_mailbox.Writer.TryWrite(new WorkItem(async () =>
            {
                try
                {
                    completion.TrySetResult(await work().ConfigureAwait(false));
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                    throw;
                }
            })))
        {
            completion.TrySetException(new InvalidOperationException("The orchestrator is not running."));
        }

        return completion.Task;
    }

    private void RecordFault(Exception exception)
    {
        Interlocked.Increment(ref _faults);
        LastFault = exception;
        Publish(new CompanionNotice(CompanionNoticeKind.Fault, Key: exception.GetType().Name));
    }

    private void Publish(CompanionNotice notice)
    {
        try
        {
            // The reflex lane publishes from the sniff thread; one notice at a time.
            lock (_publishGate)
            {
                Notice?.Invoke(this, notice);
            }
        }
        catch (Exception exception)
        {
            // A presentation failure must never break the core pipeline.
            RecordFault(exception);
        }
    }

    private DateTimeOffset Now() => _c.Time.GetUtcNow();

    // ---- event entry points (post only; nothing runs on the caller's thread) ---------

    /// <summary>
    /// The reflex lane (VIS-01). Runs on the capture client's sniff thread inside the
    /// privacy admission lease: the thread-safe attention engine observes the sniff and its
    /// Attention notices are published at once, never waiting behind the mailbox (which
    /// may be busy with disk work). Conversation and cadence consequences are posted.
    /// </summary>
    private void OnSniffAvailable(object? sender, SniffSignal sniff)
    {
        Interlocked.Increment(ref _sniffsReceived);
        var session = Volatile.Read(ref _session);
        if (session is null || Volatile.Read(ref _disposed) != 0 || !sniff.Matches(session.Grant))
        {
            return;
        }

        var latency = DateTimeOffset.UtcNow - sniff.SourceTimestamp;
        if (latency >= TimeSpan.Zero && latency < TimeSpan.FromMinutes(1))
        {
            _sniffLatency.Add(latency);
        }

        var trigger = sniff.IsTrigger;
        try
        {
            if (session.Reflex.Evaluate(sniff) is { } interest)
            {
                // The engine works in Prince's clock; the sniff's own timestamp only paces the reflex.
                var update = session.Attention.Observe(interest with { Timestamp = Max(Now(), session.Attention.Current.AsOf) });
                foreach (var intent in update.Intents)
                {
                    Publish(new CompanionNotice(CompanionNoticeKind.Attention, Attention: intent));
                }

                if (update.Transitioned || update.Intents.Count > 0 || trigger)
                {
                    Post(() => ApplyReflexAsync(session, update, trigger));
                }

                return;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            RecordFault(exception);
            return;
        }

        if (trigger)
        {
            Post(() => ApplyReflexAsync(session, null, trigger));
        }
    }

    /// <summary>
    /// The text reflex (VIS-03), on the same lane as the Sniff: new or changed words perk his
    /// ears at once and, while still novel, bring the next Glance forward.
    /// </summary>
    private void OnTextSniffAvailable(object? sender, TextSniffSignal text)
    {
        Interlocked.Increment(ref _textSniffsReceived);
        var session = Volatile.Read(ref _session);
        if (session is null || Volatile.Read(ref _disposed) != 0 || !text.Matches(session.Grant))
        {
            return;
        }

        try
        {
            var (interest, glanceNow) = session.Text.Evaluate(text);
            if (interest is null)
            {
                return;
            }

            var update = session.Attention.Observe(interest with { Timestamp = Max(Now(), session.Attention.Current.AsOf) });
            foreach (var intent in update.Intents)
            {
                Publish(new CompanionNotice(CompanionNoticeKind.Attention, Attention: intent));
            }

            if (update.Transitioned || update.Intents.Count > 0 || glanceNow)
            {
                Post(() => ApplyReflexAsync(session, update, glanceNow));
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            RecordFault(exception);
        }
    }

    /// <summary>
    /// The scene reflex (VIS-04), on the same lane as the Sniff: a different scene perks his
    /// ears at once and, on a full shift into a still-novel scene, brings the next Glance forward.
    /// </summary>
    private void OnSceneSniffAvailable(object? sender, SceneSniffSignal scene)
    {
        Interlocked.Increment(ref _sceneSniffsReceived);
        var session = Volatile.Read(ref _session);
        if (session is null || Volatile.Read(ref _disposed) != 0 || !scene.Matches(session.Grant))
        {
            return;
        }

        try
        {
            var (interest, glanceNow) = session.Scene.Evaluate(scene);
            if (interest is null)
            {
                return;
            }

            var update = session.Attention.Observe(interest with { Timestamp = Max(Now(), session.Attention.Current.AsOf) });
            foreach (var intent in update.Intents)
            {
                Publish(new CompanionNotice(CompanionNoticeKind.Attention, Attention: intent));
            }

            if (update.Transitioned || update.Intents.Count > 0 || glanceNow)
            {
                Post(() => ApplyReflexAsync(session, update, glanceNow));
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            RecordFault(exception);
        }
    }

    private Task ApplyReflexAsync(ActiveSession session, AttentionUpdate? update, bool trigger)
    {
        if (!ReferenceEquals(session, _session))
        {
            return Task.CompletedTask;
        }

        if (trigger)
        {
            _sniffTriggerPending = true;
        }

        SyncAttentionState(session);
        if (update is not null)
        {
            var now = Now();
            foreach (var intent in update.Intents)
            {
                if (intent.Kind == AttentionIntentKind.AfterglowOpening && intent.TopicKey is { } topic)
                {
                    HandleConversation(_conversation.OnAfterglowOpening(topic, now));
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies the consequences of the attention engine's current state once per change:
    /// the conversation observes it, and the capture worker looks faster or slower.
    /// </summary>
    private void SyncAttentionState(ActiveSession session)
    {
        var state = session.Attention.Current.State;
        if (session.AppliedState != state)
        {
            var first = session.AppliedState is null;
            session.AppliedState = state;
            if (!first)
            {
                HandleConversation(_conversation.ObserveAttention(state, Now()));
            }
        }

        var cadence = state switch
        {
            AttentionState.HighAttention => CaptureCadence.BnuyMode,
            AttentionState.Engaged or AttentionState.Afterglow => CaptureCadence.Engaged,
            _ => CaptureCadence.Noticing,
        };
        if (!_options.SetsCaptureCadence || ReferenceEquals(session.AppliedCadence, cadence))
        {
            return;
        }

        session.AppliedCadence = cadence;
        _lastCadenceAssert = Now();
        _ = SetCadenceAsync(session, cadence);
    }

    private async Task SetCadenceAsync(ActiveSession session, CaptureCadence cadence)
    {
        try
        {
            if (!await _c.Controller.SetCadenceAsync(cadence, _shutdown.Token).ConfigureAwait(false))
            {
                // Not running yet (or stopped): the next attention change or tick sends it again.
                Post(() =>
                {
                    if (ReferenceEquals(session.AppliedCadence, cadence))
                    {
                        session.AppliedCadence = null;
                    }

                    return Task.CompletedTask;
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A worker that cannot change pace keeps its last cadence; nothing else depends on it.
            RecordFault(exception);
        }
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;

    private void OnSessionEvent(object? sender, TargetSessionEvent targetEvent)
    {
        if (targetEvent.Kind == TargetSessionEventKind.PrivacyPaused)
        {
            // Immediate, outside the mailbox: generation fencing remains the guarantee.
            _c.Bridge.CancelPendingWork();
        }

        Post(() => OnTargetEventAsync(targetEvent));
    }

    private void OnSheetAvailable(object? sender, AttentionSheetMetadata metadata) => Post(ProcessSheetAsync);

    private void OnForeground(object? sender, ForegroundWindow window) =>
        Post(() => ApplyWatchbunAsync(engine => engine.OnForegroundChanged(window, Now())));

    private void OnInput(object? sender, EventArgs e) =>
        Post(() => ApplyWatchbunAsync(engine => engine.OnInput(Now())));

    private void OnTargetExited(object? sender, TargetExit exit) => Post(() => TargetExitedCoreAsync(exit));

    private void OnTargetLaunched(object? sender, CaptureTargetIdentity candidate) =>
        Post(() => ApplyWatchbunAsync(engine => engine.OnTargetLaunched(candidate, Now())));

    private void OnSuspended(object? sender, SuspendReason reason) =>
        Post(() => ApplyWatchbunAsync(engine => engine.OnSystemSuspend(reason, Now())));

    private void OnResumed(object? sender, SuspendReason reason) =>
        Post(() => ApplyWatchbunAsync(engine => engine.OnSystemResume(reason, Now())));

    // ---- target sessions ---------------------------------------------------------------

    private sealed class ActiveSession(
        CaptureAuthorizationGrant grant,
        AttentionEngine attention,
        SessionTranscript transcript,
        string reference,
        string game)
    {
        public CaptureAuthorizationGrant Grant { get; set; } = grant;

        public AttentionEngine Attention { get; } = attention;

        public SessionTranscript Transcript { get; } = transcript;

        public TranscriptRecorder Recorder { get; } = new(transcript);

        public string Reference { get; } = reference;

        public string Game { get; } = game;

        public HashSet<string> Topics { get; } = new(StringComparer.Ordinal);

        public StructuredGameEventAdapter Adapter { get; } = new(grant.TargetSessionId);

        public SniffReflex Reflex { get; } = new(attention.Configuration);

        public TextReflex Text { get; } = new(attention.Configuration);

        public SceneReflex Scene { get; } = new(attention.Configuration);

        /// <summary>The attention state whose consequences (conversation, cadence) were last applied.</summary>
        public AttentionState? AppliedState { get; set; }

        public CaptureCadence? AppliedCadence { get; set; }
    }

    private async Task OnTargetEventAsync(TargetSessionEvent targetEvent)
    {
        switch (targetEvent.Kind)
        {
            case TargetSessionEventKind.Authorized:
                await AuthorizedAsync().ConfigureAwait(false);
                break;
            case TargetSessionEventKind.PrivacyPaused:
                _pendingPhoto = null;
                Publish(new CompanionNotice(CompanionNoticeKind.PrivacyPaused));
                break;
            case TargetSessionEventKind.PrivacyResumed:
            case TargetSessionEventKind.Resumed:
                if (_session is { } session && _c.Controller.CurrentSession.Grant is { } refreshed
                    && refreshed.TargetSessionId == session.Grant.TargetSessionId)
                {
                    // The resumed worker starts at its default pace: tell it again.
                    session.Grant = refreshed;
                    session.AppliedCadence = null;
                    SyncAttentionState(session);
                }

                break;
            case TargetSessionEventKind.TargetEnded:
                if (_session is null)
                {
                    // Not an attached session: either our own end after a target exit or a
                    // Watchbun close (the session objects are already closed), or a restart.
                    // A recovering or paused Watchbun span is Boss's to decide, not dissolved here.
                    break;
                }

                // Boss ended the session deliberately: consolidate, back up, and finish.
                await CloseSessionObjectsAsync().ConfigureAwait(false);
                await PersistAsync().ConfigureAwait(false);
                await ConsolidateAndBackUpAsync().ConfigureAwait(false);
                _watchbun = null;
                _watchbunSessions.Clear();
                _c.Platform?.WatchTarget(null);
                _c.Platform?.WatchForRelaunch(null);
                await PersistAsync().ConfigureAwait(false);
                Publish(new CompanionNotice(CompanionNoticeKind.SessionEnded));
                break;
            case TargetSessionEventKind.TargetUnavailable:
                if (_c.Platform is null && _session is { } lost)
                {
                    // Without a process monitor the exit cannot be proven clean.
                    await TargetExitedCoreAsync(new TargetExit(lost.Grant.Target.ProcessId, -1, WindowClosedFirst: false, WasHung: false))
                        .ConfigureAwait(false);
                }

                break;
        }
    }

    private async Task AuthorizedAsync()
    {
        if (_c.Controller.CurrentSession.Grant is not { } grant)
        {
            return;
        }

        var now = Now();
        if (_session is { } current)
        {
            if (current.Grant.TargetSessionId == grant.TargetSessionId)
            {
                // A resumed grant restarts the worker at its default pace: tell it again.
                current.Grant = grant;
                current.AppliedCadence = null;
                SyncAttentionState(current);
                return;
            }

            await CloseSessionObjectsAsync().ConfigureAwait(false);
        }

        var reference = $"target-session:{grant.TargetSessionId:N}";
        _lastSemanticLook = null;
        _inspectWanted = false;
        _nextGlanceAt = null;
        _lastGlanceAt = null;
        var transcript = SessionTranscript.Open(_c.Transcripts, grant.TargetSessionId, _c.Privacy, now);
        _session = new ActiveSession(
            grant,
            new AttentionEngine(grant.TargetSessionId, now, _options.Attention),
            transcript,
            reference,
            GameReference(grant.Target.ExecutableFileName));
        _sniffTriggerPending = false;

        // M4a: a play session began; remembering counts sessions as well as days.
        if (_c.BunDex is { Health: BunDex.BunDexHealth.Healthy } bunDex)
        {
            bunDex.BeginSession();
        }
        SyncAttentionState(_session);

        if (_watchbun is { } existing
            && existing.Current.Phase is WatchbunPhase.ExitPending or WatchbunPhase.AwaitingRelaunch
                or WatchbunPhase.PausedAdventure or WatchbunPhase.Recovering)
        {
            var reattached = existing.Reattach(grant, now);
            if (reattached.Refusal == WatchbunRefusal.None)
            {
                await HandleWatchbunAsync(reattached).ConfigureAwait(false);
            }
            else
            {
                // A different application: the earlier adventure stays preserved in memory.
                await ConsolidateAndBackUpAsync().ConfigureAwait(false);
                StartFreshWatchbun(grant, now);
            }
        }
        else
        {
            if (_watchbunSessions.Count > 0)
            {
                // Sessions left from an interrupted close are consolidated, never dropped.
                await ConsolidateAndBackUpAsync().ConfigureAwait(false);
            }

            StartFreshWatchbun(grant, now);
        }

        if (!_watchbunSessions.Contains(reference, StringComparer.Ordinal))
        {
            _watchbunSessions.Add(reference);
        }

        _c.Platform?.WatchForRelaunch(null);
        _c.Platform?.WatchTarget(grant.Target);
        Publish(new CompanionNotice(CompanionNoticeKind.SessionStarted, Key: reference));
        await PersistAsync().ConfigureAwait(false);
    }

    private void StartFreshWatchbun(CaptureAuthorizationGrant grant, DateTimeOffset now)
    {
        _watchbun = new WatchbunEngine(grant, now, _options.Watchbun);
        _watchbunSessions.Clear();
    }

    private Task CloseSessionObjectsAsync()
    {
        if (_session is { } session)
        {
            try
            {
                session.Transcript.End(Now());
            }
            finally
            {
                session.Transcript.Dispose();
                _session = null;
                _pendingPhoto = null;
            }
        }

        return Task.CompletedTask;
    }

    private async Task TargetExitedCoreAsync(TargetExit exit)
    {
        if (_watchbun is not { } engine)
        {
            return;
        }

        var update = engine.OnTargetExited(exit, Now());
        if (update.Refusal != WatchbunRefusal.None)
        {
            return;
        }

        await CloseSessionObjectsAsync().ConfigureAwait(false);
        if (_c.Controller.CurrentSession.Phase != TargetSessionPhase.None)
        {
            await _c.Controller.EndSessionAsync().ConfigureAwait(false);
        }

        _c.Platform?.WatchTarget(null);
        await HandleWatchbunAsync(update).ConfigureAwait(false);
    }

    // ---- sheets, bridge, evidence -----------------------------------------------------

    private async Task ProcessSheetAsync()
    {
        var sheet = _c.Controller.TakeLatestAttentionSheet();
        if (sheet is null)
        {
            return;
        }

        if (_session is not { } session || !sheet.Metadata.Matches(session.Grant))
        {
            sheet.Dispose();
            return;
        }

        _sheetsReceived++;
        var isPhotograph = sheet.Metadata.Kind == AttentionSheetKind.Photograph;
        if (_pendingPhoto is { } action && (isPhotograph || !_photographRequested))
        {
            await PhotographAsync(session, action, sheet).ConfigureAwait(false);
            return;
        }

        if (isPhotograph)
        {
            // A photograph never reaches the semantic bridge; without its camera action it
            // is released at once (RAM-only).
            sheet.Dispose();
            return;
        }

        var allowed = _watchbun?.Current.SemanticSpendingAllowed ?? true;
        if (_options.Glance && allowed)
        {
            LookWithGlances(session, sheet);
            return;
        }

        if (_bridgeInFlight || !allowed)
        {
            sheet.Dispose();
            return;
        }

        var now = Now();
        var interval = _options.SemanticCadence.For(session.Attention.Current.State, sheet.Metadata.ChangeScore);
        if (_sniffTriggerPending && _options.SemanticCadence.EventFloor < interval)
        {
            // A sniff noticed something: look at the event floor rather than the state's pace.
            interval = _options.SemanticCadence.EventFloor;
        }

        if (_lastSemanticLook is { } last && now >= last && now - last < interval)
        {
            sheet.Dispose();
            return;
        }

        _lastSemanticLook = now;
        _sniffTriggerPending = false;
        DispatchInspect(session, sheet);
    }

    /// <summary>
    /// VIS-02 looking: the routine look is a Glance (peripheral vision); the Inspect is the
    /// main focus. A focus look runs when a Glance escalated, or at the event floor in Bnuy
    /// Mode. Otherwise a Glance runs when one is due: at once after a sniff trigger, else at
    /// the time the last Glance recommended. Glances keep running while an Inspect is in
    /// flight (no tunnel vision) and never touch it (no overwritten focus).
    /// </summary>
    private void LookWithGlances(ActiveSession session, AttentionSheet sheet)
    {
        var now = Now();
        var state = session.Attention.Current.State;
        var focusDue = !_bridgeInFlight
            && (_inspectWanted
                || (state == AttentionState.HighAttention
                    && (_lastSemanticLook is not { } lastFocus || now < lastFocus || now - lastFocus >= _options.SemanticCadence.EventFloor)));
        if (focusDue)
        {
            _inspectWanted = false;
            _lastSemanticLook = now;
            DispatchInspect(session, sheet);
            return;
        }

        var glanceDue = !_glanceInFlight
            && (_sniffTriggerPending
                || _nextGlanceAt is not { } due
                || now >= due
                || (_lastGlanceAt is { } lastGlance && now < lastGlance));
        if (!glanceDue)
        {
            sheet.Dispose();
            return;
        }

        // Until this Glance recommends otherwise, the next one follows the state's pace.
        _sniffTriggerPending = false;
        _lastGlanceAt = now;
        _nextGlanceAt = now + _options.SemanticCadence.For(state, sheet.Metadata.ChangeScore);
        _glanceInFlight = true;
        _glancesSent++;
        var metadata = sheet.Metadata;
        var grant = session.Grant;
        var token = _shutdown.Token;
        _ = Task.Run(async () =>
        {
            BridgeOutcome? outcome = null;
            try
            {
                outcome = await _c.Bridge.GlanceAsync(sheet, grant, token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                RecordFault(exception);
            }

            Post(() => ApplyGlanceOutcomeAsync(session, metadata, outcome));
        });
    }

    private Task ApplyGlanceOutcomeAsync(ActiveSession session, AttentionSheetMetadata metadata, BridgeOutcome? outcome)
    {
        _glanceInFlight = false;
        if (outcome is null)
        {
            return Task.CompletedTask;
        }

        _lastLookOutcome = outcome.Kind;

        var braincase = outcome.Kind == BridgeOutcomeKind.Glanced ? BridgeOutcomeKind.Interpreted : outcome.Kind;
        if (braincase is BridgeOutcomeKind.Interpreted or BridgeOutcomeKind.Napping or BridgeOutcomeKind.Unavailable
            && braincase != _lastBraincase)
        {
            _lastBraincase = braincase;
            Publish(new CompanionNotice(CompanionNoticeKind.Braincase, Braincase: braincase, NapReason: _c.Bridge.NapStatus.Reason));
        }

        if (!ReferenceEquals(session, _session) || outcome.Kind != BridgeOutcomeKind.Glanced || outcome.Glance is not { } reading)
        {
            return Task.CompletedTask;
        }

        var now = Now();
        var nextLook = reading.NextLook < MinimumGlanceInterval
            ? MinimumGlanceInterval
            : reading.NextLook > MaximumGlanceInterval ? MaximumGlanceInterval : reading.NextLook;
        _nextGlanceAt = now + nextLook;

        // Peripheral evidence: it adds to attention but cannot by itself enter Bnuy Mode.
        var region = metadata.Regions.FirstOrDefault(candidate => candidate.Kind == reading.ChangedRegion)?.NormalizedSource
            ?? new NormalizedRegion(0, 0, 1, 1);
        HandleAttention(session, session.Attention.Observe(new InterestEvent
        {
            TargetSessionId = session.Grant.TargetSessionId,
            Timestamp = Max(now, session.Attention.Current.AsOf),
            Region = region,
            RegionKind = reading.ChangedRegion,
            Signals = new AttentionSignals(
                Novelty: reading.Interest,
                Change: reading.Interest,
                Salience: reading.Interest,
                Urgency: 0,
                Persistence: 0.3,
                Confidence: reading.Confidence),
            TopicKey = GlanceTopic(reading.Label),
            EvidenceSource = "glance",
            EvidenceReferences = [reading.OperationId.ToString("N")],
            ScoreCeiling = Math.Max(0, session.Attention.Configuration.HighEnter - 1),
        }));

        // One closer look waits at most: further escalations fold into it.
        if (reading.Escalate || reading.Interest >= GlanceEscalationInterest)
        {
            _inspectWanted = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>A neutral, bounded topic from a Glance label (it is never shown as text).</summary>
    internal static string? GlanceTopic(string label)
    {
        var builder = new System.Text.StringBuilder("glance:");
        var dash = false;
        foreach (var character in label.ToLowerInvariant())
        {
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(character);
                dash = false;
            }
            else if (!dash && builder.Length > 7)
            {
                builder.Append('-');
                dash = true;
            }

            if (builder.Length >= 7 + 48)
            {
                break;
            }
        }

        var topic = builder.ToString().TrimEnd('-');
        return topic.Length > 7 ? topic : null;
    }

    private void DispatchInspect(ActiveSession session, AttentionSheet sheet)
    {
        _bridgeInFlight = true;
        _inspectsSent++;
        var metadata = sheet.Metadata;
        var grant = session.Grant;
        var context = new ResumeContext([], session.Game);
        var token = _shutdown.Token;
        _ = Task.Run(async () =>
        {
            BridgeOutcome? outcome = null;
            try
            {
                outcome = await _c.Bridge.InterpretAttentionSheetAsync(sheet, grant, context, token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                RecordFault(exception);
            }

            Post(() => ApplyBridgeOutcomeAsync(session, metadata, outcome));
        });
    }

    private async Task ApplyBridgeOutcomeAsync(ActiveSession session, AttentionSheetMetadata metadata, BridgeOutcome? outcome)
    {
        _bridgeInFlight = false;
        if (outcome is null)
        {
            return;
        }

        _lastLookOutcome = outcome.Kind;

        if (outcome.Kind is BridgeOutcomeKind.Interpreted or BridgeOutcomeKind.Napping or BridgeOutcomeKind.Unavailable
            && outcome.Kind != _lastBraincase)
        {
            _lastBraincase = outcome.Kind;
            Publish(new CompanionNotice(CompanionNoticeKind.Braincase, Braincase: outcome.Kind, NapReason: _c.Bridge.NapStatus.Reason));
        }

        if (!ReferenceEquals(session, _session) || outcome.Kind != BridgeOutcomeKind.Interpreted || outcome.Interpretation is not { } interpretation)
        {
            return;
        }

        var now = Now();
        var evidence = SemanticEvidenceMapper.Map(interpretation, metadata, now, NoveltyFor(session, now));

        // M3b: fights and how they went feed his courage and trust in Da Boss, which only ever grow.
        if (_c.Sightings is { } sightings && _c.Privacy.IsCurrent(session.Grant.Generation))
        {
            foreach (var encounter in interpretation.Encounters)
            {
                sightings.Encounter(session.Game, session.Scene.Label, SemanticEvidenceMapper.Topic(encounter.Threat), encounter.Outcome, now);
            }
        }
        foreach (var interest in evidence.Events)
        {
            HandleAttention(session, session.Attention.Observe(interest));
        }

        if (evidence.Observation is { } observation)
        {
            HandleConversation(_conversation.OnSemanticScan(observation, now));
        }

        if (evidence.WatchbunEvent is { } gameEvent)
        {
            await ApplyWatchbunAsync(engine => engine.OnGameEvent(gameEvent, now)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Getting used to things (M3a): every observation is recorded as a sighting, and its interest
    /// is how new it feels by thing and kind in this game. Nothing is recorded once a privacy stop
    /// has landed.
    /// </summary>
    private Func<SemanticObservation, string, double> NoveltyFor(ActiveSession session, DateTimeOffset now)
    {
        if (_c.Sightings is not { } sightings)
        {
            return SemanticEvidenceMapper.FirstTime(session.Topics);
        }

        return (observation, topic) =>
        {
            var sighting = new Sighting(now, session.Game, session.Scene.Label, topic, observation.Kind, observation.Danger, observation.Label, SightingSource.Observation);
            return _c.Privacy.IsCurrent(session.Grant.Generation) ? sightings.See(sighting) : sightings.NoveltyOf(sighting);
        };
    }

    private async Task PhotographAsync(ActiveSession session, CameraAction action, AttentionSheet sheet)
    {
        PhotographFrame? frame = null;
        var isPhotograph = sheet.Metadata.Kind == AttentionSheetKind.Photograph;
        try
        {
            using (sheet)
            {
                frame = SheetPhotographSource.TryCreate(sheet, session.Grant);
            }

            if (frame is null)
            {
                _pendingPhoto = null;
                Publish(new CompanionNotice(CompanionNoticeKind.PhotographRefused, PhotographRefusal: KeepsakeRefusal.InvalidFrame));
                return;
            }

            var policy = _c.Controller.CurrentSession.Policy?.ContentPolicy ?? TargetContentPolicy.Standard;
            var result = await _camera.TakeAsync(
                    action,
                    frame,
                    new KeepsakeContext(session.Game, null, session.Reference),
                    policy,
                    PrivacyAssessment.Clear,
                    Now(),
                    _shutdown.Token)
                .ConfigureAwait(false);
            if (result.Refusal == KeepsakeRefusal.OutsideWindow && Now() <= action.ExpiresAt)
            {
                // An older sheet: keep waiting for one captured inside the action window. A
                // photograph taken from a frame already in flight asks again for a newer one.
                if (isPhotograph)
                {
                    await RequestPhotographAgainAsync().ConfigureAwait(false);
                }

                return;
            }

            _pendingPhoto = null;
            foreach (var intent in result.Intents)
            {
                Publish(new CompanionNotice(CompanionNoticeKind.Keepsake, Keepsake: intent));
            }

            if (result.Refusal != KeepsakeRefusal.None)
            {
                Publish(new CompanionNotice(CompanionNoticeKind.PhotographRefused, PhotographRefusal: result.Refusal));
            }
        }
        finally
        {
            if (frame is not null && System.Runtime.InteropServices.MemoryMarshal.TryGetArray(frame.Bgra32, out var segment) && segment.Array is { } pixels)
            {
                Array.Clear(pixels);
            }
        }
    }

    private async Task RequestPhotographAgainAsync()
    {
        try
        {
            await _c.Controller.RequestPhotographAsync(_shutdown.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
        {
            // Fall back to the next sheet's full-context region.
            _photographRequested = false;
        }
    }

    private Task<AdapterVerdict> GameEventCoreAsync(string line)
    {
        if (_session is not { } session)
        {
            return Task.FromResult(AdapterVerdict.WrongTarget);
        }

        var verdict = session.Adapter.Parse(line, out var gameEvent);
        if (verdict != AdapterVerdict.Accepted || gameEvent is null)
        {
            return Task.FromResult(verdict);
        }

        return GameEventAcceptedAsync(session, gameEvent, verdict);
    }

    private async Task<AdapterVerdict> GameEventAcceptedAsync(ActiveSession session, StructuredGameEvent gameEvent, AdapterVerdict verdict)
    {
        var now = Now();
        await ApplyWatchbunAsync(engine => engine.OnGameEvent(gameEvent, now)).ConfigureAwait(false);
        if (gameEvent.Kind == GameEventKind.Urgent && ReferenceEquals(session, _session))
        {
            HandleAttention(session, session.Attention.Observe(new InterestEvent
            {
                TargetSessionId = session.Grant.TargetSessionId,
                Timestamp = now,
                Signals = new AttentionSignals(Novelty: 0.5, Change: 0.5, Salience: 1, Urgency: 1, Persistence: 0.3, Confidence: 1),
                TopicKey = gameEvent.Key,
                EvidenceSource = "structured-event",
            }));
        }

        return verdict;
    }

    private void HandleAttention(ActiveSession session, AttentionUpdate update)
    {
        var now = Now();
        foreach (var intent in update.Intents)
        {
            Publish(new CompanionNotice(CompanionNoticeKind.Attention, Attention: intent));
        }

        if (update.Transitioned)
        {
            SyncAttentionState(session);
        }

        foreach (var intent in update.Intents)
        {
            switch (intent.Kind)
            {
                case AttentionIntentKind.AfterglowOpening when intent.TopicKey is { } topic:
                    HandleConversation(_conversation.OnAfterglowOpening(topic, now));
                    break;
                case AttentionIntentKind.Urgent:
                    var urgentTopic = intent.TopicKey ?? "urgent";
                    var urgent = _conversation.OnUrgent(urgentTopic, now);
                    PublishConversation(urgent);
                    session.Recorder.RecordUrgent(urgentTopic, NeutralUrgentObservation, urgent, session.Grant.Generation, now);
                    break;
            }
        }
    }

    private void HandleConversation(ConversationUpdate update)
    {
        PublishConversation(update);
        if (_session is { } session)
        {
            session.Recorder.Record(update, session.Grant.Generation, Now());
        }
    }

    private void PublishConversation(ConversationUpdate update)
    {
        foreach (var intent in update.Intents)
        {
            Publish(new CompanionNotice(CompanionNoticeKind.Conversation, Conversation: intent));
        }
    }

    // ---- ticks and Watchbun -----------------------------------------------------------

    private async Task TickCoreAsync()
    {
        var now = Now();
        if (_session is { } session)
        {
            HandleAttention(session, session.Attention.Advance(now));

            // A worker restart (fault, watchdog, resume) returns it to its default pace;
            // re-assert the attention cadence now and then so it never stays wrong for long.
            if (session.AppliedCadence is null || now - _lastCadenceAssert >= CadenceReassertInterval || now < _lastCadenceAssert)
            {
                session.AppliedCadence = null;
                SyncAttentionState(session);
            }
        }

        HandleConversation(_conversation.Tick(now));
        if (_pendingPhoto is { } action && now > action.ExpiresAt)
        {
            _pendingPhoto = null;
            Publish(new CompanionNotice(CompanionNoticeKind.PhotographRefused, PhotographRefusal: KeepsakeRefusal.ActionExpired));
        }

        if (_watchbun is not null)
        {
            await ApplyWatchbunAsync(engine => engine.Tick(now)).ConfigureAwait(false);
        }

        if (now - _lastPersist >= _options.CheckpointInterval)
        {
            await PersistAsync().ConfigureAwait(false);
        }
    }

    private async Task<WatchbunRefusal> ApplyWatchbunAsync(Func<WatchbunEngine, WatchbunUpdate> apply)
    {
        if (_watchbun is not { } engine)
        {
            return WatchbunRefusal.NotAllowed;
        }

        var update = apply(engine);
        await HandleWatchbunAsync(update).ConfigureAwait(false);
        return update.Refusal;
    }

    private async Task HandleWatchbunAsync(WatchbunUpdate update)
    {
        bool persist = false, consolidate = false, close = false;
        foreach (var intent in update.Intents)
        {
            Publish(new CompanionNotice(CompanionNoticeKind.Watchbun, Watchbun: intent));
            switch (intent.Kind)
            {
                case WatchbunIntentKind.CaptureSuspended:
                case WatchbunIntentKind.SemanticSpendingPaused:
                    _c.Bridge.CancelPendingWork();
                    break;
                case WatchbunIntentKind.CheckpointRequested:
                    persist = true;
                    break;
                case WatchbunIntentKind.ConsolidationRequested:
                    consolidate = true;
                    break;
                case WatchbunIntentKind.SessionClosed:
                    close = true;
                    break;
                case WatchbunIntentKind.AdventurePaused:
                    await RecordAdventureAsync(AdventureStatus.Paused).ConfigureAwait(false);
                    break;
                case WatchbunIntentKind.AwaitingRelaunch:
                    _c.Platform?.WatchForRelaunch(update.Snapshot.BoundTarget);
                    break;
                case WatchbunIntentKind.WatchTaskCompleted when _session is { } session:
                    HandleAttention(session, session.Attention.Observe(new InterestEvent
                    {
                        TargetSessionId = session.Grant.TargetSessionId,
                        Timestamp = Now(),
                        Signals = new AttentionSignals(Novelty: 0.6, Change: 0.6, Salience: 1, Urgency: 0.5, Persistence: 0.3, Confidence: 1),
                        Kind = AttentionEventKind.Decisive,
                        DecisiveReason = DecisiveReason.WatchTaskComplete,
                        TopicKey = intent.EventKey,
                        EvidenceSource = "watch-task",
                    }));
                    break;
            }
        }

        if (persist || consolidate)
        {
            await PersistAsync().ConfigureAwait(false);
        }

        if (consolidate)
        {
            await ConsolidateAndBackUpAsync().ConfigureAwait(false);
        }

        if (close)
        {
            await CloseSessionObjectsAsync().ConfigureAwait(false);
            if (_c.Controller.CurrentSession.Phase != TargetSessionPhase.None)
            {
                await _c.Controller.EndSessionAsync().ConfigureAwait(false);
            }

            _watchbun = null;
            _watchbunSessions.Clear();
            _c.Platform?.WatchTarget(null);
            _c.Platform?.WatchForRelaunch(null);
            await PersistAsync().ConfigureAwait(false);
            Publish(new CompanionNotice(CompanionNoticeKind.SessionEnded));
        }
    }

    private async Task RecordAdventureAsync(AdventureStatus status)
    {
        if (_watchbun is not { } engine)
        {
            return;
        }

        var game = GameReference(engine.Current.BoundTarget.ExecutableFileName);
        var subject = RecallSubjects.Adventure(game, DefaultSave);
        var previous = (await _c.Repository.RetrieveBySubjectAsync(subject, _shutdown.Token).ConfigureAwait(false))
            .FirstOrDefault(memory => memory.IsCurrent);
        var proposal = ConsolidationPlanner.PlanAdventureStatus(Guid.NewGuid(), Now(), game, DefaultSave, status, previous);
        await _c.Repository.WriteGate.SubmitAsync(proposal, _shutdown.Token).ConfigureAwait(false);
    }

    // ---- consolidation, Vault, persistence --------------------------------------------

    internal sealed record PendingConsolidation(string Session, Guid OperationId, DateTimeOffset Now, string? Game);

    /// <summary>Bound on durable consolidation intents; the oldest is dropped (its originals stay intact).</summary>
    internal const int MaximumPendingConsolidations = 64;

    private async Task ConsolidateAndBackUpAsync()
    {
        var game = _watchbun is { } engine ? GameReference(engine.Current.BoundTarget.ExecutableFileName) : _session?.Game;
        var queued = (await ReadPendingAsync().ConfigureAwait(false)).Select(pending => pending.Session);
        foreach (var reference in queued.Concat(_watchbunSessions).Distinct(StringComparer.Ordinal).ToArray())
        {
            var operationId = DeriveId("consolidation", reference);
            try
            {
                await ConsolidateAsync(new PendingConsolidation(reference, operationId, Now(), game)).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not (OutOfMemoryException or OperationCanceledException))
            {
                // One session's consolidation must never block the Vault backup or cleanup;
                // its persisted intent replays on the next start.
                RecordFault(exception);
            }
        }

        try
        {
            await DaBunVault.BackUpAsync(_c.Repository, _c.Keepsakes, _c.State, _shutdown.Token).ConfigureAwait(false);
            Publish(new CompanionNotice(CompanionNoticeKind.VaultBackedUp));
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or OperationCanceledException))
        {
            // The previous Vault stays untouched; report honestly.
            Publish(new CompanionNotice(CompanionNoticeKind.VaultBackupFailed, Key: exception.GetType().Name));
        }
    }

    /// <summary>
    /// Persists the intent (operation and time) before committing, so an interrupted
    /// consolidation replays the identical proposal and is idempotent (Task 10 J3). Intents
    /// are queued per session, so one session's unfinished intent is never overwritten by
    /// another's.
    /// </summary>
    private async Task ConsolidateAsync(PendingConsolidation requested)
    {
        var pending = await ReadPendingAsync().ConfigureAwait(false);
        var intent = pending.FirstOrDefault(item => item.Session == requested.Session);
        if (intent is null)
        {
            intent = requested;
            pending.Add(intent);
            while (pending.Count > MaximumPendingConsolidations)
            {
                pending.RemoveAt(0);
            }

            await WriteJsonAsync(ConsolidationStateName, pending.ToArray()).ConfigureAwait(false);
        }

        // Stream every original of the session in record-id order (however long the session)
        // and commit it in parts that each fit one atomic append. The partition is a pure
        // function of the session's originals, so a replay reproduces every part exactly.
        var part = 0;
        var consolidated = 0;
        var batch = new List<RetrievedMemory>();
        var batchHighlights = 0;
        Guid? after = null;
        while (true)
        {
            var page = await _c.Repository
                .RetrieveSessionPageAsync(intent.Session, after, MemoryQuery.MaximumLimit, _shutdown.Token)
                .ConfigureAwait(false);
            foreach (var memory in page)
            {
                var metadata = RecallMetadata.Parse(memory.Record.RetrievalMetadataJson);
                if (metadata.Kind is RecallRecordKind.Summary or RecallRecordKind.Highlight)
                {
                    continue;
                }

                var highlight = metadata.Highlight ? 1 : 0;
                if (PlannedRecords(batch.Count + 1, batchHighlights + highlight) > ConsolidationPlanner.MaximumRecordsPerConsolidation)
                {
                    if (!await CommitPartAsync(intent, part++, batch).ConfigureAwait(false))
                    {
                        return;
                    }

                    consolidated += batch.Count;
                    batch.Clear();
                    batchHighlights = 0;
                }

                batch.Add(memory);
                batchHighlights += highlight;
            }

            if (page.Count < MemoryQuery.MaximumLimit)
            {
                break;
            }

            after = page[^1].Record.RecordId;
        }

        if (batch.Count > 0)
        {
            if (!await CommitPartAsync(intent, part, batch).ConfigureAwait(false))
            {
                return;
            }

            consolidated += batch.Count;
        }

        if (consolidated > 0)
        {
            Publish(new CompanionNotice(CompanionNoticeKind.Consolidated, Key: intent.Session, Count: consolidated));
        }

        var remaining = await ReadPendingAsync().ConfigureAwait(false);
        remaining.RemoveAll(item => item.Session == intent.Session);
        await WriteJsonAsync(ConsolidationStateName, remaining.ToArray()).ConfigureAwait(false);
        _watchbunSessions.Remove(intent.Session);
    }

    /// <summary>Records one consolidation part would append: a summary per source chunk plus each highlight.</summary>
    internal static int PlannedRecords(int entries, int highlights) =>
        ((entries + ConsolidationPlanner.MaximumSourcesPerSummary - 1) / ConsolidationPlanner.MaximumSourcesPerSummary) + highlights;

    /// <summary>The operation id of one consolidation part; part 0 keeps the intent's own id.</summary>
    internal static Guid PartOperationId(Guid intentOperation, int part) =>
        part == 0 ? intentOperation : DeriveId("consolidation-part", $"{intentOperation:N}:{part}");

    private async Task<bool> CommitPartAsync(PendingConsolidation intent, int part, IReadOnlyList<RetrievedMemory> entries)
    {
        var plan = ConsolidationPlanner.PlanSessionSummary(
            PartOperationId(intent.OperationId, part), intent.Now, intent.Session, intent.Game, save: null, entries);
        var result = await _c.Repository.WriteGate.SubmitAsync(plan.Proposal, _shutdown.Token).ConfigureAwait(false);

        // A refused part leaves the intent pending; the next start or consolidation replays it.
        return result.IsAccepted;
    }

    private async Task<List<PendingConsolidation>> ReadPendingAsync() =>
        [.. (await ReadJsonAsync<PendingConsolidation[]>(ConsolidationStateName, _shutdown.Token).ConfigureAwait(false) ?? [])
            .Where(item => item is { Session.Length: > 0 })];

    private async Task ReplayPendingConsolidationAsync()
    {
        foreach (var pending in await ReadPendingAsync().ConfigureAwait(false))
        {
            try
            {
                await ConsolidateAsync(pending).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not (OutOfMemoryException or OperationCanceledException))
            {
                RecordFault(exception);
            }
        }
    }

    private async Task PersistAsync()
    {
        _lastPersist = Now();
        await WriteJsonAsync(ConversationStateName, _conversation.Checkpoint()).ConfigureAwait(false);
        if (_watchbun is { } engine)
        {
            await WriteJsonAsync(WatchbunStateName, engine.Checkpoint()).ConfigureAwait(false);
        }
        else
        {
            await _c.State.PutAsync(WatchbunStateName, ReadOnlyMemory<byte>.Empty, _shutdown.Token).ConfigureAwait(false);
        }

        await WriteJsonAsync(SessionsStateName, _watchbunSessions.ToArray()).ConfigureAwait(false);
    }

    private async Task<ConversationCoordinator> RestoreConversationAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var read = await _c.State.GetAsync(ConversationStateName, cancellationToken).ConfigureAwait(false);
        if (read.Status == StateStatus.Missing || (read.Status == StateStatus.Verified && read.Payload.IsEmpty))
        {
            return new ConversationCoordinator(Guid.NewGuid(), now, _chance, _options.Conversation);
        }

        if (read.Status == StateStatus.Verified)
        {
            try
            {
                if (JsonSerializer.Deserialize<ConversationCheckpoint>(read.Payload.Span, Json) is { } checkpoint)
                {
                    return ConversationCoordinator.Restore(checkpoint, _chance, _options.Conversation);
                }
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
            }
        }

        // Unreadable (damaged, or written by a newer build): keep it aside before anything can
        // overwrite it, and keep the one Conversation Thread lineage whenever its id survives.
        await PreserveUnreadableAsync(ConversationStateName, read, cancellationToken).ConfigureAwait(false);
        var lineage = read.Status == StateStatus.Verified ? LineageOf(read.Payload.Span) : null;
        return new ConversationCoordinator(lineage ?? Guid.NewGuid(), now, _chance, _options.Conversation);
    }

    private async Task<WatchbunEngine?> RestoreWatchbunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var read = await _c.State.GetAsync(WatchbunStateName, cancellationToken).ConfigureAwait(false);
        if (read.Status == StateStatus.Missing || (read.Status == StateStatus.Verified && read.Payload.IsEmpty))
        {
            return null;
        }

        if (read.Status == StateStatus.Verified)
        {
            try
            {
                if (JsonSerializer.Deserialize<WatchbunCheckpoint>(read.Payload.Span, Json) is { } checkpoint)
                {
                    return WatchbunEngine.Restore(checkpoint, now, _options.Watchbun);
                }
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
            }
        }

        await PreserveUnreadableAsync(WatchbunStateName, read, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<T?> ReadJsonAsync<T>(string name, CancellationToken cancellationToken)
        where T : class
    {
        var read = await _c.State.GetAsync(name, cancellationToken).ConfigureAwait(false);
        if (read.Status == StateStatus.Missing || (read.Status == StateStatus.Verified && read.Payload.IsEmpty))
        {
            return null;
        }

        if (read.Status == StateStatus.Verified)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(read.Payload.Span, Json);
            }
            catch (JsonException)
            {
            }
        }

        await PreserveUnreadableAsync(name, read, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Keeps a state entry this build cannot use under <c>&lt;name&gt;.unreadable</c>, as a valid
    /// entry (so it never blocks a Vault backup), before the next checkpoint overwrites the
    /// original. The first preserved copy is never replaced. Reported as a contained fault.
    /// </summary>
    private async Task PreserveUnreadableAsync(string name, StateRead read, CancellationToken cancellationToken)
    {
        var preserved = name + UnreadableSuffix;
        try
        {
            if ((await _c.State.GetAsync(preserved, cancellationToken).ConfigureAwait(false)).Status != StateStatus.Missing)
            {
                return;
            }

            var bytes = read.Status == StateStatus.Verified
                ? read.Payload.ToArray()
                : await File.ReadAllBytesAsync(_c.State.PathFor(name), cancellationToken).ConfigureAwait(false);
            await _c.State.PutAsync(preserved, bytes.AsMemory(0, Math.Min(bytes.Length, VaultStateStore.MaximumPayloadBytes)), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Preservation is best effort; the failure itself is reported below.
        }
        finally
        {
            Interlocked.Increment(ref _faults);
            Publish(new CompanionNotice(CompanionNoticeKind.Fault, Key: $"UnreadableState:{name}"));
        }
    }

    private static Guid? LineageOf(ReadOnlySpan<byte> payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload.ToArray());
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(nameof(ConversationCheckpoint.CoordinatorId), out var id)
                && id.TryGetGuid(out var lineage)
                && lineage != Guid.Empty
                    ? lineage
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Task WriteJsonAsync<T>(string name, T value) =>
        _c.State.PutAsync(name, JsonSerializer.SerializeToUtf8Bytes(value, Json), _shutdown.Token);

    /// <summary>
    /// A platform-independent game reference: the same executable name yields the same
    /// BunDex key on every OS (no drive or path interpretation).
    /// </summary>
    internal static string GameReference(string executableFileName)
    {
        var name = executableFileName ?? string.Empty;
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        var dot = name.LastIndexOf('.');
        name = (dot >= 0 ? name[..dot] : name).ToLowerInvariant().Replace(':', '-');
        name = name.Length == 0 ? "application" : name;
        return name.Length > 120 ? name[..120] : name;
    }

    internal static Guid DeriveId(string purpose, string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"companion.orchestration.v1|{purpose}|{value}"))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}
