using CompanionCore.Capture.Contracts;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace CompanionCore.Capture.Worker;

internal sealed class WindowsGraphicsCaptureSource : IWorkerCaptureSource
{
    internal static readonly TimeSpan TargetCheckInterval = TimeSpan.FromMilliseconds(250);
    internal static readonly TimeSpan NoFrameThreshold = TimeSpan.FromSeconds(2);

    private const int FramePoolBufferCount = CaptureWorkerMetrics.MaximumSourceFrames;
    private const int BytesPerPixel = 4;
    private static readonly DirectXPixelFormat PixelFormat =
        DirectXPixelFormat.B8G8R8A8UIntNormalized;

    private readonly object _gate = new();
    private readonly ISystemClock _clock;
    private readonly CaptureSignalTracker _signalTracker = new(NoFrameThreshold);
    private CaptureIpcAuthorization? _authorization;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private WinRtD3DDeviceLease? _device;
    private CancellationTokenSource? _monitorLifetime;
    private Task? _monitor;
    private SizeInt32 _contentSize;
    private bool _active;
    private bool _disposed;

    internal WindowsGraphicsCaptureSource(ISystemClock? clock = null)
    {
        _clock = clock ?? SystemClock.Instance;
    }

    public event EventHandler<CaptureSourceFrame>? FrameArrived;

    public event EventHandler<CaptureSourceStatusChanged>? StatusChanged;

    public Task StartAsync(
        CaptureIpcAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()
            || !GraphicsCaptureSession.IsSupported())
        {
            throw new PlatformNotSupportedException("Windows Graphics Capture is unavailable.");
        }

        var validation = WindowsCaptureTargetValidator.Validate(authorization);
        if (validation != CaptureTargetValidationResult.Valid)
        {
            throw CreateValidationException(validation);
        }

        lock (_gate)
        {
            if (_active)
            {
                throw new InvalidOperationException("The Windows capture source is already active.");
            }
        }

