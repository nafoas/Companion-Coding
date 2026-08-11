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
    private const int MaximumBufferedMetadata = 3;
    private readonly ISystemClock _clock;
    private readonly Queue<CaptureFrameMetadata> _bufferedMetadata = new();
    private long _sequence;
    private bool _disposed;

    public FakeCaptureWorker(ISystemClock? clock = null)
    {
        _clock = clock ?? SystemClock.Instance;
    }

    public CaptureWorkerStatus Status { get; private set; } = CaptureWorkerStatus.Stopped;

    public int BufferedMetadataCount => _bufferedMetadata.Count;

    public event EventHandler<CaptureWorkerStatusChanged>? StatusChanged;

    public event EventHandler<CaptureFrameMetadata>? FrameProduced;

    public Task StartAsync(
        CaptureAuthorizationGrant authorization,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();

        if (Status != CaptureWorkerStatus.Stopped)
        {
            throw new InvalidOperationException("The synthetic capture worker is already active.");
        }

        SetStatus(CaptureWorkerStatus.Starting);
        SetStatus(CaptureWorkerStatus.Running);
        EmitSyntheticFrame(authorization);
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

    public Task<CaptureStopResult> StopAndClearAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        SetStatus(CaptureWorkerStatus.Stopped);
        var count = _bufferedMetadata.Count;
        _bufferedMetadata.Clear();
        return Task.FromResult(new CaptureStopResult(count));
    }

    public async Task RestartAsync(
        CaptureAuthorizationGrant authorization,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        // StopAsync/StartAsync each independently validate the token and leave status
        // untouched if it's already cancelled, so a cancelled restart never leaves the
        // worker in a half-transitioned state.
        await StopAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        SetStatus(CaptureWorkerStatus.Restarting);
        // StartAsync accepts only Stopped; Restarting is an observable transition, not
        // an active state. Return to Stopped before entering the normal start path.
        Status = CaptureWorkerStatus.Stopped;
        await StartAsync(authorization, cancellationToken).ConfigureAwait(false);
    }

    private void EmitSyntheticFrame(CaptureAuthorizationGrant authorization)
    {
        // Fixed 1x1 synthetic dimensions: this is metadata proving the event fired, not
        // a real capture of anything.
        var frame = new CaptureFrameMetadata(
            authorization,
            Interlocked.Increment(ref _sequence),
            _clock.UtcNow,
            width: 1,
            height: 1);
        while (_bufferedMetadata.Count >= MaximumBufferedMetadata)
        {
            _bufferedMetadata.Dequeue();
        }

        _bufferedMetadata.Enqueue(frame);
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
        _bufferedMetadata.Clear();
    }
}
