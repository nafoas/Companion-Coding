namespace CompanionCore.Capture.Client;

public sealed class CaptureWorkerCommandException : Exception
{
    internal CaptureWorkerCommandException(string operation)
        : base($"The capture worker rejected the bounded {operation} operation.")
    {
    }
}
