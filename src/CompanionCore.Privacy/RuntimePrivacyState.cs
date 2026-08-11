namespace CompanionCore.Privacy;

/// <summary>
/// Runtime-wide privacy generation and live-write admission authority. Revocation is
/// synchronous: once <see cref="PauseAndRevoke"/> returns, old capture/semantic work is
/// stale and no new live write can acquire a lease. Cleanup and already-admitted write
/// drainage may finish asynchronously without reopening the boundary.
/// </summary>
public sealed class RuntimePrivacyState
{
    private readonly object _gate = new();
    private long _generation = 1;
    private bool _paused;
    private int _activeAdmissions;
    private TaskCompletionSource? _admissionsDrained;

    public PrivacyStateSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return new PrivacyStateSnapshot(_generation, _paused, _activeAdmissions);
            }
        }
    }

    public bool IsCurrent(long generation)
    {
        lock (_gate)
        {
            return !_paused && generation > 0 && generation == _generation;
        }
    }

    /// <summary>
    /// Acquires admission against whatever generation is current at this instant.
    /// This is the Task 4 live-memory path. Task 7 must use the expected-generation
    /// overload when admitting a remote result so a pre-stop result cannot become new
    /// merely because explicit resume has since created a later generation.
    /// </summary>
    public bool TryAcquireAdmissionLease(out PrivacyAdmissionLease? lease)
    {
        lock (_gate)
        {
            return TryAcquireAdmissionLeaseLocked(_generation, out lease);
        }
    }

    public bool TryAcquireAdmissionLease(long expectedGeneration, out PrivacyAdmissionLease? lease)
    {
        lock (_gate)
        {
            return TryAcquireAdmissionLeaseLocked(expectedGeneration, out lease);
        }
    }

    /// <summary>
    /// Atomically pauses live-write admission and revokes the current generation. A
    /// repeated call remains stop-only and advances the revocation generation so it
    /// also fences an explicit-resume attempt that may already be in flight.
    /// </summary>
    internal PrivacyPauseReceipt PauseAndRevoke()
    {
        lock (_gate)
        {
            var revokedGeneration = _generation;
            var wasAlreadyPaused = _paused;
            _paused = true;
            _generation = AdvanceGenerationLocked();
            return new PrivacyPauseReceipt(
                revokedGeneration,
                _generation,
                wasAlreadyPaused,
                GetDrainTaskLocked());
        }
    }

    /// <summary>
    /// Explicitly creates a fresh active generation. Callers must finish target and
    /// worker revalidation/cleanup first; automatic callers and the privacy hotkey do
    /// not receive a resume operation.
    /// </summary>
    internal long ResumeExplicitly()
    {
        var expectedPausedGeneration = Snapshot.Generation;
        return ResumeExplicitly(expectedPausedGeneration);
    }

    internal long ResumeExplicitly(long expectedPausedGeneration)
    {
        lock (_gate)
        {
            if (!_paused)
            {
                throw new InvalidOperationException("Privacy is not paused.");
            }

            if (_activeAdmissions != 0)
            {
                throw new InvalidOperationException(
                    "Privacy cannot resume until every already-admitted live write has drained.");
            }

            if (expectedPausedGeneration <= 0 || expectedPausedGeneration != _generation)
            {
                throw new InvalidOperationException(
                    "Privacy changed after explicit resume began; the runtime remains paused.");
            }

            _generation = AdvanceGenerationLocked();
            _paused = false;
            _admissionsDrained = null;
            return _generation;
        }
    }

    /// <summary>
    /// Revokes an active target generation without pausing unrelated local runtime
    /// work. Ending or explicitly replacing a target uses this fence.
    /// </summary>
    internal long RevokeActiveGeneration()
    {
        lock (_gate)
        {
            if (_paused)
            {
                return _generation;
            }

            _generation = AdvanceGenerationLocked();
            return _generation;
        }
    }

    private bool TryAcquireAdmissionLeaseLocked(long expectedGeneration, out PrivacyAdmissionLease? lease)
    {
        if (_paused || expectedGeneration <= 0 || expectedGeneration != _generation)
        {
            lease = null;
            return false;
        }

        checked
        {
            _activeAdmissions++;
        }

        lease = new PrivacyAdmissionLease(this, _generation);
        return true;
    }

    private Task GetDrainTaskLocked()
    {
        if (_activeAdmissions == 0)
        {
            return Task.CompletedTask;
        }

        _admissionsDrained ??= new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        return _admissionsDrained.Task;
    }

    private long AdvanceGenerationLocked()
    {
        if (_generation == long.MaxValue)
        {
            _paused = true;
            throw new InvalidOperationException(
                "The privacy generation is exhausted; the runtime remains fail-closed.");
        }

        return _generation + 1;
    }

    internal void ReleaseAdmissionLease()
    {
        TaskCompletionSource? toComplete = null;
        lock (_gate)
        {
            if (_activeAdmissions <= 0)
            {
                throw new InvalidOperationException("A privacy write lease was released more than once.");
            }

            _activeAdmissions--;
            if (_activeAdmissions == 0 && _paused)
            {
                toComplete = _admissionsDrained;
                _admissionsDrained = null;
            }
        }

        toComplete?.TrySetResult();
    }
}
