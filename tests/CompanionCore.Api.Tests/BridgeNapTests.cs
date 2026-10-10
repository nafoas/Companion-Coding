namespace CompanionCore.Api.Tests;

public sealed class BridgeNapTests
{
    [Fact]
    public async Task Outage_CheckpointsReleasesBuffersNotifiesOnceAndStopsRepeatedRequests()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        await harness.SeedMemoryAsync("synthetic.subject.kept", "Kept synthetic recollection.");
        var keptBefore = await harness.RetrieveAsync("synthetic.subject.kept");
        var mock = new MockSemanticProvider([MockSemanticProvider.Reply(ProviderReply.Outage())]);
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();
        var sheet = ApiTestHarness.Sheet(grant);
        var notices = new List<(BraincaseNotice Notice, bool Checkpointed, int SheetLength)>();
        bridge.NoticeRaised += (_, notice) => notices.Add((notice, bridge.NapStatus.IsNapping, sheet.Length));

        var first = await bridge.InterpretAttentionSheetAsync(sheet, grant);

        Assert.Equal(BridgeOutcomeKind.Napping, first.Kind);
        Assert.Equal(BraincaseNapReason.Outage, first.NapReason);
        var announced = Assert.Single(notices);
        Assert.Equal(new BraincaseNotice(BraincaseNoticeKind.Naptime, BraincaseNapReason.Outage, ProviderUnavailableReason.None), announced.Notice);
        Assert.True(announced.Checkpointed);
        Assert.Equal(0, announced.SheetLength);
        Assert.Equal(ApiTestHarness.BaselineUtc + TimeSpan.FromMinutes(1), bridge.NapStatus.Until);

        var secondSheet = ApiTestHarness.Sheet(grant);
        var second = await bridge.InterpretAttentionSheetAsync(secondSheet, grant);

