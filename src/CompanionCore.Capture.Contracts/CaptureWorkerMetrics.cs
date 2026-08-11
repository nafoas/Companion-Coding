namespace CompanionCore.Capture.Contracts;

/// <summary>
/// Privacy-safe capture resource accounting. This deliberately contains no titles,
/// raw paths, pixels, pixel-derived hashes, or unrelated-process information.
/// </summary>
public sealed record CaptureWorkerMetrics
{
    public const long ScreenshotBudgetBytes = 64L * 1024 * 1024;
    public const int MaximumSourceFrames = 3;

    public int WorkerProcessId { get; init; }

    public CaptureWorkerStatus Status { get; init; } = CaptureWorkerStatus.Stopped;

    public long AcceptedFrames { get; init; }

    public long DroppedFrames { get; init; }

    public long DisposedFrames { get; init; }

    public int RingFrameCount { get; init; }

    public long RingBytes { get; init; }

    public int QueueDepth { get; init; }

    public int QueueCapacity { get; init; }

    public int CurrentSourceFrames { get; init; }

    public int MaximumObservedSourceFrames { get; init; }

    public long CurrentAccountedBytes { get; init; }

    public long MaximumObservedAccountedBytes { get; init; }

    public long ResizeCount { get; init; }

    public long StallCount { get; init; }

    public long FaultCount { get; init; }

    public long RestartCount { get; init; }

    public TimeSpan OldestFrameLifetime { get; init; }

    public long WorkingSetBytes { get; init; }

    public long PrivateMemoryBytes { get; init; }

    public int NativeHandleCount { get; init; }

    public static CaptureWorkerMetrics Empty { get; } = new();

    public bool IsProtocolSafe() =>
        WorkerProcessId > 0
        && Enum.IsDefined(Status)
        && AcceptedFrames >= 0
        && DroppedFrames >= 0
        && DisposedFrames >= 0
        && RingFrameCount is >= 0 and <= MaximumSourceFrames
        && RingBytes is >= 0 and <= ScreenshotBudgetBytes
        && QueueDepth >= 0
        && QueueCapacity is >= 0 and <= 16
        && QueueDepth <= QueueCapacity
        && CurrentSourceFrames is >= 0 and <= MaximumSourceFrames
        && MaximumObservedSourceFrames is >= 0 and <= MaximumSourceFrames
        && CurrentSourceFrames <= MaximumObservedSourceFrames
        && CurrentAccountedBytes is >= 0 and <= ScreenshotBudgetBytes
        && MaximumObservedAccountedBytes is >= 0 and <= ScreenshotBudgetBytes
        && CurrentAccountedBytes <= MaximumObservedAccountedBytes
        && RingFrameCount <= CurrentSourceFrames
        && RingBytes <= CurrentAccountedBytes
        && ResizeCount >= 0
        && StallCount >= 0
        && FaultCount >= 0
        && RestartCount >= 0
        && OldestFrameLifetime >= TimeSpan.Zero
        && WorkingSetBytes >= 0
        && PrivateMemoryBytes >= 0
        && NativeHandleCount >= 0;
}
