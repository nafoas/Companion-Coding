using CompanionCore.Api;
using static CompanionCore.Sightings.Tests.SightingHarness;

namespace CompanionCore.Sightings.Tests;

/// <summary>
/// M3b. Boss, 2026-10-13: "Trust should never go down — only up... it should be unconditional."
/// Trust in Da Boss only grows; fear only eases; neither ever quiets real danger.
/// </summary>
public sealed class BraveryTests
{
    private const string Game = "Blocky World";

    [Fact]
    public void InAnyHistoryAtAll_TrustNeverGoesDown_AndFearNeverGoesUp()
    {
        var random = new Random(20261013);
        for (var history = 0; history < 200; history++)
        {
            using var harness = new SightingHarness();
            var store = harness.Open();
            var at = T0;
            var threats = new[] { "zombie", "skeleton", "creeper" };
            var trust = threats.ToDictionary(threat => threat, _ => 0.0);
            var fear = threats.ToDictionary(threat => threat, _ => 1.0);
            for (var step = 0; step < 60; step++)
            {
                var threat = threats[random.Next(threats.Length)];
                at = at.Add(random.Next(4) == 0 ? TimeSpan.FromDays(random.Next(1, 90)) : TimeSpan.FromSeconds(random.Next(1, 120)));
                switch (random.Next(6))
                {
                    case 0:
                        store.Encounter(Game, null, threat, (EncounterOutcome)random.Next(4), at);
                        break;
                    case 1:
                        store.See(Seen(threat, at, game: Game, danger: true));
                        break;
                    case 2:
                        store.See(Seen(threat, at, game: Game));
                        break;
                    case 3:
                        store.Encounter(Game, null, threat, EncounterOutcome.BossLost, at);
                        break;
                    case 4:
                        store.Encounter(Game, null, threat, EncounterOutcome.BossHurt, at);
                        break;
                    default:
                        // Silence: Boss says nothing, nothing happens. There is no input for it at all.
                        break;
                }

                foreach (var name in threats)
                {
                    var nowTrust = store.Trust(Game, name);
                    var nowFear = store.Fear(Game, name);
                    Assert.True(nowTrust >= trust[name], $"History {history}, step {step}: trust in Boss went down for {name}.");
                    Assert.True(nowFear <= fear[name], $"History {history}, step {step}: fear went up for {name}.");
                    (trust[name], fear[name]) = (nowTrust, nowFear);
                }
            }

            // And a reopen changes nothing.
            var reopened = harness.Open();
            Assert.All(threats, name => Assert.Equal((trust[name], fear[name]), (reopened.Trust(Game, name), reopened.Fear(Game, name))));
        }
    }

    [Fact]
    public void WatchingBossWin_BuildsTrust_ThatBossHasGotItCovered()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        store.See(Seen("zombie", T0, game: Game, danger: true));
        Assert.Equal(["Prince feels scared of this."], store.FeelingsAbout(Game, "zombie").Select(feeling => feeling.Line));

        for (var win = 1; win <= 3; win++)
        {
            store.Encounter(Game, null, "zombie", EncounterOutcome.BossWon, T0.AddMinutes(win));
        }

