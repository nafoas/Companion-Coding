using System.Security.Cryptography;
using System.Text;
using CompanionCore.Capture.Contracts;
using CompanionCore.Memory;
using CompanionCore.Privacy;
using CompanionCore.Runtime.Diagnostics;

namespace CompanionCore.Api;

/// <summary>
/// The stateless faraway-Braincase bridge. It builds every request from local state, owns
/// bounded retry/timeout/cancellation, validates every reply through one strict parser
/// and the local allowlist, and admits accepted appends only through
/// <see cref="LocalWriteGate"/> under the request's privacy generation. It never owns
/// identity or durable memory, holds no remote session, and sends nothing while napping.
/// </summary>
public sealed class ApiBridge : IAsyncDisposable
{
    private const string DiagnosticsCategory = "braincase";

    private readonly ISemanticProvider _provider;
    private readonly LocalWriteGate _writeGate;
    private readonly ResumePacketBuilder _packets;
    private readonly ILocalMemoryReader _memory;
    private readonly RuntimePrivacyState _privacy;
    private readonly ICredentialStore _credentials;
    private readonly BridgeOptions _options;
    private readonly TimeProvider _time;
    private readonly IDiagnosticsSink _diagnostics;
    private readonly BraincaseJournal _journal;
    private readonly SemaphoreSlim _operationSlot = new(1, 1);

    // The Glance lane (VIS-02): at most one Glance alongside at most one Inspect.
    private readonly SemaphoreSlim _glanceSlot = new(1, 1);
    private long _glances;
    private long _glancesMissed;
    private long _glancesBusy;
    private long _lastGlanceTicks;
    private long _maximumGlanceTicks;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private CancellationTokenSource _pendingWork = new();
    private ProviderUnavailableReason _announcedUnavailable;
    private int _disposeState;
    private volatile bool _journalFaulted;

    private long _attemptsSent;
    private long _retries;
    private long _timeouts;
    private long _transientFailures;
    private long _invalidResponses;
    private long _rejectedProposalBatches;
    private long _commits;
    private long _alreadyCommitted;
    private long _notAuthorized;
    private long _busy;
    private long _refusedWhileNapping;

    private ApiBridge(
        ISemanticProvider provider,
        LocalWriteGate writeGate,
        ILocalMemoryReader memory,
        RuntimePrivacyState privacy,
        ICredentialStore credentials,
        BridgeOptions options,
        TimeProvider time,
        IDiagnosticsSink diagnostics,
        BraincaseJournal journal)
    {
        _provider = provider;
        _writeGate = writeGate;
        _packets = new ResumePacketBuilder(memory);
        _memory = memory;
        _privacy = privacy;
        _credentials = credentials;
        _options = options;
        _time = time;
        _diagnostics = diagnostics;
        _journal = journal;
    }

    /// <summary>Raised at most once per operation, only for a privacy-current result.</summary>
    public event EventHandler<SemanticInterpretation>? InterpretationProduced;

    /// <summary>Naptime once per nap episode, Awake after a successful probe, Unavailable once per reason.</summary>
    public event EventHandler<BraincaseNotice>? NoticeRaised;

    public string ProviderName => _provider.ProviderName;

    public BraincaseNapStatus NapStatus
    {
        get
        {
            var state = _journal.State;
            return new BraincaseNapStatus(state.NapReason, state.NapUntil);
        }
    }

    public static ApiBridge Open(
        ISemanticProvider provider,
        LocalWriteGate writeGate,
        ILocalMemoryReader memory,
        RuntimePrivacyState privacy,
        BraincaseStateLocation stateLocation,
        ICredentialStore credentials,
        BridgeOptions? options = null,
        TimeProvider? time = null,
        IDiagnosticsSink? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(writeGate);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(privacy);
        ArgumentNullException.ThrowIfNull(stateLocation);
        ArgumentNullException.ThrowIfNull(credentials);
        options ??= new BridgeOptions();
        options.Validate();
        time ??= TimeProvider.System;
        var journal = BraincaseJournal.Open(stateLocation, time.GetUtcNow());
        return new ApiBridge(
            provider,
            writeGate,
            memory,
            privacy,
            credentials,
            options,
            time,
            diagnostics ?? NullDiagnosticsSink.Instance,
            journal);
    }

