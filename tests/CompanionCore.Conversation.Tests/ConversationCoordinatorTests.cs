using System.Text.Json;
using CompanionCore.Attention;
using static CompanionCore.Conversation.Tests.ConversationHarness;

namespace CompanionCore.Conversation.Tests;

public sealed class ConversationCoordinatorTests
{
    // ---- Priority combinations -------------------------------------------------------

    [Fact]
    public void GameConversation_ReplacesAnUnlockedInitiatedThread()
    {
        var harness = new ConversationHarness();
        var initiated = harness.StartInitiatedThread();

        var update = harness.Coordinator.StartGameConversation("game.boss-strategy", false, At(20));

        var settled = harness.Single(update, ConversationIntentKind.ThreadSettled);
        Assert.Equal(initiated.ThreadId, settled.ThreadId);
        Assert.Equal(SettleReason.Replaced, settled.SettleReason);
        Assert.Equal(ThreadKind.Game, update.Snapshot.Thread!.Kind);
        Assert.Equal("game.boss-strategy", update.Snapshot.Thread.TopicKey);
    }

    [Fact]
    public void InitiatedOpenings_NeverInterruptAGameConversation()
    {
        var harness = new ConversationHarness();
        harness.Coordinator.StartGameConversation("game.puzzle", false, At(1));
        harness.Coordinator.AddInitiatedSeed("interest.clouds", 1.0, false, At(2));

        for (var second = 15; second <= 300; second += 30)
        {
            harness.Record(harness.Coordinator.Tick(At(second)));
            harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Click), At(second));
        }

        Assert.DoesNotContain(harness.Intents, intent => intent.Kind == ConversationIntentKind.OfferOpening);
        Assert.Equal(ThreadKind.Game, harness.Coordinator.Current.Thread!.Kind);
    }

    [Fact]
    public void ExplicitGameConversation_ReplacesAnUnlockedGameThread()
    {
        var harness = new ConversationHarness();
        var first = harness.Coordinator.StartGameConversation("game.first", false, At(1)).Snapshot.Thread!;

        var update = harness.Coordinator.StartGameConversation("game.second", false, At(2));

        Assert.Equal(first.ThreadId, harness.Single(update, ConversationIntentKind.ThreadSettled).ThreadId);
        Assert.Equal("game.second", update.Snapshot.Thread!.TopicKey);
    }

    [Fact]
    public void Lock_BlocksEveryReplacement_AndAmbientPromotion()
    {
        var harness = new ConversationHarness();
        var initiated = harness.StartInitiatedThread();
        harness.Coordinator.SetLock(true, At(20));
        harness.Coordinator.ObserveAttention(AttentionState.HighAttention, At(21));
        var ambient = harness.Single(harness.Coordinator.OnSemanticScan(new GameObservation("game.dragon", 0.9), At(22)), ConversationIntentKind.AmbientExpression);

        var start = harness.Coordinator.StartGameConversation("game.other", false, At(23));
        var promote = harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive, ambient.ReferenceId), At(24));

        Assert.Equal(ConversationRefusal.Locked, start.Refusal);
        Assert.Equal(ConversationRefusal.Locked, promote.Refusal);
        Assert.Equal(initiated.ThreadId, promote.Snapshot.Thread!.ThreadId);
        Assert.True(promote.Snapshot.Thread.Locked);
    }

    [Fact]
    public void AmbientPromotion_RespectsPriority_ReplacingAnUnlockedThread()
    {
        var harness = new ConversationHarness();
        var initiated = harness.StartInitiatedThread();
        harness.Coordinator.ObserveAttention(AttentionState.HighAttention, At(20));
        var ambient = harness.Single(harness.Coordinator.OnSemanticScan(new GameObservation("game.dragon", 0.9), At(21)), ConversationIntentKind.AmbientExpression);
        Assert.Equal(initiated.ThreadId, harness.Coordinator.Current.Thread!.ThreadId);

        var promote = harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive, ambient.ReferenceId), At(22));

        Assert.Equal(SettleReason.Replaced, harness.Single(promote, ConversationIntentKind.ThreadSettled).SettleReason);
        Assert.Equal(ThreadKind.Game, promote.Snapshot.Thread!.Kind);
        Assert.Equal("game.dragon", promote.Snapshot.Thread.TopicKey);
    }

    // ---- Ambient expression ----------------------------------------------------------

    [Theory]
    [InlineData(false, AmbientKind.Commentary)]
    [InlineData(true, AmbientKind.PepTalk)]
    public void AmbientCommentaryAndPepTalks_NeverCreateAThreadWithoutExplicitInteraction(bool struggling, AmbientKind expected)
    {
        var harness = new ConversationHarness();
        harness.Coordinator.ObserveAttention(AttentionState.HighAttention, At(0));

        for (var second = 1; second <= 20; second++)
        {
            harness.Record(harness.Coordinator.OnSemanticScan(new GameObservation("game.fight", 0.9, PlayerStruggling: struggling), At(second)));
            harness.Record(harness.Coordinator.Tick(At(second)));
        }

        harness.Record(harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Click), At(21)));
        harness.Record(harness.Coordinator.ObserveAttention(AttentionState.Noticing, At(400)));

        Assert.Contains(harness.Intents, intent => intent.Kind == ConversationIntentKind.AmbientExpression && intent.Ambient == expected);
        Assert.DoesNotContain(harness.Intents, intent => intent.Kind == ConversationIntentKind.ThreadStarted);
        Assert.Null(harness.Coordinator.Current.Thread);
    }

    [Fact]
    public void AmbientPromotion_ExpiresAfterItsWindow()
    {
        var harness = new ConversationHarness();
        harness.Coordinator.ObserveAttention(AttentionState.HighAttention, At(0));
        var ambient = harness.Single(harness.Coordinator.OnSemanticScan(new GameObservation("game.fight", 0.9), At(1)), ConversationIntentKind.AmbientExpression);

        var late = harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive, ambient.ReferenceId), At(1 + 121));

        Assert.Equal(ConversationRefusal.UnknownReference, late.Refusal);
        Assert.Null(late.Snapshot.Thread);
    }

    // ---- Urgent interruption and resumption -------------------------------------------

    [Fact]
    public void UrgentEvent_CheckpointsAnUnlockedThread_AndLaterOffersTheSameThreadBack()
    {
        var harness = new ConversationHarness();
        var thread = harness.StartInitiatedThread();
        harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive), At(17));

        var urgent = harness.Coordinator.OnUrgent("threat.ambush", At(18));
        Assert.Equal(thread.ThreadId, harness.Single(urgent, ConversationIntentKind.HoldThought).ThreadId);
        Assert.Equal(ThreadStatus.Suspended, urgent.Snapshot.Thread!.Status);

        Assert.Empty(harness.Coordinator.ObserveAttention(AttentionState.HighAttention, At(19)).Intents);
        Assert.Empty(harness.Coordinator.ObserveAttention(AttentionState.Afterglow, At(60)).Intents);
        var cleared = harness.Coordinator.ObserveAttention(AttentionState.Noticing, At(120));
        var offer = harness.Single(cleared, ConversationIntentKind.ResumeOffered);
        Assert.Equal(thread.ThreadId, offer.ThreadId);
        Assert.False(offer.AskFirst);

        var resumed = harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive, offer.ReferenceId), At(121));
        Assert.Equal(thread.ThreadId, harness.Single(resumed, ConversationIntentKind.ThreadResumed).ThreadId);
        Assert.Equal(thread.ThreadId, resumed.Snapshot.Thread!.ThreadId);
        Assert.Equal(ThreadStatus.Active, resumed.Snapshot.Thread.Status);
        Assert.Equal(3, resumed.Snapshot.Thread.SubstantiveTurns);
    }

    [Fact]
    public void ResumeOffer_WaitsForTheResumeDelay()
    {
        var harness = new ConversationHarness();
        harness.StartInitiatedThread();
        harness.Coordinator.OnUrgent("threat.ambush", At(20));

        Assert.DoesNotContain(harness.Coordinator.Tick(At(49)).Intents, intent => intent.Kind == ConversationIntentKind.ResumeOffered);
        Assert.Single(harness.Coordinator.Tick(At(50)).Intents, intent => intent.Kind == ConversationIntentKind.ResumeOffered);
    }

    [Fact]
    public void UnansweredResume_SettlesASubstantiveThreadIntoAResumableSeed()
    {
        var harness = new ConversationHarness();
        var thread = harness.StartInitiatedThread();
        harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive), At(17));
        harness.Coordinator.OnUrgent("threat.ambush", At(18));
        harness.Coordinator.Tick(At(48));

        var expired = harness.Coordinator.Tick(At(48 + 60));
        var settled = harness.Single(expired, ConversationIntentKind.ThreadSettled);
        Assert.Equal(SettleReason.ResumeNotTaken, settled.SettleReason);
        Assert.DoesNotContain(expired.Intents, intent => intent.Kind == ConversationIntentKind.OfferOpening);
        var seed = Assert.Single(expired.Snapshot.InitiatedSeeds);
        Assert.Equal(SeedOrigin.SettledConversation, seed.Origin);
        Assert.Equal(1, seed.Presentations);
        Assert.Equal(thread.ThreadId, seed.ResumableThread!.ThreadId);
        Assert.DoesNotContain(expired.Snapshot.EngagementProfile, pair => pair.Value < 0);

        // Later the seed is offered again and resumes the very same thread.
        var tick = harness.Coordinator.Tick(At(48 + 60 + 30));
        var offer = harness.Single(tick, ConversationIntentKind.OfferOpening);
        var accepted = harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive, offer.ReferenceId), At(48 + 60 + 31));
        Assert.Equal(thread.ThreadId, harness.Single(accepted, ConversationIntentKind.ThreadResumed).ThreadId);
    }

    [Fact]
    public void UrgentEventWithNoThread_IsAnAmbientAlert()
    {
        var harness = new ConversationHarness();

        var update = harness.Coordinator.OnUrgent("threat.ambush", At(1));

        Assert.Equal(AmbientKind.UrgentAlert, harness.Single(update, ConversationIntentKind.AmbientExpression).Ambient);
        Assert.Null(update.Snapshot.Thread);
    }

    // ---- Lock survives escalation and restart -----------------------------------------

    [Fact]
    public void LockedConversation_SurvivesEscalationUrgencyIdleAndRestart()
    {
        var harness = new ConversationHarness();
        var thread = harness.StartInitiatedThread();
        harness.Coordinator.SetLock(true, At(20));

        harness.Coordinator.ObserveAttention(AttentionState.HighAttention, At(21));
        var urgent = harness.Coordinator.OnUrgent("threat.dragon", At(22));
        Assert.Equal(AmbientKind.UrgentAlert, harness.Single(urgent, ConversationIntentKind.AmbientExpression).Ambient);
        Assert.DoesNotContain(urgent.Intents, intent => intent.Kind == ConversationIntentKind.HoldThought);
        harness.Coordinator.ObserveAttention(AttentionState.Noticing, At(600));
        harness.Coordinator.Tick(At(3600));

        // Restart through a serialized checkpoint.
        var json = JsonSerializer.Serialize(harness.Coordinator.Checkpoint());
        var restored = ConversationCoordinator.Restore(JsonSerializer.Deserialize<ConversationCheckpoint>(json)!, new ScriptedChance());
        restored.Tick(At(7200));

        var current = restored.Current.Thread!;
        Assert.Equal(thread.ThreadId, current.ThreadId);
        Assert.True(current.Locked);
        Assert.Equal(ThreadStatus.Active, current.Status);
    }

    [Fact]
    public void Checkpoint_RoundTripsEveryPieceOfState()
    {
        var harness = new ConversationHarness();
        harness.StartInitiatedThread();
        harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive), At(17));
        harness.Coordinator.OnSemanticScan(new GameObservation("game.map", 0.8), At(18));
        harness.Coordinator.AddInitiatedSeed("interest.rain", 0.4, true, At(19));
        harness.Coordinator.OnUrgent("threat.ambush", At(20));

        var checkpoint = harness.Coordinator.Checkpoint();
        var json = JsonSerializer.Serialize(checkpoint);
        var restored = ConversationCoordinator.Restore(JsonSerializer.Deserialize<ConversationCheckpoint>(json)!, new ScriptedChance());

        Assert.Equal(json, JsonSerializer.Serialize(restored.Checkpoint()));
        var original = harness.Coordinator.Tick(At(60));
        var replay = restored.Tick(At(60));
        Assert.Equal(JsonSerializer.Serialize(original.Intents), JsonSerializer.Serialize(replay.Intents));
    }

    public static TheoryData<string> CorruptCheckpoints => new()
    {
        "version", "identity", "counter", "time", "attention", "duplicate-seed", "wrong-bank", "offer-without-seed", "resume-without-suspended", "negative-profile",
    };

    [Theory]
    [MemberData(nameof(CorruptCheckpoints))]
    public void CorruptCheckpoint_IsRejected(string scenario)
    {
        var harness = new ConversationHarness();
        harness.StartInitiatedThread();
        harness.Coordinator.OnSemanticScan(new GameObservation("game.map", 0.8), At(18));
        var good = harness.Coordinator.Checkpoint();
        var seed = good.GameSeeds[0];
        var bad = scenario switch
        {
            "version" => good with { Version = 99 },
            "identity" => good with { CoordinatorId = Guid.Empty },
            "counter" => good with { IdCounter = -1 },
            "time" => good with { Now = good.Start - TimeSpan.FromSeconds(1) },
            "attention" => good with { Attention = (AttentionState)99 },
            "duplicate-seed" => good with { GameSeeds = [seed, seed] },
            "wrong-bank" => good with { InitiatedSeeds = [seed] },
            "offer-without-seed" => good with { Offer = new PendingOffer(Guid.NewGuid(), OfferKind.Opening, Guid.NewGuid(), null, "x", At(99), false) },
            "resume-without-suspended" => good with { Offer = new PendingOffer(Guid.NewGuid(), OfferKind.Resume, null, Guid.NewGuid(), "x", At(99), false) },
            "negative-profile" => good with { Profile = new Dictionary<string, int> { ["topic"] = -1 } },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        Assert.ThrowsAny<ArgumentException>(() => ConversationCoordinator.Restore(bad, new ScriptedChance()));
    }

    // ---- Seeding qualification ------------------------------------------------------------

    [Theory]
    [InlineData(UserTurnKind.Click)]
    [InlineData(UserTurnKind.YesNo)]
    [InlineData(UserTurnKind.Acknowledgement)]
    public void TrivialEngagement_NeverProducesASeed(UserTurnKind kind)
    {
        var harness = new ConversationHarness();
        harness.Coordinator.StartGameConversation("game.trivia", false, At(1));
        for (var turn = 0; turn < 10; turn++)
        {
            harness.Coordinator.OnUserTurn(new UserTurn(kind), At(2 + turn));
        }

        var closed = harness.Coordinator.Close(finished: false, At(20));

        Assert.Empty(closed.Snapshot.GameSeeds);
        Assert.DoesNotContain(closed.Intents, intent => intent.Kind == ConversationIntentKind.SeedBanked);
        Assert.Empty(closed.Snapshot.EngagementProfile);
    }

    [Fact]
    public void SubstantiveUnfinishedEngagement_BecomesAResumableSeed_ButAFinishedOneDoesNot()
    {
        var harness = new ConversationHarness();
        var thread = harness.Coordinator.StartGameConversation("game.lore", false, At(1)).Snapshot.Thread!;
        harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive), At(2));
        harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive), At(3));

        var closed = harness.Coordinator.Close(finished: false, At(4));
        var seed = Assert.Single(closed.Snapshot.GameSeeds);
        Assert.Equal(thread.ThreadId, seed.ResumableThread!.ThreadId);
        Assert.Equal(2, closed.Snapshot.EngagementProfile["game.lore"]);

        var other = new ConversationHarness();
        other.Coordinator.StartGameConversation("game.lore", false, At(1));
        other.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive), At(2));
        other.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive), At(3));
        Assert.Empty(other.Coordinator.Close(finished: true, At(4)).Snapshot.GameSeeds);
    }

    [Fact]
    public void IdleUnlockedThread_SettlesWhenTheMomentPasses()
    {
        var harness = new ConversationHarness();
        harness.Coordinator.StartGameConversation("game.chat", false, At(1));

        Assert.NotNull(harness.Coordinator.Tick(At(1 + 299)).Snapshot.Thread);
        var idle = harness.Coordinator.Tick(At(1 + 300));

        Assert.Equal(SettleReason.Idle, harness.Single(idle, ConversationIntentKind.ThreadSettled).SettleReason);
        Assert.Null(idle.Snapshot.Thread);
    }

    // ---- Neutral non-response -------------------------------------------------------------

    [Fact]
    public void ThreeNeutralPresentations_RetireASeed_WithoutAnyPreferenceChange()
    {
        var harness = new ConversationHarness();
        harness.Coordinator.AddInitiatedSeed("interest.stars", 0.9, false, At(1));
        harness.Coordinator.AddInitiatedSeed("interest.rocks", 0.3, false, At(1));
        var otherBefore = harness.Coordinator.Current.InitiatedSeeds.Single(seed => seed.TopicKey == "interest.rocks");

        var offers = 0;
        var second = 15.0;
        while (harness.Coordinator.Current.InitiatedSeeds.Any(seed => seed.TopicKey == "interest.stars"))
        {
            Assert.True(offers < 10, "The seed must leave the pool after its presentation limit.");
            var tick = harness.Record(harness.Coordinator.Tick(At(second)));
            if (tick.Intents.Any(intent => intent.Kind == ConversationIntentKind.OfferOpening && intent.TopicKey == "interest.stars"))
            {
                offers++;
            }

            second += 30;
        }

        Assert.Equal(3, offers);
        var retired = Assert.Single(harness.Intents, intent => intent.Kind == ConversationIntentKind.SeedRetired);
        Assert.Equal(SeedRetirement.PresentedWithoutResponse, retired.Retirement);
        Assert.Empty(harness.Coordinator.Current.EngagementProfile);
        var otherAfter = harness.Coordinator.Current.InitiatedSeeds.Single(seed => seed.TopicKey == "interest.rocks");
        Assert.Equal(otherBefore.Value, otherAfter.Value);
        Assert.InRange(otherAfter.Presentations, 0, 1);
    }

    // ---- Follow-up saturation, brain farts, sensitivity ---------------------------------

    [Fact]
    public void FollowUps_SaturateAtTheirBound()
    {
        var harness = new ConversationHarness();
        harness.Coordinator.StartGameConversation("game.mystery", false, At(1));

        for (var index = 0; index < 3; index++)
        {
            Assert.Equal(ConversationRefusal.None, harness.Coordinator.RequestFollowUp(At(2 + index)).Refusal);
        }

        Assert.Equal(ConversationRefusal.FollowUpsSaturated, harness.Coordinator.RequestFollowUp(At(10)).Refusal);
        Assert.Equal(ConversationRefusal.FollowUpsSaturated, harness.Coordinator.RequestFollowUp(At(20)).Refusal);
    }

    [Fact]
    public void BrainFarts_OccurOnlyWhenRelaxed_AndNeverCreateSeeds()
    {
        var harness = new ConversationHarness();
        Assert.Equal(ConversationRefusal.NotAllowed, harness.Coordinator.TryBrainFart(At(1)).Refusal);

        harness.Coordinator.StartGameConversation("game.chat", false, At(2));
        var allowed = harness.Coordinator.TryBrainFart(At(3));
        Assert.Single(allowed.Intents, intent => intent.Kind == ConversationIntentKind.BrainFart);
        Assert.Equal(ConversationRefusal.NotAllowed, harness.Coordinator.TryBrainFart(At(4)).Refusal);

        var engaged = new ConversationHarness();
        engaged.Coordinator.StartGameConversation("game.chat", false, At(1));
        engaged.Coordinator.ObserveAttention(AttentionState.Engaged, At(2));
        Assert.Equal(ConversationRefusal.NotAllowed, engaged.Coordinator.TryBrainFart(At(3)).Refusal);
        engaged.Coordinator.ObserveAttention(AttentionState.Noticing, At(4));
        Assert.Equal(ConversationRefusal.None, engaged.Coordinator.TryBrainFart(At(5)).Refusal);

        var sensitive = new ConversationHarness();
        sensitive.Coordinator.StartGameConversation("game.loss", true, At(1));
        Assert.Equal(ConversationRefusal.NotAllowed, sensitive.Coordinator.TryBrainFart(At(2)).Refusal);

        var unlucky = new ConversationHarness(chance: new ScriptedChance(0.5));
        unlucky.Coordinator.StartGameConversation("game.chat", false, At(1));
        Assert.Equal(ConversationRefusal.NotAllowed, unlucky.Coordinator.TryBrainFart(At(2)).Refusal);
        Assert.Empty(allowed.Snapshot.GameSeeds);
    }

    [Fact]
    public void SensitiveSeedsAndThreads_AskFirst()
    {
        var harness = new ConversationHarness();
        harness.Coordinator.AddInitiatedSeed("interest.heavy", 0.9, true, At(1));

        var offer = harness.Single(harness.Coordinator.Tick(At(15)), ConversationIntentKind.OfferOpening);
        Assert.True(offer.AskFirst);
        harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive, offer.ReferenceId), At(16));
        harness.Coordinator.OnUrgent("threat", At(17));
        var resume = harness.Single(harness.Coordinator.Tick(At(47)), ConversationIntentKind.ResumeOffered);
        Assert.True(resume.AskFirst);
    }

    // ---- Clocks and the shared gate --------------------------------------------------------

    [Fact]
    public void InitiatedClock_ChecksEvery30SecondsWithItsOffset_AndCatchUpIsBounded()
    {
        var harness = new ConversationHarness(chance: new ScriptedChance(0.99));
        harness.Coordinator.AddInitiatedSeed("interest.clouds", 0.7, false, At(1));

        harness.Coordinator.Tick(At(14));
        Assert.Equal(0, harness.Chance.Draws);
        harness.Coordinator.Tick(At(15));
        Assert.Equal(1, harness.Chance.Draws);
        harness.Coordinator.Tick(At(44));
        Assert.Equal(1, harness.Chance.Draws);
        harness.Coordinator.Tick(At(45));
        Assert.Equal(2, harness.Chance.Draws);

        harness.Coordinator.Tick(At(45 + 3600));
        Assert.Equal(3, harness.Chance.Draws);
        harness.Coordinator.Tick(At(45 + 3601));
        Assert.Equal(3, harness.Chance.Draws);
    }

    [Fact]
    public void GameObservationClock_FollowsSemanticScansOnly()
    {
        var harness = new ConversationHarness(chance: new ScriptedChance(0.99));

        harness.Coordinator.OnSemanticScan(new GameObservation("game.map", 0.8), At(5));
        harness.Coordinator.OnSemanticScan(new GameObservation("game.map", 0.8), At(6));

        Assert.Equal(2, harness.Chance.Draws);
    }

    [Fact]
    public void GameContext_IsFavoredAtTheSharedGate()
    {
        var harness = new ConversationHarness();
        harness.Coordinator.AddInitiatedSeed("interest.clouds", 1.0, false, At(1));

        var scan = harness.Coordinator.OnSemanticScan(new GameObservation("game.chest", 0.6), At(15));
        var tick = harness.Coordinator.Tick(At(15));

        Assert.Equal(SeedBank.Game, harness.Single(scan, ConversationIntentKind.OfferOpening).Bank);
        Assert.DoesNotContain(tick.Intents, intent => intent.Kind == ConversationIntentKind.OfferOpening);
    }

    [Fact]
    public void SilentGameCheck_StillClaimsTheGateForItsInstant()
    {
        var harness = new ConversationHarness();
        harness.Coordinator.AddInitiatedSeed("interest.clouds", 1.0, false, At(1));

        // The game check has a seed, draws a failing chance, and stays silent...
        harness.Chance.Enqueue(0.99);
        var scan = harness.Coordinator.OnSemanticScan(new GameObservation("game.map", 0.6), At(15));
        var drawsAfterScan = harness.Chance.Draws;
        var sameInstant = harness.Coordinator.Tick(At(15));
        var nextCheck = harness.Coordinator.Tick(At(45));

        // ...but the initiated opening still waits for the next check.
        Assert.Equal(1, drawsAfterScan);
        Assert.DoesNotContain(scan.Intents, intent => intent.Kind == ConversationIntentKind.OfferOpening);
        Assert.DoesNotContain(sameInstant.Intents, intent => intent.Kind == ConversationIntentKind.OfferOpening);
        Assert.Equal(SeedBank.Initiated, harness.Single(nextCheck, ConversationIntentKind.OfferOpening).Bank);
    }

    [Theory]
    [InlineData(AttentionState.HighAttention)]
    [InlineData(AttentionState.Afterglow)]
    public void UnrelatedInitiatedOpenings_WaitThroughHighAttentionAndAfterglow(AttentionState state)
    {
        var harness = new ConversationHarness();
        harness.Coordinator.AddInitiatedSeed("interest.clouds", 1.0, false, At(1));
        harness.Coordinator.ObserveAttention(state, At(2));

        for (var second = 15; second <= 315; second += 30)
        {
            harness.Record(harness.Coordinator.Tick(At(second)));
        }

        Assert.DoesNotContain(harness.Intents, intent => intent.Kind == ConversationIntentKind.OfferOpening);
        harness.Coordinator.ObserveAttention(AttentionState.Noticing, At(320));
        Assert.Single(harness.Coordinator.Tick(At(345)).Intents, intent => intent.Kind == ConversationIntentKind.OfferOpening);
    }

    [Fact]
    public void Afterglow_GuaranteesAContextualOpening_RegardlessOfChance()
    {
        var harness = new ConversationHarness(chance: new ScriptedChance(0.999));
        harness.Coordinator.ObserveAttention(AttentionState.Afterglow, At(1));

        var update = harness.Coordinator.OnAfterglowOpening("encounter.boss", At(2));

        var offer = harness.Single(update, ConversationIntentKind.OfferOpening);
        Assert.Equal("encounter.boss", offer.TopicKey);
        Assert.Equal(SeedBank.Game, offer.Bank);
    }

    // ---- Banks --------------------------------------------------------------------------

    [Fact]
    public void Banks_AreBoundedDeduplicatedAndEvictLowerValueCandidates()
    {
        var harness = new ConversationHarness(new ConversationConfiguration { MaximumSeedsPerBank = 3 }, new ScriptedChance(0.99));
        harness.Coordinator.OnSemanticScan(new GameObservation("game.a", 0.6), At(1));
        harness.Coordinator.OnSemanticScan(new GameObservation("game.b", 0.7), At(2));
        harness.Coordinator.OnSemanticScan(new GameObservation("game.c", 0.8), At(3));
        harness.Coordinator.OnSemanticScan(new GameObservation("game.a", 0.65), At(4));

        var duplicate = harness.Coordinator.Current.GameSeeds;
        Assert.Equal(3, duplicate.Count);
        Assert.Equal(0.65, duplicate.Single(seed => seed.TopicKey == "game.a").Value);

        // A lower-value duplicate keeps the original and retires nothing.
        var lowerDuplicate = harness.Coordinator.OnSemanticScan(new GameObservation("game.c", 0.6), At(4.5));
        Assert.DoesNotContain(lowerDuplicate.Intents, intent => intent.Kind == ConversationIntentKind.SeedRetired);
        Assert.Equal(0.8, lowerDuplicate.Snapshot.GameSeeds.Single(seed => seed.TopicKey == "game.c").Value);
        Assert.Single(lowerDuplicate.Snapshot.GameSeeds, seed => seed.TopicKey == "game.c");

        var higher = harness.Coordinator.OnSemanticScan(new GameObservation("game.d", 0.9), At(5));
        Assert.Equal(SeedRetirement.Evicted, harness.Single(higher, ConversationIntentKind.SeedRetired).Retirement);
        Assert.DoesNotContain(higher.Snapshot.GameSeeds, seed => seed.TopicKey == "game.a");

        var lower = harness.Coordinator.OnSemanticScan(new GameObservation("game.e", 0.55), At(6));
        Assert.Equal(SeedRetirement.Rejected, harness.Single(lower, ConversationIntentKind.SeedRetired).Retirement);
        Assert.Equal(3, lower.Snapshot.GameSeeds.Count);

        var minor = harness.Coordinator.OnSemanticScan(new GameObservation("game.minor", 0.2), At(7));
        Assert.DoesNotContain(minor.Snapshot.GameSeeds, seed => seed.TopicKey == "game.minor");

        // Below the significance threshold an observation never enters even an empty bank.
        var empty = new ConversationHarness(chance: new ScriptedChance(0.99));
        Assert.Empty(empty.Coordinator.OnSemanticScan(new GameObservation("game.minor", 0.2), At(1)).Snapshot.GameSeeds);
    }

    // ---- Determinism and validation -------------------------------------------------------

    [Fact]
    public void IdenticalInputsAndChance_ProduceIdenticalOutputs()
    {
        static string Run()
        {
            var coordinator = new ConversationCoordinator(Identity, Start, new SeededExpressionChance(42));
            var log = new List<ConversationUpdate>();
            coordinator.AddInitiatedSeed("interest.a", 0.6, false, At(1));
            coordinator.AddInitiatedSeed("interest.b", 0.5, true, At(2));
            for (var second = 3; second < 900; second += 7)
            {
                log.Add(second % 3 == 0
                    ? coordinator.OnSemanticScan(new GameObservation($"game.{second % 5}", 0.4 + ((second % 6) / 10.0)), At(second))
                    : coordinator.Tick(At(second)));
                if (second == 300)
                {
                    log.Add(coordinator.ObserveAttention(AttentionState.HighAttention, At(second)));
                }

                if (second == 500)
                {
                    log.Add(coordinator.ObserveAttention(AttentionState.Noticing, At(second)));
                }
            }

            return JsonSerializer.Serialize(log.Select(update => update.Intents)) + JsonSerializer.Serialize(coordinator.Checkpoint());
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void InvalidInputsAndConfiguration_AreRejected()
    {
        var coordinator = new ConversationHarness().Coordinator;
        Assert.ThrowsAny<ArgumentException>(() => coordinator.AddInitiatedSeed(" ", 0.5, false, At(1)));
        Assert.ThrowsAny<ArgumentException>(() => coordinator.AddInitiatedSeed("t", double.NaN, false, At(1)));
        Assert.ThrowsAny<ArgumentException>(() => coordinator.OnSemanticScan(new GameObservation("t", 1.5), At(1)));
        Assert.ThrowsAny<ArgumentException>(() => coordinator.OnSemanticScan(new GameObservation(new string('t', 257), 0.5), At(1)));
        Assert.ThrowsAny<ArgumentException>(() => coordinator.ObserveAttention((AttentionState)42, At(1)));
        Assert.ThrowsAny<ArgumentException>(() => coordinator.OnUserTurn(new UserTurn((UserTurnKind)42), At(1)));
        Assert.ThrowsAny<ArgumentException>(() => coordinator.Tick(default));
        Assert.ThrowsAny<ArgumentException>(() => new ConversationCoordinator(Identity, Start, new ScriptedChance(), new ConversationConfiguration { InitiatedClockOffset = TimeSpan.FromSeconds(30) }));
        Assert.ThrowsAny<ArgumentException>(() => new ConversationCoordinator(Identity, Start, new ScriptedChance(), new ConversationConfiguration { GameChanceNoticing = 2 }));
        Assert.ThrowsAny<ArgumentException>(() => new ConversationCoordinator(Guid.Empty, Start, new ScriptedChance()));
    }

    [Fact]
    public void UserTurnWithoutAThread_IsRefusedHarmlessly()
    {
        var harness = new ConversationHarness();

        var update = harness.Coordinator.OnUserTurn(new UserTurn(UserTurnKind.Substantive), At(1));

        Assert.Equal(ConversationRefusal.NoActiveThread, update.Refusal);
        Assert.Empty(update.Snapshot.EngagementProfile);
    }
}
