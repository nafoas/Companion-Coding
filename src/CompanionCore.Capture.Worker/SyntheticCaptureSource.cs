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
    private long _frameIndex;
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
            const int width = 32;
            var height = Math.Max(1, _frameBytes / (width * 4));
            var exactBytes = checked(width * height * 4);
            var frameIndex = Interlocked.Increment(ref _frameIndex);
            var resource = new SyntheticFrameResource(
                width,
                height,
                exactBytes,
                pattern: checked((int)((frameIndex - 1) / 3)));
            var frame = new CaptureSourceFrame(
                _clock.UtcNow,
                width,
                height,
                accountedBytes: exactBytes,
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

    private sealed class SyntheticFrameResource : ICapturePixelSource
    {
        private byte[]? _buffer;
        private readonly int _length;
        private readonly int _width;
        private readonly int _height;

        internal SyntheticFrameResource(int width, int height, int length, int pattern)
        {
            _width = width;
            _height = height;
            _length = length;
            _buffer = ArrayPool<byte>.Shared.Rent(length);
            var pixels = _buffer.AsSpan(0, length);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var offset = checked((y * width + x) * 4);
                    pixels[offset] = unchecked((byte)(x * 7 + pattern * 17));
                    pixels[offset + 1] = unchecked((byte)(y * 9 + pattern * 11));
                    pixels[offset + 2] = unchecked((byte)((x + y) * 5 + pattern * 23));
                    pixels[offset + 3] = 255;
                }
            }
        }

        public ValueTask<OwnedBgra32Buffer> CopyPixelsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = Volatile.Read(ref _buffer)
                ?? throw new ObjectDisposedException(nameof(SyntheticFrameResource));
            var copy = GC.AllocateUninitializedArray<byte>(_length);
            source.AsSpan(0, _length).CopyTo(copy);
            return ValueTask.FromResult(new OwnedBgra32Buffer(_width, _height, copy));
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
