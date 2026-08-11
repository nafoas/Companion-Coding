using CompanionCore.Privacy;

namespace CompanionCore.Privacy.Tests;

public sealed class RuntimePrivacyStateTests
{
    [Fact]
    public void NewState_HasOneActivePositiveGeneration()
    {
        var state = new RuntimePrivacyState();

        var snapshot = state.Snapshot;

        Assert.Equal(1, snapshot.Generation);
        Assert.False(snapshot.IsPaused);
        Assert.Equal(0, snapshot.ActiveAdmissionCount);
        Assert.True(state.IsCurrent(snapshot.Generation));
    }

    [Fact]
    public async Task Pause_RevokesSynchronously_WaitsForAdmittedWork_AndRepeatedPauseIsStopOnly()
    {
        var state = new RuntimePrivacyState();
        var original = state.Snapshot.Generation;
        Assert.True(state.TryAcquireAdmissionLease(original, out var lease));

        var firstPause = state.PauseAndRevoke();

        Assert.False(state.IsCurrent(original));
        Assert.True(state.Snapshot.IsPaused);
        Assert.True(firstPause.PausedGeneration > original);
        Assert.False(firstPause.AdmittedWorkDrained.IsCompleted);
        Assert.False(state.TryAcquireAdmissionLease(out _));

        var repeated = state.PauseAndRevoke();
        Assert.True(repeated.WasAlreadyPaused);
        Assert.True(repeated.PausedGeneration > firstPause.PausedGeneration);

        lease!.Dispose();
        await firstPause.AdmittedWorkDrained;
        await repeated.AdmittedWorkDrained;
    }

    [Fact]
    public void StopArrivingDuringResume_InvalidatesTheOlderResumeAttempt()
    {
        var state = new RuntimePrivacyState();
        var initialPause = state.PauseAndRevoke();
        var laterStop = state.PauseAndRevoke();

        Assert.Throws<InvalidOperationException>(() =>
            state.ResumeExplicitly(initialPause.PausedGeneration));
        Assert.True(state.Snapshot.IsPaused);

        var resumed = state.ResumeExplicitly(laterStop.PausedGeneration);
        Assert.True(state.IsCurrent(resumed));
    }

    [Fact]
    public void ExplicitResume_CreatesFreshGeneration_AndOldExpectedGenerationStaysRejected()
    {
        var state = new RuntimePrivacyState();
        var oldGeneration = state.Snapshot.Generation;
        state.PauseAndRevoke();

        var resumed = state.ResumeExplicitly();

        Assert.True(resumed > oldGeneration);
        Assert.True(state.IsCurrent(resumed));
        Assert.False(state.TryAcquireAdmissionLease(oldGeneration, out _));
        Assert.True(state.TryAcquireAdmissionLease(resumed, out var current));
        current!.Dispose();
    }

    [Fact]
    public void Resume_WhenNotPaused_IsRejected()
    {
        var state = new RuntimePrivacyState();

        Assert.Throws<InvalidOperationException>(() => state.ResumeExplicitly());
    }

    [Fact]
    public void EndingTarget_AdvancesActiveGenerationWithoutPausingRuntime()
    {
        var state = new RuntimePrivacyState();
        var prior = state.Snapshot.Generation;

        var next = state.RevokeActiveGeneration();

        Assert.True(next > prior);
        Assert.False(state.Snapshot.IsPaused);
        Assert.False(state.IsCurrent(prior));
        Assert.True(state.IsCurrent(next));
    }
}
