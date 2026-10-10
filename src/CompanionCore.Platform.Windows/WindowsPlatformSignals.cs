using CompanionCore.Capture.Contracts;
using CompanionCore.Orchestration;
using CompanionCore.TargetAuth;
using CompanionCore.Watchbun;

namespace CompanionCore.Platform.Windows;

/// <summary>
/// Local platform signals for the one authorized target, polled on one bounded background
/// loop. Foreground is minimized to "the target" or a zero sentinel, so no other
/// application's identity leaves this adapter. Input is the system last-input tick only.
/// Exit facts come from a held process handle and window polling; ambiguity always errs
/// toward a suspected crash. Nothing is polled while nothing is watched.
/// </summary>
public sealed class WindowsPlatformSignals : IPlatformSignals, IAsyncDisposable
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan IdleInterval = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan RelaunchInterval = TimeSpan.FromSeconds(2);

    /// <summary>Bound on remembered relaunch candidates, so a respawning executable cannot grow state.</summary>
    internal const int MaximumAnnouncedRelaunches = 64;

    private static readonly ForegroundWindow NotTarget = new(0, 0);

    private readonly IWindowsPlatformNative _native;
    private readonly ITargetDiscovery _discovery;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task? _loop;
    private readonly HashSet<int> _announced = [];

    private CaptureTargetIdentity? _target;
    private IProcessProbe? _probe;
    private bool _targetLost;
    private bool _windowGoneSeen;
    private bool _lastHung;
    private bool? _lastForegroundIsTarget;
    private uint? _lastInputTick;
    private CaptureTargetIdentity? _relaunchOf;
    private DateTimeOffset _nextRelaunchPoll;
    private int _disposed;
    private long _containedFailures;

    public WindowsPlatformSignals(IWindowsPlatformNative native, ITargetDiscovery discovery, TimeProvider? time = null)
        : this(native, discovery, time, startLoop: true)
    {
    }

    internal WindowsPlatformSignals(IWindowsPlatformNative native, ITargetDiscovery discovery, TimeProvider? time, bool startLoop)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _time = time ?? TimeProvider.System;
        if (startLoop)
        {
            _loop = Task.Run(LoopAsync);
        }
    }

    public event EventHandler<ForegroundWindow>? ForegroundChanged;

    public event EventHandler? InputObserved;

    public event EventHandler<TargetExit>? TargetExited;

    public event EventHandler<CaptureTargetIdentity>? TargetLaunched;

    public event EventHandler<SuspendReason>? Suspended;

    public event EventHandler<SuspendReason>? Resumed;

    /// <summary>Handler and discovery failures contained by the loop (they never stop it).</summary>
    public long ContainedFailures => Interlocked.Read(ref _containedFailures);

    public void WatchTarget(CaptureTargetIdentity? target)
    {
        lock (_gate)
        {
            _probe?.Dispose();
            _probe = null;
            _target = target;
            _targetLost = false;
            _windowGoneSeen = false;
            _lastHung = false;
            _lastForegroundIsTarget = null;
            _lastInputTick = null;
            if (target is null || Volatile.Read(ref _disposed) != 0)
            {
                _target = null;
                return;
            }

            // A window that no longer belongs to the process means the target is already
            // gone (or its identifiers were reused): report it as an unknown exit.
            if (_native.GetWindowProcessId(target.WindowId) != target.ProcessId)
            {
                _targetLost = true;
                return;
            }

            _probe = _native.OpenProcess(target.ProcessId);
        }
    }

    public void WatchForRelaunch(CaptureTargetIdentity? previous)
    {
        lock (_gate)
        {
            _relaunchOf = Volatile.Read(ref _disposed) != 0 ? null : previous;
            _announced.Clear();
            _nextRelaunchPoll = _time.GetUtcNow();
        }
    }

    /// <summary>Session lock or system sleep, fed by the App's system-event subscription.</summary>
    public void ReportSuspended(SuspendReason reason) => Raise(Suspended, reason);

    public void ReportResumed(SuspendReason reason) => Raise(Resumed, reason);

    internal bool Watching
    {
        get
        {
            lock (_gate)
            {
                return _target is not null || _relaunchOf is not null;
            }
        }
    }

    /// <summary>One poll of every watched signal. The loop calls this; tests call it directly.</summary>
    internal async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        TargetExit? exit = null;
        ForegroundWindow? foreground = null;
        var input = false;
        CaptureTargetIdentity? relaunchOf = null;
        lock (_gate)
        {
            if (_target is { } target)
            {
                exit = ObserveExit(target);
                if (exit is null)
                {
                    var (window, process) = _native.GetForeground();
                    var isTarget = window == target.WindowId && process == target.ProcessId;
                    if (isTarget != _lastForegroundIsTarget)
                    {
                        _lastForegroundIsTarget = isTarget;
                        foreground = isTarget ? new ForegroundWindow(target.WindowId, target.ProcessId) : NotTarget;
                    }

                    if (_native.GetLastInputTick() is { } tick)
                    {
                        input = _lastInputTick is { } last && last != tick;
                        _lastInputTick = tick;
                    }
                }
            }

            if (_relaunchOf is { } previous && _time.GetUtcNow() >= _nextRelaunchPoll)
            {
                relaunchOf = previous;
                _nextRelaunchPoll = _time.GetUtcNow() + RelaunchInterval;
            }
        }

        if (exit is not null)
        {
            Raise(TargetExited, exit);
        }

        if (foreground is not null)
        {
            Raise(ForegroundChanged, foreground);
        }

        if (input)
        {
            Raise(InputObserved);
        }

        if (relaunchOf is not null)
        {
            await PollRelaunchAsync(relaunchOf, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _shutdown.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        lock (_gate)
        {
            _probe?.Dispose();
            _probe = null;
            _target = null;
            _relaunchOf = null;
        }

        _shutdown.Dispose();
    }

    /// <summary>Observes the exit first, so a window seen gone in this same poll never counts as "first".</summary>
    private TargetExit? ObserveExit(CaptureTargetIdentity target)
    {
        var exited = false;
        var exitCode = -1;
        if (_targetLost)
        {
            exited = true;
        }
        else if (_probe is { } probe)
        {
            switch (probe.Poll(out exitCode))
            {
                case ProcessProbeState.Exited:
                    exited = true;
                    break;
                case ProcessProbeState.Unknown when !_native.IsWindow(target.WindowId):
                    // The handle can no longer answer and the window is gone: an unknown exit.
                    exited = true;
                    exitCode = -1;
                    break;
            }
        }
        else
        {
            // Unopenable process: only the window's disappearance can be seen.
            exited = _windowGoneSeen;
        }

        if (exited)
        {
            var observed = new TargetExit(target.ProcessId, exitCode, _windowGoneSeen && !_targetLost, _lastHung);
            _probe?.Dispose();
            _probe = null;
            _target = null;
            _targetLost = false;
            return observed;
        }

        if (_native.IsWindow(target.WindowId))
        {
            _lastHung = _native.IsHung(target.WindowId);
        }
        else
        {
            _windowGoneSeen = true;
        }

        return null;
    }

    private async Task PollRelaunchAsync(CaptureTargetIdentity previous, CancellationToken cancellationToken)
    {
        IReadOnlyList<TargetCandidate> candidates;
        try
        {
            candidates = await _discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Interlocked.Increment(ref _containedFailures);
            return;
        }

        var launched = new List<CaptureTargetIdentity>();
        lock (_gate)
        {
            if (!ReferenceEquals(_relaunchOf, previous))
            {
                return;
            }

            foreach (var candidate in candidates)
            {
                var identity = candidate.Identity;
                if (identity.ProcessId != previous.ProcessId
                    && string.Equals(identity.ExecutablePathFingerprint, previous.ExecutablePathFingerprint, StringComparison.Ordinal)
                    && string.Equals(identity.ExecutableFileName, previous.ExecutableFileName, StringComparison.OrdinalIgnoreCase)
                    && _announced.Count < MaximumAnnouncedRelaunches
                    && _announced.Add(identity.ProcessId))
                {
                    launched.Add(identity);
                }
            }
        }

        foreach (var identity in launched)
        {
            Raise(TargetLaunched, identity);
        }
    }

    private async Task LoopAsync()
    {
        var token = _shutdown.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (Watching)
                {
                    await PollOnceAsync(token).ConfigureAwait(false);
                }

                await Task.Delay(Watching ? PollInterval : IdleInterval, _time, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Interlocked.Increment(ref _containedFailures);
                try
                {
                    await Task.Delay(IdleInterval, _time, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private void Raise<T>(EventHandler<T>? handler, T value)
    {
        if (handler is null)
        {
            return;
        }

        foreach (var single in handler.GetInvocationList().Cast<EventHandler<T>>())
        {
            try
            {
                single(this, value);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Interlocked.Increment(ref _containedFailures);
            }
        }
    }

    private void Raise(EventHandler? handler)
    {
        if (handler is null)
        {
            return;
        }

        foreach (var single in handler.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                single(this, EventArgs.Empty);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Interlocked.Increment(ref _containedFailures);
            }
        }
    }
}
