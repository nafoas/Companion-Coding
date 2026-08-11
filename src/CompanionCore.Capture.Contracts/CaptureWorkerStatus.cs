namespace CompanionCore.Capture.Contracts;

public enum CaptureWorkerStatus
{
    Stopped,
    Starting,
    Running,
    Restarting,
    PausedMinimized,
    NoSignal,
    Faulted
}
