namespace CompanionCore.Capture.Worker;

internal sealed class CaptureSourceFrame : IDisposable
{
    private IDisposable? _resource;

    internal CaptureSourceFrame(
        DateTimeOffset timestamp,
        int width,
        int height,
        long accountedBytes,
        IDisposable resource)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (accountedBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(accountedBytes));
        }

        Timestamp = timestamp;
        Width = width;
        Height = height;
        AccountedBytes = accountedBytes;
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));
    }

    internal DateTimeOffset Timestamp { get; }

    internal int Width { get; }

    internal int Height { get; }

    internal long AccountedBytes { get; }

    internal bool IsDisposed => Volatile.Read(ref _resource) is null;

    public void Dispose() => Interlocked.Exchange(ref _resource, null)?.Dispose();
}
