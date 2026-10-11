namespace CompanionCore.Orchestration;

/// <summary>A bounded, thread-safe window of recent latencies: last, maximum, and the 95th percentile.</summary>
internal sealed class LatencyWindow
{
    private readonly object _gate = new();
    private readonly TimeSpan[] _samples;
    private int _count;
    private int _next;
    private TimeSpan _last;
    private TimeSpan _maximum;

    internal LatencyWindow(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _samples = new TimeSpan[capacity];
    }

    internal void Add(TimeSpan latency)
    {
        lock (_gate)
        {
            _samples[_next] = latency;
            _next = (_next + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
            _last = latency;
            _maximum = latency > _maximum ? latency : _maximum;
        }
    }

    internal (TimeSpan Last, TimeSpan Maximum, TimeSpan Percentile95) Snapshot()
    {
        TimeSpan[] window;
        TimeSpan last;
        TimeSpan maximum;
        lock (_gate)
        {
            window = _samples[.._count];
            last = _last;
            maximum = _maximum;
        }

        if (window.Length == 0)
        {
            return (TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        }

        Array.Sort(window);
        var index = Math.Clamp((int)Math.Ceiling(window.Length * 0.95) - 1, 0, window.Length - 1);
        return (last, maximum, window[index]);
    }
}
