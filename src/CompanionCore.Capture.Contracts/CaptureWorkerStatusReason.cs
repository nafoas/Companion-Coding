namespace CompanionCore.Capture.Contracts;

public enum CaptureWorkerStatusReason
{
    None,
    Minimized,
    FrameArrivalStalled,
    TargetClosed,
    TargetIdentityChanged,
    CaptureUnavailable,
    DeviceLost,
    ProtocolFailure,
    WorkerExited
}
