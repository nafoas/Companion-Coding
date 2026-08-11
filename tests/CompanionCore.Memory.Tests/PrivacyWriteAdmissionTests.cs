using CompanionCore.Privacy;

namespace CompanionCore.Memory.Tests;

public sealed class PrivacyWriteAdmissionTests
{
    [Fact]
    public async Task PrivacyPause_WaitsForAlreadyAdmittedDurableAppendAndRejectsNewAppend()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var admitted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new LocalWriteGate(
            repository.Coordinator,
            directory.PrivacyState,
            new BlockingAdmissionHook(admitted, release));
        var firstRecord = SyntheticMemory.Record(subjectKey: "privacy.admitted");
        var firstWrite = gate.SubmitAsync(SyntheticMemory.Proposal(firstRecord));
        await admitted.Task;

        var pause = directory.PrivacyState.PauseAndRevoke();
        var rejected = await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(
                SyntheticMemory.Record(subjectKey: "privacy.rejected")));

        Assert.False(pause.AdmittedWorkDrained.IsCompleted);
        Assert.Equal(WriteGateRejectionReason.PrivacyPausedOrStale, rejected.RejectionReason);

        release.TrySetResult();
        var committed = await firstWrite;
        await pause.AdmittedWorkDrained;

        Assert.True(committed.IsAccepted);
        Assert.Single(await repository.RetrieveBySubjectAsync("privacy.admitted"));
        Assert.Empty(await repository.RetrieveBySubjectAsync("privacy.rejected"));
    }

    [Fact]
    public async Task PrivacyPause_RejectsNewLiveWriteBeforeJournalOrStore_AndExplicitResumeAllowsNewWork()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var record = SyntheticMemory.Record(subjectKey: "privacy.paused");
        var pause = directory.PrivacyState.PauseAndRevoke();
        await pause.AdmittedWorkDrained;

        var rejected = await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(record));

        Assert.Equal(WriteGateStatus.Rejected, rejected.Status);
        Assert.Equal(WriteGateRejectionReason.PrivacyPausedOrStale, rejected.RejectionReason);
        Assert.Empty(await repository.RetrieveBySubjectAsync("privacy.paused"));

        directory.PrivacyState.ResumeExplicitly();
        var accepted = await repository.WriteGate.SubmitAsync(SyntheticMemory.Proposal(record));
        Assert.True(accepted.IsAccepted);
        Assert.Single(await repository.RetrieveBySubjectAsync("privacy.paused"));
    }

    [Fact]
    public async Task OldGenerationBoundProposal_RemainsRejectedAfterExplicitResume()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        var oldGeneration = directory.PrivacyState.Snapshot.Generation;
        directory.PrivacyState.PauseAndRevoke();
        var currentGeneration = directory.PrivacyState.ResumeExplicitly();
        var oldProposal = SyntheticMemory.Proposal(
            SyntheticMemory.Record(subjectKey: "privacy.old-generation"));

        var stale = await repository.WriteGate.SubmitAsync(
            oldProposal,
            oldGeneration,
            CancellationToken.None);
        var current = await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(
                SyntheticMemory.Record(subjectKey: "privacy.current-generation")),
            currentGeneration,
            CancellationToken.None);

        Assert.Equal(WriteGateRejectionReason.PrivacyPausedOrStale, stale.RejectionReason);
        Assert.True(current.IsAccepted);
        Assert.Empty(await repository.RetrieveBySubjectAsync("privacy.old-generation"));
        Assert.Single(await repository.RetrieveBySubjectAsync("privacy.current-generation"));
    }

    private sealed class BlockingAdmissionHook(
        TaskCompletionSource admitted,
        TaskCompletionSource release) : ILiveWriteAdmissionTestHook
    {
        public async Task OnAdmittedAsync(CancellationToken cancellationToken)
        {
            admitted.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        }
    }
}
