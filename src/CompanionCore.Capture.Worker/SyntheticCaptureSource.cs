using System.Buffers;
using System.Security.Cryptography;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed class SyntheticCaptureSource : IWorkerCaptureSource
{
    internal const int DefaultFrameBytes = 4096;
    private readonly TimeSpan _interval;
    private readonly int _frameBytes;
    private readonly ISystemClock _clock;
    private readonly object _gate = new();
    private CancellationTokenSource? _captureLifetime;
    private Task? _producer;
    private bool _disposed;

    internal SyntheticCaptureSource(
        TimeSpan? interval = null,
        int frameBytes = DefaultFrameBytes,
        ISystemClock? clock = null)
    {
        if (frameBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameBytes));
        }

        _interval = interval ?? TimeSpan.FromMilliseconds(10);
        _frameBytes = frameBytes;
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
        ValidateAuthorization(authorization);

        lock (_gate)
        {
            if (_producer is { IsCompleted: false })
            {
                throw new InvalidOperationException("The synthetic source is already running.");
            }

            _captureLifetime?.Dispose();
            _captureLifetime = new CancellationTokenSource();
            _producer = ProduceAsync(_captureLifetime.Token);
        }

        StatusChanged?.Invoke(this, new CaptureSourceStatusChanged(
            CaptureWorkerStatus.Running,
            CaptureWorkerStatusReason.None));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        CancellationTokenSource? lifetime;
        Task? producer;
        lock (_gate)
        {
            lifetime = _captureLifetime;
            producer = _producer;
            _captureLifetime = null;
            _producer = null;
        }

        lifetime?.Cancel();
        if (producer is not null)
        {
            try
            {
                await producer.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lifetime?.IsCancellationRequested == true)
            {
            }
        }

        lifetime?.Dispose();
    }

    private async Task ProduceAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var resource = new SyntheticFrameResource(_frameBytes);
            var frame = new CaptureSourceFrame(
                _clock.UtcNow,
                width: 32,
                height: Math.Max(1, _frameBytes / (32 * 4)),
                accountedBytes: _frameBytes,
                resource);
            var handler = FrameArrived;
            if (handler is null)
            {
                frame.Dispose();
            }
            else
            {
                handler(this, frame);
            }

            await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateAuthorization(CaptureIpcAuthorization authorization)
    {
        if (authorization.TargetSessionId == Guid.Empty || authorization.Generation <= 0)
        {
            throw new ArgumentException("Synthetic authorization is incomplete.", nameof(authorization));
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

        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _disposed = true;
            StatusChanged = null;
            FrameArrived = null;
        }
    }

    private sealed class SyntheticFrameResource : IDisposable
    {
        private byte[]? _buffer;
        private readonly int _length;

        internal SyntheticFrameResource(int length)
        {
            _length = length;
            _buffer = ArrayPool<byte>.Shared.Rent(length);
            _buffer.AsSpan(0, length).Clear();
        }

        public void Dispose()
        {
            var buffer = Interlocked.Exchange(ref _buffer, null);
            if (buffer is null)
            {
                return;
            }

            CryptographicOperations.ZeroMemory(buffer.AsSpan(0, _length));
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
