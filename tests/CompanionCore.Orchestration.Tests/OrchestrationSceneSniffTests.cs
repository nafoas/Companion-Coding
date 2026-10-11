using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// VIS-04 — the scene reflex: a different scene perks Prince's ears at once and, when it is
/// a full shift into a scene he has not just seen, brings the next Glance forward. Scene
/// evidence never enters Bnuy Mode on its own, and a scene he keeps returning to habituates.
/// </summary>
public sealed class OrchestrationSceneSniffTests
{
    private static OrchestratorOptions Glancing => new() { Glance = true };

    private static MockSemanticProvider.MockStep Glance(int nextLookSeconds = 30) =>
        MockSemanticProvider.Respond(request => MockSemanticProvider.GlanceResponseJson(
            request, 0.3, 0.8, "fullContext", "synthetic scene", false, nextLookSeconds));

    [Fact]
    public async Task ASceneShift_PerksHisEars_AndGlancesAtOnce()
    {
        await using var harness = await CreateAsync(Glancing);
        await harness.AuthorizeAsync();
        harness.Provider.EnqueueGlance(Glance(nextLookSeconds: 30));
        await harness.SheetAsync(changeScore: 0.05);
        harness.Time.Advance(TimeSpan.FromSeconds(2));
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(1, harness.Provider.GlanceCount);

        // A fight breaks out: his ears perk on the scene's own thread …
        harness.Worker.EmitSceneSniff(harness.Time.GetUtcNow(), novelty: 0.45, label: "combat");
        Assert.Contains(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Attention
            && notice.Attention!.Kind == AttentionIntentKind.Observing
            && notice.Attention.TopicKey == "scene:combat");
        await harness.SettleAsync();

        // … and the very next sheet is glanced at.
        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(2, harness.Provider.GlanceCount);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task APartialChange_IsEvidence_ButAsksForNoLook()
    {
        await using var harness = await CreateAsync(Glancing);
        await harness.AuthorizeAsync();
        harness.Provider.EnqueueGlance(Glance(nextLookSeconds: 30));
        await harness.SheetAsync(changeScore: 0.05);
        harness.Time.Advance(TimeSpan.FromSeconds(2));

        // Weak evidence: forwarded to the engine, which holds it until something corroborates it.
        harness.Worker.EmitSceneSniff(harness.Time.GetUtcNow(), novelty: 0.12, label: "dialogue");
        await harness.SettleAsync();
        Assert.Equal(1, (await harness.Orchestrator.GetSnapshotAsync()).Sniff.SceneForwarded);

        await harness.SheetAsync(changeScore: 0.05);
        Assert.Equal(1, harness.Provider.GlanceCount);
    }

    [Fact]
    public async Task SceneEvidenceAlone_NeverEntersBnuyMode()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var at = harness.Time.GetUtcNow();
        for (var index = 0; index < 60; index++)
        {
            at += TimeSpan.FromMilliseconds(500);
            harness.Time.Advance(TimeSpan.FromMilliseconds(500));
            harness.Worker.EmitSceneSniff(at, novelty: 0.9, label: SceneVocabulary.Keys[index % SceneVocabulary.Keys.Count]);
        }

        await harness.SettleAsync();
        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.NotEqual(AttentionState.HighAttention, snapshot.Attention!.State);
        Assert.True(snapshot.Attention.Score < new AttentionConfiguration().HighEnter);
        Assert.DoesNotContain(harness.Notices, notice => notice.Attention?.Kind == AttentionIntentKind.HighAttentionStarted);
        Assert.Equal((60L, 60L), (snapshot.Sniff.SceneReceived, snapshot.Sniff.SceneForwarded));
    }

    [Fact]
    public void AFamiliarScene_StopsAskingForGlances_AndRecovers()
    {
        var reflex = new SceneReflex(new AttentionConfiguration());
        var at = DateTimeOffset.UnixEpoch.AddDays(20_000);
        var menu = SceneVocabulary.Keys.ToList().IndexOf("menu");
        var glances = 0;
        for (var index = 0; index < 10; index++)
        {
            var (interest, glanceNow) = reflex.Evaluate(Scene(at.AddSeconds(5 * index), 0.5, menu));
            glances += glanceNow ? 1 : 0;
            Assert.Equal("scene:menu", interest!.TopicKey);
            Assert.Equal(AttentionRegionKind.FullContext, interest.RegionKind);
            Assert.Equal(new AttentionConfiguration().HighEnter - 1, interest.ScoreCeiling);
            Assert.Equal(SceneReflex.EvidenceSource, interest.EvidenceSource);
        }

        // The menu opened ten times in under a minute: only the first few times asked for a look.
        Assert.InRange(glances, 1, 3);

        // A different kind of scene is fresh.
        Assert.True(reflex.Evaluate(Scene(at.AddSeconds(50), 0.5, SceneVocabulary.Keys.ToList().IndexOf("combat"))).GlanceNow);

        // Minutes later, the menu is interesting again.
        Assert.True(reflex.Evaluate(Scene(at.AddMinutes(5), 0.5, menu)).GlanceNow);
    }

    [Fact]
    public void ChangesAreWeakerThanShifts_UnsureScenesHaveNoTopic_AndMalformedOnesAreIgnored()
    {
        var reflex = new SceneReflex(new AttentionConfiguration());
        var at = DateTimeOffset.UnixEpoch.AddDays(20_000);
        var (shift, shiftGlance) = reflex.Evaluate(Scene(at, 0.5, SceneVocabulary.Unsure));
        var (change, changeGlance) = new SceneReflex(new AttentionConfiguration()).Evaluate(Scene(at, 0.12, SceneVocabulary.Unsure));
        Assert.True(shiftGlance);
        Assert.False(changeGlance);
        Assert.Null(shift!.TopicKey);
        Assert.True(change!.Signals.Novelty < shift.Signals.Novelty);
        Assert.True(change.Signals.Confidence < shift.Signals.Confidence);
        Assert.True(change.Signals.Salience < shift.Signals.Salience);

        Assert.Null(reflex.Evaluate(Scene(at, 1.5, 0)).Interest);
        Assert.Equal(1, reflex.Suppressed);
    }

    [Fact]
    public async Task ScenesForAStaleGrant_AreIgnored()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        var stale = CaptureAuthorizationGrant.Issue(Guid.NewGuid(), grant.Generation, grant.Target);
        harness.Worker.EmitSceneSniff(harness.Time.GetUtcNow(), grant: stale);
        Assert.DoesNotContain(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Attention);
        Assert.Equal(0, (await harness.Orchestrator.GetSnapshotAsync()).Sniff.SceneReceived);
    }

    private static SceneSniffSignal Scene(DateTimeOffset at, double novelty, int label) => new()
    {
        TargetSessionId = Guid.Parse("8d3f8a51-0d0c-4f55-9c1b-2f1a3c4d5e6f"),
        Generation = 1,
        Target = new CaptureTargetIdentity(1, 2, "synthetic.exe", new string('a', 64)),
        SourceSequenceNumber = 1,
        SourceTimestamp = at,
        SourceWidth = 1920,
        SourceHeight = 1080,
        SourceAccountedBytes = 1920L * 1080 * 4,
        Novelty = novelty,
        Shift = novelty >= 0.2,
        Label = label,
        LabelConfidence = label == SceneVocabulary.Unsure ? 0 : 0.2,
    };
}
