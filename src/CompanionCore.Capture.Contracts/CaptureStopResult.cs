namespace CompanionCore.Capture.Contracts;

public readonly record struct CaptureStopResult(int ClearedMetadataCount, long ClearedBytes = 0);