        Assert.True(store.Trust(Game, "zombie") >= Bravery.BossHasGotItAt);
        Assert.Contains("Prince feels Boss has got this covered.", store.FeelingsAbout(Game, "zombie").Select(feeling => feeling.Line));
    }

    [Fact]
    public void LossesAndHurts_ChangeNothing_AboutHowHeFeelsAboutBoss()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        for (var win = 0; win < 5; win++)
        {
            store.Encounter(Game, null, "zombie", EncounterOutcome.BossWon, T0.AddMinutes(win));
        }

        var trust = store.Trust(Game, "zombie");
        var fear = store.Fear(Game, "zombie");
        for (var loss = 0; loss < 50; loss++)
        {
            store.Encounter(Game, null, "zombie", loss % 2 == 0 ? EncounterOutcome.BossLost : EncounterOutcome.BossHurt, T0.AddHours(1 + loss));
        }

        Assert.Equal((trust, fear), (store.Trust(Game, "zombie"), store.Fear(Game, "zombie")));
        Assert.Contains("Prince feels Boss has got this covered.", store.FeelingsAbout(Game, "zombie").Select(feeling => feeling.Line));
    }

    [Fact]
    public void FearEases_WithSafeExposure_InThatGameOnly()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        store.See(Seen("zombie", T0, game: Game, danger: true));
        var lines = new List<string> { store.FeelingsAbout(Game, "zombie")[0].Line };
        for (var look = 1; look <= 40; look++)
        {
            store.See(Seen("zombie", T0.AddMinutes(look), game: Game));
            lines.Add(store.FeelingsAbout(Game, "zombie")[0].Line);
        }

        Assert.Equal(
            ["Prince feels scared of this.", "Prince feels a little scared of this.", "Prince is still a little scared, but he can look now.", "Prince feels brave around this."],
            lines.Distinct());
        Assert.Equal(Bravery.FearFloor, store.Fear(Game, "zombie"), 9);

        // A whole new game's zombies are their own thing.
        store.See(Seen("zombie", T0.AddHours(2), game: "Hollow House", danger: true));
        Assert.Equal(1, store.Fear("Hollow House", "zombie"));
    }

    [Fact]
    public void WatchingBossWin_IsItselfASafeExposure_AndFightsAreNotMoreSightings()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        for (var win = 0; win < 4; win++)
        {
            store.Encounter(Game, null, "zombie", EncounterOutcome.BossWon, T0.AddMinutes(win));
        }

        Assert.True(store.Fear(Game, "zombie") < 0.5);
        Assert.Null(store.ImpressionOf(Game, "zombie"));
        Assert.Equal(4, store.Count);
    }

    [Fact]
    public void DangerRightNow_NeverEasesFear_AndTimeNeverBringsItBack()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        for (var look = 0; look < 20; look++)
        {
            store.See(Seen("zombie", T0.AddMinutes(look), game: Game, danger: true));
        }

        Assert.Equal(1, store.Fear(Game, "zombie"));
        store.See(Seen("zombie", T0.AddHours(1), game: Game));
        var eased = store.Fear(Game, "zombie");
        store.See(Seen("zombie", T0.AddDays(365), game: Game, danger: true));
        Assert.Equal(eased, store.Fear(Game, "zombie"));
    }

    [Fact]
    public void ATrustedFight_FeelsLessPressing_ButRealDangerNeverFadesBelowTheFloor()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        store.See(Seen("zombie", T0, game: Game, danger: true));
        var before = store.NoveltyOf(Seen("zombie", T0.AddSeconds(1), game: Game));
        for (var win = 1; win <= 10; win++)
        {
            store.Encounter(Game, null, "zombie", EncounterOutcome.BossWon, T0.AddSeconds(win));
        }

        var after = store.NoveltyOf(Seen("zombie", T0.AddSeconds(11), game: Game));
        Assert.True(after < before * 0.6);

        // Familiar and trusted, but dangerous right now: still fully alert-worthy.
        for (var look = 0; look < 100; look++)
        {
            store.See(Seen("zombie", T0.AddMinutes(1).AddSeconds(look), game: Game));
        }

        Assert.True(store.NoveltyOf(Seen("zombie", T0.AddMinutes(5), game: Game, danger: true)) >= Familiarity.DangerFloor);
    }

    [Fact]
    public void SomethingThatWasNeverAThreat_BringsNoFeelings_AndIsNotCalmed()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        store.See(Seen("butterfly", T0, game: Game, kind: ObservationKind.Creature));

        Assert.Empty(store.FeelingsAbout(Game, "butterfly"));
        Assert.Equal(store.NoveltyOf(Seen("moth", T0.AddDays(60), game: Game)), store.NoveltyOf(Seen("moth", T0.AddDays(60), game: Game)));
    }

    [Fact]
    public void CourageAndTrust_ComeBackWithTheRecordFromDaBunVault()
    {
        using var harness = new SightingHarness();
        var store = harness.Open();
        store.See(Seen("zombie", T0, game: Game, danger: true));
        for (var win = 1; win <= 4; win++)
        {
            store.Encounter(Game, null, "zombie", EncounterOutcome.BossWon, T0.AddMinutes(win));
        }

        var trust = store.Trust(Game, "zombie");
        var snapshot = SightingVault.SnapshotSegments(harness.Location);
        Directory.Delete(harness.Location.RootPath, recursive: true);
        SightingVault.RestoreIfMissing(harness.Location, snapshot);

        Assert.Equal(trust, harness.Open().Trust(Game, "zombie"));
    }
}
