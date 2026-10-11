using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;
using CompanionCore.Conversation;
using CompanionCore.Watchbun;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// Stage 11 calibration: the numbers Boss and Prince agreed in the Design BunDex are the
/// running defaults, and the semantic cadence is enforced end to end.
/// </summary>
public sealed class OrchestrationCadenceTests
{
    [Fact]
    public async Task Noticing_LooksAboutEveryTwelveSeconds_AndReleasesSheetsInBetween()
    {
        await using var harness = await CreateAsync(new OrchestratorOptions());
        await harness.AuthorizeAsync();

        var first = await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(1, harness.Provider.CallCount);

        harness.Time.Advance(TimeSpan.FromSeconds(5));
        var early = await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(1, harness.Provider.CallCount);
        Assert.Equal(0, early.Length);

        harness.Time.Advance(TimeSpan.FromSeconds(7));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.CallCount);
        Assert.Equal(0, first.Length);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task AMeaningfulChange_TriggersAnEarlyLook_AtTheBoundedEventFloor()
    {
        await using var harness = await CreateAsync(new OrchestratorOptions());
        await harness.AuthorizeAsync();
        await harness.SheetAsync(changeScore: 0.05);

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.SheetAsync(changeScore: 0.9);
        Assert.Equal(1, harness.Provider.CallCount);

        harness.Time.Advance(TimeSpan.FromSeconds(2));
        await harness.SheetAsync(changeScore: 0.9);
        Assert.Equal(2, harness.Provider.CallCount);

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.CallCount);
    }

    [Fact]
    public async Task ANewTargetSession_LooksImmediately()
    {
        await using var harness = await CreateAsync(new OrchestratorOptions());
        await harness.AuthorizeAsync();
        await harness.SheetAsync(changeScore: 0.05);
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded));

        await harness.AuthorizeAsync(Candidate(processId: 5555, windowId: 0x5555));
        await harness.SheetAsync(changeScore: 0.05);

        Assert.Equal(2, harness.Provider.CallCount);
    }

    [Theory]
    [InlineData(AttentionState.Noticing, 0.0, 12)]
    [InlineData(AttentionState.Engaged, 0.0, 6)]
    [InlineData(AttentionState.Afterglow, 0.0, 6)]
    [InlineData(AttentionState.HighAttention, 0.0, 3)]
    [InlineData(AttentionState.Noticing, 0.25, 3)]
    [InlineData(AttentionState.Engaged, 0.9, 3)]
    [InlineData(AttentionState.Noticing, double.NaN, 12)]
    public void TheCadence_MapsEachAttentionStateToItsAgreedInterval(AttentionState state, double change, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), new SemanticCadence().For(state, change));

    [Fact]
    public void WithoutAnAttentionEngine_TheCadenceIsNoticing() =>
        Assert.Equal(TimeSpan.FromSeconds(12), new SemanticCadence().For(null, 0));

    [Fact]
    public void ACadenceMustStayOrderedAndBounded()
    {
        new SemanticCadence().Validate();
        SemanticCadence.Unthrottled.Validate();
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticCadence { EventFloor = TimeSpan.FromSeconds(10) }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticCadence { Engaged = TimeSpan.FromSeconds(20) }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticCadence { Noticing = TimeSpan.FromMinutes(6) }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticCadence { TriggerChange = 1.5 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrchestratorOptions { SemanticCadence = new SemanticCadence { EventFloor = TimeSpan.FromSeconds(-1) } }.Validate());
    }

    /// <summary>Every number Boss explicitly agreed in the Design BunDex, pinned as the running default.</summary>
    [Fact]
    public void AgreedNumbers_AreTheRunningDefaults()
    {
        var cadence = new SemanticCadence();
        Assert.InRange(cadence.Noticing, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));
        Assert.InRange(cadence.Engaged, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8));
        Assert.InRange(cadence.EventFloor, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4));

        // "BICs have a separate 30 second timer, offset from the normal one."
        var conversation = new ConversationConfiguration();
        Assert.Equal(TimeSpan.FromSeconds(30), conversation.InitiatedClockInterval);
        Assert.True(conversation.InitiatedClockOffset > TimeSpan.Zero && conversation.InitiatedClockOffset < conversation.InitiatedClockInterval);

        // "After an hour of nothing, the Watchbun check … after another hour … consolidation."
        var watchbun = new WatchbunConfiguration();
        Assert.Equal(TimeSpan.FromHours(1), watchbun.QuietThreshold);
        Assert.Equal(TimeSpan.FromHours(1), watchbun.SecondQuietThreshold);

        // Provisional memory protection: a 64 MB ring and at most three full-resolution frames.
        Assert.Equal(64L * 1024 * 1024, CaptureWorkerMetrics.ScreenshotBudgetBytes);
        Assert.Equal(3, CaptureWorkerMetrics.MaximumSourceFrames);
    }
}
