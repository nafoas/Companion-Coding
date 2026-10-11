using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// VIS-03 — the text reflex: new words on screen perk Prince's ears at once and bring the
/// next Glance forward, never entering Bnuy Mode on their own; a ticking HUD habituates,
/// and vanished words are weaker evidence that asks for no look.
/// </summary>
public sealed class OrchestrationTextSniffTests
{
    private static OrchestratorOptions Glancing => new() { Glance = true };

    private static MockSemanticProvider.MockStep Glance(int nextLookSeconds = 30) =>
        MockSemanticProvider.Respond(request => MockSemanticProvider.GlanceResponseJson(
            request, 0.3, 0.8, "fullContext", "synthetic scene", false, nextLookSeconds));

    [Fact]
    public async Task NewDialogue_PerksHisEars_AndGlancesAtOnce()
    {
        await using var harness = await CreateAsync(Glancing);
        await harness.AuthorizeAsync();
        harness.Provider.EnqueueGlance(Glance(nextLookSeconds: 30));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(1, harness.Provider.GlanceCount);

        // Well inside the 30 s the last Glance asked for, a quiet sheet is not looked at.
        harness.Time.Advance(TimeSpan.FromSeconds(2));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(1, harness.Provider.GlanceCount);
        var before = (await harness.Orchestrator.GetSnapshotAsync()).Attention!.Score;

        // A new dialogue line: his ears perk on the text's own thread …
        harness.Worker.EmitTextSniff(harness.Time.GetUtcNow(), lines: 2);
        Assert.Contains(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Attention
            && notice.Attention!.Kind == AttentionIntentKind.Observing);
        await harness.SettleAsync();
        Assert.True((await harness.Orchestrator.GetSnapshotAsync()).Attention!.Score > before);

        // … and the very next sheet is glanced at.
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.GlanceCount);
        Assert.Equal(0, harness.Provider.InspectCount);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task TextAlone_NeverEntersBnuyMode()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var at = harness.Time.GetUtcNow();
        var kinds = new[] { AttentionRegionKind.LowerDialogueInventory, AttentionRegionKind.UpperLeft, AttentionRegionKind.UpperRight };
        for (var index = 0; index < 90; index++)
        {
            at += TimeSpan.FromMilliseconds(500);
            harness.Time.Advance(TimeSpan.FromMilliseconds(500));
            harness.Worker.EmitTextSniff(at, lines: 3, kind: kinds[index % 3]);
        }

        await harness.SettleAsync();
        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.NotEqual(AttentionState.HighAttention, snapshot.Attention!.State);
        Assert.True(snapshot.Attention.Score < new AttentionConfiguration().HighEnter);
        Assert.DoesNotContain(harness.Notices, notice => notice.Attention?.Kind == AttentionIntentKind.HighAttentionStarted);
        Assert.Equal(90, snapshot.Sniff.TextReceived);
        Assert.Equal(90, snapshot.Sniff.TextForwarded);
    }

    [Fact]
    public void ATickingHud_Habituates_AndStopsAskingForGlances()
    {
        var reflex = new TextReflex(new AttentionConfiguration());
        var at = DateTimeOffset.UnixEpoch.AddDays(20_000);
        var glances = 0;
        var firstNovelty = 0d;
        var lastNovelty = 0d;

        // A HUD clock in the upper right changes its digits every half second for a minute.
        for (var index = 0; index < 120; index++)
        {
            var (interest, glanceNow) = reflex.Evaluate(Text(at.AddMilliseconds(500 * index), AttentionRegionKind.UpperRight, lines: 1));
            glances += glanceNow ? 1 : 0;
            Assert.NotNull(interest);
            Assert.Equal("text:UpperRight", interest.EvidenceSource);
            Assert.Equal(new AttentionConfiguration().HighEnter - 1, interest.ScoreCeiling);
            Assert.Null(interest.TopicKey);
            firstNovelty = index == 0 ? interest.Signals.Novelty : firstNovelty;
            lastNovelty = interest.Signals.Novelty;
        }

        Assert.InRange(glances, 1, 3);
        Assert.True(lastNovelty < firstNovelty * 0.1, $"novelty {firstNovelty} → {lastNovelty}");

        // The dialogue band is its own region: a new line there is fresh and asks for a look.
        var (dialogue, glance) = reflex.Evaluate(Text(at.AddSeconds(60), AttentionRegionKind.LowerDialogueInventory, lines: 2));
        Assert.True(glance);
        Assert.True(dialogue!.Signals.Salience > 0.5);

        // Once the clock stops, the region recovers.
        var (_, recovered) = reflex.Evaluate(Text(at.AddMinutes(3), AttentionRegionKind.UpperRight, lines: 1));
        Assert.True(recovered);
    }

    [Fact]
    public void VanishedWords_AreWeakerEvidence_AndAskForNoLook()
    {
        var reflex = new TextReflex(new AttentionConfiguration());
        var at = DateTimeOffset.UnixEpoch.AddDays(20_000);
        var (appeared, appearedGlance) = reflex.Evaluate(Text(at, AttentionRegionKind.LowerDialogueInventory, lines: 2));
        var otherReflex = new TextReflex(new AttentionConfiguration());
        var (vanished, vanishedGlance) = otherReflex.Evaluate(Text(at, AttentionRegionKind.LowerDialogueInventory, lines: 0));
        Assert.True(appearedGlance);
        Assert.False(vanishedGlance);
        Assert.True(vanished!.Signals.Novelty < appeared!.Signals.Novelty);
        Assert.True(vanished.Signals.Salience < appeared.Signals.Salience);
        Assert.True(vanished.Signals.Confidence < appeared.Signals.Confidence);

        // An unchanged or malformed reading is no evidence at all.
        Assert.Null(reflex.Evaluate(Text(at.AddSeconds(1), AttentionRegionKind.LowerDialogueInventory, lines: 2) with { TextChanged = false }).Interest);
        Assert.Null(reflex.Evaluate(Text(at.AddSeconds(2), AttentionRegionKind.LowerDialogueInventory, lines: 999)).Interest);
        Assert.Equal(2, reflex.Suppressed);
    }

    [Fact]
    public async Task TextForAStaleGrant_IsIgnored()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        var stale = CaptureAuthorizationGrant.Issue(Guid.NewGuid(), grant.Generation, grant.Target);
        harness.Worker.EmitTextSniff(harness.Time.GetUtcNow(), grant: stale);
        Assert.DoesNotContain(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Attention);
        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Equal(0, snapshot.Sniff.TextReceived);
        Assert.Equal(0, snapshot.Sniff.TextForwarded);
    }

    private static TextSniffSignal Text(DateTimeOffset at, AttentionRegionKind kind, int lines) => new()
    {
        TargetSessionId = Guid.Parse("8d3f8a51-0d0c-4f55-9c1b-2f1a3c4d5e6f"),
        Generation = 1,
        Target = new CaptureTargetIdentity(1, 2, "synthetic.exe", new string('a', 64)),
        SourceSequenceNumber = 1,
        SourceTimestamp = at,
        SourceWidth = 1920,
        SourceHeight = 1080,
        SourceAccountedBytes = 1920L * 1080 * 4,
        RegionKind = kind,
        Region = new NormalizedRegion(0.05, 0.65, 0.90, 0.32),
        Lines = lines,
        Characters = lines * 20,
        TextChanged = true,
    };
}
