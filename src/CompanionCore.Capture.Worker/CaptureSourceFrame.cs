namespace CompanionCore.Capture.Worker;

internal sealed class CaptureSourceFrame : IDisposable
{
    private IDisposable? _resource;
    private long _sequenceNumber;

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

    internal long SequenceNumber => Volatile.Read(ref _sequenceNumber);

    internal bool HasPixels => Volatile.Read(ref _resource) is ICapturePixelSource;

    internal void AssignSequence(long sequenceNumber)
    {
        if (sequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        }

        if (Interlocked.CompareExchange(ref _sequenceNumber, sequenceNumber, 0) != 0)
        {
            throw new InvalidOperationException("A source frame sequence can be assigned only once.");
        }
    }

    internal ValueTask<OwnedBgra32Buffer?> CopyPixelsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Volatile.Read(ref _resource) is ICapturePixelSource pixels
            ? CopyCoreAsync(pixels, cancellationToken)
            : ValueTask.FromResult<OwnedBgra32Buffer?>(null);
    }

    private static async ValueTask<OwnedBgra32Buffer?> CopyCoreAsync(
        ICapturePixelSource source,
        CancellationToken cancellationToken) =>
        await source.CopyPixelsAsync(cancellationToken).ConfigureAwait(false);

    public void Dispose() => Interlocked.Exchange(ref _resource, null)?.Dispose();
}
