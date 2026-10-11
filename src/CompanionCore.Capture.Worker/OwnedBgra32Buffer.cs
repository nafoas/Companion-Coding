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
    private readonly bool _pooled;

    internal OwnedBgra32Buffer(int width, int height, byte[] pixels)
        : this(width, height, pixels, pooled: false)
    {
    }

    private OwnedBgra32Buffer(int width, int height, byte[] pixels, bool pooled)
    {
        _pooled = pooled;
        ArgumentNullException.ThrowIfNull(pixels);
        var expected = (long)width * height * 4;
        if (width <= 0
            || height <= 0
            || expected > CaptureWorkerMetrics.VisualWorkingBudgetBytes
            || expected != pixels.Length)
        {
            CryptographicOperations.ZeroMemory(pixels);
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

    /// <summary>
    /// A working buffer from the bounded pixel pool (VIS-01): at 4–10 looks a second, fresh
    /// multi-megabyte arrays would make the collector stall the Sniff. It always starts
    /// zeroed; the caller overwrites every byte.
    /// </summary>
    internal static OwnedBgra32Buffer Allocate(int width, int height)
    {
        var length = checked(width * height * 4);
        return new(width, height, PixelBufferPool.Rent(length), pooled: true);
    }

    public void Dispose()
    {
        var pixels = Interlocked.Exchange(ref _pixels, null);
        if (pixels is not null)
        {
            CryptographicOperations.ZeroMemory(pixels);
            if (_pooled)
            {
                PixelBufferPool.Return(pixels);
            }
        }
    }
}

/// <summary>
/// A small, bounded pool of zeroed pixel arrays, reused instead of reallocated. It keeps at
/// most <see cref="MaximumPerLength"/> arrays of each length and at most
/// <see cref="MaximumRetainedBytes"/> in total; arrays are always zeroed before they are
/// pooled, so no pixel content outlives its owner.
/// </summary>
internal static class PixelBufferPool
{
    internal const int MaximumPerLength = 4;
    internal const long MaximumRetainedBytes = CaptureWorkerMetrics.VisualWorkingBudgetBytes;

    private static readonly object Gate = new();
    private static readonly Dictionary<int, Stack<byte[]>> Free = [];
    private static long _retainedBytes;

    internal static long RetainedBytes
    {
        get
        {
            lock (Gate)
            {
                return _retainedBytes;
            }
        }
    }

    internal static byte[] Rent(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        lock (Gate)
        {
            if (Free.TryGetValue(length, out var stack) && stack.Count > 0)
            {
                _retainedBytes -= length;
                return stack.Pop();
            }
        }

        // Rare once the pool is warm; a fresh array is zeroed, so a buffer never starts with stale memory.
        return new byte[length];
    }

    /// <summary>Takes back an array the caller has already zeroed.</summary>
    internal static void Return(byte[] pixels)
    {
        lock (Gate)
        {
            if (!Free.TryGetValue(pixels.Length, out var stack))
            {
                // Only a few geometries are ever live; forget older lengths first.
                if (Free.Count >= MaximumPerLength)
                {
                    foreach (var (length, old) in Free.ToArray())
                    {
                        _retainedBytes -= (long)length * old.Count;
                        Free.Remove(length);
                    }
                }

                stack = new Stack<byte[]>();
                Free[pixels.Length] = stack;
            }

            if (stack.Count < MaximumPerLength && _retainedBytes + pixels.Length <= MaximumRetainedBytes)
            {
                stack.Push(pixels);
                _retainedBytes += pixels.Length;
            }
        }
    }
}
