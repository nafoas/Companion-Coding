using CompanionCore.Runtime.Diagnostics;

namespace CompanionCore.Runtime;

/// <summary>
/// Owns the single authoritative lifecycle state for one companion process. The
/// constructor is internal: only the application composition root
/// (<c>CompanionCore.App</c>, granted access via <see cref="System.Runtime.CompilerServices.InternalsVisibleToAttribute"/>
/// in <c>AssemblyInfo.cs</c>) and this project's own tests may construct one. No window,
/// worker, or view model can construct a second instance — that capability simply isn't
/// exposed to them.
/// </summary>
public sealed class CompanionRuntime : IDisposable
{
    private static int _constructionCount;

    /// <summary>
    /// Total instances constructed in this process. Task 1's "one runtime" acceptance
    /// test asserts this never exceeds one for the process's actual composition root
    /// usage; it exists to make that assertion possible, not as production behavior.
    /// </summary>
    public static int ConstructionCount => _constructionCount;

    private readonly object _gate = new();
    private readonly IDiagnosticsSink _diagnostics;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly CancellationToken _lifetimeToken;
    private RuntimeState _state = RuntimeState.NotStarted;
    private bool _disposed;

    internal CompanionRuntime(IDiagnosticsSink? diagnostics = null)
    {
        _diagnostics = diagnostics ?? NullDiagnosticsSink.Instance;
        // Cached once: CancellationToken is a struct that remains safely queryable
        // (IsCancellationRequested, etc.) even after the source CancellationTokenSource
        // is disposed. Re-reading _lifetimeCts.Token on every access would instead throw
        // ObjectDisposedException once Dispose() has run, which would make it impossible
        // for shutdown code to check "was this cancelled?" after disposal — exactly when
        // it's most likely to ask.
        _lifetimeToken = _lifetimeCts.Token;
        Interlocked.Increment(ref _constructionCount);
    }

    public RuntimeState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Cancelled when the runtime stops or is disposed. Owned background work (capture
    /// workers, pending requests) should observe this token so shutdown can cancel
    /// everything the runtime owns without leaving orphaned work behind.
    /// </summary>
    public CancellationToken LifetimeToken => _lifetimeToken;

    public LifecycleTransitionResult Start(bool checkpointRecovered = false)
    {
        lock (_gate)
        {
            var prior = _state;
            if (prior != RuntimeState.NotStarted)
            {
                return Invalid(LifecycleEvent.Start, prior, checkpointRecovered);
            }

            _state = RuntimeState.Running;
            return new LifecycleTransitionResult(LifecycleEvent.Start, true, prior, _state, checkpointRecovered);
        }
    }

    public LifecycleTransitionResult Nap()
    {
        lock (_gate)
        {
            var prior = _state;
            if (prior != RuntimeState.Running)
            {
                return Invalid(LifecycleEvent.Nap, prior, false);
            }

            _state = RuntimeState.Napping;
            return new LifecycleTransitionResult(LifecycleEvent.Nap, true, prior, _state, false);
        }
    }

    public LifecycleTransitionResult Wake()
    {
        lock (_gate)
        {
            var prior = _state;
            if (prior != RuntimeState.Napping)
            {
                return Invalid(LifecycleEvent.Wake, prior, false);
            }

            _state = RuntimeState.Running;
            return new LifecycleTransitionResult(LifecycleEvent.Wake, true, prior, _state, false);
        }
    }

    public LifecycleTransitionResult Stop()
    {
        lock (_gate)
        {
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

    /// <summary>
    /// Idempotent. Cancels owned work and moves the runtime to <see cref="RuntimeState.Stopped"/>
    /// if it wasn't already; calling this more than once, or after <see cref="Stop"/>, is safe.
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
            if (_state == RuntimeState.Running || _state == RuntimeState.Napping)
            {
                _state = RuntimeState.Stopped;
            }

            SignalLifetimeEnd();
        }

        _lifetimeCts.Dispose();
    }
}
