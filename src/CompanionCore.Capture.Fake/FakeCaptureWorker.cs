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
    private long _disposedMetadata;
    private int _maximumBufferedMetadata;
    private CaptureAuthorizationGrant? _currentAuthorization;
    private NormalizedRegion? _manualRegion;
    private bool _disposed;

    public FakeCaptureWorker(ISystemClock? clock = null)
    {
        _clock = clock ?? SystemClock.Instance;
    }

    public CaptureWorkerStatus Status { get; private set; } = CaptureWorkerStatus.Stopped;

    public int BufferedMetadataCount => _bufferedMetadata.Count;

    public event EventHandler<CaptureWorkerStatusChanged>? StatusChanged;

    public event EventHandler<CaptureFrameMetadata>? FrameProduced;

    public event EventHandler<AttentionSheetMetadata>? AttentionSheetProduced;

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
        _currentAuthorization = authorization;
        _manualRegion = null;
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

        _currentAuthorization = null;
        _manualRegion = null;
        SetStatus(CaptureWorkerStatus.Stopped);
        return Task.CompletedTask;
    }

    public Task<CaptureStopResult> StopAndClearAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        _currentAuthorization = null;
        _manualRegion = null;
        SetStatus(CaptureWorkerStatus.Stopped);
        var count = _bufferedMetadata.Count;
        _disposedMetadata += count;
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

    public Task SetManualRegionAsync(
        CaptureAuthorizationGrant authorization,
        NormalizedRegion? region,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(authorization, _currentAuthorization)
            || Status != CaptureWorkerStatus.Running)
        {
            throw new InvalidOperationException("The manual region does not match the active grant.");
        }

        region?.Validate(nameof(region));
        _manualRegion = region;
        return Task.CompletedTask;
    }

    public AttentionSheet? TakeLatestAttentionSheet()
    {
        ThrowIfDisposed();
        return null;
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
            _disposedMetadata++;
        }

        _bufferedMetadata.Enqueue(frame);
        _maximumBufferedMetadata = Math.Max(_maximumBufferedMetadata, _bufferedMetadata.Count);
        FrameProduced?.Invoke(this, frame);
    }

    public Task<CaptureWorkerMetrics> GetMetricsAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CaptureWorkerMetrics
        {
            Status = Status,
            AcceptedFrames = _sequence,
            DisposedFrames = _disposedMetadata,
            RingFrameCount = _bufferedMetadata.Count,
            QueueCapacity = 0,
            CurrentSourceFrames = _bufferedMetadata.Count,
            MaximumObservedSourceFrames = _maximumBufferedMetadata,
        });
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
        _currentAuthorization = null;
        _manualRegion = null;
        Status = CaptureWorkerStatus.Stopped;
        _disposedMetadata += _bufferedMetadata.Count;
        _bufferedMetadata.Clear();
        StatusChanged = null;
        FrameProduced = null;
        AttentionSheetProduced = null;
    }
}
