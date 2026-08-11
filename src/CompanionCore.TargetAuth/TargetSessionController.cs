using CompanionCore.Capture.Contracts;
using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth;

/// <summary>
/// Serializes worker control around the one authorization service. Privacy revocation
/// deliberately occurs before this controller waits for its operation lock, so a hung
/// start/stop cannot keep old work current.
/// </summary>
public sealed class TargetSessionController : IAsyncDisposable
{
    private readonly TargetAuthorizationService _authorization;
    private readonly ICaptureWorker _worker;
    private readonly AuthorizedFrameAdmissionGate _frameGate;
    private readonly Func<CaptureFrameMetadata, PrivacyAssessment> _assessmentProvider;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _workGate = new();
    private CancellationTokenSource? _targetWork;
    private bool _cleanupComplete = true;
    private bool _disposed;

    public TargetSessionController(
        TargetAuthorizationService authorization,
        ICaptureWorker worker,
        RuntimePrivacyState privacyState,
        LocalPrivacyGuard privacyGuard,
        Func<CaptureFrameMetadata, PrivacyAssessment>? assessmentProvider = null)
    {
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _frameGate = new AuthorizedFrameAdmissionGate(
            authorization,
            privacyState ?? throw new ArgumentNullException(nameof(privacyState)),
            privacyGuard ?? throw new ArgumentNullException(nameof(privacyGuard)));
        _assessmentProvider = assessmentProvider ?? (_ => PrivacyAssessment.Clear);
        _worker.FrameProduced += OnFrameProduced;
    }

    public event EventHandler<CaptureFrameMetadata>? FrameAdmitted;

    public event EventHandler<TargetSessionEvent>? SessionEvent;

    public TargetSessionSnapshot CurrentSession => _authorization.CurrentSession;

    public CancellationToken CurrentTargetWorkToken
    {
        get
        {
            lock (_workGate)
            {
                return _targetWork?.Token ?? new CancellationToken(canceled: true);
            }
        }
    }

    public async Task<TargetControllerOperationResult> AuthorizeAsync(
        TargetCandidate candidate,
        bool explicitConsent,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!await EnsureCleanupCompleteAsync().ConfigureAwait(false))
            {
                Publish(TargetSessionEventKind.Failed, candidate, TargetAuthorizationStatus.Failed);
                return new TargetControllerOperationResult(
                    Succeeded: false,
                    TargetSessionEventKind.Failed,
                    Authorization: null,
                    CleanupComplete: false);
            }

            var result = await _authorization.AuthorizeAsync(
                    candidate,
                    explicitConsent,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsAuthorized)
            {
                var eventKind = MapAuthorizationFailure(result.Status);
                var eventCandidate = result.Status is TargetAuthorizationStatus.AnotherTargetActive
                    or TargetAuthorizationStatus.PrivacyPaused
                    ? CurrentSession.Candidate ?? candidate
                    : candidate;
                Publish(eventKind, eventCandidate, result.Status);
                return new TargetControllerOperationResult(
                    Succeeded: false,
                    eventKind,
                    result,
                    _cleanupComplete);
            }

