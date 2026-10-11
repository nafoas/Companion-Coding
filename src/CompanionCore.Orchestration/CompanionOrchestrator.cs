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
    TimeProvider Time);

public sealed record OrchestratorSnapshot(
    Guid? TargetSessionId,
    WatchbunSnapshot? Watchbun,
    ConversationSnapshot Conversation,
    AttentionSnapshot? Attention,
    bool PhotographPending,
    bool BridgeInFlight,
    IReadOnlyList<string> UnconsolidatedSessions,
    long Faults,
    Guid ConversationLineage);

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
    private Task? _loop;
    private bool _bridgeInFlight;
    private BridgeOutcomeKind? _lastBraincase;
    private DateTimeOffset? _lastSemanticLook;
    private DateTimeOffset _lastPersist;
    private long _faults;
    private int _disposed;

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
            _conversation.Checkpoint().CoordinatorId)));

    public long Faults => Interlocked.Read(ref _faults);

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

    /// <summary>Shows the camera action; the next current sheet becomes the photograph.</summary>
    public Task<KeepsakeRefusal> TakePhotographAsync() =>
        InvokeAsync(() =>
        {
            if (_session is not { } session)
            {
                return Task.FromResult(KeepsakeRefusal.UnknownAction);
            }

            var begun = _camera.BeginCameraAction(session.Grant, Now());
            foreach (var intent in begun.Intents)
            {
                Publish(new CompanionNotice(CompanionNoticeKind.Keepsake, Keepsake: intent));
            }

            if (begun.Action is { } action)
            {
                _pendingPhoto = action;
            }

            return Task.FromResult(begun.Refusal);
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
            Notice?.Invoke(this, notice);
        }
        catch (Exception exception)
        {
            // A presentation failure must never break the core pipeline.
            RecordFault(exception);
        }
    }

    private DateTimeOffset Now() => _c.Time.GetUtcNow();

    // ---- event entry points (post only; nothing runs on the caller's thread) ---------

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
                    session.Grant = refreshed;
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
                current.Grant = grant;
                return;
            }

            await CloseSessionObjectsAsync().ConfigureAwait(false);
        }

        var reference = $"target-session:{grant.TargetSessionId:N}";
        _lastSemanticLook = null;
        var transcript = SessionTranscript.Open(_c.Transcripts, grant.TargetSessionId, _c.Privacy, now);
        _session = new ActiveSession(
            grant,
            new AttentionEngine(grant.TargetSessionId, now, _options.Attention),
            transcript,
            reference,
            GameReference(grant.Target.ExecutableFileName));

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

        if (_pendingPhoto is { } action)
        {
            await PhotographAsync(session, action, sheet).ConfigureAwait(false);
            return;
        }

        var allowed = _watchbun?.Current.SemanticSpendingAllowed ?? true;
        if (_bridgeInFlight || !allowed)
        {
            sheet.Dispose();
            return;
        }

        var now = Now();
        var interval = _options.SemanticCadence.For(session.Attention.Current.State, sheet.Metadata.ChangeScore);
        if (_lastSemanticLook is { } last && now >= last && now - last < interval)
        {
            sheet.Dispose();
            return;
        }

        _lastSemanticLook = now;

        _bridgeInFlight = true;
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
        var evidence = SemanticEvidenceMapper.Map(interpretation, metadata, now, session.Topics);
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

    private async Task PhotographAsync(ActiveSession session, CameraAction action, AttentionSheet sheet)
    {
        PhotographFrame? frame = null;
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
                // An older sheet: keep waiting for one captured inside the action window.
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
            HandleConversation(_conversation.ObserveAttention(update.Snapshot.State, now));
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

        var originals = await _c.Repository
            .RetrieveAsync(new MemoryQuery { SessionReference = intent.Session, Limit = MemoryQuery.MaximumLimit }, _shutdown.Token)
            .ConfigureAwait(false);
        var entries = originals.Where(memory => RecallMetadata.Parse(memory.Record.RetrievalMetadataJson).Kind
            is not RecallRecordKind.Summary and not RecallRecordKind.Highlight).ToArray();
        if (entries.Length > 0)
        {
            var plan = ConsolidationPlanner.PlanSessionSummary(intent.OperationId, intent.Now, intent.Session, intent.Game, save: null, entries);
            var result = await _c.Repository.WriteGate.SubmitAsync(plan.Proposal, _shutdown.Token).ConfigureAwait(false);
            if (!result.IsAccepted)
            {
                // Left pending; the next start or consolidation replays it.
                return;
            }

            Publish(new CompanionNotice(CompanionNoticeKind.Consolidated, Key: intent.Session, Count: entries.Length));
        }

        var remaining = await ReadPendingAsync().ConfigureAwait(false);
        remaining.RemoveAll(item => item.Session == intent.Session);
        await WriteJsonAsync(ConsolidationStateName, remaining.ToArray()).ConfigureAwait(false);
        _watchbunSessions.Remove(intent.Session);
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
