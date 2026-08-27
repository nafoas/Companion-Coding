using System.Security.Cryptography;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal interface ICapturePixelSource : IDisposable
{
    ValueTask<OwnedBgra32Buffer> CopyPixelsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Exact-length, tightly packed BGRA32 working copy. It never owns a window or capture
/// authority and zeroes its pixel bytes deterministically.
/// </summary>
internal sealed class OwnedBgra32Buffer : IDisposable
{
    private byte[]? _pixels;

    internal OwnedBgra32Buffer(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height));
        }

        var expected = checked(width * height * 4);
        if (expected > CaptureWorkerMetrics.VisualWorkingBudgetBytes
            || pixels is null
            || pixels.Length != expected)
        {
            throw new ArgumentException("BGRA32 storage must exactly match bounded dimensions.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Stride = checked(width * 4);
        _pixels = pixels;
    }

    internal int Width { get; }

    internal int Height { get; }

    internal int Stride { get; }

    internal int ByteLength => checked(Stride * Height);

    internal Memory<byte> Memory =>
        Volatile.Read(ref _pixels) is { } pixels
            ? pixels
            : throw new ObjectDisposedException(nameof(OwnedBgra32Buffer));

    internal Span<byte> Span => Memory.Span;

    internal ReadOnlySpan<byte> ReadOnlySpan => Memory.Span;

    internal static OwnedBgra32Buffer Allocate(int width, int height) =>
        new(width, height, GC.AllocateUninitializedArray<byte>(checked(width * height * 4)));

    public void Dispose()
    {
        var pixels = Interlocked.Exchange(ref _pixels, null);
        if (pixels is not null)
        {
            CryptographicOperations.ZeroMemory(pixels);
        }
    }
}
