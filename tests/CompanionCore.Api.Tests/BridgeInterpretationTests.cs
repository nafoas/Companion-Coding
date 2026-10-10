using CompanionCore.Capture.Contracts;
using CompanionCore.Memory;

namespace CompanionCore.Api.Tests;

public sealed class BridgeInterpretationTests
{
    [Fact]
    public async Task MockProvider_TurnsOneAuthorizedSheetIntoOneInterpretationOneCommitAndOneOutput()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: [SyntheticResponses.Append("synthetic.subject.alpha", "Alpha synthetic recollection.")])),
        ]);
        var bridge = harness.OpenBridge(mock);
        var published = new List<SemanticInterpretation>();
        bridge.InterpretationProduced += (_, interpretation) => published.Add(interpretation);
        var grant = harness.Grant();
        var sheet = ApiTestHarness.Sheet(grant);
        var imageLength = sheet.Length;

        var outcome = await bridge.InterpretAttentionSheetAsync(sheet, grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.Equal(1, outcome.Attempts);
        Assert.Equal(outcome.Interpretation, Assert.Single(published));
        Assert.Equal(grant.TargetSessionId, outcome.Interpretation!.TargetSessionId);
        Assert.Equal("Neutral synthetic summary.", outcome.Interpretation.Summary);
        Assert.Equal(WriteGateStatus.Committed, outcome.Memory.GateResult!.Status);
        Assert.Equal(0, sheet.Length);

        var stored = Assert.Single(await harness.RetrieveAsync("synthetic.subject.alpha"));
        Assert.Equal(outcome.OperationId, stored.LocalOperationId);
        Assert.Equal(RemoteProposalAllowlist.DeriveRecordId(outcome.OperationId, 0), stored.Record.RecordId);
        Assert.Equal(MemorySourceKind.Observed, stored.Record.SourceKind);
        Assert.Equal($"target-session:{grant.TargetSessionId:N}", stored.Record.SessionReference);
        Assert.Contains(outcome.OperationId.ToString("D"), stored.Record.RetrievalMetadataJson);

        var recorded = Assert.Single(mock.Requests);
        Assert.Equal(outcome.OperationId, recorded.OperationId);
        Assert.Equal(imageLength, recorded.ImageLength);
        Assert.DoesNotContain(Convert.ToBase64String(new byte[] { 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A }), recorded.RequestJson);
    }

    [Fact]
    public async Task ReplayProvider_TurnsOneAuthorizedSheetIntoOneInterpretation()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var replay = ReplaySemanticProvider.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "replay"));
        var bridge = harness.OpenBridge(replay);
        var published = new List<SemanticInterpretation>();
        bridge.InterpretationProduced += (_, interpretation) => published.Add(interpretation);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.Equal("Replayed synthetic orientation summary.", Assert.Single(published).Summary);
        var stored = Assert.Single(await harness.RetrieveAsync("synthetic.replay.scene"));
        Assert.Equal("Replayed synthetic recollection.", stored.Record.VisibleRecollection);
        Assert.Equal(outcome.OperationId, stored.LocalOperationId);
        Assert.Equal(40, bridge.GetDiagnosticsSnapshot().EstimatedOutputUnits);
    }

    [Fact]
    public async Task ReplayProvider_ReplaysAScriptedRetryThroughTheSameContract()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var replay = ReplaySemanticProvider.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "replay"));
        var bridge = harness.OpenBridge(replay);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(
            ApiTestHarness.Sheet(grant, AttentionSheetKind.Regional),
            grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.Equal(2, outcome.Attempts);
        Assert.Equal(AttentionRegionKind.CenterEnvironment, Assert.Single(outcome.Interpretation!.Observations).Region);
        Assert.Equal(1, bridge.GetDiagnosticsSnapshot().Retries);
    }

    [Fact]
    public async Task MismatchedGrant_IsRefusedBeforeAnyProviderCall()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider();
        var bridge = harness.OpenBridge(mock);
        var sheet = ApiTestHarness.Sheet(harness.Grant());

        var outcome = await bridge.InterpretAttentionSheetAsync(sheet, harness.Grant());

        Assert.Equal(BridgeOutcomeKind.NotAuthorized, outcome.Kind);
        Assert.Equal(0, mock.CallCount);
        Assert.Equal(0, sheet.Length);
    }

    [Fact]
    public async Task PrivacyPausedOrStaleGeneration_IsRefusedBeforeAnyProviderCall()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider();
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();
        harness.Privacy.PauseAndRevoke();

        var paused = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        harness.Privacy.ResumeExplicitly();
        var stale = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.NotAuthorized, paused.Kind);
        Assert.Equal(BridgeOutcomeKind.NotAuthorized, stale.Kind);
        Assert.Equal(0, mock.CallCount);
    }

    [Fact]
    public async Task AlreadyDisposedSheet_IsRefused()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider();
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();
        var sheet = ApiTestHarness.Sheet(grant);
        sheet.Dispose();

        var outcome = await bridge.InterpretAttentionSheetAsync(sheet, grant);

        Assert.Equal(BridgeOutcomeKind.NotAuthorized, outcome.Kind);
        Assert.Equal(0, mock.CallCount);
    }

    [Fact]
    public async Task PrivacyStopDuringAnInFlightRequest_FencesTheLateResultFromMemoryAndOutput()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mock = new MockSemanticProvider([
            SyntheticResponses.Gated(entered, release.Task, request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: [SyntheticResponses.Append("synthetic.subject.fenced", "Fenced synthetic recollection.")])),
        ]);
        var bridge = harness.OpenBridge(mock);
        var published = new List<SemanticInterpretation>();
        bridge.InterpretationProduced += (_, interpretation) => published.Add(interpretation);
        var grant = harness.Grant();

        var pending = bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        harness.Privacy.PauseAndRevoke();
        release.SetResult();
        var outcome = await pending.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(BridgeOutcomeKind.PrivacyFenced, outcome.Kind);
        Assert.Null(outcome.Interpretation);
        Assert.Empty(published);
        Assert.Empty(await harness.RetrieveAsync("synthetic.subject.fenced"));
        Assert.Equal(1, mock.CallCount);
    }

    [Fact]
    public async Task CancelPendingWork_CancelsWithoutRetryWriteOrOutput()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource();
        var mock = new MockSemanticProvider([
            SyntheticResponses.Gated(entered, never.Task, request => SyntheticResponses.Interpretation(request.OperationId)),
        ]);
        var bridge = harness.OpenBridge(mock);
        var published = new List<SemanticInterpretation>();
        bridge.InterpretationProduced += (_, interpretation) => published.Add(interpretation);
        var grant = harness.Grant();
        var sheet = ApiTestHarness.Sheet(grant);

        var pending = bridge.InterpretAttentionSheetAsync(sheet, grant);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        bridge.CancelPendingWork();
        var outcome = await pending.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(BridgeOutcomeKind.Cancelled, outcome.Kind);
        Assert.Empty(published);
        Assert.Equal(1, mock.CallCount);
        Assert.Equal(0, sheet.Length);

        // Later work is not affected by the earlier cancellation.
        var next = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Equal(BridgeOutcomeKind.Interpreted, next.Kind);
    }

    [Fact]
    public async Task CallerCancellation_StopsWithoutRetryWriteOrOutput()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource();
        var mock = new MockSemanticProvider([
            SyntheticResponses.Gated(entered, never.Task, request => SyntheticResponses.Interpretation(request.OperationId)),
        ]);
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();
        using var cancellation = new CancellationTokenSource();

        var pending = bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant, cancellationToken: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cancellation.CancelAsync();
        var outcome = await pending.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(BridgeOutcomeKind.Cancelled, outcome.Kind);
        Assert.Equal(1, mock.CallCount);
        Assert.False(bridge.NapStatus.IsNapping);
    }

    [Fact]
    public async Task ConcurrentRequest_IsBusyImmediatelyAndItsSheetIsReleased()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mock = new MockSemanticProvider([
            SyntheticResponses.Gated(entered, release.Task, request => SyntheticResponses.Interpretation(request.OperationId)),
        ]);
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        var first = bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var secondSheet = ApiTestHarness.Sheet(grant);
        var second = await bridge.InterpretAttentionSheetAsync(secondSheet, grant);
        release.SetResult();

        Assert.Equal(BridgeOutcomeKind.Busy, second.Kind);
        Assert.Equal(0, secondSheet.Length);
        Assert.Equal(BridgeOutcomeKind.Interpreted, (await first.WaitAsync(TimeSpan.FromSeconds(10))).Kind);
        Assert.Equal(1, mock.CallCount);
    }

    [Fact]
    public async Task Disposal_AbandonsANonCooperativeProviderPromptly()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource();
        var mock = new MockSemanticProvider([
            SyntheticResponses.Gated(
                entered,
                never.Task,
                request => SyntheticResponses.Interpretation(request.OperationId),
                honorCancellation: false),
        ]);
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        var pending = bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await bridge.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(BridgeOutcomeKind.Cancelled, (await pending.WaitAsync(TimeSpan.FromSeconds(10))).Kind);
        Assert.Equal(
            BridgeOutcomeKind.Disposed,
            (await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant)).Kind);
    }

    [Fact]
    public async Task ThrowingOutputHandler_IsContainedAndDiagnosedWithoutItsMessage()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var bridge = harness.OpenBridge(new MockSemanticProvider());
        bridge.InterpretationProduced += (_, _) => throw new InvalidOperationException("handler-detail-text");
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.Contains(harness.Diagnostics.Lines, line => line.Contains("interpretation-handler-fault", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Diagnostics.Lines, line => line.Contains("handler-detail-text", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RequestBody_CarriesNoRemoteConversationOrCapsuleState()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider();
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        using var document = System.Text.Json.JsonDocument.Parse(Assert.Single(mock.Requests).RequestJson);
        Assert.Equal(
            ["attempt", "operationId", "operationKind", "resumePacket", "schemaVersion", "sheet"],
            document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                "basisSequence", "builtAtUtc", "items", "packetId",
                "sessionReference", "subjects", "truncated",
            ],
            document.RootElement.GetProperty("resumePacket").EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
    }
}
