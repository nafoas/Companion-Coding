using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed class CaptureFramePipeline : IAsyncDisposable
{
    internal const int ProcessingQueueCapacity = 2;

    private readonly object _gate = new();
    private readonly Queue<CaptureSourceFrame> _pending = new();
    private readonly ByteBoundedFrameRing _ring;
    private readonly SemaphoreSlim _available = new(0);
    private readonly SemaphoreSlim _processing = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Func<CaptureSourceFrame, CancellationToken, ValueTask> _processor;
    private readonly ISystemClock _clock;
    private readonly Task _consumer;
    private CaptureSourceFrame? _processingFrame;
    private bool _accepting;
    private bool _disposed;
    private long _accepted;
    private long _dropped;
    private long _disposedFrames;
    private long _disposedBytes;
    private int _maximumObservedFrames;
    private long _maximumObservedBytes;

    internal CaptureFramePipeline(
        ISystemClock? clock = null,
        Func<CaptureSourceFrame, CancellationToken, ValueTask>? processor = null,
        int maximumFrames = CaptureWorkerMetrics.MaximumSourceFrames)
    {
        if (maximumFrames is <= 0 or > CaptureWorkerMetrics.MaximumSourceFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrames));
        }

        _clock = clock ?? SystemClock.Instance;
        _processor = processor ?? (static (_, _) => ValueTask.CompletedTask);
        _ring = new ByteBoundedFrameRing(
            CaptureWorkerMetrics.ScreenshotBudgetBytes,
            maximumFrames);
        _consumer = ConsumeAsync();
    }

    internal event EventHandler<CaptureSourceFrame>? FrameReady;

    internal void Resume()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _accepting = true;
        }
    }

    internal void Pause()
    {
        lock (_gate)
        {
            _accepting = false;
        }
    }

    internal bool TryOffer(CaptureSourceFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        List<CaptureSourceFrame>? dispose = null;
        var accepted = false;

        lock (_gate)
        {
            if (_disposed || !_accepting
                || frame.AccountedBytes > CaptureWorkerMetrics.ScreenshotBudgetBytes)
            {
                _dropped++;
                dispose = [frame];
            }
            else
            {
                dispose = MakeRoomFor(frame.AccountedBytes);
                if (CurrentFrameCountUnsafe() >= _ring.MaximumFrames
                    || checked(CurrentBytesUnsafe() + frame.AccountedBytes)
                        > CaptureWorkerMetrics.ScreenshotBudgetBytes)
                {
                    _dropped++;
                    dispose.Add(frame);
                }
                else
                {
                    while (_pending.Count >= ProcessingQueueCapacity)
                    {
                        var oldestPending = _pending.Dequeue();
                        _dropped++;
                        dispose.Add(oldestPending);
                    }

                    _pending.Enqueue(frame);
                    _accepted++;
                    accepted = true;
                    UpdateMaximaUnsafe();
                    _available.Release();
                }
            }
        }

        DisposeFrames(dispose);
        return accepted;
    }

    internal async Task<CaptureStopResult> ClearAsync(CancellationToken cancellationToken)
    {
        Pause();
        List<CaptureSourceFrame> dispose;
        long disposedBefore;
        long disposedBytesBefore;
        lock (_gate)
        {
            disposedBefore = _disposedFrames;
            disposedBytesBefore = _disposedBytes;
            dispose = [.. _pending];
            _pending.Clear();
        }

        DisposeFrames(dispose);
        await _processing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                dispose = [.. _ring.Drain()];
            }

            DisposeFrames(dispose);
            lock (_gate)
            {
                return new CaptureStopResult(
                    checked((int)(_disposedFrames - disposedBefore)),
                    _disposedBytes - disposedBytesBefore);
            }
        }
        finally
        {
            _processing.Release();
        }
    }

    internal CaptureWorkerMetrics Snapshot(
        int processId,
        CaptureWorkerStatus status,
        long resizeCount,
        long stallCount,
        long faultCount,
        long restartCount,
        long workingSet,
        long privateMemory,
        int handleCount)
    {
        lock (_gate)
        {
            return new CaptureWorkerMetrics
            {
                WorkerProcessId = processId,
                Status = status,
                AcceptedFrames = _accepted,
                DroppedFrames = _dropped,
                DisposedFrames = _disposedFrames,
                RingFrameCount = _ring.Count,
                RingBytes = _ring.Bytes,
                QueueDepth = _pending.Count,
                QueueCapacity = ProcessingQueueCapacity,
                CurrentSourceFrames = CurrentFrameCountUnsafe(),
                MaximumObservedSourceFrames = _maximumObservedFrames,
                CurrentAccountedBytes = CurrentBytesUnsafe(),
                MaximumObservedAccountedBytes = _maximumObservedBytes,
                ResizeCount = resizeCount,
                StallCount = stallCount,
                FaultCount = faultCount,
                RestartCount = restartCount,
                OldestFrameLifetime = _ring.OldestLifetime(_clock.UtcNow),
                WorkingSetBytes = workingSet,
                PrivateMemoryBytes = privateMemory,
                NativeHandleCount = handleCount,
            };
        }
    }

    private List<CaptureSourceFrame> MakeRoomFor(long incomingBytes)
    {
        var dispose = new List<CaptureSourceFrame>();
        while (CurrentFrameCountUnsafe() >= _ring.MaximumFrames
               || checked(CurrentBytesUnsafe() + incomingBytes)
                   > CaptureWorkerMetrics.ScreenshotBudgetBytes)
        {
            var oldest = _ring.RemoveOldest();
            if (oldest is null && _pending.Count > 0)
            {
                oldest = _pending.Dequeue();
                _dropped++;
            }

            if (oldest is null)
            {
                break;
            }

            dispose.Add(oldest);
        }

        return dispose;
    }

    private async Task ConsumeAsync()
    {
        try
        {
            while (true)
            {
                await _available.WaitAsync(_lifetime.Token).ConfigureAwait(false);
                await _processing.WaitAsync(_lifetime.Token).ConfigureAwait(false);
                try
                {
                    CaptureSourceFrame? frame;
                    lock (_gate)
                    {
                        frame = _pending.Count > 0 ? _pending.Dequeue() : null;
                        _processingFrame = frame;
                    }

                    if (frame is null)
                    {
                        continue;
                    }

                    try
                    {
                        await _processor(frame, _lifetime.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
                    {
                        DisposeFrames([frame]);
                        throw;
                    }
                    catch (Exception)
                    {
                        lock (_gate)
                        {
                            _processingFrame = null;
                            _dropped++;
                        }

                        DisposeFrames([frame]);
                        continue;
                    }

                    IReadOnlyList<CaptureSourceFrame> evicted;
                    var publish = false;
                    lock (_gate)
                    {
                        _processingFrame = null;
                        if (_accepting && !_disposed)
                        {
                            evicted = _ring.Add(frame);
                            publish = !evicted.Contains(frame);
                        }
                        else
                        {
                            evicted = [frame];
                        }
                    }

                    DisposeFrames(evicted);
                    if (publish)
                    {
                        FrameReady?.Invoke(this, frame);
                    }
                }
                finally
                {
                    lock (_gate)
                    {
                        _processingFrame = null;
                    }

                    _processing.Release();
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    private int CurrentFrameCountUnsafe() =>
        _pending.Count + _ring.Count + (_processingFrame is null ? 0 : 1);

    private long CurrentBytesUnsafe() =>
        _pending.Sum(frame => frame.AccountedBytes)
        + _ring.Bytes
        + (_processingFrame?.AccountedBytes ?? 0);

    private void UpdateMaximaUnsafe()
    {
        _maximumObservedFrames = Math.Max(_maximumObservedFrames, CurrentFrameCountUnsafe());
        _maximumObservedBytes = Math.Max(_maximumObservedBytes, CurrentBytesUnsafe());
    }

    private void DisposeFrames(IEnumerable<CaptureSourceFrame> frames)
    {
        foreach (var frame in frames)
        {
            var wasDisposed = frame.IsDisposed;
            try
            {
                frame.Dispose();
            }
            catch (Exception)
            {
                // Ownership was atomically relinquished by CaptureSourceFrame before
                // the resource callback ran. Continue clearing every other frame even
                // if one third-party/native release reports a failure.
            }
            finally
            {
                if (!wasDisposed)
                {
                    lock (_gate)
                    {
                        _disposedFrames++;
                        _disposedBytes += frame.AccountedBytes;
                    }
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _accepting = false;
        }

        try
        {
            await ClearAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _disposed = true;
            }

            _lifetime.Cancel();
        }

        try
        {
            await _consumer.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _lifetime.Dispose();
            _available.Dispose();
            _processing.Dispose();
        }
    }
}
