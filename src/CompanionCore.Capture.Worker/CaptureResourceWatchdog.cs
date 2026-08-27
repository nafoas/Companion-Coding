using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal sealed class CaptureResourceWatchdog
{
    internal const int WarmupSamples = 10;
    internal const int GrowthWindowSamples = 8;
    internal const long PrivateMemoryGrowthThreshold = 128L * 1024 * 1024;
    internal const int HandleGrowthThreshold = 128;
    internal static readonly TimeSpan MinimumSampleInterval = TimeSpan.FromSeconds(1);

    private readonly Queue<ResourceSample> _samples = new();
    private DateTimeOffset _lastSample;
    private int _observedSamples;
    private bool _tripped;

    internal bool Observe(CaptureWorkerMetrics metrics, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        if (_tripped)
        {
            return false;
        }

        if (ViolatesHardBounds(metrics))
        {
            _tripped = true;
            return true;
        }

        if (_lastSample != default && timestamp - _lastSample < MinimumSampleInterval)
        {
            return false;
        }

        _lastSample = timestamp;
        _observedSamples++;
        _samples.Enqueue(new ResourceSample(metrics.PrivateMemoryBytes, metrics.NativeHandleCount));
        while (_samples.Count > GrowthWindowSamples)
        {
            _samples.Dequeue();
        }

        if (_observedSamples <= WarmupSamples || _samples.Count < GrowthWindowSamples)
        {
            return false;
        }

        var values = _samples.ToArray();
        var risingMemory = 0;
        var risingHandles = 0;
        for (var index = 1; index < values.Length; index++)
        {
            risingMemory += values[index].PrivateMemoryBytes > values[index - 1].PrivateMemoryBytes ? 1 : 0;
            risingHandles += values[index].NativeHandleCount > values[index - 1].NativeHandleCount ? 1 : 0;
        }

        var memoryGrowth = values[^1].PrivateMemoryBytes - values[0].PrivateMemoryBytes;
        var handleGrowth = values[^1].NativeHandleCount - values[0].NativeHandleCount;
        if ((risingMemory >= GrowthWindowSamples - 2
             && memoryGrowth >= PrivateMemoryGrowthThreshold)
            || (risingHandles >= GrowthWindowSamples - 2
                && handleGrowth >= HandleGrowthThreshold))
        {
            _tripped = true;
            return true;
        }

        return false;
    }

    internal void Reset()
    {
        _samples.Clear();
        _lastSample = default;
        _observedSamples = 0;
        _tripped = false;
    }

    private static bool ViolatesHardBounds(CaptureWorkerMetrics metrics) =>
        !metrics.IsProtocolSafe();

    private readonly record struct ResourceSample(long PrivateMemoryBytes, int NativeHandleCount);
}
