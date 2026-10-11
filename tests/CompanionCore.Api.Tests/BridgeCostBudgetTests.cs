using System.Text.Json.Nodes;

namespace CompanionCore.Api.Tests;

/// <summary>
/// LIVE-02: the Bun Budget in dollars per (UTC) day. Input and output usage both count;
/// reaching the limit puts the Braincase to sleep until the next day without a call.
/// </summary>
public sealed class BridgeCostBudgetTests
{
    // $0.01 per day at $4 / $20 per million input / output units.
    private static readonly DailyCostBudget Budget = new(0.01m, 4m, 20m);

    private static MockSemanticProvider Spending(long input, long output, int replies)
    {
        var mock = new MockSemanticProvider();
        for (var index = 0; index < replies; index++)
        {
            mock.Enqueue(MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                usage: new JsonObject { ["inputUnits"] = input, ["outputUnits"] = output })));
        }

        return mock;
    }

    [Fact]
    public void Cost_CountsInputAndOutputAtTheirPrices()
    {
        Assert.Equal(0.004m, Budget.CostOf(1000, 0));
        Assert.Equal(0.008m, Budget.CostOf(0, 400));
        Assert.Equal(0.012m, Budget.CostOf(1000, 400));
    }

    [Fact]
    public async Task OutputSpending_ReachesTheLimit_AndTheNextLookSleepsUntilTomorrowWithoutACall()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        // Input alone ($0.004) stays under $0.01; with output ($0.008) the day is spent.
        var mock = Spending(input: 1000, output: 400, replies: 2);
        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { DailyCostBudget = Budget });
        var grant = harness.Grant();

        var first = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        var second = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, first.Kind);
        Assert.Equal(BraincaseNapReason.LocalBudget, second.NapReason);
        Assert.Equal(1, mock.CallCount);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero), bridge.NapStatus.Until);
    }

    [Fact]
    public async Task SpendingUnderTheLimit_KeepsLooking_AndANewDayStartsFresh()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = Spending(input: 200, output: 100, replies: 4);
        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { DailyCostBudget = new DailyCostBudget(0.006m, 4m, 20m) });
        var grant = harness.Grant();

        // $0.0028 per look: two fit in $0.006, a third would not.
        Assert.Equal(BridgeOutcomeKind.Interpreted, (await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant)).Kind);
        Assert.Equal(BridgeOutcomeKind.Interpreted, (await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant)).Kind);
        Assert.Equal(BraincaseNapReason.LocalBudget, (await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant)).NapReason);

        // A new day starts with nothing spent: two full looks fit again.
        harness.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(BridgeOutcomeKind.Interpreted, (await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant)).Kind);
        Assert.Equal(BridgeOutcomeKind.Interpreted, (await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant)).Kind);
        Assert.Equal(4, mock.CallCount);
    }

    [Fact]
    public async Task TheDaysSpending_SurvivesARestart()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var options = ApiTestHarness.FastOptions with { DailyCostBudget = Budget };
        var grant = harness.Grant();
        var before = harness.OpenBridge(Spending(input: 1000, output: 400, replies: 1), options);
        Assert.Equal(BridgeOutcomeKind.Interpreted, (await before.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant)).Kind);
        await harness.CloseBridgesAsync();

        var mock = Spending(input: 1, output: 1, replies: 1);
        var after = harness.OpenBridge(mock, options);
        var outcome = await after.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BraincaseNapReason.LocalBudget, outcome.NapReason);
        Assert.Equal(0, mock.CallCount);
    }

    [Theory]
    [InlineData(0, 4, 20)]
    [InlineData(-1, 4, 20)]
    [InlineData(5, -1, 20)]
    [InlineData(5, 0, 0)]
    public async Task InvalidBudgets_AreRefusedAtOpen(double limit, double input, double output)
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        Assert.Throws<ArgumentOutOfRangeException>(() => harness.OpenBridge(
            new MockSemanticProvider(),
            new BridgeOptions { DailyCostBudget = new DailyCostBudget((decimal)limit, (decimal)input, (decimal)output) }));
    }
}
