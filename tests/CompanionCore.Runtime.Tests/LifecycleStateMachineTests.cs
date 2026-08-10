using CompanionCore.Runtime;
using CompanionCore.Runtime.Diagnostics;

namespace CompanionCore.Runtime.Tests;

/// <summary>
/// Exercises the actual state-machine behavior directly against
/// <see cref="LifecycleStateMachine"/>, which — unlike <see cref="CompanionRuntime"/> —
/// carries no one-shot construction restriction, so each test can freely construct its
/// own isolated instance. <see cref="CompanionRuntimeConstructionTests"/> covers the
/// one-shot authority mechanism <see cref="CompanionRuntime"/> wraps around this.
/// </summary>
public sealed class LifecycleStateMachineTests
{
    [Fact]
    public void Start_FromNotStarted_IsValidAndRuns()
    {
        var machine = new LifecycleStateMachine();

        var result = machine.Start();

        Assert.True(result.IsValid);
        Assert.Equal(LifecycleEvent.Start, result.Event);
        Assert.Equal(RuntimeState.NotStarted, result.PriorState);
        Assert.Equal(RuntimeState.Running, result.ResultingState);
        Assert.False(result.CheckpointRecovered);
        Assert.Equal(RuntimeState.Running, machine.State);
    }

    [Fact]
    public void Start_WithCheckpointRecovered_CarriesTheFlagThrough()
    {
        var machine = new LifecycleStateMachine();

        var result = machine.Start(checkpointRecovered: true);

        Assert.True(result.IsValid);
        Assert.True(result.CheckpointRecovered);
    }

    [Fact]
    public void Start_WhenAlreadyRunning_IsInvalidAndStateUnchanged()
    {
        var machine = new LifecycleStateMachine();
        machine.Start();

        var second = machine.Start();

        Assert.False(second.IsValid);
        Assert.Equal(RuntimeState.Running, second.PriorState);
        Assert.Equal(RuntimeState.Running, second.ResultingState);
        Assert.Equal(RuntimeState.Running, machine.State);
    }

    [Fact]
    public void Nap_FromRunning_IsValid()
    {
        var machine = new LifecycleStateMachine();
        machine.Start();

        var result = machine.Nap();

        Assert.True(result.IsValid);
        Assert.Equal(RuntimeState.Napping, machine.State);
    }

    [Fact]
    public void Nap_BeforeStart_IsInvalid()
    {
        var machine = new LifecycleStateMachine();

        var result = machine.Nap();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.NotStarted, machine.State);
    }

    [Fact]
    public void Nap_WhileAlreadyNapping_IsInvalid()
    {
        var machine = new LifecycleStateMachine();
        machine.Start();
        machine.Nap();

        var result = machine.Nap();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.Napping, machine.State);
    }

    [Fact]
    public void Wake_FromNapping_IsValid()
    {
        var machine = new LifecycleStateMachine();
        machine.Start();
        machine.Nap();

        var result = machine.Wake();

        Assert.True(result.IsValid);
        Assert.Equal(RuntimeState.Running, machine.State);
    }

    [Fact]
    public void Wake_WhileRunning_IsInvalid()
    {
        var machine = new LifecycleStateMachine();
        machine.Start();

        var result = machine.Wake();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.Running, machine.State);
    }

    [Fact]
    public void Wake_BeforeStart_IsInvalid()
    {
        var machine = new LifecycleStateMachine();

        var result = machine.Wake();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.NotStarted, machine.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stop_FromRunningOrNapping_IsValid(bool napFirst)
    {
        var machine = new LifecycleStateMachine();
        machine.Start();
        if (napFirst)
        {
            machine.Nap();
        }

        var result = machine.Stop();

        Assert.True(result.IsValid);
        Assert.Equal(RuntimeState.Stopped, machine.State);
    }

    [Fact]
    public void Stop_BeforeStart_IsInvalid()
    {
        var machine = new LifecycleStateMachine();

        var result = machine.Stop();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.NotStarted, machine.State);
    }

    [Fact]
    public void Stop_WhenAlreadyStopped_IsInvalid()
    {
        var machine = new LifecycleStateMachine();
        machine.Start();
        machine.Stop();

        var result = machine.Stop();

        Assert.False(result.IsValid);
        Assert.Equal(RuntimeState.Stopped, machine.State);
    }

    [Fact]
    public void Stop_CancelsTheLifetimeToken()
    {
        var machine = new LifecycleStateMachine();
        machine.Start();

        Assert.False(machine.LifetimeToken.IsCancellationRequested);
        machine.Stop();

        Assert.True(machine.LifetimeToken.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var machine = new LifecycleStateMachine();
        machine.Start();

        machine.Dispose();
        var exception = Record.Exception(machine.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_WithoutExplicitStop_StillCancelsLifetimeAndStops()
    {
        var machine = new LifecycleStateMachine();
        machine.Start();

        machine.Dispose();

        Assert.Equal(RuntimeState.Stopped, machine.State);
        Assert.True(machine.LifetimeToken.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_BeforeStart_IsTerminalAndRejectsEveryLaterTransition()
    {
        var machine = new LifecycleStateMachine();

        machine.Dispose();

        Assert.Equal(RuntimeState.Stopped, machine.State);
        Assert.True(machine.LifetimeToken.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => machine.Start());
        Assert.Throws<ObjectDisposedException>(() => machine.Nap());
        Assert.Throws<ObjectDisposedException>(() => machine.Wake());
        Assert.Throws<ObjectDisposedException>(() => machine.Stop());
    }

    [Fact]
    public void InvalidTransition_IsLoggedOnlyToTheSuppliedDiagnosticsSink()
    {
        var capturingSink = new CapturingDiagnosticsSink();
        var machine = new LifecycleStateMachine(capturingSink);

        // Wake before Start is invalid.
        machine.Wake();

        var entry = Assert.Single(capturingSink.Entries);
        Assert.Equal("lifecycle.invalid-transition", entry.Category);
        Assert.Contains("Wake", entry.Message);
    }

    [Fact]
    public void ValidTransitions_NeverLogToTheDiagnosticsSink()
    {
        var capturingSink = new CapturingDiagnosticsSink();
        var machine = new LifecycleStateMachine(capturingSink);

        machine.Start();
        machine.Nap();
        machine.Wake();
        machine.Stop();

        Assert.Empty(capturingSink.Entries);
    }

    private sealed class CapturingDiagnosticsSink : IDiagnosticsSink
    {
        public List<(string Category, string Message)> Entries { get; } = new();

        public void Log(string category, string message) => Entries.Add((category, message));
    }
}
