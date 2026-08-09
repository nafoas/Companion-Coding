using CompanionCore.Runtime;
using CompanionCore.Runtime.Diagnostics;

namespace CompanionCore.Runtime.Tests;

public sealed class CompanionRuntimeTests
{
    [Fact]
    public void Start_FromNotStarted_IsValidAndRuns()
    {
        var runtime = new CompanionRuntime();

        var result = runtime.Start();

        Assert.True(result.IsValid);
        Assert.Equal(LifecycleEvent.Start, result.Event);
        Assert.Equal(RuntimeState.NotStarted, result.PriorState);
        Assert.Equal(RuntimeState.Running, result.ResultingState);
        Assert.False(result.CheckpointRecovered);
        Assert.Equal(RuntimeState.Running, runtime.State);
    }

    [Fact]
    public void Start_WithCheckpointRecovered_CarriesTheFlagThrough()
    {
        var runtime = new CompanionRuntime();

        var result = runtime.Start(checkpointRecovered: true);

        Assert.True(result.IsValid);
        Assert.True(result.CheckpointRecovered);
    }

    [Fact]
    public void Start_WhenAlreadyRunning_IsInvalidAndStateUnchanged()
    {
        var runtime = new CompanionRuntime();
        runtime.Start();

        var second = runtime.Start();

        Assert.False(second.IsValid);
        Assert.Equal(RuntimeState.Running, second.PriorState);
        Assert.Equal(RuntimeState.Running, second.ResultingState);
        Assert.Equal(RuntimeState.Running, runtime.State);
    }

    [Fact]
    public void Nap_FromRunning_IsValid()
    {
        var runtime = new CompanionRuntime();
        runtime.Start();

        var result = runtime.Nap();

        Assert.True(result.IsValid);
        Assert.Equal(RuntimeState.Napping, runtime.State);
    }

    [Fact]
    public void Nap_BeforeStart_IsInvalid()
    {
        var runtime = new CompanionRuntime();

        var result = runtime.Nap();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.NotStarted, runtime.State);
    }

    [Fact]
    public void Nap_WhileAlreadyNapping_IsInvalid()
    {
        var runtime = new CompanionRuntime();
        runtime.Start();
        runtime.Nap();

        var result = runtime.Nap();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.Napping, runtime.State);
    }

    [Fact]
    public void Wake_FromNapping_IsValid()
    {
        var runtime = new CompanionRuntime();
        runtime.Start();
        runtime.Nap();

        var result = runtime.Wake();

        Assert.True(result.IsValid);
        Assert.Equal(RuntimeState.Running, runtime.State);
    }

    [Fact]
    public void Wake_WhileRunning_IsInvalid()
    {
        var runtime = new CompanionRuntime();
        runtime.Start();

        var result = runtime.Wake();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.Running, runtime.State);
    }

    [Fact]
    public void Wake_BeforeStart_IsInvalid()
    {
        var runtime = new CompanionRuntime();

        var result = runtime.Wake();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.NotStarted, runtime.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stop_FromRunningOrNapping_IsValid(bool napFirst)
    {
        var runtime = new CompanionRuntime();
        runtime.Start();
        if (napFirst)
        {
            runtime.Nap();
        }

        var result = runtime.Stop();

        Assert.True(result.IsValid);
        Assert.Equal(RuntimeState.Stopped, runtime.State);
    }

    [Fact]
    public void Stop_BeforeStart_IsInvalid()
    {
        var runtime = new CompanionRuntime();

        var result = runtime.Stop();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.NotStarted, runtime.State);
    }

    [Fact]
    public void Stop_WhenAlreadyStopped_IsInvalid()
    {
        var runtime = new CompanionRuntime();
        runtime.Start();
        runtime.Stop();

        var result = runtime.Stop();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.Stopped, runtime.State);
    }

    [Fact]
    public void Stop_CancelsTheLifetimeToken()
    {
        var runtime = new CompanionRuntime();
        runtime.Start();

        Assert.False(runtime.LifetimeToken.IsCancellationRequested);
        runtime.Stop();

        Assert.True(runtime.LifetimeToken.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var runtime = new CompanionRuntime();
        runtime.Start();

        runtime.Dispose();
        var exception = Record.Exception(runtime.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_WithoutExplicitStop_StillCancelsLifetimeAndStops()
    {
        var runtime = new CompanionRuntime();
        runtime.Start();

        runtime.Dispose();

        Assert.Equal(RuntimeState.Stopped, runtime.State);
        Assert.True(runtime.LifetimeToken.IsCancellationRequested);
    }

    [Fact]
    public void InvalidTransition_IsLoggedOnlyToTheSuppliedDiagnosticsSink()
    {
        var capturingSink = new CapturingDiagnosticsSink();
        var runtime = new CompanionRuntime(capturingSink);

        // Wake before Start is invalid.
        runtime.Wake();

        var entry = Assert.Single(capturingSink.Entries);
        Assert.Equal("lifecycle.invalid-transition", entry.Category);
        Assert.Contains("Wake", entry.Message);
    }

    [Fact]
    public void ValidTransitions_NeverLogToTheDiagnosticsSink()
    {
        var capturingSink = new CapturingDiagnosticsSink();
        var runtime = new CompanionRuntime(capturingSink);

        runtime.Start();
        runtime.Nap();
        runtime.Wake();
        runtime.Stop();

        Assert.Empty(capturingSink.Entries);
    }

    [Fact]
    public void ConstructionCount_IncreasesByExactlyOnePerConstructedInstance()
    {
        // ConstructionCount is a process-wide static counter. This test only asserts the
        // delta caused by its own construction, which is safe as long as no other test
        // in this assembly constructs a CompanionRuntime concurrently with it — true for
        // this file today. It intentionally does not assert an absolute value.
        var before = CompanionRuntime.ConstructionCount;

        _ = new CompanionRuntime();

        Assert.Equal(before + 1, CompanionRuntime.ConstructionCount);
    }

    private sealed class CapturingDiagnosticsSink : IDiagnosticsSink
    {
        public List<(string Category, string Message)> Entries { get; } = new();

        public void Log(string category, string message) => Entries.Add((category, message));
    }
}
