using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

internal static class CaptureWorkerTestSupport
{
    internal static readonly DateTimeOffset FixedTime =
        new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    internal static CaptureIpcAuthorization CreateAuthorization(long generation = 7) => new()
    {
        TargetSessionId = Guid.Parse("15151515-1515-1515-1515-151515151515"),
        Generation = generation,
        WindowId = 42,
        ProcessId = 100,
        ExecutableFileName = "private-safe-fixture.exe",
        ExecutablePathFingerprint = new string('A', 64),
    };

    internal static CaptureAuthorizationGrant CreateGrant(long generation = 7) =>
        CaptureAuthorizationGrant.Issue(
            Guid.Parse("15151515-1515-1515-1515-151515151515"),
            generation,
            new CaptureTargetIdentity(
                windowId: 42,
                processId: 100,
                executableFileName: "private-safe-fixture.exe",
                executablePathFingerprint: new string('A', 64)));

    internal static CaptureWorkerMetrics Snapshot(CaptureFramePipeline pipeline) =>
        pipeline.Snapshot(
            processId: 123,
            CaptureWorkerStatus.Running,
            resizeCount: 0,
            stallCount: 0,
            faultCount: 0,
            restartCount: 0,
            workingSet: 0,
            privateMemory: 0,
            handleCount: 0);

    internal static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The bounded capture test condition was not reached.");
            }

            await Task.Delay(5).ConfigureAwait(false);
        }
    }

    internal static CaptureSourceFrame CreatePixelFrame(
        int width,
        int height,
        int pattern,
        DateTimeOffset? timestamp = null,
        PixelOwnershipCounter? counter = null)
    {
        var resource = new TestPixelResource(width, height, pattern, counter);
        return new CaptureSourceFrame(
            timestamp ?? FixedTime,
            width,
            height,
            checked((long)width * height * 4),
            resource);
    }
}

internal sealed class ManualClock(DateTimeOffset initial) : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = initial;
}

internal sealed class TrackingResource : IDisposable
{
    private int _disposeCount;

    internal int DisposeCount => Volatile.Read(ref _disposeCount);

    public void Dispose() => Interlocked.Increment(ref _disposeCount);
}

internal sealed class ThrowingResource : IDisposable
{
    private int _disposeCount;

    internal int DisposeCount => Volatile.Read(ref _disposeCount);

    public void Dispose()
    {
        Interlocked.Increment(ref _disposeCount);
        throw new InvalidOperationException("Synthetic disposal failure.");
    }
}

internal sealed class SharedDisposalCounter
{
    internal long Created;
    internal long Disposed;
}

internal sealed class CountedResource : IDisposable
{
    private readonly SharedDisposalCounter _counter;
    private int _disposed;

    internal CountedResource(SharedDisposalCounter counter)
    {
        _counter = counter;
        Interlocked.Increment(ref _counter.Created);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Interlocked.Increment(ref _counter.Disposed);
        }
    }
}

internal sealed class PixelOwnershipCounter
{
    internal long SourcesCreated;
    internal long SourcesDisposed;
    internal long CopiesCreated;
}

internal sealed class TestPixelResource : ICapturePixelSource
{
    private byte[]? _pixels;
    private readonly int _width;
    private readonly int _height;
    private readonly PixelOwnershipCounter? _counter;

    internal TestPixelResource(
        int width,
        int height,
        int pattern,
        PixelOwnershipCounter? counter = null)
    {
        _width = width;
        _height = height;
        _counter = counter;
        _pixels = GC.AllocateUninitializedArray<byte>(checked(width * height * 4));
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                _pixels[offset] = unchecked((byte)(pattern + x));
                _pixels[offset + 1] = unchecked((byte)(pattern * 3 + y));
                _pixels[offset + 2] = unchecked((byte)(pattern * 7 + x + y));
                _pixels[offset + 3] = 255;
            }
        }

        if (counter is not null)
        {
            Interlocked.Increment(ref counter.SourcesCreated);
        }
    }

    public ValueTask<OwnedBgra32Buffer> CopyPixelsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pixels = Volatile.Read(ref _pixels)
            ?? throw new ObjectDisposedException(nameof(TestPixelResource));
        var copy = GC.AllocateUninitializedArray<byte>(pixels.Length);
        pixels.CopyTo(copy, 0);
        if (_counter is not null)
        {
            Interlocked.Increment(ref _counter.CopiesCreated);
        }

        return ValueTask.FromResult(new OwnedBgra32Buffer(_width, _height, copy));
    }

    public void Dispose()
    {
        var pixels = Interlocked.Exchange(ref _pixels, null);
        if (pixels is null)
        {
            return;
        }

        System.Security.Cryptography.CryptographicOperations.ZeroMemory(pixels);
        if (_counter is not null)
        {
            Interlocked.Increment(ref _counter.SourcesDisposed);
        }
    }
}

internal sealed class ControllableCaptureSource : IWorkerCaptureSource
{
    internal Func<CaptureIpcAuthorization, CancellationToken, Task>? StartHandler { get; set; }

    internal int StopCount { get; private set; }

    public event EventHandler<CaptureSourceFrame>? FrameArrived;

    public event EventHandler<CaptureSourceStatusChanged>? StatusChanged;

    public Task StartAsync(
        CaptureIpcAuthorization authorization,
        CancellationToken cancellationToken) =>
        StartHandler?.Invoke(authorization, cancellationToken) ?? Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        return Task.CompletedTask;
    }

    internal void Emit(CaptureSourceFrame frame)
    {
        var handler = FrameArrived;
        if (handler is null)
        {
            frame.Dispose();
            return;
        }

        handler(this, frame);
    }

    internal void Report(CaptureSourceStatusChanged change) =>
        StatusChanged?.Invoke(this, change);

    public ValueTask DisposeAsync()
    {
        FrameArrived = null;
        StatusChanged = null;
        return ValueTask.CompletedTask;
    }
}
