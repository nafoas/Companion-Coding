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
    private long _sequence;
    private bool _disposed;

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
        SetStatus(CaptureWorkerStatus.Stopped);
        return Task.CompletedTask;
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await StopAsync(cancellationToken).ConfigureAwait(false);
        SetStatus(CaptureWorkerStatus.Restarting);
        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private void EmitSyntheticFrame()
    {
        // Fixed 1x1 synthetic dimensions: this is metadata proving the event fired, not
        // a real capture of anything.
        var frame = new CaptureFrameMetadata(
            Interlocked.Increment(ref _sequence),
            DateTimeOffset.UtcNow,
            Width: 1,
            Height: 1);
        FrameProduced?.Invoke(this, frame);
    }

    private void SetStatus(CaptureWorkerStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, new CaptureWorkerStatusChanged(status, DateTimeOffset.UtcNow));
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
