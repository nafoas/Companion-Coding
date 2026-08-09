namespace CompanionCore.Capture.Contracts;

/// <summary>
/// Metadata only — never a pixel buffer. Task 1's fake worker emits this with synthetic
/// values; the real out-of-process worker (Task 5+) is the only future implementation
/// permitted to attach actual frame data, and even then only across the bounded IPC
/// surface the architecture describes.
/// </summary>
public sealed record CaptureFrameMetadata(long SequenceNumber, DateTimeOffset Timestamp, int Width, int Height);
