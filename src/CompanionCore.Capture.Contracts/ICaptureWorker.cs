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
    /// Transfers ownership of the newest available sheet to the caller and disposes
    /// any older queued sheet. Returns null when no current sheet is available.
    /// </summary>
    AttentionSheet? TakeLatestAttentionSheet();

    Task<CaptureWorkerMetrics> GetMetricsAsync(CancellationToken cancellationToken);
}