            ReplaceTargetWorkToken();
            try
            {
                await StartWorkerWithCancellationFenceAsync(result.Grant!, cancellationToken)
                    .ConfigureAwait(false);
                if (!_authorization.IsCurrent(result.Grant!))
                {
                    throw new InvalidOperationException(
                        "The authorization grant was revoked before worker start completed.");
                }

                _cleanupComplete = true;
                Publish(TargetSessionEventKind.Authorized, candidate, result.Status);
                return new TargetControllerOperationResult(
                    Succeeded: true,
                    TargetSessionEventKind.Authorized,
                    result,
                    CleanupComplete: true);
            }
            catch (Exception)
            {
                await FailClosedAfterWorkerErrorAsync().ConfigureAwait(false);
                Publish(
                    TargetSessionEventKind.PrivacyPaused,
                    candidate,
                    TargetAuthorizationStatus.PrivacyPaused);
                return new TargetControllerOperationResult(
                    Succeeded: false,
                    TargetSessionEventKind.PrivacyPaused,
                    result,
                    _cleanupComplete);
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Applies an explicit user policy change through the same serialized authority
    /// boundary as start/resume. Denying the currently authorized executable revokes
    /// first and clears worker metadata before the persisted policy becomes visible.
    /// </summary>
    public async Task<TargetPolicy> SetExplicitPolicyAsync(
        TargetCandidate candidate,
        TargetPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policy);

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var current = CurrentSession;
            if (policy.AuthorizationCategory == AuthorizationCategory.Denied
                && current.Phase == TargetSessionPhase.Authorized
                && HasSameExecutablePolicyIdentity(current.Candidate, candidate))
            {
                var pause = _authorization.PauseForPrivacy();
                CancelTargetWork();

                var cleanupComplete = false;
                try
                {
                    await _worker.StopAndClearAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                    cleanupComplete = true;
                }
                catch (Exception)
                {
                    cleanupComplete = false;
                }

                await pause.PrivacyReceipt.AdmittedWorkDrained.ConfigureAwait(false);
                _cleanupComplete = cleanupComplete;
                Publish(
                    TargetSessionEventKind.PrivacyPaused,
                    current.Candidate,
                    TargetAuthorizationStatus.PrivacyPaused);
            }

            return await _authorization.SetExplicitPolicyAsync(
                    candidate,
                    policy,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<PrivacyStopResult> PrivacyStopAsync()
    {
        ThrowIfDisposed();
        // Stop-only and revocation-first. This synchronous call makes every old frame
        // stale before waiting on any asynchronous worker operation below.
        var pause = _authorization.PauseForPrivacy();
        CancelTargetWork();

        await _operationLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var cleared = 0;
            var cleanupComplete = false;
            try
            {
                var result = await _worker.StopAndClearAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                cleared = result.ClearedMetadataCount;
                cleanupComplete = true;
            }
            catch (Exception)
            {
                cleanupComplete = false;
            }

            await pause.PrivacyReceipt.AdmittedWorkDrained.ConfigureAwait(false);
            _cleanupComplete = cleanupComplete;
            Publish(TargetSessionEventKind.PrivacyPaused, CurrentSession.Candidate, null);
            return new PrivacyStopResult(
                pause.PrivacyReceipt.WasAlreadyPaused,
                pause.HadActiveTarget,
                cleanupComplete,
                cleared);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<TargetControllerOperationResult> ResumeExplicitlyAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            // A prior stop failure cannot be bypassed by resume. Retry the exact
            // stop-and-clear fence first and remain paused if it still fails.
            if (!await EnsureCleanupCompleteAsync().ConfigureAwait(false))
            {
                Publish(TargetSessionEventKind.Failed, CurrentSession.Candidate, TargetAuthorizationStatus.Failed);
                return new TargetControllerOperationResult(
                    Succeeded: false,
                    TargetSessionEventKind.Failed,
                    Authorization: null,
                    CleanupComplete: false);
            }

            if (CurrentSession.Phase == TargetSessionPhase.None)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Publish(TargetSessionEventKind.Failed, candidate: null, TargetAuthorizationStatus.Cancelled);
                    return new TargetControllerOperationResult(
                        Succeeded: false,
                        TargetSessionEventKind.Failed,
                        Authorization: null,
                        CleanupComplete: true);
                }

                if (!_authorization.HasExactlyOneAttachedDisplay())
                {
                    Publish(
                        TargetSessionEventKind.DiscoveryBlocked,
                        candidate: null,
                        TargetAuthorizationStatus.UnsupportedDisplayTopology);
                    return new TargetControllerOperationResult(
                        Succeeded: false,
                        TargetSessionEventKind.DiscoveryBlocked,
                        Authorization: null,
                        CleanupComplete: true);
                }

                if (!_authorization.ResumePrivacyWithoutTargetExplicitly())
                {
                    Publish(TargetSessionEventKind.Failed, candidate: null, TargetAuthorizationStatus.Failed);
                    return new TargetControllerOperationResult(
                        Succeeded: false,
                        TargetSessionEventKind.Failed,
                        Authorization: null,
                        CleanupComplete: true);
                }

                Publish(TargetSessionEventKind.PrivacyResumed, candidate: null, status: null);
                return new TargetControllerOperationResult(
                    Succeeded: true,
                    TargetSessionEventKind.PrivacyResumed,
                    Authorization: null,
                    CleanupComplete: true);
            }

            var result = await _authorization.ResumeExplicitlyAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsAuthorized)
            {
                var eventKind = MapAuthorizationFailure(result.Status);
                Publish(eventKind, result.Candidate, result.Status);
                return new TargetControllerOperationResult(false, eventKind, result, _cleanupComplete);
            }

