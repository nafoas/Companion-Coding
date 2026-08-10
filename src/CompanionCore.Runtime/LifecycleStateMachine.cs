using CompanionCore.Runtime.Diagnostics;

namespace CompanionCore.Runtime;

/// <summary>
/// The actual lifecycle state machine: deterministic Start/Nap/Wake/Stop transitions,
/// shutdown, and invalid-transition diagnostics. This class carries none of
/// <see cref="CompanionRuntime"/>'s "exactly one per process" identity guarantee — it's
/// freely constructible (visible to this project's tests via
/// <see cref="System.Runtime.CompilerServices.InternalsVisibleToAttribute"/>) precisely
/// so the state machine's behavior can be unit-tested exhaustively without fighting a
/// one-shot production gate. <see cref="CompanionRuntime"/> is the only place that wraps
/// one of these behind the single-construction authority the packet actually requires.
/// </summary>
internal sealed class LifecycleStateMachine : IDisposable
{
    private readonly object _gate = new();
    private readonly IDiagnosticsSink _diagnostics;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly CancellationToken _lifetimeToken;
    private RuntimeState _state = RuntimeState.NotStarted;
    private bool _disposed;

    internal LifecycleStateMachine(IDiagnosticsSink? diagnostics = null)
    {
        _diagnostics = diagnostics ?? NullDiagnosticsSink.Instance;
        // Cached once: CancellationToken is a struct that remains safely queryable
        // (IsCancellationRequested, etc.) even after the source CancellationTokenSource
        // is disposed. Re-reading _lifetimeCts.Token on every access would instead throw
        // ObjectDisposedException once Dispose() has run, which would make it impossible
        // for shutdown code to check "was this cancelled?" after disposal — exactly when
        // it's most likely to ask.
        _lifetimeToken = _lifetimeCts.Token;
    }

    internal RuntimeState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    internal CancellationToken LifetimeToken => _lifetimeToken;

    internal LifecycleTransitionResult Start(bool checkpointRecovered = false)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var prior = _state;
            if (prior != RuntimeState.NotStarted)
            {
                return Invalid(LifecycleEvent.Start, prior, checkpointRecovered);
            }

            _state = RuntimeState.Running;
            return new LifecycleTransitionResult(LifecycleEvent.Start, true, prior, _state, checkpointRecovered);
        }
    }

    internal LifecycleTransitionResult Nap()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var prior = _state;
            if (prior != RuntimeState.Running)
            {
                return Invalid(LifecycleEvent.Nap, prior, false);
            }

            _state = RuntimeState.Napping;
            return new LifecycleTransitionResult(LifecycleEvent.Nap, true, prior, _state, false);
        }
    }

    internal LifecycleTransitionResult Wake()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var prior = _state;
            if (prior != RuntimeState.Napping)
            {
                return Invalid(LifecycleEvent.Wake, prior, false);
            }

            _state = RuntimeState.Running;
            return new LifecycleTransitionResult(LifecycleEvent.Wake, true, prior, _state, false);
        }
    }

    internal LifecycleTransitionResult Stop()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var prior = _state;
            if (prior != RuntimeState.Running && prior != RuntimeState.Napping)
            {
                return Invalid(LifecycleEvent.Stop, prior, false);
            }

            _state = RuntimeState.Stopped;
            SignalLifetimeEnd();
            return new LifecycleTransitionResult(LifecycleEvent.Stop, true, prior, _state, false);
        }
    }

    private LifecycleTransitionResult Invalid(LifecycleEvent @event, RuntimeState prior, bool checkpointRecovered)
    {
        // The raw event/state pair is diagnostics-only and is never surfaced through the
        // presentation mapping, which always renders "lifecycle.unknown" for an invalid
        // transition regardless of what actually happened here.
        _diagnostics.Log("lifecycle.invalid-transition", $"event={@event} priorState={prior}");
        return new LifecycleTransitionResult(@event, false, prior, prior, checkpointRecovered);
    }

    private void SignalLifetimeEnd()
    {
        if (!_lifetimeCts.IsCancellationRequested)
        {
            _lifetimeCts.Cancel();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// Idempotent. Cancels owned work and moves to <see cref="RuntimeState.Stopped"/> if
    /// it wasn't already; calling this more than once, or after <see cref="Stop"/>, is safe.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            // Disposal is a terminal lifecycle action even if startup never completed.
            // Leaving a disposed machine in NotStarted would allow a later Start call
            // to appear successful while its lifetime token was already cancelled.
            _state = RuntimeState.Stopped;

            SignalLifetimeEnd();
        }

        _lifetimeCts.Dispose();
    }
}