    /// <summary>
    /// Interprets one authorized attention sheet. The bridge takes ownership of
    /// <paramref name="sheet"/> and disposes (zeroes) it on every path.
    /// </summary>
    public async Task<BridgeOutcome> InterpretAttentionSheetAsync(
        AttentionSheet sheet,
        CaptureAuthorizationGrant grant,
        ResumeContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        using (sheet)
        {
            ArgumentNullException.ThrowIfNull(grant);
            if (Volatile.Read(ref _disposeState) != 0)
            {
                return BridgeOutcome.Simple(BridgeOutcomeKind.Disposed);
            }

            // A keepsake photograph is never uploaded: interpretation uses attention sheets only
            // ("no full-resolution upload merely because the source display supports it").
            if (sheet.Length == 0
                || sheet.Metadata.Kind == AttentionSheetKind.Photograph
                || !sheet.Metadata.Matches(grant)
                || !_privacy.IsCurrent(grant.Generation))
            {
                Interlocked.Increment(ref _notAuthorized);
                return BridgeOutcome.Simple(BridgeOutcomeKind.NotAuthorized);
            }

            if (!_operationSlot.Wait(0))
            {
                if (Volatile.Read(ref _disposeState) != 0)
                {
                    return BridgeOutcome.Simple(BridgeOutcomeKind.Disposed);
                }

                Interlocked.Increment(ref _busy);
                return BridgeOutcome.Simple(BridgeOutcomeKind.Busy);
            }

            try
            {
                if (Volatile.Read(ref _disposeState) != 0)
                {
                    return BridgeOutcome.Simple(BridgeOutcomeKind.Disposed);
                }

                return await RunOperationAsync(sheet, grant, context ?? ResumeContext.Empty, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _operationSlot.Release();
            }
        }
    }

    /// <summary>
    /// One Glance (VIS-02): a single, short, peripheral look at an authorized sheet. It runs
    /// alongside an Inspect, never replaces one, and can never propose a memory. It obeys the
    /// same privacy fence, nap, and daily budget; the bridge takes ownership of
    /// <paramref name="sheet"/> and disposes (zeroes) it on every path.
    /// </summary>
    public async Task<BridgeOutcome> GlanceAsync(
        AttentionSheet sheet,
        CaptureAuthorizationGrant grant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        using (sheet)
        {
            ArgumentNullException.ThrowIfNull(grant);
            if (Volatile.Read(ref _disposeState) != 0)
            {
                return BridgeOutcome.Simple(BridgeOutcomeKind.Disposed);
            }

            if (sheet.Length == 0
                || sheet.Metadata.Kind == AttentionSheetKind.Photograph
                || !sheet.Metadata.Matches(grant)
                || !_privacy.IsCurrent(grant.Generation))
            {
                Interlocked.Increment(ref _notAuthorized);
                return BridgeOutcome.Simple(BridgeOutcomeKind.NotAuthorized);
            }

            if (!_glanceSlot.Wait(0))
            {
                Interlocked.Increment(ref _glancesBusy);
                return BridgeOutcome.Simple(Volatile.Read(ref _disposeState) != 0 ? BridgeOutcomeKind.Disposed : BridgeOutcomeKind.Busy);
            }

            try
            {
                if (Volatile.Read(ref _disposeState) != 0)
                {
                    return BridgeOutcome.Simple(BridgeOutcomeKind.Disposed);
                }

                return await RunGlanceAsync(sheet, grant, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _glanceSlot.Release();
            }
        }
    }

    public GlanceDiagnostics GetGlanceDiagnostics() => new(
        Interlocked.Read(ref _glances),
        Interlocked.Read(ref _glancesMissed),
        Interlocked.Read(ref _glancesBusy),
        TimeSpan.FromTicks(Interlocked.Read(ref _lastGlanceTicks)),
        TimeSpan.FromTicks(Interlocked.Read(ref _maximumGlanceTicks)));

    /// <summary>
    /// Cancels any in-flight request without retry, write, or output. The privacy stop
    /// path calls this; generation fencing still applies if a result races the call.
    /// </summary>
    public void CancelPendingWork()
    {
        CancellationTokenSource previous;
        lock (_gate)
        {
            previous = _pendingWork;
            _pendingWork = new CancellationTokenSource();
        }

        TryCancel(previous);
    }

    public BridgeDiagnosticsSnapshot GetDiagnosticsSnapshot()
    {
        var state = _journal.State;
        var recovery = _journal.Recovery;
        return new BridgeDiagnosticsSnapshot(
            state.Operations,
            state.Completed,
            state.Interrupted,
            Interlocked.Read(ref _attemptsSent),
            Interlocked.Read(ref _retries),
            Interlocked.Read(ref _timeouts),
            Interlocked.Read(ref _transientFailures),
            Interlocked.Read(ref _invalidResponses),
            Interlocked.Read(ref _rejectedProposalBatches),
            Interlocked.Read(ref _commits),
            Interlocked.Read(ref _alreadyCommitted),
            Interlocked.Read(ref _notAuthorized),
            Interlocked.Read(ref _busy),
            Interlocked.Read(ref _refusedWhileNapping),
            state.NapEpisodes,
            new BraincaseNapStatus(state.NapReason, state.NapUntil),
            state.TotalInputUnits,
            state.TotalOutputUnits,
            state.UsageInputUnitsOn(DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime)),
            recovery.TornTailTruncated,
            recovery.CorruptJournalPreserved);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        TryCancel(_lifetime);
        CancelPendingWork();

        // The in-flight operation observes cancellation (abandoning a non-cooperative
        // provider), records its outcome, and releases the slot; then the journal closes.
        await _operationSlot.WaitAsync().ConfigureAwait(false);
        await _glanceSlot.WaitAsync().ConfigureAwait(false);
        _journal.Dispose();
        _lifetime.Dispose();
    }

    private async Task<BridgeOutcome> RunOperationAsync(
        AttentionSheet sheet,
        CaptureAuthorizationGrant grant,
        ResumeContext context,
        CancellationToken cancellationToken)
    {
        if (_journalFaulted)
        {
            return BridgeOutcome.Simple(BridgeOutcomeKind.LocalStateUnavailable);
        }

        var now = _time.GetUtcNow();
        var state = _journal.State;
        var isProbe = false;
        if (state.NapReason != BraincaseNapReason.None)
        {
            if (state.NapUntil is not { } until || now < until)
            {
                Interlocked.Increment(ref _refusedWhileNapping);
                return BridgeOutcome.Simple(BridgeOutcomeKind.Napping) with { NapReason = state.NapReason };
            }

            isProbe = true;
        }

        var operationId = Guid.CreateVersion7(now);
        CancellationToken pendingToken;
        lock (_gate)
        {
            pendingToken = _pendingWork.Token;
        }

        using var operation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetime.Token,
            pendingToken);
        var token = operation.Token;

        ResumePacket packet;
        try
        {
            packet = await _packets.BuildAsync(
                    context,
                    state.RecentSubjects.ToArray(),
                    SessionReference(grant),
                    now,
                    token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return BridgeOutcome.Simple(BridgeOutcomeKind.Cancelled, operationId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Log("local-state-unavailable", exception.GetType().Name);
            return BridgeOutcome.Simple(BridgeOutcomeKind.LocalStateUnavailable, operationId);
        }

        var description = Describe(sheet);
        var request = new SemanticRequest(
            operationId,
            SemanticOperationKind.InterpretAttentionSheet,
            attempt: 1,
            packet,
            description,
            sheet.EncodedImage);
        var estimatedInputUnits = EstimateInputUnits(request);

        var budgetDay = DateOnly.FromDateTime(now.UtcDateTime);
        if (OverBudget(state, budgetDay, inspectInput: estimatedInputUnits, glanceInput: 0))
        {
            var nextDay = new DateTimeOffset(budgetDay.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            return EnterNap(
                sheet,
                operationId,
                started: false,
                attempts: 0,
                inputUnits: 0,
                BraincaseNapReason.LocalBudget,
                nextDay,
                TimeSpan.Zero,
                announce: !isProbe);
        }

        if (!TryAppend(new BraincaseJournalEntry
            {
                At = now,
                Type = BraincaseJournalEntryType.Started,
                Op = operationId,
            }))
        {
            return BridgeOutcome.Simple(BridgeOutcomeKind.LocalStateUnavailable, operationId);
        }

        var maximumAttempts = isProbe ? 1 : _options.MaximumAttempts;
        var attempts = 0;
        long inputUnits = 0;
        ProviderReply? success = null;
        ProviderReply? terminal = null;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            if (!_privacy.IsCurrent(grant.Generation))
            {
                return Complete(BridgeOutcome.Simple(BridgeOutcomeKind.PrivacyFenced, operationId, attempts), inputUnits);
            }

            if (token.IsCancellationRequested)
            {
                return Complete(BridgeOutcome.Simple(BridgeOutcomeKind.Cancelled, operationId, attempts), inputUnits);
            }

            attempts = attempt;
            inputUnits += estimatedInputUnits;
            Interlocked.Increment(ref _attemptsSent);
            AttemptResult result;
            try
            {
                result = await InvokeAttemptAsync(request.WithAttempt(attempt), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return Complete(BridgeOutcome.Simple(BridgeOutcomeKind.Cancelled, operationId, attempts), inputUnits);
            }

            if (result.Reply is { IsSuccess: true } reply)
            {
                success = reply;
                break;
            }

            if (result.Reply is { FailureKind: ProviderFailureKind.Outage
                    or ProviderFailureKind.RateLimited
                    or ProviderFailureKind.Unavailable } terminalReply)
            {
                terminal = terminalReply;
                break;
            }

            if (result.TimedOut)
            {
                Interlocked.Increment(ref _timeouts);
            }
            else
            {
                Interlocked.Increment(ref _transientFailures);
            }

            if (attempt < maximumAttempts)
            {
                Interlocked.Increment(ref _retries);
                try
                {
                    await Task.Delay(Backoff(attempt), _time, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return Complete(BridgeOutcome.Simple(BridgeOutcomeKind.Cancelled, operationId, attempts), inputUnits);
                }
            }
        }

        // The remote exchange is over: release the RAM-only image now, on every path.
        sheet.Dispose();
        var replyTime = _time.GetUtcNow();

        if (success is null)
        {
            switch (terminal?.FailureKind)
            {
                case ProviderFailureKind.Unavailable when isProbe:
                    // A probe that finds the provider unavailable is a failed probe: the nap
                    // continues silently so later requests do not each become a new probe.
                    var probeInterval = NextProbeInterval(isProbe, state);
                    var extended = EnterNap(
                        sheet,
                        operationId,
                        started: true,
                        attempts,
                        inputUnits,
                        state.NapReason,
                        replyTime + probeInterval,
                        probeInterval,
                        announce: false) with
                    {
                        UnavailableReason = terminal.UnavailableReason,
                    };
                    AnnounceUnavailable(terminal.UnavailableReason);
                    return extended;
                case ProviderFailureKind.Unavailable:
                    var completed = Complete(
                        BridgeOutcome.Simple(BridgeOutcomeKind.Unavailable, operationId, attempts) with
                        {
                            UnavailableReason = terminal.UnavailableReason,
                        },
                        inputUnits);
                    AnnounceUnavailable(terminal.UnavailableReason);
                    return completed;
                case ProviderFailureKind.RateLimited:
                    return EnterNap(
                        sheet,
                        operationId,
                        started: true,
                        attempts,
                        inputUnits,
                        BraincaseNapReason.RateLimited,
                        replyTime + ClampRetryAfter(terminal.RetryAfter),
                        TimeSpan.Zero,
                        announce: !isProbe);
                default:
                    var interval = NextProbeInterval(isProbe, state);
                    return EnterNap(
                        sheet,
                        operationId,
                        started: true,
                        attempts,
                        inputUnits,
                        terminal is null && !isProbe ? BraincaseNapReason.RetriesExhausted : BraincaseNapReason.Outage,
                        replyTime + interval,
                        interval,
                        announce: !isProbe);
            }
        }

        return await ProcessSuccessAsync(
                success.ResponseJson!,
                operationId,
                attempts,
                inputUnits,
                estimatedInputUnits,
                isProbe,
                packet,
                description,
                grant,
                replyTime)
            .ConfigureAwait(false);
    }

    private async Task<BridgeOutcome> RunGlanceAsync(
        AttentionSheet sheet,
        CaptureAuthorizationGrant grant,
        CancellationToken cancellationToken)
    {
        if (_journalFaulted)
        {
            return BridgeOutcome.Simple(BridgeOutcomeKind.LocalStateUnavailable);
        }

        var now = _time.GetUtcNow();
        var state = _journal.State;

        // A napping Braincase sends nothing. Only the Inspect lane probes for waking.
        if (state.NapReason != BraincaseNapReason.None)
        {
            Interlocked.Increment(ref _refusedWhileNapping);
            return BridgeOutcome.Simple(BridgeOutcomeKind.Napping) with { NapReason = state.NapReason };
        }

        var operationId = Guid.CreateVersion7(now);
        CancellationToken pendingToken;
        lock (_gate)
        {
            pendingToken = _pendingWork.Token;
        }

        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token, pendingToken);
        var token = operation.Token;

        // A Glance carries no memories: only the session and application it looks at.
        var packet = new ResumePacket(
            Guid.CreateVersion7(now),
            now,
            SessionReference(grant),
            null,
            [],
            [],
            0,
            false);
        var description = Describe(sheet);
        var request = new SemanticRequest(operationId, SemanticOperationKind.Glance, attempt: 1, packet, description, sheet.EncodedImage);
        var estimatedInputUnits = EstimateInputUnits(request);

        var budgetDay = DateOnly.FromDateTime(now.UtcDateTime);
        if (OverBudget(state, budgetDay, inspectInput: 0, glanceInput: estimatedInputUnits))
        {
            var nextDay = new DateTimeOffset(budgetDay.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            return EnterNap(sheet, operationId, started: false, attempts: 0, inputUnits: 0, BraincaseNapReason.LocalBudget, nextDay, TimeSpan.Zero, announce: true);
        }

        if (!_privacy.IsCurrent(grant.Generation))
        {
            return BridgeOutcome.Simple(BridgeOutcomeKind.PrivacyFenced, operationId);
        }

        Interlocked.Increment(ref _attemptsSent);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        AttemptResult result;
        try
        {
            result = await InvokeAttemptAsync(request, token, _options.GlanceTimeout).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            RecordGlance(operationId, estimatedInputUnits, 0);
            return BridgeOutcome.Simple(BridgeOutcomeKind.Cancelled, operationId, 1);
        }
        finally
        {
            sheet.Dispose();
        }

        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
        Interlocked.Exchange(ref _lastGlanceTicks, elapsed.Ticks);
        InterlockedMax(ref _maximumGlanceTicks, elapsed.Ticks);

        if (result.Reply is not { IsSuccess: true } reply)
        {
            // The request may have been billed: count its estimate either way.
            RecordGlance(operationId, estimatedInputUnits, 0);
            switch (result.Reply?.FailureKind)
            {
                case ProviderFailureKind.Unavailable:
                    AnnounceUnavailable(result.Reply.UnavailableReason);
                    return BridgeOutcome.Simple(BridgeOutcomeKind.Unavailable, operationId, 1) with
                    {
                        UnavailableReason = result.Reply.UnavailableReason,
                    };
                case ProviderFailureKind.RateLimited:
                    return EnterNap(sheet, operationId, started: false, 1, 0, BraincaseNapReason.RateLimited,
                        _time.GetUtcNow() + ClampRetryAfter(result.Reply.RetryAfter), TimeSpan.Zero, announce: true);
                case ProviderFailureKind.Outage:
                    return EnterNap(sheet, operationId, started: false, 1, 0, BraincaseNapReason.Outage,
                        _time.GetUtcNow() + _options.OutageInitialProbeInterval, _options.OutageInitialProbeInterval, announce: true);
                default:
                    // Timed out, transient, or faulted: a stale glance is simply missed.
                    if (result.TimedOut)
                    {
                        Interlocked.Increment(ref _timeouts);
                    }

                    Interlocked.Increment(ref _glancesMissed);
                    return BridgeOutcome.Simple(BridgeOutcomeKind.Missed, operationId, 1);
            }
        }

        lock (_gate)
        {
            _announcedUnavailable = ProviderUnavailableReason.None;
        }

        var responseJson = reply.ResponseJson!;
        ParsedGlance? parsed = null;
        var invalid = _credentials.AnyAppearsIn(responseJson)
            ? SemanticResponseInvalidReason.CredentialEcho
            : GlanceResponseParser.TryParse(responseJson, operationId, description.RegionKinds.ToHashSet(), out parsed, out var parseReason)
                ? SemanticResponseInvalidReason.None
                : parseReason;
        if (invalid == SemanticResponseInvalidReason.None && _credentials.AnyAppearsIn(parsed!.Label))
        {
            invalid = SemanticResponseInvalidReason.CredentialEcho;
            parsed = null;
        }

        var inputUnits = parsed?.InputUnits ?? estimatedInputUnits;
        var outputUnits = parsed?.OutputUnits ?? (Encoding.UTF8.GetByteCount(responseJson) + 3) / 4;
        RecordGlance(operationId, inputUnits, outputUnits);
        if (invalid != SemanticResponseInvalidReason.None)
        {
            Interlocked.Increment(ref _invalidResponses);
            Interlocked.Increment(ref _glancesMissed);
            return BridgeOutcome.Simple(BridgeOutcomeKind.InvalidResponse, operationId, 1) with { InvalidReason = invalid };
        }

        // Output is fenced like an Inspect's: a privacy stop during the look suppresses it.
        if (!_privacy.IsCurrent(grant.Generation))
        {
            return BridgeOutcome.Simple(BridgeOutcomeKind.PrivacyFenced, operationId, 1);
        }

        Interlocked.Increment(ref _glances);
        return BridgeOutcome.Simple(BridgeOutcomeKind.Glanced, operationId, 1) with
        {
            Glance = new GlanceReading(
                operationId,
                grant.TargetSessionId,
                parsed!.Interest,
                parsed.Confidence,
                parsed.ChangedRegion,
                parsed.Label,
                parsed.Escalate,
                parsed.NextLook),
        };
    }

    /// <summary>The day's spend across both lanes, plus a pending estimate, against the Bun Budget.</summary>
    private bool OverBudget(BraincaseJournalState state, DateOnly day, long inspectInput, long glanceInput)
    {
        var inspectIn = state.UsageInputUnitsOn(day);
        var inspectOut = state.UsageOutputUnitsOn(day);
        var glanceIn = state.GlanceInputUnitsOn(day);
        var glanceOut = state.GlanceOutputUnitsOn(day);
        return (_options.DailyInputUnitBudget is { } budget
                && inspectIn + glanceIn + inspectInput + glanceInput > budget)
            || (_options.DailyCostBudget is { } cost
                && cost.CostOf(inspectIn + inspectInput, inspectOut, glanceIn + glanceInput, glanceOut) > cost.LimitPerDay);
    }

    private void RecordGlance(Guid operationId, long inputUnits, long outputUnits)
    {
        var now = _time.GetUtcNow();
        if (TryAppend(new BraincaseJournalEntry
            {
                At = now,
                Type = BraincaseJournalEntryType.Glanced,
                Op = operationId,
                InputUnits = inputUnits,
                OutputUnits = outputUnits,
            }))
        {
            try
            {
                _journal.CompactIfNeeded(now);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log("journal-compaction-failed", exception.GetType().Name);
            }
        }
    }

    private static void InterlockedMax(ref long target, long value)
    {
        var current = Interlocked.Read(ref target);
        while (value > current)
        {
            var observed = Interlocked.CompareExchange(ref target, value, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    private async Task<BridgeOutcome> ProcessSuccessAsync(
        string responseJson,
        Guid operationId,
        int attempts,
        long inputUnits,
        long attemptInputEstimate,
        bool isProbe,
        ResumePacket packet,
        AttentionSheetDescription description,
        CaptureAuthorizationGrant grant,
        DateTimeOffset replyTime)
    {
        // The service answered: a probe ends the nap and an unavailability announcement resets.
        if (isProbe)
        {
            if (TryAppend(new BraincaseJournalEntry { At = replyTime, Type = BraincaseJournalEntryType.Awake }))
            {
                RaiseNotice(new BraincaseNotice(
                    BraincaseNoticeKind.Awake,
                    BraincaseNapReason.None,
                    ProviderUnavailableReason.None));
            }
        }

        lock (_gate)
        {
            _announcedUnavailable = ProviderUnavailableReason.None;
        }

        ParsedSemanticResponse? parsed = null;
        var invalid = _credentials.AnyAppearsIn(responseJson)
            ? SemanticResponseInvalidReason.CredentialEcho
            : SemanticResponseParser.TryParse(
                responseJson,
                operationId,
                description.RegionKinds.ToHashSet(),
                out parsed,
                out var parseReason)
                ? SemanticResponseInvalidReason.None
                : parseReason;

        // JSON escapes can hide a secret from the raw-text check; also check every decoded field.
        if (invalid == SemanticResponseInvalidReason.None && DecodedText(parsed!).Any(_credentials.AnyAppearsIn))
        {
            invalid = SemanticResponseInvalidReason.CredentialEcho;
            parsed = null;
        }
        var outputUnits = parsed?.OutputUnits ?? (Encoding.UTF8.GetByteCount(responseJson) + 3) / 4;

        // A provider-reported input count replaces the local estimate for the answered attempt,
        // so the daily budget follows real usage; earlier failed attempts keep their estimates.
        if (parsed?.InputUnits is { } reportedInput)
        {
            inputUnits = Math.Max(0, inputUnits - attemptInputEstimate) + reportedInput;
        }
        if (invalid != SemanticResponseInvalidReason.None)
        {
            Interlocked.Increment(ref _invalidResponses);
            return Complete(
                BridgeOutcome.Simple(BridgeOutcomeKind.InvalidResponse, operationId, attempts) with
                {
                    InvalidReason = invalid,
                },
                inputUnits,
                outputUnits);
        }

        var decision = RemoteProposalAllowlist.Evaluate(
            operationId,
            parsed!.Proposals,
            packet,
            replyTime,
            _provider.ProviderName);
        if (decision.Rejection != RemoteProposalRejection.None)
        {
            Interlocked.Increment(ref _rejectedProposalBatches);
        }

        var memory = new BridgeMemoryResult(parsed.Proposals.Count, decision.Rejection, null);
        if (!_privacy.TryAcquireAdmissionLease(grant.Generation, out var lease))
        {
            return Complete(
                new BridgeOutcome(BridgeOutcomeKind.PrivacyFenced, operationId, attempts, null, memory),
                inputUnits,
                outputUnits);
        }

        SemanticInterpretation? interpretation = null;
        using (lease)
        {
            var admitted = decision.Proposal;
            if (admitted is not null && _options.NoveltyGate is { } novelty)
            {
                // Recorded as sightings either way; only what is new becomes a memory.
                var novel = await novelty.SiftAsync(admitted.Records, _memory, CancellationToken.None).ConfigureAwait(false);
                memory = memory with { Repeats = admitted.Records.Count - novel.Count };
                admitted = novel.Count == 0 ? null : new AppendMemoryProposal(admitted.LocalOperationId, novel);
            }

            if (admitted is { } proposal)
            {
                // An admitted append is completed rather than cancelled half-way.
                var gateResult = await _writeGate
                    .SubmitAsync(proposal, grant.Generation, CancellationToken.None)
                    .ConfigureAwait(false);
                memory = memory with { GateResult = gateResult };
                if (gateResult.Status == WriteGateStatus.Committed)
                {
                    Interlocked.Increment(ref _commits);
                }
                else if (gateResult.Status == WriteGateStatus.AlreadyCommitted)
                {
                    Interlocked.Increment(ref _alreadyCommitted);
                }
            }

            // Output is fenced as tightly as possible: a stop that landed while the
            // already-admitted append drained still suppresses publication.
            if (_privacy.IsCurrent(grant.Generation))
            {
                interpretation = new SemanticInterpretation(
                    operationId,
                    grant.TargetSessionId,
                    parsed.Summary,
                    parsed.Observations)
                {
                    Encounters = parsed.Encounters,
                };
                Publish(interpretation);
            }
        }

        var subjects = memory.GateResult?.IsAccepted == true ? decision.Subjects : [];
        return Complete(
            new BridgeOutcome(
                interpretation is null ? BridgeOutcomeKind.PrivacyFenced : BridgeOutcomeKind.Interpreted,
                operationId,
                attempts,
                interpretation,
                memory),
            inputUnits,
            outputUnits,
            subjects);
    }

    private Task<AttemptResult> InvokeAttemptAsync(SemanticRequest request, CancellationToken token) =>
        InvokeAttemptAsync(request, token, _options.AttemptTimeout);

    private async Task<AttemptResult> InvokeAttemptAsync(SemanticRequest request, CancellationToken token, TimeSpan timeout)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<ProviderReply> call;
        try
        {
            call = _provider.InterpretAsync(request, attempt.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Provider exception text is never logged; it may echo remote or secret content.
            return AttemptResult.Fault;
        }

        if (call is null)
        {
            return AttemptResult.Fault;
        }

        try
        {
            var reply = await call.WaitAsync(timeout, _time, token).ConfigureAwait(false);
            return reply is null ? AttemptResult.Fault : new AttemptResult(reply, TimedOut: false);
        }
        catch (TimeoutException)
        {
            TryCancel(attempt);
            Abandon(call);
            return AttemptResult.Timeout;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            TryCancel(attempt);
            Abandon(call);
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return AttemptResult.Fault;
        }
    }

    private BridgeOutcome EnterNap(
        AttentionSheet sheet,
        Guid operationId,
        bool started,
        int attempts,
        long inputUnits,
        BraincaseNapReason reason,
        DateTimeOffset until,
        TimeSpan probeInterval,
        bool announce)
    {
        // 1. Checkpoint the nap durably.
        TryAppend(new BraincaseJournalEntry
        {
            At = _time.GetUtcNow(),
            Type = BraincaseJournalEntryType.Nap,
            Op = operationId,
            NapReason = reason,
            NapUntil = until,
            ProbeIntervalSeconds = (long)probeInterval.TotalSeconds,
        });

        // 2. Release buffers.
        sheet.Dispose();

        // 3. Record the operation's end.
        var outcome = BridgeOutcome.Simple(BridgeOutcomeKind.Napping, operationId, attempts) with { NapReason = reason };
        if (started)
        {
            outcome = Complete(outcome, inputUnits);
        }

        // 4. One packaged notice per nap episode.
        if (announce)
        {
            RaiseNotice(new BraincaseNotice(BraincaseNoticeKind.Naptime, reason, ProviderUnavailableReason.None));
        }

        Log("nap", reason.ToString());
        return outcome;
    }

    private BridgeOutcome Complete(
        BridgeOutcome outcome,
        long inputUnits,
        long outputUnits = 0,
        IReadOnlyList<string>? subjects = null)
    {
        var now = _time.GetUtcNow();
        if (TryAppend(new BraincaseJournalEntry
            {
                At = now,
                Type = BraincaseJournalEntryType.Completed,
                Op = outcome.OperationId,
                Outcome = outcome.Kind,
                Attempts = outcome.Attempts,
                Proposed = outcome.Memory.ProposedCount,
                Rejection = outcome.Memory.Rejection == RemoteProposalRejection.None ? null : outcome.Memory.Rejection,
                Commit = outcome.Memory.GateResult?.Status.ToString(),
                Subjects = subjects is { Count: > 0 } ? [.. subjects] : null,
                InputUnits = inputUnits,
                OutputUnits = outputUnits,
            }))
        {
            try
            {
                _journal.CompactIfNeeded(now);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log("journal-compaction-failed", exception.GetType().Name);
            }
        }

        Log("operation", $"outcome={outcome.Kind} attempts={outcome.Attempts}");
        return outcome;
    }

    private bool TryAppend(BraincaseJournalEntry entry)
    {
        try
        {
            _journal.Append(entry);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Fail closed: with no trustworthy local record, send nothing further.
            _journalFaulted = true;
            Log("journal-faulted", exception.GetType().Name);
            return false;
        }
    }

    private void AnnounceUnavailable(ProviderUnavailableReason reason)
    {
        lock (_gate)
        {
            if (_announcedUnavailable == reason)
            {
                return;
            }

            _announcedUnavailable = reason;
        }

        RaiseNotice(new BraincaseNotice(BraincaseNoticeKind.Unavailable, BraincaseNapReason.None, reason));
    }

    private void Publish(SemanticInterpretation interpretation)
    {
        foreach (var handler in InterpretationProduced?.GetInvocationList() ?? [])
        {
            try
            {
                ((EventHandler<SemanticInterpretation>)handler)(this, interpretation);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Log("interpretation-handler-fault", exception.GetType().Name);
            }
        }
    }

    private void RaiseNotice(BraincaseNotice notice)
    {
        foreach (var handler in NoticeRaised?.GetInvocationList() ?? [])
        {
            try
            {
                ((EventHandler<BraincaseNotice>)handler)(this, notice);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Log("notice-handler-fault", exception.GetType().Name);
            }
        }
    }

    private TimeSpan Backoff(int attempt)
    {
        var scaled = _options.InitialBackoff.Ticks * (1L << Math.Min(attempt - 1, 20));
        return TimeSpan.FromTicks(Math.Min(scaled, _options.MaximumBackoff.Ticks));
    }

    private TimeSpan NextProbeInterval(bool isProbe, BraincaseJournalState state)
    {
        if (!isProbe || state.ProbeInterval <= TimeSpan.Zero)
        {
            return _options.OutageInitialProbeInterval;
        }

        var doubled = state.ProbeInterval.Ticks >= _options.OutageMaximumProbeInterval.Ticks / 2
            ? _options.OutageMaximumProbeInterval
            : state.ProbeInterval * 2;
        return doubled > _options.OutageMaximumProbeInterval ? _options.OutageMaximumProbeInterval : doubled;
    }

    private TimeSpan ClampRetryAfter(TimeSpan? retryAfter)
    {
        var value = retryAfter ?? _options.RateLimitDefault;
        if (value < _options.RateLimitMinimum)
        {
            return _options.RateLimitMinimum;
        }

        return value > _options.RateLimitMaximum ? _options.RateLimitMaximum : value;
    }

    private void Log(string category, string message) =>
        _diagnostics.Log(DiagnosticsCategory, $"{category}: {message}");

    internal static long EstimateInputUnits(SemanticRequest request) =>
        (Encoding.UTF8.GetByteCount(request.RequestJson) + 3) / 4
        + (request.EncodedImage.Length + 1023) / 1024;

    private static IEnumerable<string> DecodedText(ParsedSemanticResponse parsed)
    {
        yield return parsed.Summary;
        foreach (var observation in parsed.Observations)
        {
            yield return observation.Label;
        }

        foreach (var encounter in parsed.Encounters)
        {
            yield return encounter.Threat;
        }

        foreach (var proposal in parsed.Proposals)
        {
            yield return proposal.Operation;
            yield return proposal.SubjectKey ?? string.Empty;
            yield return proposal.Recollection ?? string.Empty;
            foreach (var entity in proposal.EntityReferences ?? [])
            {
                yield return entity ?? string.Empty;
            }
        }
    }

    private static string SessionReference(CaptureAuthorizationGrant grant) =>
        $"target-session:{grant.TargetSessionId:N}";

    private static AttentionSheetDescription Describe(AttentionSheet sheet)
    {
        var metadata = sheet.Metadata;
        return new AttentionSheetDescription(
            metadata.Kind,
            metadata.Regions.Select(region => region.Kind).ToArray(),
            metadata.SourceWidth,
            metadata.SourceHeight,
            metadata.SheetWidth,
            metadata.SheetHeight,
            AttentionSheet.MediaType,
            sheet.Length,
            Convert.ToHexStringLower(SHA256.HashData(sheet.EncodedImage.Span)));
    }

    private static void Abandon(Task<ProviderReply> call) =>
        _ = call.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static void TryCancel(CancellationTokenSource source)
    {
        try
        {
            source.Cancel();
        }
        catch (AggregateException)
        {
            // A cancellation callback registered by a provider is never allowed to escape.
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private sealed record AttemptResult(ProviderReply? Reply, bool TimedOut)
    {
        internal static AttemptResult Fault { get; } = new(null, false);

        internal static AttemptResult Timeout { get; } = new(null, true);
    }
}