            ReplaceTargetWorkToken();
            try
            {
                await StartWorkerWithCancellationFenceAsync(result.Grant!, cancellationToken)
                    .ConfigureAwait(false);
                if (!_authorization.IsCurrent(result.Grant!))
                {
                    throw new InvalidOperationException(
                        "The resumed authorization grant was revoked before worker start completed.");
                }

                _cleanupComplete = true;
                Publish(TargetSessionEventKind.Resumed, result.Candidate, result.Status);
                return new TargetControllerOperationResult(
                    Succeeded: true,
                    TargetSessionEventKind.Resumed,
                    result,
                    CleanupComplete: true);
            }
            catch (Exception)
            {
                await FailClosedAfterWorkerErrorAsync().ConfigureAwait(false);
                Publish(
                    TargetSessionEventKind.PrivacyPaused,
                    result.Candidate,
                    TargetAuthorizationStatus.PrivacyPaused);
                return new TargetControllerOperationResult(
                    Succeeded: false,
                    TargetSessionEventKind.PrivacyPaused,
                    result,
                    _cleanupComplete);
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<bool> HandleDisplayTopologyChangedAsync()
    {
        ThrowIfDisposed();
        if (_authorization.HasExactlyOneAttachedDisplay())
        {
            return false;
        }

        var hadTargetSession = CurrentSession.Phase != TargetSessionPhase.None;
        if (hadTargetSession)
        {
            await PrivacyStopAsync().ConfigureAwait(false);
        }

        Publish(TargetSessionEventKind.DiscoveryBlocked, CurrentSession.Candidate, null);
        return hadTargetSession;
    }

    public async Task EndSessionAsync()
    {
        ThrowIfDisposed();
        _authorization.EndSession();
        CancelTargetWork();
        await _operationLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            try
            {
                await _worker.StopAndClearAsync(CancellationToken.None).ConfigureAwait(false);
                _cleanupComplete = true;
            }
            catch (Exception)
            {
                _cleanupComplete = false;
            }

            Publish(TargetSessionEventKind.TargetEnded, candidate: null, status: null);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _authorization.EndSession();
        CancelTargetWork();
        _worker.FrameProduced -= OnFrameProduced;
        await _operationLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await _worker.StopAndClearAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Generation is already revoked and the handler detached. Disposal remains
            // fail-closed even if a synthetic/real worker is itself unhealthy.
        }
        finally
        {
            _operationLock.Release();
        }

        lock (_workGate)
        {
            _targetWork?.Dispose();
            _targetWork = null;
        }

        _worker.Dispose();
        _operationLock.Dispose();
    }

    private async Task FailClosedAfterWorkerErrorAsync()
    {
        var pause = _authorization.PauseForPrivacy();
        CancelTargetWork();
        try
        {
            await _worker.StopAndClearAsync(CancellationToken.None).ConfigureAwait(false);
            _cleanupComplete = true;
        }
        catch (Exception)
        {
            _cleanupComplete = false;
        }

        await pause.PrivacyReceipt.AdmittedWorkDrained.ConfigureAwait(false);
    }

