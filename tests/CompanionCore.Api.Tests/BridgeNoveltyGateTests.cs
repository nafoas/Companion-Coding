using CompanionCore.Memory;

namespace CompanionCore.Api.Tests;

/// <summary>M3a: the bridge asks the novelty gate before the write gate, inside the same privacy admission.</summary>
public sealed class BridgeNoveltyGateTests
{
    [Fact]
    public async Task OnlyWhatTheGateKeeps_IsWritten_AndRepeatsAreCounted()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var gate = new KeepFirst();
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: [SyntheticResponses.Append("synthetic.subject.kept", "Kept."), SyntheticResponses.Append("synthetic.subject.repeat", "Repeat.")])),
        ]);
        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { NoveltyGate = gate });
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.Equal((2, 1), (outcome.Memory.ProposedCount, outcome.Memory.Repeats));
        Assert.Equal(WriteGateStatus.Committed, outcome.Memory.GateResult!.Status);
        Assert.Single(await harness.RetrieveAsync("synthetic.subject.kept"));
        Assert.Empty(await harness.RetrieveAsync("synthetic.subject.repeat"));
        Assert.Equal(2, gate.Seen);
    }

    [Fact]
    public async Task WhenEverythingIsARepeat_NothingIsWritten_ButTheLookStillHappens()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: [SyntheticResponses.Append("synthetic.subject.repeat", "Repeat.")])),
        ]);
        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { NoveltyGate = new KeepNone() });
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.NotNull(outcome.Interpretation);
        Assert.Null(outcome.Memory.GateResult);
        Assert.Equal(1, outcome.Memory.Repeats);
        Assert.Empty(await harness.RetrieveAsync("synthetic.subject.repeat"));
    }

    [Fact]
    public async Task APrivacyStop_FencesTheGate_SoNothingIsEvenRecorded()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var gate = new KeepFirst();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mock = new MockSemanticProvider([
            SyntheticResponses.Gated(entered, release.Task, request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: [SyntheticResponses.Append("synthetic.subject.fenced", "Fenced.")])),
        ]);
        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { NoveltyGate = gate });
        var grant = harness.Grant();

        var pending = bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        harness.Privacy.PauseAndRevoke();
        release.SetResult();

        Assert.Equal(BridgeOutcomeKind.PrivacyFenced, (await pending.WaitAsync(TimeSpan.FromSeconds(10))).Kind);
        Assert.Equal(0, gate.Seen);
    }

    private sealed class KeepFirst : IMemoryNoveltyGate
    {
        public int Seen { get; private set; }

        public Task<IReadOnlyList<MemoryRecordDraft>> SiftAsync(IReadOnlyList<MemoryRecordDraft> drafts, ILocalMemoryReader memories, CancellationToken cancellationToken)
        {
            Seen += drafts.Count;
            return Task.FromResult<IReadOnlyList<MemoryRecordDraft>>([drafts[0]]);
        }
    }

    private sealed class KeepNone : IMemoryNoveltyGate
    {
        public Task<IReadOnlyList<MemoryRecordDraft>> SiftAsync(IReadOnlyList<MemoryRecordDraft> drafts, ILocalMemoryReader memories, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MemoryRecordDraft>>([]);
    }
}
