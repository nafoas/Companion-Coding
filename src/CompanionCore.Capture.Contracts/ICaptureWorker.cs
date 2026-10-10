namespace CompanionCore.Capture.Contracts;

/// <summary>
/// The bounded, cancellable capture-worker contract. The normal Task 5 application
/// uses a dedicated out-of-process implementation; <c>CompanionCore.Capture.Fake</c>
/// remains only for deterministic tests. Nothing in this interface exposes raw full-screen
/// capture, identity construction, or memory-write capability — see architecture §6.1.
/// </summary>
public interface ICaptureWorker : IDisposable
{
    CaptureWorkerStatus Status { get; }

    event EventHandler<CaptureWorkerStatusChanged>? StatusChanged;

    event EventHandler<CaptureFrameMetadata>? FrameProduced;

    event EventHandler<AttentionSheetMetadata>? AttentionSheetProduced;

    Task StartAsync(CaptureAuthorizationGrant authorization, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task<CaptureStopResult> StopAndClearAsync(CancellationToken cancellationToken);

    Task RestartAsync(CaptureAuthorizationGrant authorization, CancellationToken cancellationToken);

    Task SetManualRegionAsync(
        CaptureAuthorizationGrant authorization,
        NormalizedRegion? region,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asks the worker to emit a fresh orientation sheet from its next usable frame.
    /// Consumers use this only as a bounded failsafe when an orientation could not be
    /// delivered; it requires the active grant.
    /// </summary>
    Task RequestOrientationAsync(
        CaptureAuthorizationGrant authorization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Transfers ownership of the next sheet the caller must see. An undelivered
    /// orientation is handed over first and is never displaced by newer regional
    /// sheets; otherwise the newest sheet is returned and any older queued sheet is
    /// disposed. Returns null when no current sheet is available.
    /// </summary>
    AttentionSheet? TakeLatestAttentionSheet();

    Task<CaptureWorkerMetrics> GetMetricsAsync(CancellationToken cancellationToken);
}
