namespace CompanionCore.Capture.Contracts;

/// <summary>
/// The bounded, cancellable capture-worker contract later tasks build against. Task 1
/// ships only <c>CompanionCore.Capture.Fake</c>'s in-process implementation of this
/// interface; the real out-of-process worker (Task 5+) implements the same contract
/// from across a process boundary. Nothing in this interface exposes raw full-screen
/// capture, identity construction, or memory-write capability — see architecture §6.1.
/// </summary>
public interface ICaptureWorker : IDisposable
{
    CaptureWorkerStatus Status { get; }

    event EventHandler<CaptureWorkerStatusChanged>? StatusChanged;

    event EventHandler<CaptureFrameMetadata>? FrameProduced;

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task RestartAsync(CancellationToken cancellationToken);
}
