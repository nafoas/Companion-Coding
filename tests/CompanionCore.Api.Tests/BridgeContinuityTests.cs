namespace CompanionCore.Api.Tests;

public sealed class BridgeContinuityTests
{
    private const string Subject = "synthetic.subject.continuity";
    private const string LocalRecollection = "LOCAL-RECOLLECTION-91c2";
    private const string RemoteOnlySummary = "REMOTE-ONLY-SUMMARY-7f3a";

    [Fact]
    public async Task FreshBridgeAndRemoteConversation_RecoverContinuitySolelyFromLocalState()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var first = new MockSemanticProvider([
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                summary: RemoteOnlySummary,
                proposals: [SyntheticResponses.Append(Subject, LocalRecollection)])),
        ]);
        var bridge = harness.OpenBridge(first);
        var grant = harness.Grant();
        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        // Within one bridge, the next request carries only local state too.
        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Contains(LocalRecollection, first.Requests[1].RequestJson);
        Assert.DoesNotContain(RemoteOnlySummary, first.Requests[1].RequestJson);

        // Restart: new repository handle, new bridge, brand-new remote conversation.
        await harness.CloseBridgesAsync();
        await harness.ReopenRepositoryAsync();
        var fresh = new MockSemanticProvider();
        var restarted = harness.OpenBridge(fresh);
        var newGrant = harness.Grant();

        var outcome = await restarted.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(newGrant), newGrant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        var request = Assert.Single(fresh.Requests);
        Assert.Contains(Subject, request.RequestJson);
        Assert.Contains(LocalRecollection, request.RequestJson);
        Assert.DoesNotContain(RemoteOnlySummary, request.RequestJson);
        Assert.DoesNotContain(first.Requests[0].OperationId.ToString("D"), request.RequestJson);
    }

    [Fact]
    public async Task NapState_SurvivesRestart_SoARestartCannotResumeHammering()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var bridge = harness.OpenBridge(new MockSemanticProvider([MockSemanticProvider.Reply(ProviderReply.Outage())]));
        var grant = harness.Grant();
        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        await harness.CloseBridgesAsync();

        var fresh = new MockSemanticProvider();
        var restarted = harness.OpenBridge(fresh);
        var notices = 0;
        restarted.NoticeRaised += (_, _) => notices++;
        var outcome = await restarted.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Napping, outcome.Kind);
        Assert.Equal(BraincaseNapReason.Outage, restarted.NapStatus.Reason);
        Assert.Equal(0, fresh.CallCount);
        Assert.Equal(0, notices);
    }

    [Fact]
    public async Task OperationInFlightAtACrash_IsMarkedInterruptedAndNeverResent()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var crashed = Guid.NewGuid();
        using (var journal = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc))
        {
            journal.Append(new BraincaseJournalEntry
            {
                At = ApiTestHarness.BaselineUtc,
                Type = BraincaseJournalEntryType.Started,
                Op = crashed,
            });
        }

        var mock = new MockSemanticProvider();
        var bridge = harness.OpenBridge(mock);

        var diagnostics = bridge.GetDiagnosticsSnapshot();
        Assert.Equal(1, diagnostics.OperationsStarted);
        Assert.Equal(1, diagnostics.OperationsInterrupted);
        Assert.Equal(0, mock.CallCount);

        var grant = harness.Grant();
        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.DoesNotContain(mock.Requests, request => request.OperationId == crashed);
    }

    [Fact]
    public async Task BridgeJournal_HoldsNoRecollectionSummaryImageOrCredentialText()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var bridge = harness.OpenBridge(new MockSemanticProvider([
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                summary: RemoteOnlySummary,
                proposals: [SyntheticResponses.Append(Subject, LocalRecollection)])),
        ]));
        var grant = harness.Grant();
        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        await harness.CloseBridgesAsync();

        var journal = await File.ReadAllTextAsync(harness.StateLocation.JournalPath);

        Assert.Contains(Subject, journal);
        Assert.DoesNotContain(LocalRecollection, journal);
        Assert.DoesNotContain(RemoteOnlySummary, journal);
        Assert.DoesNotContain("synthetic observation", journal);
    }
}
