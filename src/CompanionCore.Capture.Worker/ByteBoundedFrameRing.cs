namespace CompanionCore.Capture.Worker;

internal sealed class ByteBoundedFrameRing
{
    private readonly Queue<CaptureSourceFrame> _frames = new();

    internal ByteBoundedFrameRing(long maximumBytes, int maximumFrames)
    {
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        if (maximumFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrames));
        }

        MaximumBytes = maximumBytes;
        MaximumFrames = maximumFrames;
    }

    internal long MaximumBytes { get; }

    internal int MaximumFrames { get; }

    internal long Bytes { get; private set; }

    internal int Count => _frames.Count;

    internal IReadOnlyList<CaptureSourceFrame> Add(CaptureSourceFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var evicted = new List<CaptureSourceFrame>();
        if (frame.AccountedBytes > MaximumBytes)
        {
            evicted.Add(frame);
            return evicted;
        }

        while (_frames.Count >= MaximumFrames
               || checked(Bytes + frame.AccountedBytes) > MaximumBytes)
        {
            var oldest = _frames.Dequeue();
            Bytes -= oldest.AccountedBytes;
            evicted.Add(oldest);
        }

        _frames.Enqueue(frame);
        Bytes += frame.AccountedBytes;
        return evicted;
    }

    internal CaptureSourceFrame? RemoveOldest()
    {
        if (_frames.Count == 0)
        {
            return null;
        }

        var frame = _frames.Dequeue();
        Bytes -= frame.AccountedBytes;
        return frame;
    }

    internal IReadOnlyList<CaptureSourceFrame> Drain()
    {
        var drained = _frames.ToArray();
        _frames.Clear();
        Bytes = 0;
        return drained;
    }

    internal TimeSpan OldestLifetime(DateTimeOffset now)
    {
        if (!_frames.TryPeek(out var frame))
        {
            return TimeSpan.Zero;
        }

        var lifetime = now - frame.Timestamp;
        return lifetime > TimeSpan.Zero ? lifetime : TimeSpan.Zero;
    }
}