        GraphicsCaptureItem? item = null;
        Direct3D11CaptureFramePool? framePool = null;
        GraphicsCaptureSession? session = null;
        WinRtD3DDeviceLease? device = null;
        CancellationTokenSource? monitorLifetime = null;
        try
        {
            var window = new IntPtr(authorization.WindowId);
            device = WindowsCaptureInterop.CreateDirect3DDevice();
            item = WindowsCaptureInterop.CreateItemForWindow(window);
            validation = WindowsCaptureTargetValidator.Validate(authorization);
            if (validation != CaptureTargetValidationResult.Valid)
            {
                throw CreateValidationException(validation);
            }

            var size = item.Size;
            _ = GetAccountedBytes(size);
            framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device.Device,
                PixelFormat,
                FramePoolBufferCount,
                size);
            session = framePool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = false;
            monitorLifetime = new CancellationTokenSource();

            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _authorization = authorization;
                _item = item;
                _framePool = framePool;
                _session = session;
                _device = device;
                _monitorLifetime = monitorLifetime;
                _contentSize = size;
                _signalTracker.Reset(_clock.UtcNow);
                _active = true;
                item = null;
                framePool = null;
                session = null;
                device = null;
                monitorLifetime = null;
            }

            var ownedItem = _item;
            var ownedFramePool = _framePool;
            var ownedSession = _session;
            var ownedMonitorLifetime = _monitorLifetime;
            ownedItem.Closed += OnItemClosed;
            ownedFramePool.FrameArrived += OnFrameArrived;
            ownedSession.StartCapture();
            lock (_gate)
            {
                _monitor = MonitorAsync(ownedMonitorLifetime.Token);
            }

            return Task.CompletedTask;
        }
        catch
        {
            monitorLifetime?.Cancel();
            monitorLifetime?.Dispose();
            CloseCaptureObjects(item, framePool, session, device);
            if (IsActive())
            {
                var monitor = StopCore(detachMonitor: true);
                monitor.Lifetime.Dispose();
            }

            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var monitor = StopCore(detachMonitor: true);
        if (monitor.Task is not null)
        {
            try
            {
                await monitor.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (monitor.Lifetime.IsCancellationRequested)
            {
            }
        }

        monitor.Lifetime.Dispose();
    }

    private void OnFrameArrived(
        Direct3D11CaptureFramePool sender,
        object args)
    {
        Direct3D11CaptureFrame? frame = null;
        try
        {
            if (!IsActive())
            {
                return;
            }

            frame = sender.TryGetNextFrame();
            if (frame is null)
            {
                return;
            }

            var size = frame.ContentSize;
            var accountedBytes = GetAccountedBytes(size);
            var resize = false;
            lock (_gate)
            {
                if (!_active || !ReferenceEquals(sender, _framePool))
                {
                    frame.Dispose();
                    frame = null;
                    return;
                }

                _signalTracker.MarkFrame(_clock.UtcNow);
                resize = size.Width != _contentSize.Width || size.Height != _contentSize.Height;
                if (resize)
                {
                    _contentSize = size;
                }
            }

            if (resize)
            {
                frame.Dispose();
                frame = null;
                PublishStatus(new CaptureSourceStatusChanged(
                    CaptureWorkerStatus.Running,
                    CaptureWorkerStatusReason.None,
                    ClearRetainedFrames: true,
                    IsResize: true));
                WinRtD3DDeviceLease? device;
                lock (_gate)
                {
                    device = _active && ReferenceEquals(sender, _framePool) ? _device : null;
                }

                if (device is not null)
                {
                    sender.Recreate(device.Device, PixelFormat, FramePoolBufferCount, size);
                }

                return;
            }

            CaptureIpcAuthorization? authorization;
            lock (_gate)
            {
                authorization = _authorization;
            }

            if (authorization is null
                || WindowsCaptureTargetValidator.IsMinimized(authorization.WindowId))
            {
                frame.Dispose();
                frame = null;
                ReportTransientState(
                    CaptureWorkerStatus.PausedMinimized,
                    CaptureWorkerStatusReason.Minimized);
                return;
            }

            var sourceFrame = new CaptureSourceFrame(
                _clock.UtcNow,
                size.Width,
                size.Height,
                accountedBytes,
                new WgcFrameLease(frame));
            frame = null;
            var handler = FrameArrived;
            if (handler is null)
            {
                sourceFrame.Dispose();
            }
            else
            {
                try
                {
                    handler(this, sourceFrame);
                }
                catch (Exception)
                {
                    sourceFrame.Dispose();
                    throw;
                }
            }
        }
        catch (Exception)
        {
            frame?.Dispose();
            SignalTerminalFault(CaptureWorkerStatusReason.DeviceLost);
        }
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TargetCheckInterval, cancellationToken).ConfigureAwait(false);
                CaptureIpcAuthorization? authorization;
                lock (_gate)
                {
                    if (!_active)
                    {
                        return;
                    }

                    authorization = _authorization;
                }

                if (authorization is null)
                {
                    return;
                }

                var validation = WindowsCaptureTargetValidator.Validate(authorization);
                if (validation != CaptureTargetValidationResult.Valid)
                {
                    SignalTerminalFault(validation switch
                    {
                        CaptureTargetValidationResult.TargetClosed =>
                            CaptureWorkerStatusReason.TargetClosed,
                        CaptureTargetValidationResult.IdentityMismatch =>
                            CaptureWorkerStatusReason.TargetIdentityChanged,
                        _ => CaptureWorkerStatusReason.CaptureUnavailable,
                    });
                    return;
                }

                CaptureSourceStatusChanged? signalChange;
                lock (_gate)
                {
                    signalChange = _active
                        ? _signalTracker.Evaluate(
                            _clock.UtcNow,
                            WindowsCaptureTargetValidator.IsMinimized(authorization.WindowId))
                        : null;
                }

                if (signalChange is not null)
                {
                    PublishStatus(signalChange);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SignalTerminalFault(CaptureWorkerStatusReason.CaptureUnavailable);
        }
    }

    private void ReportTransientState(
        CaptureWorkerStatus status,
        CaptureWorkerStatusReason reason)
    {
        CaptureSourceStatusChanged? change;
        lock (_gate)
        {
            if (!_active)
            {
                return;
            }

            change = _signalTracker.TryTransition(
                status,
                reason,
                isStall: status == CaptureWorkerStatus.NoSignal);
        }

        if (change is not null)
        {
            PublishStatus(change);
        }
    }

    private void OnItemClosed(GraphicsCaptureItem sender, object args) =>
        SignalTerminalFault(CaptureWorkerStatusReason.TargetClosed);

    private void SignalTerminalFault(CaptureWorkerStatusReason reason)
    {
        lock (_gate)
        {
            if (!_active)
            {
                return;
            }

            _ = _signalTracker.TryTransition(
                CaptureWorkerStatus.Faulted,
                reason,
                isStall: false);
        }

        _ = StopCore(detachMonitor: false);
        PublishStatus(new CaptureSourceStatusChanged(
            CaptureWorkerStatus.Faulted,
            reason,
            ClearRetainedFrames: true,
            IsFault: true));
    }

    private MonitorOwner StopCore(bool detachMonitor)
    {
        GraphicsCaptureItem? item;
        Direct3D11CaptureFramePool? framePool;
        GraphicsCaptureSession? session;
        WinRtD3DDeviceLease? device;
        CancellationTokenSource lifetime;
        Task? monitor;
        lock (_gate)
        {
            _active = false;
            _authorization = null;
            item = _item;
            framePool = _framePool;
            session = _session;
            device = _device;
            _item = null;
            _framePool = null;
            _session = null;
            _device = null;
            _contentSize = default;
            _signalTracker.Stop();
            lifetime = _monitorLifetime ?? new CancellationTokenSource();
            monitor = _monitor;
            if (detachMonitor)
            {
                _monitorLifetime = null;
                _monitor = null;
            }
        }

        lifetime.Cancel();
        if (item is not null)
        {
            try
            {
                item.Closed -= OnItemClosed;
            }
            catch (Exception)
            {
            }
        }

        if (framePool is not null)
        {
            try
            {
                framePool.FrameArrived -= OnFrameArrived;
            }
            catch (Exception)
            {
            }
        }

        CloseCaptureObjects(item, framePool, session, device);
        return new MonitorOwner(lifetime, monitor);
    }

    private static void CloseCaptureObjects(
        GraphicsCaptureItem? item,
        Direct3D11CaptureFramePool? framePool,
        GraphicsCaptureSession? session,
        WinRtD3DDeviceLease? device)
    {
        TryDispose(() => session?.Dispose());
        TryDispose(() => framePool?.Dispose());
        TryDispose(() => WindowsCaptureInterop.DisposeProjectedObject(item));
        TryDispose(() => device?.Dispose());
    }

    private void PublishStatus(CaptureSourceStatusChanged change)
    {
        try
        {
            StatusChanged?.Invoke(this, change);
        }
        catch (Exception)
        {
            _ = StopCore(detachMonitor: false);
        }
    }

    private static void TryDispose(Action dispose)
    {
        try
        {
            dispose();
        }
        catch (Exception)
        {
        }
    }

    private bool IsActive()
    {
        lock (_gate)
        {
            return _active;
        }
    }

    private static Exception CreateValidationException(CaptureTargetValidationResult validation) =>
        validation switch
        {
            CaptureTargetValidationResult.TargetClosed =>
                new InvalidOperationException("The authorized capture target is no longer available."),
            CaptureTargetValidationResult.IdentityMismatch =>
                new InvalidOperationException("The authorized capture target identity changed."),
            _ => new InvalidOperationException("The authorized capture target could not be revalidated."),
        };

    private static long GetAccountedBytes(SizeInt32 size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new InvalidOperationException("Windows Graphics Capture reported an invalid content size.");
        }

        var accountedBytes = checked((long)size.Width * size.Height * BytesPerPixel);
        if (accountedBytes > CaptureWorkerMetrics.ScreenshotBudgetBytes)
        {
            throw new InvalidOperationException(
                "The capture target exceeds the bounded source-frame budget.");
        }

        return accountedBytes;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        FrameArrived = null;
        StatusChanged = null;
    }

    private readonly record struct MonitorOwner(
        CancellationTokenSource Lifetime,
        Task? Task);

    private sealed class WgcFrameLease : IDisposable
    {
        private Direct3D11CaptureFrame? _frame;

        internal WgcFrameLease(Direct3D11CaptureFrame frame)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        }

        public void Dispose() => Interlocked.Exchange(ref _frame, null)?.Dispose();
    }
}
