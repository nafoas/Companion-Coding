using System.Diagnostics;
using System.Runtime.ExceptionServices;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed class CaptureWorkerEngine : IAsyncDisposable
{
    private readonly IWorkerCaptureSource _source;
    private readonly CaptureFramePipeline _pipeline;
    private readonly VisualObservationPipeline _visual;
    private readonly CaptureResourceWatchdog _watchdog;
    private readonly ISystemClock _clock;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private CaptureIpcAuthorization? _authorization;
    private CaptureWorkerStatus _status = CaptureWorkerStatus.Stopped;
    private long _sequence;
    private long _resizeCount;
    private long _stallCount;
    private long _faultCount;
    private long _watchdogTrips;
    private int _watchdogFaultPending;
    private Task _watchdogCleanup = Task.CompletedTask;
    private bool _disposed;

    /// <summary>
    /// The agreed local capture cadence (Design BunDex, provisional capture timing): Noticing
    /// compares every 1–2 s and Investigating every 0.5–1 s; 1 s is the one spacing inside
    /// both. Frames arriving sooner are released at once, so local comparison never runs at
    /// the display's frame rate.
    /// </summary>
    internal static readonly TimeSpan AgreedLocalFrameSpacing = TimeSpan.FromSeconds(1);

    private readonly TimeSpan _minimumFrameSpacing;
    private DateTimeOffset? _lastOfferedFrame;
    private long _spacedOutFrames;

    internal CaptureWorkerEngine(
        IWorkerCaptureSource source,
        CaptureFramePipeline? pipeline = null,
        ISystemClock? clock = null,
        int maximumFrames = CaptureWorkerMetrics.MaximumSourceFrames,
        TimeSpan minimumFrameSpacing = default)
    {
        if (minimumFrameSpacing < TimeSpan.Zero || minimumFrameSpacing > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumFrameSpacing));
        }

        _minimumFrameSpacing = minimumFrameSpacing;
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _clock = clock ?? SystemClock.Instance;
        _visual = new VisualObservationPipeline();
        _watchdog = new CaptureResourceWatchdog();
        _pipeline = pipeline ?? new CaptureFramePipeline(
            _clock,
            ProcessVisualAsync,
            maximumFrames);
        _source.FrameArrived += OnSourceFrameArrived;
        _source.StatusChanged += OnSourceStatusChanged;
        _pipeline.FrameReady += OnFrameReady;
    }

    internal event EventHandler<CaptureEngineFrame>? FrameProduced;

    internal event EventHandler<CaptureWorkerStatusChanged>? StatusChanged;

    internal event EventHandler<OwnedWorkerAttentionSheet>? AttentionSheetProduced;

    internal event EventHandler? VisualStateReset;

    internal CaptureWorkerStatus Status => _status;

    /// <summary>Frames released at arrival because they came sooner than the agreed spacing.</summary>
    internal long SpacedOutFrames => Interlocked.Read(ref _spacedOutFrames);

    internal async Task StartAsync(
        CaptureIpcAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(authorization);
        ValidateAuthorization(authorization);
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_status != CaptureWorkerStatus.Stopped)
            {
                throw new InvalidOperationException("The capture worker is already active.");
            }

            _authorization = authorization;
            _lastOfferedFrame = null;
            _visual.Reset();
            _watchdog.Reset();
            Interlocked.Exchange(ref _watchdogFaultPending, 0);
            SetStatus(CaptureWorkerStatus.Starting);
            _pipeline.Resume();
            try
            {
                await _source.StartAsync(authorization, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                SetStatus(CaptureWorkerStatus.Running);
            }
            catch (Exception exception)
            {
                _pipeline.Pause();
                _authorization = null;
                try
                {
                    await _source.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                await _pipeline.ClearAsync(CancellationToken.None).ConfigureAwait(false);
                if (exception is OperationCanceledException)
                {
                    SetStatus(CaptureWorkerStatus.Stopped);
                }
                else
                {
                    _faultCount++;
                    SetStatus(CaptureWorkerStatus.Faulted, CaptureWorkerStatusReason.CaptureUnavailable);
                }

                throw;
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    internal async Task<CaptureStopResult> StopAndClearAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pipeline.Pause();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _authorization = null;
            ExceptionDispatchInfo? sourceFailure = null;
            try
            {
                await _source.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                sourceFailure = ExceptionDispatchInfo.Capture(exception);
            }

            var result = await _pipeline.ClearAsync(CancellationToken.None).ConfigureAwait(false);
            _visual.Reset();
            VisualStateReset?.Invoke(this, EventArgs.Empty);
            SetStatus(CaptureWorkerStatus.Stopped);
            sourceFailure?.Throw();
            return result;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    internal CaptureWorkerMetrics GetMetrics()
    {
        using var process = Process.GetCurrentProcess();
        long workingSet = 0;
        long privateMemory = 0;
        var handleCount = 0;
        try
        {
            workingSet = process.WorkingSet64;
            privateMemory = process.PrivateMemorySize64;
            handleCount = OperatingSystem.IsWindows() ? process.HandleCount : 0;
        }
        catch (Exception)
        {
        }

        var visual = _visual.Snapshot();
        var metrics = _pipeline.Snapshot(
            Environment.ProcessId,
            _status,
            _resizeCount,
            _stallCount,
            _faultCount,
            restartCount: 0,
            workingSet,
            privateMemory,
            handleCount);
        metrics = metrics with
        {
            ChangedFrames = visual.ChangedFrames,
            DuplicateFrames = visual.DuplicateFrames,
            ProducedAttentionSheets = visual.ProducedSheets,
            ProducedOrientationSheets = visual.OrientationSheets,
            DroppedAttentionSheets = visual.DroppedSheets,
            CurrentAttentionSheets = visual.CurrentSheets,
            MaximumObservedAttentionSheets = visual.MaximumSheets,
            CurrentAttentionSheetBytes = visual.CurrentSheetBytes,
            MaximumObservedAttentionSheetBytes = visual.MaximumSheetBytes,
            CurrentVisualWorkingBytes = visual.CurrentWorkingBytes,
            MaximumObservedVisualWorkingBytes = visual.MaximumWorkingBytes,
            LastChangeScore = visual.LastChangeScore,
            ResourceWatchdogTrips = Interlocked.Read(ref _watchdogTrips),
        };
        EvaluateWatchdog(metrics);
        return metrics with
        {
            Status = _status,
            FaultCount = _faultCount,
            ResourceWatchdogTrips = Interlocked.Read(ref _watchdogTrips),
        };
    }

    internal async Task<ManualRegionUpdateFence> BeginManualRegionUpdateAsync(
        CaptureIpcAuthorization authorization,
        NormalizedRegion? region,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(authorization);
        region?.Validate(nameof(region));
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var returnFence = false;
        try
        {
            if (_status != CaptureWorkerStatus.Running
                || _authorization is null
                || !_authorization.Matches(authorization))
            {
                throw new InvalidOperationException("The manual region does not match the active capture grant.");
            }

            _pipeline.Pause();
            await _pipeline.ClearAsync(CancellationToken.None).ConfigureAwait(false);
            _visual.SetManualRegion(region);
            VisualStateReset?.Invoke(this, EventArgs.Empty);
            var fence = new ManualRegionUpdateFence(this, authorization);
            returnFence = true;
            return fence;
        }
        finally
        {
            if (!returnFence)
            {
                if (_status == CaptureWorkerStatus.Running
                    && _authorization is not null
                    && _authorization.Matches(authorization))
                {
                    _pipeline.Resume();
                }

                _operationLock.Release();
            }
        }
    }

    /// <summary>
    /// Re-arms the one orientation sheet for the active grant. The next usable frame,
    /// even a duplicate, becomes a fresh orientation. Consumers use this only as a
    /// bounded failsafe when an orientation could not be delivered.
    /// </summary>
    internal async Task RequestOrientationAsync(
        CaptureIpcAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(authorization);
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_status != CaptureWorkerStatus.Running
                || _authorization is null
                || !_authorization.Matches(authorization))
            {
                throw new InvalidOperationException("The orientation request does not match the active capture grant.");
            }

            _visual.RequestOrientation();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void CompleteManualRegionUpdate(
        CaptureIpcAuthorization authorization,
        bool responseCommitted)
    {
        try
        {
            if (responseCommitted
                && _status == CaptureWorkerStatus.Running
                && _authorization is not null
                && _authorization.Matches(authorization))
            {
                _pipeline.Resume();
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void OnSourceFrameArrived(object? sender, CaptureSourceFrame frame)
    {
        if (_status is CaptureWorkerStatus.NoSignal or CaptureWorkerStatus.PausedMinimized)
        {
            _pipeline.Resume();
            SetStatus(CaptureWorkerStatus.Running);
        }

        if (_status != CaptureWorkerStatus.Running)
        {
            frame.Dispose();
            return;
        }

        if (_minimumFrameSpacing > TimeSpan.Zero
            && _lastOfferedFrame is { } last
            && frame.Timestamp >= last
            && frame.Timestamp - last < _minimumFrameSpacing)
        {
            Interlocked.Increment(ref _spacedOutFrames);
            frame.Dispose();
            return;
        }

        _lastOfferedFrame = frame.Timestamp;
        frame.AssignSequence(Interlocked.Increment(ref _sequence));
        _pipeline.TryOffer(frame);
    }

    private void OnSourceStatusChanged(object? sender, CaptureSourceStatusChanged change)
    {
        if (change.IsResize)
        {
            Interlocked.Increment(ref _resizeCount);
        }

        if (change.IsStall)
        {
            Interlocked.Increment(ref _stallCount);
        }

        if (change.IsFault)
        {
            Interlocked.Increment(ref _faultCount);
            _authorization = null;
        }

        if (change.ClearRetainedFrames)
        {
            // The first frame after a reset (resize, stall, fault) is never spaced out.
            _lastOfferedFrame = null;
            try
            {
                _pipeline.Pause();
                _pipeline.ClearAsync(CancellationToken.None).GetAwaiter().GetResult();
                _visual.Reset(clearManualRegion: !change.IsResize);
                VisualStateReset?.Invoke(this, EventArgs.Empty);
                if (change.Status == CaptureWorkerStatus.Running && !change.IsFault)
                {
                    _pipeline.Resume();
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }

        SetStatus(change.Status, change.Reason);
    }

    private void OnFrameReady(object? sender, CaptureSourceFrame frame)
    {
        var authorization = Volatile.Read(ref _authorization);
        if (authorization is null || _status != CaptureWorkerStatus.Running)
        {
            return;
        }

        FrameProduced?.Invoke(
            this,
            new CaptureEngineFrame(
                authorization,
                frame.SequenceNumber,
                frame.Timestamp,
                frame.Width,
                frame.Height,
                frame.AccountedBytes));
    }

    private async ValueTask ProcessVisualAsync(
        CaptureSourceFrame frame,
        CancellationToken cancellationToken)
    {
        var authorization = Volatile.Read(ref _authorization);
        if (authorization is null || _status != CaptureWorkerStatus.Running)
        {
            return;
        }

        OwnedWorkerAttentionSheet? sheet = null;
        var visualCompleted = false;
        try
        {
            sheet = await _visual.ProcessAsync(frame, authorization, cancellationToken)
                .ConfigureAwait(false);
            visualCompleted = true;
            if (sheet is null)
            {
                return;
            }

            var current = Volatile.Read(ref _authorization);
            if (_status != CaptureWorkerStatus.Running
                || current is null
                || !current.Matches(authorization))
            {
                sheet.Dispose();
                return;
            }

            var handler = AttentionSheetProduced;
            if (handler is null)
            {
                sheet.Dispose();
                return;
            }

            handler(this, sheet);
            sheet = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            sheet?.Dispose();
            throw;
        }
        catch (Exception)
        {
            sheet?.Dispose();
            if (visualCompleted)
            {
                _visual.RecordTransportDrop();
            }
        }
        finally
        {
            EvaluateWatchdog(GetMetricsWithoutWatchdog());
        }
    }

    private CaptureWorkerMetrics GetMetricsWithoutWatchdog()
    {
        using var process = Process.GetCurrentProcess();
        long workingSet = 0;
        long privateMemory = 0;
        var handleCount = 0;
        try
        {
            workingSet = process.WorkingSet64;
            privateMemory = process.PrivateMemorySize64;
            handleCount = OperatingSystem.IsWindows() ? process.HandleCount : 0;
        }
        catch (Exception)
        {
        }

        var visual = _visual.Snapshot();
        return _pipeline.Snapshot(
            Environment.ProcessId,
            _status,
            _resizeCount,
            _stallCount,
            _faultCount,
            restartCount: 0,
            workingSet,
            privateMemory,
            handleCount) with
        {
            ChangedFrames = visual.ChangedFrames,
            DuplicateFrames = visual.DuplicateFrames,
            ProducedAttentionSheets = visual.ProducedSheets,
            ProducedOrientationSheets = visual.OrientationSheets,
            DroppedAttentionSheets = visual.DroppedSheets,
            CurrentAttentionSheets = visual.CurrentSheets,
            MaximumObservedAttentionSheets = visual.MaximumSheets,
            CurrentAttentionSheetBytes = visual.CurrentSheetBytes,
            MaximumObservedAttentionSheetBytes = visual.MaximumSheetBytes,
            CurrentVisualWorkingBytes = visual.CurrentWorkingBytes,
            MaximumObservedVisualWorkingBytes = visual.MaximumWorkingBytes,
            LastChangeScore = visual.LastChangeScore,
            ResourceWatchdogTrips = Interlocked.Read(ref _watchdogTrips),
        };
    }

    private void EvaluateWatchdog(CaptureWorkerMetrics metrics)
    {
        if (_status == CaptureWorkerStatus.Running
            && _watchdog.Observe(metrics, _clock.UtcNow)
            && Interlocked.Exchange(ref _watchdogFaultPending, 1) == 0)
        {
            Interlocked.Increment(ref _watchdogTrips);
            Interlocked.Increment(ref _faultCount);
            _authorization = null;
            _pipeline.Pause();
            _visual.Reset();
            VisualStateReset?.Invoke(this, EventArgs.Empty);
            SetStatus(CaptureWorkerStatus.Faulted, CaptureWorkerStatusReason.ResourceBudgetExceeded);
            _watchdogCleanup = ClearAfterWatchdogTripAsync();
        }
    }

    private async Task ClearAfterWatchdogTripAsync()
    {
        await _operationLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            try
            {
                await _source.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            await _pipeline.ClearAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void SetStatus(
        CaptureWorkerStatus status,
        CaptureWorkerStatusReason reason = CaptureWorkerStatusReason.None)
    {
        _status = status;
        StatusChanged?.Invoke(this, new CaptureWorkerStatusChanged(status, _clock.UtcNow, reason));
    }

    private static void ValidateAuthorization(CaptureIpcAuthorization authorization)
    {
        if (authorization.TargetSessionId == Guid.Empty || authorization.Generation <= 0)
        {
            throw new ArgumentException("Capture authorization is incomplete.", nameof(authorization));
        }

        _ = new CaptureTargetIdentity(
            authorization.WindowId,
            authorization.ProcessId,
            authorization.ExecutableFileName,
            authorization.ExecutablePathFingerprint);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _authorization = null;
        _pipeline.Pause();
        _source.FrameArrived -= OnSourceFrameArrived;
        _source.StatusChanged -= OnSourceStatusChanged;
        _pipeline.FrameReady -= OnFrameReady;
        try
        {
            await _source.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        await _pipeline.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _watchdogCleanup.ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        _visual.Dispose();
        await _source.DisposeAsync().ConfigureAwait(false);
        _operationLock.Dispose();
        _status = CaptureWorkerStatus.Stopped;
    }

    internal sealed class ManualRegionUpdateFence : IDisposable
    {
        private CaptureWorkerEngine? _owner;
        private readonly CaptureIpcAuthorization _authorization;
        private bool _responseCommitted;

        internal ManualRegionUpdateFence(
            CaptureWorkerEngine owner,
            CaptureIpcAuthorization authorization)
        {
            _owner = owner;
            _authorization = authorization;
        }

        internal void MarkResponseCommitted() => _responseCommitted = true;

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.CompleteManualRegionUpdate(_authorization, _responseCommitted);
        }
    }
}

internal sealed record CaptureEngineFrame(
    CaptureIpcAuthorization Authorization,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    int Width,
    int Height,
    long AccountedBytes);