        Assert.Equal(BridgeOutcomeKind.Napping, second.Kind);
        Assert.Equal(1, mock.CallCount);
        Assert.Single(notices);
        Assert.Equal(0, secondSheet.Length);
        Assert.Equal(1, bridge.GetDiagnosticsSnapshot().RefusedWhileNapping);
        Assert.Equal(1, bridge.GetDiagnosticsSnapshot().NapEpisodes);
        Assert.Equal(
            keptBefore.Select(memory => memory.RecordChecksum),
            (await harness.RetrieveAsync("synthetic.subject.kept")).Select(memory => memory.RecordChecksum));
    }

    [Fact]
    public async Task OutageProbes_AreSingleAttemptExponentialCappedAndSilentUntilAwake()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider([MockSemanticProvider.Reply(ProviderReply.Outage())]);
        var bridge = harness.OpenBridge(mock);
        var notices = new List<BraincaseNotice>();
        bridge.NoticeRaised += (_, notice) => notices.Add(notice);
        var grant = harness.Grant();
        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        var expectedIntervals = new[] { 2, 4, 8, 16, 30, 30 };
        var calls = 1;
        var interval = TimeSpan.FromMinutes(1);
        foreach (var minutes in expectedIntervals)
        {
            // Just before the probe time: refused locally, no call.
            harness.Clock.Advance(interval - TimeSpan.FromSeconds(1));
            Assert.Equal(
                BridgeOutcomeKind.Napping,
                (await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant)).Kind);
            Assert.Equal(calls, mock.CallCount);

            // At the probe time: exactly one attempt, even for a transient failure.
            harness.Clock.Advance(TimeSpan.FromSeconds(1));
            mock.Enqueue(MockSemanticProvider.Reply(ProviderReply.Transient()));
            var probe = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
            calls++;
            Assert.Equal(BridgeOutcomeKind.Napping, probe.Kind);
            Assert.Equal(1, probe.Attempts);
            Assert.Equal(calls, mock.CallCount);
            interval = TimeSpan.FromMinutes(minutes);
            Assert.Equal(harness.Clock.GetUtcNow() + interval, bridge.NapStatus.Until);
        }

        Assert.Single(notices);

        harness.Clock.Advance(interval);
        var awake = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, awake.Kind);
        Assert.False(bridge.NapStatus.IsNapping);
        Assert.Equal(BraincaseNoticeKind.Awake, notices[^1].Kind);
        Assert.Equal(2, notices.Count);
        Assert.Equal(1, bridge.GetDiagnosticsSnapshot().NapEpisodes);
    }

    [Theory]
    [InlineData(600, 600)]
    [InlineData(1, 30)]
    [InlineData(172_800, 21_600)]
    [InlineData(-1, 300)]
    public async Task RateLimit_HonorsTheClampedRetryAfter(int retryAfterSeconds, int expectedSeconds)
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var reply = ProviderReply.RateLimited(retryAfterSeconds < 0 ? null : TimeSpan.FromSeconds(retryAfterSeconds));
        var mock = new MockSemanticProvider([MockSemanticProvider.Reply(reply)]);
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BraincaseNapReason.RateLimited, outcome.NapReason);
        Assert.Equal(1, outcome.Attempts);
        Assert.Equal(ApiTestHarness.BaselineUtc + TimeSpan.FromSeconds(expectedSeconds), bridge.NapStatus.Until);
        harness.Clock.Advance(TimeSpan.FromSeconds(expectedSeconds - 1));
        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Equal(1, mock.CallCount);
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Equal(2, mock.CallCount);
    }

    [Fact]
    public async Task ExhaustedRetries_BecomeANapWithOneNotice()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Reply(ProviderReply.Transient()),
            MockSemanticProvider.Reply(ProviderReply.Transient()),
            MockSemanticProvider.Reply(ProviderReply.Transient()),
        ]);
        var bridge = harness.OpenBridge(mock);
        var notices = new List<BraincaseNotice>();
        bridge.NoticeRaised += (_, notice) => notices.Add(notice);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Napping, outcome.Kind);
        Assert.Equal(BraincaseNapReason.RetriesExhausted, outcome.NapReason);
        Assert.Equal(3, outcome.Attempts);
        Assert.Equal(3, mock.CallCount);
        Assert.Equal(BraincaseNoticeKind.Naptime, Assert.Single(notices).Kind);
    }

    [Fact]
    public async Task LocalBudget_NapsUntilTheNextUtcDayWithoutContactingTheProvider()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider();
        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { DailyInputUnitBudget = 1 });
        var grant = harness.Grant();
        var sheet = ApiTestHarness.Sheet(grant);
        var notices = new List<BraincaseNotice>();
        var lengthsAtNotice = new List<int>();
        bridge.NoticeRaised += (_, notice) =>
        {
            notices.Add(notice);
            lengthsAtNotice.Add(sheet.Length);
        };

        var outcome = await bridge.InterpretAttentionSheetAsync(sheet, grant);

        Assert.Equal(BraincaseNapReason.LocalBudget, outcome.NapReason);
        Assert.Equal(0, mock.CallCount);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero), bridge.NapStatus.Until);
        Assert.Equal(BraincaseNapReason.LocalBudget, Assert.Single(notices).NapReason);
        Assert.Equal([0], lengthsAtNotice);
    }

    [Fact]
    public async Task UsageWithinBudget_IsCountedPerUtcDay()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider();
        var bridge = harness.OpenBridge(mock, ApiTestHarness.FastOptions with { DailyInputUnitBudget = 100_000 });
        var grant = harness.Grant();

        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        var today = bridge.GetDiagnosticsSnapshot().EstimatedInputUnitsToday;
        harness.Clock.Advance(TimeSpan.FromDays(1));

        Assert.True(today > 0);
        Assert.Equal(today, bridge.GetDiagnosticsSnapshot().EstimatedInputUnits);
        Assert.Equal(0, bridge.GetDiagnosticsSnapshot().EstimatedInputUnitsToday);
    }

    [Fact]
    public async Task UnavailableProvider_IsNeutralNotANap_AndAnnouncedOncePerReason()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var bridge = harness.OpenBridge(new RealSemanticProviderShell(harness.Credentials));
        var notices = new List<BraincaseNotice>();
        bridge.NoticeRaised += (_, notice) => notices.Add(notice);
        var grant = harness.Grant();

        var first = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        var second = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        harness.Credentials.Set(
            RealSemanticProviderShell.PrimaryCredentialName,
            ProtectedCredential.FromCharacters("synthetic-not-a-key"));
        var third = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Unavailable, first.Kind);
        Assert.Equal(ProviderUnavailableReason.CredentialsMissing, first.UnavailableReason);
        Assert.Equal(1, first.Attempts);
        Assert.Equal(ProviderUnavailableReason.CredentialsMissing, second.UnavailableReason);
        Assert.Equal(ProviderUnavailableReason.LiveCallsDisabled, third.UnavailableReason);
        Assert.False(bridge.NapStatus.IsNapping);
        Assert.Equal(
            [ProviderUnavailableReason.CredentialsMissing, ProviderUnavailableReason.LiveCallsDisabled],
            notices.Select(notice => notice.UnavailableReason));
        Assert.All(notices, notice => Assert.Equal(BraincaseNoticeKind.Unavailable, notice.Kind));
    }

    [Fact]
    public async Task ProbeThatFindsTheProviderUnavailable_ExtendsTheNapSilently()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Reply(ProviderReply.Outage()),
            MockSemanticProvider.Reply(ProviderReply.Unavailable(ProviderUnavailableReason.Disabled)),
        ]);
        var bridge = harness.OpenBridge(mock);
        var notices = new List<BraincaseNotice>();
        bridge.NoticeRaised += (_, notice) => notices.Add(notice);
        var grant = harness.Grant();
        await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        harness.Clock.Advance(TimeSpan.FromMinutes(1));

        var probe = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        var after = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Napping, probe.Kind);
        Assert.Equal(ProviderUnavailableReason.Disabled, probe.UnavailableReason);
        Assert.Equal(BridgeOutcomeKind.Napping, after.Kind);
        Assert.Equal(2, mock.CallCount);
        Assert.Equal(harness.Clock.GetUtcNow() + TimeSpan.FromMinutes(2), bridge.NapStatus.Until);
        Assert.Equal(
            [BraincaseNoticeKind.Naptime, BraincaseNoticeKind.Unavailable],
            notices.Select(notice => notice.Kind));
    }
}
