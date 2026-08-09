using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Fake;

/// <summary>
/// The only capture-worker implementation Task 1 ships: an in-process, deterministic
/// synthetic double. It never touches a real window, never spawns a process, never
/// constructs runtime/identity state, and never writes memory — it exists purely to let
/// later components (and their tests) exercise <see cref="ICaptureWorker"/> before the
/// real out-of-process worker exists (Task 5+).
/// </summary>
public sealed class FakeCaptureWorker : ICaptureWorker
{
    private readonly ISystemClock _clock;
    private long _sequence;
    private bool _disposed;

    public FakeCaptureWorker(ISystemClock? clock = null)
    {
        _clock = clock ?? SystemClock.Instance;
    }

    public CaptureWorkerStatus Status { get; private set; } = CaptureWorkerStatus.Stopped;

    public event EventHandler<CaptureWorkerStatusChanged>? StatusChanged;

    public event EventHandler<CaptureFrameMetadata>? FrameProduced;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        SetStatus(CaptureWorkerStatus.Starting);
        SetStatus(CaptureWorkerStatus.Running);
        EmitSyntheticFrame();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        // An already-cancelled token must not mutate status — the caller asked to stop
        // via cancellation, not requested a state change we should still apply.
        cancellationToken.ThrowIfCancellationRequested();

        SetStatus(CaptureWorkerStatus.Stopped);
        return Task.CompletedTask;
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        // StopAsync/StartAsync each independently validate the token and leave status
        // untouched if it's already cancelled, so a cancelled restart never leaves the
        // worker in a half-transitioned state.
        await StopAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        SetStatus(CaptureWorkerStatus.Restarting);
        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private void EmitSyntheticFrame()
    {
        // Fixed 1x1 synthetic dimensions: this is metadata proving the event fired, not
        // a real capture of anything.
        var frame = new CaptureFrameMetadata(
            Interlocked.Increment(ref _sequence),
            _clock.UtcNow,
            Width: 1,
            Height: 1);
        FrameProduced?.Invoke(this, frame);
    }

    private void SetStatus(CaptureWorkerStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, new CaptureWorkerStatusChanged(status, _clock.UtcNow));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Status = CaptureWorkerStatus.Stopped;
    }
}
