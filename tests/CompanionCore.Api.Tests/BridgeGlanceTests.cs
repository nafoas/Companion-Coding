using System.Text.Json.Nodes;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Api.Tests;

/// <summary>
/// VIS-02 — the Glance lane: Prince's quick, peripheral look. It runs beside an Inspect,
/// never instead of one, can never propose a memory, and obeys the same privacy fence, nap,
/// and dollars-per-day Bun Budget.
/// </summary>
public sealed class BridgeGlanceTests
{
    [Fact]
    public async Task AGlance_RunsWhileAnInspectIsInFlight_AndASecondGlanceIsBusy()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var inspectEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inspectRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var glanceEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var glanceRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mock = new MockSemanticProvider();
        mock.Enqueue(SyntheticResponses.Gated(inspectEntered, inspectRelease.Task, request => SyntheticResponses.Interpretation(request.OperationId)));
        mock.EnqueueGlance(SyntheticResponses.Gated(glanceEntered, glanceRelease.Task, request => MockSemanticProvider.GlanceResponseJson(request, interest: 0.6, nextLookSeconds: 4)));
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        var inspect = bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        await inspectEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var glance = bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant);
        await glanceEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // One Glance lane: a second Glance while one is in flight is Busy, without a call.
        var busy = await bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Equal(BridgeOutcomeKind.Busy, busy.Kind);

        glanceRelease.SetResult();
        var glanced = await glance;
        Assert.Equal(BridgeOutcomeKind.Glanced, glanced.Kind);
        Assert.False(inspect.IsCompleted);
        var reading = Assert.IsType<GlanceReading>(glanced.Glance);
        Assert.Equal((0.6, AttentionRegionKind.FullContext, TimeSpan.FromSeconds(4)), (reading.Interest, reading.ChangedRegion, reading.NextLook));
        Assert.Equal(grant.TargetSessionId, reading.TargetSessionId);

        inspectRelease.SetResult();
        Assert.Equal(BridgeOutcomeKind.Interpreted, (await inspect).Kind);
        Assert.Equal((1, 1), (mock.InspectCount, mock.GlanceCount));
        Assert.Equal(1, bridge.GetGlanceDiagnostics().Busy);
        Assert.Equal(1, bridge.GetGlanceDiagnostics().Glanced);
    }

    [Fact]
    public async Task AGlance_CanNeverProposeAMemory()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider();
        mock.EnqueueGlance(MockSemanticProvider.Respond(request =>
        {
            var answer = JsonNode.Parse(MockSemanticProvider.GlanceResponseJson(request))!.AsObject();
            answer["memoryProposals"] = new JsonArray(SyntheticResponses.Append("glance.sneaky", "[neutral memory] a glance tried to remember."));
            return answer.ToJsonString();
        }));
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        var outcome = await bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.InvalidResponse, outcome.Kind);
        Assert.Equal(SemanticResponseInvalidReason.Malformed, outcome.InvalidReason);
        Assert.Null(outcome.Glance);
        Assert.Empty(await harness.RetrieveAsync("glance.sneaky"));
    }

    [Fact]
    public async Task AGlance_IsFencedByAPrivacyStop_AndRefusesAStaleGrant()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mock = new MockSemanticProvider();
        mock.EnqueueGlance(SyntheticResponses.Gated(entered, release.Task, request => MockSemanticProvider.GlanceResponseJson(request), honorCancellation: false));
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        var glance = bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        harness.Privacy.PauseAndRevoke();
        release.SetResult();
        var fenced = await glance;
        Assert.Equal(BridgeOutcomeKind.PrivacyFenced, fenced.Kind);
        Assert.Null(fenced.Glance);

        // The old generation is no longer current: nothing is sent for it.
        var refused = await bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Equal(BridgeOutcomeKind.NotAuthorized, refused.Kind);
        Assert.Equal(1, mock.GlanceCount);
    }

    [Fact]
    public async Task NothingIsSentWhileNapping_AndAGlanceOutageStartsTheSameNap()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider();
        mock.EnqueueGlance(MockSemanticProvider.Reply(ProviderReply.Outage()));
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        var outage = await bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Equal((BridgeOutcomeKind.Napping, BraincaseNapReason.Outage), (outage.Kind, outage.NapReason));
        Assert.True(bridge.NapStatus.IsNapping);

        var napping = await bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant);
        var inspect = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Equal(BridgeOutcomeKind.Napping, napping.Kind);
        Assert.Equal(BridgeOutcomeKind.Napping, inspect.Kind);
        Assert.Equal(1, mock.CallCount);
    }

    [Fact]
    public async Task ATimedOutGlance_IsMissed_AndAnUnavailableProviderIsReportedOnce()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider();
        mock.EnqueueGlance((_, cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => ProviderReply.Transient(), TaskScheduler.Default));
        mock.EnqueueGlance(MockSemanticProvider.Reply(ProviderReply.Unavailable(ProviderUnavailableReason.LiveCallsDisabled)));
        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { GlanceTimeout = TimeSpan.FromMilliseconds(50) });
        var notices = new List<BraincaseNotice>();
        bridge.NoticeRaised += (_, notice) => notices.Add(notice);
        var grant = harness.Grant();

        var missed = await bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant);
        var unavailable = await bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Missed, missed.Kind);
        Assert.Equal((BridgeOutcomeKind.Unavailable, ProviderUnavailableReason.LiveCallsDisabled), (unavailable.Kind, unavailable.UnavailableReason));
        Assert.Equal(BraincaseNoticeKind.Unavailable, Assert.Single(notices).Kind);
        Assert.False(bridge.NapStatus.IsNapping);
        Assert.Equal(1, bridge.GetGlanceDiagnostics().Missed);
    }

    [Fact]
    public async Task OneBunBudget_CountsBothLanesAtTheirOwnPrices()
    {
        await using var harness = await ApiTestHarness.CreateAsync();

        // $0.01 per day; the Inspect at $4 / $20 and the Glance at $0.10 / $0.50 per million.
        var budget = new DailyCostBudget(0.01m, 4m, 20m) { GlanceInputCostPerMillionUnits = 0.10m, GlanceOutputCostPerMillionUnits = 0.50m };
        Assert.Equal(0.0001m, budget.CostOf(0, 0, 1000, 0));
        Assert.Equal(0.0041m, budget.CostOf(1000, 0, 1000, 0));
        var mock = new MockSemanticProvider();
        var usage = new JsonObject { ["inputUnits"] = 1500, ["outputUnits"] = 100 };
        mock.Enqueue(MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(request.OperationId, usage: usage.DeepClone().AsObject())));
        for (var index = 0; index < 4; index++)
        {
            mock.EnqueueGlance(MockSemanticProvider.Respond(request =>
            {
                var answer = JsonNode.Parse(MockSemanticProvider.GlanceResponseJson(request))!.AsObject();
                answer["usage"] = new JsonObject { ["inputUnits"] = 20_000, ["outputUnits"] = 2_000 };
                return answer.ToJsonString();
            }));
        }

        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { DailyCostBudget = budget });
        var grant = harness.Grant();

        // The Inspect spends $0.008; each Glance $0.003 at Glance prices.
        Assert.Equal(BridgeOutcomeKind.Interpreted, (await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant)).Kind);
        Assert.Equal(BridgeOutcomeKind.Glanced, (await bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant)).Kind);

        // The day is now spent ($0.011): the next Glance sleeps until tomorrow without a call.
        var spent = await bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Equal(BraincaseNapReason.LocalBudget, spent.NapReason);
        Assert.Equal((1, 1), (mock.InspectCount, mock.GlanceCount));
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero), bridge.NapStatus.Until);
    }

    [Fact]
    public async Task GlanceUsage_SurvivesAReopen_AndAJournalWithoutGlancesKeepsItsOldShape()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var budget = new DailyCostBudget(0.01m, 4m, 20m) { GlanceInputCostPerMillionUnits = 0.10m, GlanceOutputCostPerMillionUnits = 0.50m };
        var mock = new MockSemanticProvider();
        mock.Enqueue(MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(request.OperationId, usage: new JsonObject { ["inputUnits"] = 100, ["outputUnits"] = 10 })));
        mock.EnqueueGlance(MockSemanticProvider.Respond(request =>
        {
            var answer = JsonNode.Parse(MockSemanticProvider.GlanceResponseJson(request))!.AsObject();
            answer["usage"] = new JsonObject { ["inputUnits"] = 60_000, ["outputUnits"] = 8_000 };
            return answer.ToJsonString();
        }));
        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { DailyCostBudget = budget });
        var grant = harness.Grant();

        // An Inspect-only journal is written exactly as before VIS-02: no Glance fields at all.
        Assert.Equal(BridgeOutcomeKind.Interpreted, (await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant)).Kind);
        await harness.CloseBridgesAsync();
        Assert.DoesNotContain("glance", File.ReadAllText(harness.StateLocation.JournalPath), StringComparison.OrdinalIgnoreCase);

        bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { DailyCostBudget = budget });
        Assert.Equal(BridgeOutcomeKind.Glanced, (await bridge.GlanceAsync(ApiTestHarness.Sheet(grant), grant)).Kind);
        await harness.CloseBridgesAsync();

        // $0.0006 of Inspect plus $0.010 of Glance is remembered across a reopen: the day is spent.
        var reopened = harness.OpenBridge(new MockSemanticProvider(), ApiTestHarness.FastOptions with { DailyCostBudget = budget });
        var spent = await reopened.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Equal(BraincaseNapReason.LocalBudget, spent.NapReason);
    }

    [Fact]
    public async Task GlanceUsage_SurvivesCompaction()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var day = DateOnly.FromDateTime(ApiTestHarness.BaselineUtc.UtcDateTime);
        using (var journal = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc, compactionThresholdBytes: 1024))
        {
            for (var index = 0; index < 40; index++)
            {
                journal.Append(new BraincaseJournalEntry
                {
                    At = ApiTestHarness.BaselineUtc,
                    Type = BraincaseJournalEntryType.Glanced,
                    Op = Guid.NewGuid(),
                    InputUnits = 100,
                    OutputUnits = 10,
                });
                journal.CompactIfNeeded(ApiTestHarness.BaselineUtc);
            }

            Assert.Equal((4000L, 400L, 40L), (journal.State.GlanceInputUnitsOn(day), journal.State.GlanceOutputUnitsOn(day), journal.State.Glances));
            Assert.Equal(0, journal.State.UsageInputUnitsOn(day));
        }

        using (var reopened = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc))
        {
            Assert.Equal((4000L, 400L, 40L), (reopened.State.GlanceInputUnitsOn(day), reopened.State.GlanceOutputUnitsOn(day), reopened.State.Glances));

            // A new day starts both lanes fresh.
            Assert.Equal(0, reopened.State.GlanceInputUnitsOn(day.AddDays(1)));
        }

        Assert.Contains("\"type\":\"snapshot\"", File.ReadAllText(harness.StateLocation.JournalPath), StringComparison.Ordinal);
    }

    [Fact]
    public void TheGlanceParser_IsStrict()
    {
        var operation = Guid.NewGuid();
        AttentionRegionKind[] regions = [AttentionRegionKind.FullContext, AttentionRegionKind.CenterEnvironment];
        string Glance(Action<JsonObject>? change = null)
        {
            var glance = new JsonObject
            {
                ["interest"] = 0.5,
                ["confidence"] = 0.9,
                ["changedRegion"] = "centerEnvironment",
                ["label"] = "a door opens",
                ["escalate"] = true,
                ["nextLookSeconds"] = 3,
            };
            change?.Invoke(glance);
            return new JsonObject { ["schemaVersion"] = SemanticSchema.Version, ["operationId"] = operation.ToString("D"), ["glance"] = glance }.ToJsonString();
        }

        SemanticResponseInvalidReason Reason(string json) =>
            GlanceResponseParser.TryParse(json, operation, regions, out _, out var reason) ? SemanticResponseInvalidReason.None : reason;

        Assert.True(GlanceResponseParser.TryParse(Glance(), operation, regions, out var parsed, out _));
        Assert.Equal((0.5, AttentionRegionKind.CenterEnvironment, true, TimeSpan.FromSeconds(3)), (parsed!.Interest, parsed.ChangedRegion, parsed.Escalate, parsed.NextLook));
        Assert.Equal(SemanticResponseInvalidReason.OutOfBounds, Reason(Glance(glance => glance["interest"] = 1.5)));
        Assert.Equal(SemanticResponseInvalidReason.OutOfBounds, Reason(Glance(glance => glance["nextLookSeconds"] = 0)));
        Assert.Equal(SemanticResponseInvalidReason.OutOfBounds, Reason(Glance(glance => glance["nextLookSeconds"] = 121)));
        Assert.Equal(SemanticResponseInvalidReason.OutOfBounds, Reason(Glance(glance => glance["label"] = "two\nlines")));
        Assert.Equal(SemanticResponseInvalidReason.OutOfBounds, Reason(Glance(glance => glance["label"] = new string('x', 65))));
        Assert.Equal(SemanticResponseInvalidReason.UnknownRegion, Reason(Glance(glance => glance["changedRegion"] = "upperLeft")));
        Assert.Equal(SemanticResponseInvalidReason.Malformed, Reason(Glance(glance => glance["summary"] = "extra")));
        Assert.Equal(SemanticResponseInvalidReason.Malformed, Reason(Glance(glance => glance.Remove("escalate"))));
        Assert.Equal(SemanticResponseInvalidReason.OperationMismatch, GlanceResponseParser.TryParse(Glance(), Guid.NewGuid(), regions, out _, out var mismatch) ? SemanticResponseInvalidReason.None : mismatch);
        Assert.Equal(SemanticResponseInvalidReason.TooLarge, Reason(new string(' ', GlanceSchema.MaximumResponseBytes + 1)));
    }
}
