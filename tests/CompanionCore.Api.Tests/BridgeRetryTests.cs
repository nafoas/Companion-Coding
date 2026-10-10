using CompanionCore.Memory;

namespace CompanionCore.Api.Tests;

public sealed class BridgeRetryTests
{
    [Fact]
    public async Task TimedOutAttempt_IsRetriedOnce_AndItsLateReplyIsNeverObserved()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var lateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ProviderReply>? lateCall = null;
        var mock = new MockSemanticProvider([
            (request, cancellationToken) => lateCall = SyntheticResponses.Gated(
                lateEntered,
                lateRelease.Task,
                late => SyntheticResponses.Interpretation(
                    late.OperationId,
                    summary: "Late synthetic summary.",
                    proposals: [SyntheticResponses.Append("synthetic.subject.late", "Late synthetic recollection.")]),
                honorCancellation: false)(request, cancellationToken),
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                summary: "On-time synthetic summary.",
                proposals: [SyntheticResponses.Append("synthetic.subject.ontime", "On-time synthetic recollection.")])),
        ]);
        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(100),
        });
        var published = new List<SemanticInterpretation>();
        bridge.InterpretationProduced += (_, interpretation) => published.Add(interpretation);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        // The abandoned first attempt now answers for the very same operation.
        lateRelease.SetResult();
        await lateCall!.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Yield();

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.Equal(2, outcome.Attempts);
        Assert.Equal("On-time synthetic summary.", Assert.Single(published).Summary);
        Assert.Single(await harness.RetrieveAsync("synthetic.subject.ontime"));
        Assert.Empty(await harness.RetrieveAsync("synthetic.subject.late"));
        Assert.All(mock.Requests, request => Assert.Equal(outcome.OperationId, request.OperationId));
        Assert.Equal([1, 2], mock.Requests.Select(request => request.Attempt));
        var diagnostics = bridge.GetDiagnosticsSnapshot();
        Assert.Equal(1, diagnostics.Timeouts);
        Assert.Equal(1, diagnostics.Retries);
        Assert.Equal(1, diagnostics.Commits);
    }

    [Fact]
    public async Task TransientFailures_RetryWithinTheBound_ThenSucceedExactlyOnce()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Reply(ProviderReply.Transient()),
            (_, _) => throw new InvalidOperationException("synthetic provider fault"),
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: [SyntheticResponses.Append("synthetic.subject.retry", "Retried synthetic recollection.")])),
        ]);
        var bridge = harness.OpenBridge(mock);
        var published = 0;
        bridge.InterpretationProduced += (_, _) => published++;
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.Equal(3, outcome.Attempts);
        Assert.Equal(1, published);
        Assert.Single(await harness.RetrieveAsync("synthetic.subject.retry"));
        var diagnostics = bridge.GetDiagnosticsSnapshot();
        Assert.Equal(2, diagnostics.TransientFailures);
        Assert.Equal(2, diagnostics.Retries);
        Assert.Equal(3, diagnostics.AttemptsSent);
    }

    [Fact]
    public async Task FaultedProviderTask_IsATransientFailureNotACrash()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider([
            (_, _) => Task.FromException<ProviderReply>(new IOException("synthetic faulted task")),
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(request.OperationId)),
        ]);
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.Equal(2, outcome.Attempts);
    }

    [Fact]
    public async Task ReplayingTheSameOperationsAppend_IsAlreadyCommittedNeverADuplicate()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var operationId = Guid.CreateVersion7(ApiTestHarness.BaselineUtc);
        var packet = new ResumePacket(
            Guid.NewGuid(),
            ApiTestHarness.BaselineUtc,
            "target-session:synthetic",
            null,
            [],
            [],
            0,
            false);
        MemoryProposalWire[] proposals =
        [
            new()
            {
                Operation = SemanticSchema.AppendOperationName,
                Scope = MemoryScope.Session,
                SourceKind = MemorySourceKind.Observed,
                Confidence = 0.6,
                SubjectKey = "synthetic.subject.idempotent",
                Recollection = "Idempotent synthetic recollection.",
            },
        ];

        var first = RemoteProposalAllowlist.Evaluate(operationId, proposals, packet, ApiTestHarness.BaselineUtc, "mock");
        var replayed = RemoteProposalAllowlist.Evaluate(operationId, proposals, packet, ApiTestHarness.BaselineUtc, "mock");
        var generation = harness.Privacy.Snapshot.Generation;

        var committed = await harness.Repository.WriteGate.SubmitAsync(first.Proposal!, generation);
        var again = await harness.Repository.WriteGate.SubmitAsync(replayed.Proposal!, generation);

        Assert.Equal(WriteGateStatus.Committed, committed.Status);
        Assert.Equal(WriteGateStatus.AlreadyCommitted, again.Status);
        Assert.Equal(committed.RecordIds, again.RecordIds);
        Assert.Single(await harness.RetrieveAsync("synthetic.subject.idempotent"));
    }

    [Fact]
    public void DerivedRecordIds_AreDeterministicDistinctAndVersioned()
    {
        var operationId = Guid.NewGuid();
        var first = RemoteProposalAllowlist.DeriveRecordId(operationId, 0);

        Assert.Equal(first, RemoteProposalAllowlist.DeriveRecordId(operationId, 0));
        Assert.NotEqual(first, RemoteProposalAllowlist.DeriveRecordId(operationId, 1));
        Assert.NotEqual(first, RemoteProposalAllowlist.DeriveRecordId(Guid.NewGuid(), 0));
        Assert.Equal(8, first.Version);
    }
}
