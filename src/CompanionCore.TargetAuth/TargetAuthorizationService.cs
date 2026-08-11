using CompanionCore.Capture.Contracts;
using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth;

/// <summary>
/// Owns exactly one target authorization session. Discovery never changes this state;
/// only explicit authorization, privacy stop/resume, or end operations do.
/// </summary>
public sealed class TargetAuthorizationService
{
    private readonly ITargetDiscovery _discovery;
    private readonly IDisplayTopology _displayTopology;
    private readonly TargetPolicyCatalog _policyCatalog;
    private readonly RuntimePrivacyState _privacyState;
    private readonly Func<Guid> _newSessionId;
    private readonly object _gate = new();
    private TargetSessionSnapshot _session = TargetSessionSnapshot.None;

    public TargetAuthorizationService(
        ITargetDiscovery discovery,
        IDisplayTopology displayTopology,
        TargetPolicyCatalog policyCatalog,
        RuntimePrivacyState privacyState)
        : this(discovery, displayTopology, policyCatalog, privacyState, Guid.NewGuid)
    {
    }

    internal TargetAuthorizationService(
        ITargetDiscovery discovery,
        IDisplayTopology displayTopology,
        TargetPolicyCatalog policyCatalog,
        RuntimePrivacyState privacyState,
        Func<Guid> newSessionId)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _displayTopology = displayTopology ?? throw new ArgumentNullException(nameof(displayTopology));
        _policyCatalog = policyCatalog ?? throw new ArgumentNullException(nameof(policyCatalog));
        _privacyState = privacyState ?? throw new ArgumentNullException(nameof(privacyState));
        _newSessionId = newSessionId ?? throw new ArgumentNullException(nameof(newSessionId));
    }

    public TargetSessionSnapshot CurrentSession
    {
        get
        {
            lock (_gate)
            {
                return _session;
            }
        }
    }

    public bool HasExactlyOneAttachedDisplay() => TryReadDisplayCount() == 1;

    public async Task<TargetDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var displayCount = TryReadDisplayCount();
        if (displayCount != 1)
        {
            return TargetDiscoveryResult.Unsupported(displayCount);
        }

        try
        {
            var candidates = await _discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = candidates
                .Where(candidate => candidate is not null)
                .DistinctBy(candidate => candidate.Identity.WindowId)
                .OrderBy(candidate => candidate.Identity.ExecutableFileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Identity.ProcessId)
                .ThenBy(candidate => candidate.Identity.WindowId)
                .ToArray();
            return new TargetDiscoveryResult(TargetDiscoveryStatus.Ready, displayCount, snapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return TargetDiscoveryResult.Failed();
        }
    }

    public TargetInvitation Inspect(TargetCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var policy = _policyCatalog.Resolve(candidate);
        var disposition = policy.AuthorizationCategory switch
        {
            AuthorizationCategory.Denied => TargetInvitationDisposition.DeniedWithoutPrompt,
            AuthorizationCategory.StandingAuthorized => TargetInvitationDisposition.StandingAuthorizationAvailable,
            _ => TargetInvitationDisposition.ConsentRequired
        };
        return new TargetInvitation(candidate, policy, disposition);
    }

    internal async Task<TargetPolicy> SetExplicitPolicyAsync(
        TargetCandidate candidate,
        TargetPolicy policy,
        CancellationToken cancellationToken = default)
    {
        await _policyCatalog.SetExplicitPolicyAsync(candidate, policy, cancellationToken)
            .ConfigureAwait(false);
        return _policyCatalog.Resolve(candidate);
    }

    internal async Task<TargetAuthorizationResult> AuthorizeAsync(
        TargetCandidate candidate,
        bool explicitConsent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var policy = _policyCatalog.Resolve(candidate);
        var initialPrivacy = _privacyState.Snapshot;
        var initialFailure = PreflightAuthorization(
            candidate,
            policy,
            explicitConsent,
            initialPrivacy.IsPaused);
        if (initialFailure is not null)
        {
            return initialFailure;
        }

        bool isValid;
        try
        {
            isValid = await _discovery.IsStillValidAsync(candidate, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Failure(TargetAuthorizationStatus.Cancelled, candidate, policy);
        }
        catch (Exception)
        {
            return Failure(TargetAuthorizationStatus.Failed, candidate, policy);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Failure(TargetAuthorizationStatus.Cancelled, candidate, policy);
        }

        if (!isValid)
        {
            return Failure(TargetAuthorizationStatus.StaleTarget, candidate, policy);
        }

        lock (_gate)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Failure(TargetAuthorizationStatus.Cancelled, candidate, policy);
            }

            policy = _policyCatalog.Resolve(candidate);
            var finalFailure = PreflightAuthorizationLocked(candidate, policy, explicitConsent);
            if (finalFailure is not null)
            {
                return finalFailure;
            }

            if (TryReadDisplayCount() != 1)
            {
                return Failure(TargetAuthorizationStatus.UnsupportedDisplayTopology, candidate, policy);
            }

            var privacy = _privacyState.Snapshot;
            if (privacy.IsPaused)
            {
                return Failure(TargetAuthorizationStatus.PrivacyPaused, candidate, policy);
            }

            if (privacy.Generation != initialPrivacy.Generation)
            {
                return Failure(TargetAuthorizationStatus.Failed, candidate, policy);
            }

            Guid sessionId;
            CaptureAuthorizationGrant grant;
            try
            {
                sessionId = _newSessionId();
                if (sessionId == Guid.Empty)
                {
                    return Failure(TargetAuthorizationStatus.Failed, candidate, policy);
                }

                grant = CaptureAuthorizationGrant.Issue(
                    sessionId,
                    privacy.Generation,
                    candidate.Identity);
            }
            catch (Exception)
            {
                return Failure(TargetAuthorizationStatus.Failed, candidate, policy);
            }

            _session = new TargetSessionSnapshot(
                TargetSessionPhase.Authorized,
                sessionId,
                privacy.Generation,
                candidate,
                policy,
                grant);
            return new TargetAuthorizationResult(
                TargetAuthorizationStatus.Authorized,
                candidate,
                policy,
                shouldPrompt: false,
                grant: grant);
        }
    }

    internal TargetPrivacyPause PauseForPrivacy()
    {
        // Revocation is deliberately first. Even if a caller is blocked on the session
        // lock or later worker cleanup fails, old frames are already stale globally.
        var receipt = _privacyState.PauseAndRevoke();
        lock (_gate)
        {
            var hadActive = _session.Phase != TargetSessionPhase.None;
            if (hadActive)
            {
                _session = _session with
                {
                    Phase = TargetSessionPhase.PrivacyPaused,
                    Generation = receipt.PausedGeneration,
                    Grant = null,
                };
            }

            return new TargetPrivacyPause(hadActive, _session, receipt);
        }
    }

    internal async Task<TargetAuthorizationResult> ResumeExplicitlyAsync(
        CancellationToken cancellationToken = default)
    {
        TargetSessionSnapshot paused;
        lock (_gate)
        {
            paused = _session;
        }

        if (paused.Phase != TargetSessionPhase.PrivacyPaused || paused.Candidate is null || paused.Policy is null)
        {
            var candidate = paused.Candidate ?? MissingCandidate();
            return Failure(TargetAuthorizationStatus.Failed, candidate, paused.Policy ?? DefaultPolicy());
        }

        if (TryReadDisplayCount() != 1)
        {
            return Failure(TargetAuthorizationStatus.UnsupportedDisplayTopology, paused.Candidate, paused.Policy);
        }

        bool isValid;
        try
        {
            isValid = await _discovery.IsStillValidAsync(paused.Candidate, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Failure(TargetAuthorizationStatus.Cancelled, paused.Candidate, paused.Policy);
        }
        catch (Exception)
        {
            return Failure(TargetAuthorizationStatus.Failed, paused.Candidate, paused.Policy);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Failure(TargetAuthorizationStatus.Cancelled, paused.Candidate, paused.Policy);
        }

        if (!isValid)
        {
            return Failure(TargetAuthorizationStatus.StaleTarget, paused.Candidate, paused.Policy);
        }

        lock (_gate)
        {
            if (_session.Phase != TargetSessionPhase.PrivacyPaused
                || _session.TargetSessionId != paused.TargetSessionId
                || _session.Generation != paused.Generation)
            {
                return Failure(TargetAuthorizationStatus.Failed, paused.Candidate, paused.Policy);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Failure(TargetAuthorizationStatus.Cancelled, paused.Candidate, paused.Policy);
            }

            var currentPolicy = _policyCatalog.Resolve(paused.Candidate);
            if (currentPolicy.AuthorizationCategory == AuthorizationCategory.Denied)
            {
                return Failure(TargetAuthorizationStatus.Denied, paused.Candidate, currentPolicy);
            }

            if (TryReadDisplayCount() != 1)
            {
                return Failure(TargetAuthorizationStatus.UnsupportedDisplayTopology, paused.Candidate, currentPolicy);
            }

            long generation;
            try
            {
                generation = _privacyState.ResumeExplicitly(paused.Generation);
            }
            catch (InvalidOperationException)
            {
                return Failure(TargetAuthorizationStatus.Failed, paused.Candidate, currentPolicy);
            }

            var grant = CaptureAuthorizationGrant.Issue(
                paused.TargetSessionId,
                generation,
                paused.Candidate.Identity);
            _session = new TargetSessionSnapshot(
                TargetSessionPhase.Authorized,
                paused.TargetSessionId,
                generation,
                paused.Candidate,
                currentPolicy,
                grant);
            return new TargetAuthorizationResult(
                TargetAuthorizationStatus.Authorized,
                paused.Candidate,
                currentPolicy,
                shouldPrompt: false,
                grant: grant);
        }
    }

    /// <summary>
    /// Explicitly clears a global privacy pause when there is no target session to
    /// resume. This never creates a target or grant; it only reopens local admission
    /// after cleanup and one-display checks have been completed by the controller.
    /// </summary>
    internal bool ResumePrivacyWithoutTargetExplicitly()
    {
        var paused = _privacyState.Snapshot;
        if (!paused.IsPaused || TryReadDisplayCount() != 1)
        {
            return false;
        }

        lock (_gate)
        {
            if (_session.Phase != TargetSessionPhase.None
                || TryReadDisplayCount() != 1)
            {
                return false;
            }

            try
            {
                _privacyState.ResumeExplicitly(paused.Generation);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    internal void EndSession()
    {
        // Keep the global lock order privacy -> target session everywhere. Advancing an
        // otherwise idle generation is harmless and avoids a session/privacy deadlock.
        _privacyState.RevokeActiveGeneration();
        lock (_gate)
        {
            if (_session.Phase == TargetSessionPhase.None)
            {
                return;
            }

            _session = TargetSessionSnapshot.None;
        }
    }

    public bool IsCurrent(CaptureFrameMetadata frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return TryGetCurrentPolicy(frame, out _);
    }

    internal bool TryGetCurrentPolicy(
        CaptureFrameMetadata frame,
        out TargetPolicy? policy)
    {
        policy = null;
        if (!_privacyState.IsCurrent(frame.Generation)
            || TryReadDisplayCount() != 1)
        {
            return false;
        }

        lock (_gate)
        {
            var matches = _session.Phase == TargetSessionPhase.Authorized
                && frame.TargetSessionId == _session.TargetSessionId
                && frame.Generation == _session.Generation
                && frame.Target == _session.Candidate?.Identity;
            if (!matches || _session.Candidate is null)
            {
                return false;
            }

            var currentPolicy = _policyCatalog.Resolve(_session.Candidate);
            if (currentPolicy.AuthorizationCategory == AuthorizationCategory.Denied)
            {
                return false;
            }

            policy = currentPolicy;
            return true;
        }
    }

    internal bool IsCurrent(CaptureAuthorizationGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (!_privacyState.IsCurrent(grant.Generation)
            || TryReadDisplayCount() != 1)
        {
            return false;
        }

        lock (_gate)
        {
            var matches = _session.Phase == TargetSessionPhase.Authorized
                && grant.TargetSessionId == _session.TargetSessionId
                && grant.Generation == _session.Generation
                && grant.Target == _session.Candidate?.Identity
                && ReferenceEquals(grant, _session.Grant);
            return matches
                && _session.Candidate is not null
                && _policyCatalog.Resolve(_session.Candidate).AuthorizationCategory
                    != AuthorizationCategory.Denied;
        }
    }

    private TargetAuthorizationResult? PreflightAuthorization(
        TargetCandidate candidate,
        TargetPolicy policy,
        bool explicitConsent,
        bool privacyPaused)
    {
        lock (_gate)
        {
            return PreflightAuthorizationLocked(candidate, policy, explicitConsent, privacyPaused);
        }
    }

    private TargetAuthorizationResult? PreflightAuthorizationLocked(
        TargetCandidate candidate,
        TargetPolicy policy,
        bool explicitConsent,
        bool privacyPaused = false)
    {
        if (_session.Phase != TargetSessionPhase.None)
        {
            return Failure(TargetAuthorizationStatus.AnotherTargetActive, candidate, policy);
        }

        if (privacyPaused)
        {
            return Failure(TargetAuthorizationStatus.PrivacyPaused, candidate, policy);
        }

        if (TryReadDisplayCount() != 1)
        {
            return Failure(TargetAuthorizationStatus.UnsupportedDisplayTopology, candidate, policy);
        }

        if (policy.AuthorizationCategory == AuthorizationCategory.Denied)
        {
            return Failure(TargetAuthorizationStatus.Denied, candidate, policy);
        }

        if (policy.AuthorizationCategory is AuthorizationCategory.FamiliarAsk or AuthorizationCategory.UnknownAsk
            && !explicitConsent)
        {
            return new TargetAuthorizationResult(
                TargetAuthorizationStatus.ConsentRequired,
                candidate,
                policy,
                shouldPrompt: true,
                grant: null);
        }

        return null;
    }

    private int TryReadDisplayCount()
    {
        try
        {
            return _displayTopology.GetAttachedDisplayCount();
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static TargetAuthorizationResult Failure(
        TargetAuthorizationStatus status,
        TargetCandidate candidate,
        TargetPolicy policy) =>
        new(status, candidate, policy, shouldPrompt: false, grant: null);

    private static TargetCandidate MissingCandidate() =>
        new(
            new CaptureTargetIdentity(1, 1, "unavailable.exe", new string('0', 64)),
            ApplicationCategory.Other);

    private static TargetPolicy DefaultPolicy() =>
        new(AuthorizationCategory.UnknownAsk, TargetContentPolicy.Standard);
}
