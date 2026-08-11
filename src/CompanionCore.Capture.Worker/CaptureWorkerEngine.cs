using System.Diagnostics;
using System.Runtime.ExceptionServices;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed class CaptureWorkerEngine : IAsyncDisposable
{
    private readonly IWorkerCaptureSource _source;
    private readonly CaptureFramePipeline _pipeline;
    private readonly ISystemClock _clock;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private CaptureIpcAuthorization? _authorization;
    private CaptureWorkerStatus _status = CaptureWorkerStatus.Stopped;
    private long _sequence;
    private long _resizeCount;
    private long _stallCount;
    private long _faultCount;
    private bool _disposed;

    internal CaptureWorkerEngine(
        IWorkerCaptureSource source,
        CaptureFramePipeline? pipeline = null,
        ISystemClock? clock = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _clock = clock ?? SystemClock.Instance;
        _pipeline = pipeline ?? new CaptureFramePipeline(_clock);
        _source.FrameArrived += OnSourceFrameArrived;
        _source.StatusChanged += OnSourceStatusChanged;
        _pipeline.FrameReady += OnFrameReady;
    }

    internal event EventHandler<CaptureEngineFrame>? FrameProduced;

    internal event EventHandler<CaptureWorkerStatusChanged>? StatusChanged;

    internal CaptureWorkerStatus Status => _status;

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

        return _pipeline.Snapshot(
            Environment.ProcessId,
            _status,
            _resizeCount,
            _stallCount,
            _faultCount,
            restartCount: 0,
            workingSet,
            privateMemory,
            handleCount);
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
            try
            {
                _pipeline.Pause();
                _pipeline.ClearAsync(CancellationToken.None).GetAwaiter().GetResult();
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
                Interlocked.Increment(ref _sequence),
                frame.Timestamp,
                frame.Width,
                frame.Height,
                frame.AccountedBytes));
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
        await _source.DisposeAsync().ConfigureAwait(false);
        _operationLock.Dispose();
        _status = CaptureWorkerStatus.Stopped;
    }
}

internal sealed record CaptureEngineFrame(
    CaptureIpcAuthorization Authorization,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    int Width,
    int Height,
    long AccountedBytes);
