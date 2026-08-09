namespace CompanionCore.Capture.Contracts;

public sealed record CaptureWorkerStatusChanged(CaptureWorkerStatus Status, DateTimeOffset Timestamp);
