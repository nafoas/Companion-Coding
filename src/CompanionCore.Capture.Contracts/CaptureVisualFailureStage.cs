namespace CompanionCore.Capture.Contracts;

/// <summary>FIX-08: where a frame's visual processing (sniff and sheet) failed.</summary>
public enum CaptureVisualFailureStage
{
    None = 0,
    PixelCopy = 1,
    Sniff = 2,
    Compose = 3,
    Encode = 4,
    Contract = 5,
}
