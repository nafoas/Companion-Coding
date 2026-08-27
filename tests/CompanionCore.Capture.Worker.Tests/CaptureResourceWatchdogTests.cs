using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class CaptureResourceWatchdogTests
{
    [Fact]
    public void OrdinaryFluctuationDoesNotTrip()
    {
        var watchdog = new CaptureResourceWatchdog();
        var now = CaptureWorkerTestSupport.FixedTime;
        for (var index = 0; index < 40; index++)
        {
            var metrics = SafeMetrics() with
            {
                PrivateMemoryBytes = 120_000_000 + ((index % 4) * 2_000_000),
                NativeHandleCount = 140 + (index % 3),
            };
            Assert.False(watchdog.Observe(metrics, now.AddSeconds(index)));
        }
    }

    [Fact]
    public void SustainedPrivateMemoryGrowthTripsExactlyOnce()
    {
        var watchdog = new CaptureResourceWatchdog();
        var now = CaptureWorkerTestSupport.FixedTime;
        var trips = 0;
        for (var index = 0; index < 24; index++)
        {
            var metrics = SafeMetrics() with
            {
                PrivateMemoryBytes = 100_000_000 + (index * 24L * 1024 * 1024),
                NativeHandleCount = 100,
            };
            trips += watchdog.Observe(metrics, now.AddSeconds(index)) ? 1 : 0;
        }

        Assert.Equal(1, trips);
    }

    [Fact]
    public void ImpossibleHardBoundTripsImmediately()
    {
        var watchdog = new CaptureResourceWatchdog();
        var metrics = SafeMetrics() with
        {
            CurrentAttentionSheets = AttentionSheet.MaximumRetainedSheets + 1,
        };

        Assert.True(watchdog.Observe(metrics, CaptureWorkerTestSupport.FixedTime));
        Assert.False(watchdog.Observe(metrics, CaptureWorkerTestSupport.FixedTime.AddSeconds(1)));
    }

    private static CaptureWorkerMetrics SafeMetrics() => new()
    {
        WorkerProcessId = 123,
        Status = CaptureWorkerStatus.Running,
        QueueCapacity = 2,
    };
}
