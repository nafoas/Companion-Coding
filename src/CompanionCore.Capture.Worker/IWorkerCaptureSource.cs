using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal interface IWorkerCaptureSource : IAsyncDisposable
{
    event EventHandler<CaptureSourceFrame>? FrameArrived;

    event EventHandler<CaptureSourceStatusChanged>? StatusChanged;

    Task StartAsync(CaptureIpcAuthorization authorization, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

internal sealed record CaptureSourceStatusChanged(
    CaptureWorkerStatus Status,
    CaptureWorkerStatusReason Reason,
    bool ClearRetainedFrames = false,
    bool IsResize = false,
    bool IsStall = false,
    bool IsFault = false);
