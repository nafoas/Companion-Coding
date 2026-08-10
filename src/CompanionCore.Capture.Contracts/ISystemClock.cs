namespace CompanionCore.Capture.Contracts;

/// <summary>
/// An injectable time source. <see cref="CompanionCore.Capture.Fake.FakeCaptureWorker"/>
/// uses this instead of calling <see cref="DateTimeOffset.UtcNow"/> directly so its
/// output is genuinely deterministic under test — two scripted runs with the same clock
/// values produce byte-identical <see cref="CaptureFrameMetadata"/>/<see cref="CaptureWorkerStatusChanged"/>
/// records, not just "equal in structure."
/// </summary>
public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