    private async Task StartWorkerWithCancellationFenceAsync(
        CaptureAuthorizationGrant grant,
        CancellationToken cancellationToken)
    {
        using var cancellationRegistration = cancellationToken.UnsafeRegister(
            static state => ((TargetSessionController)state!).RevokeCancelledWorkerStart(),
            this);

        cancellationToken.ThrowIfCancellationRequested();
        await _worker.StartAsync(grant, CurrentTargetWorkToken)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void RevokeCancelledWorkerStart()
    {
        try
        {
            // Cancellation is an authority event, not merely a request to a cooperative
            // worker. Revoke synchronously so a worker that ignores cancellation cannot
            // emit one last current frame before asynchronous cleanup catches up.
            _authorization.PauseForPrivacy();
        }
        catch (Exception)
        {
            // Generation exhaustion still marks RuntimePrivacyState paused before it
            // throws. Cleanup below and the normal fail-closed path remain best effort.
        }

        CancelTargetWork();
    }

    private void OnFrameProduced(object? sender, CaptureFrameMetadata frame)
    {
        PrivacyAssessment assessment;
        try
        {
            assessment = _assessmentProvider(frame) ?? PrivacyAssessment.Unavailable;
        }
        catch (Exception)
        {
            assessment = PrivacyAssessment.Unavailable;
        }

        _frameGate.TryAdmit(
            frame,
            assessment,
            admitted => FrameAdmitted?.Invoke(this, admitted));
    }

    private void ReplaceTargetWorkToken()
    {
        lock (_workGate)
        {
            _targetWork?.Cancel();
            _targetWork?.Dispose();
            _targetWork = new CancellationTokenSource();
        }
    }

    private void CancelTargetWork()
    {
        lock (_workGate)
        {
            if (_targetWork is { IsCancellationRequested: false })
            {
                _targetWork.Cancel();
            }
        }
    }

    private async Task<bool> EnsureCleanupCompleteAsync()
    {
        if (_cleanupComplete)
        {
            return true;
        }

        try
        {
            await _worker.StopAndClearAsync(CancellationToken.None).ConfigureAwait(false);
            _cleanupComplete = true;
            return true;
        }
        catch (Exception)
        {
            _cleanupComplete = false;
            return false;
        }
    }

    private static bool HasSameExecutablePolicyIdentity(
        TargetCandidate? current,
        TargetCandidate proposed) =>
        current is not null
        && string.Equals(
            current.Identity.ExecutablePathFingerprint,
            proposed.Identity.ExecutablePathFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            current.Identity.ExecutableFileName,
            proposed.Identity.ExecutableFileName,
            StringComparison.OrdinalIgnoreCase);

    private void Publish(
        TargetSessionEventKind kind,
        TargetCandidate? candidate,
        TargetAuthorizationStatus? status) =>
        SessionEvent?.Invoke(this, new TargetSessionEvent(kind, candidate, status));

    private static TargetSessionEventKind MapAuthorizationFailure(TargetAuthorizationStatus status) =>
        status switch
        {
            TargetAuthorizationStatus.ConsentRequired => TargetSessionEventKind.ConsentRequired,
            TargetAuthorizationStatus.Denied => TargetSessionEventKind.Denied,
            TargetAuthorizationStatus.AnotherTargetActive => TargetSessionEventKind.AnotherTargetActive,
            TargetAuthorizationStatus.PrivacyPaused => TargetSessionEventKind.PrivacyPaused,
            TargetAuthorizationStatus.UnsupportedDisplayTopology => TargetSessionEventKind.DiscoveryBlocked,
            TargetAuthorizationStatus.StaleTarget => TargetSessionEventKind.TargetUnavailable,
            _ => TargetSessionEventKind.Failed
        };

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
