namespace CompanionCore.Capture.Contracts;

public sealed record CaptureWorkerStatusChanged(
    CaptureWorkerStatus Status,
    DateTimeOffset Timestamp,
    CaptureWorkerStatusReason Reason = CaptureWorkerStatusReason.None);
