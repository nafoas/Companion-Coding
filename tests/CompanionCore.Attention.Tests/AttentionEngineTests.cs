using CompanionCore.Capture.Contracts;

namespace CompanionCore.Attention.Tests;

public sealed class AttentionEngineTests
{
    [Fact]
    public void WeakIsolatedMotion_DecaysWithoutEscalation()
    {
        var stream = new SyntheticStream();

        for (var second = 0; second < 60; second += 20)
        {
            var update = stream.At(second, SyntheticStream.WeakMotion());
            Assert.Equal(AttentionEventDisposition.HeldForCorroboration, update.Record!.Disposition);
            Assert.Equal(0, update.Record.Contribution);
        }

        stream.AdvanceTo(120);
        Assert.Equal(AttentionState.Noticing, stream.Engine.Current.State);
        Assert.Equal(0, stream.Engine.Current.Score);
        Assert.DoesNotContain(AttentionIntentKind.Investigating, stream.IntentKinds);
    }

    [Fact]
    public void CorroboratedEnemyEvidenceFromIndependentSources_RaisesAttention()
    {
        var stream = new SyntheticStream();

        var first = stream.At(0, SyntheticStream.EnemyEvidence("region:center"));
        var second = stream.At(2, SyntheticStream.EnemyEvidence("region:upper-left"));

        Assert.Equal(AttentionEventDisposition.HeldForCorroboration, first.Record!.Disposition);
        Assert.Equal(AttentionEventDisposition.Corroborated, second.Record!.Disposition);
        Assert.Equal(AttentionState.Engaged, stream.Engine.Current.State);
        var investigating = Assert.Single(stream.Updates.SelectMany(update => update.Intents), intent => intent.Kind == AttentionIntentKind.Investigating);
        Assert.Equal("threat.enemy-nearby", investigating.TopicKey);
        Assert.Equal([AttentionRegionKind.UpperLeft], investigating.Regions);
    }

    [Fact]
    public void SameSourceRepeats_DoNotCorroborate()
    {
        var stream = new SyntheticStream();

        for (var second = 0; second < 8; second += 2)
        {
            Assert.Equal(
                AttentionEventDisposition.HeldForCorroboration,
                stream.At(second, SyntheticStream.EnemyEvidence("region:center")).Record!.Disposition);
        }

        Assert.Equal(AttentionState.Noticing, stream.Engine.Current.State);
    }

    [Fact]
    public void EvidenceOutsideTheCorroborationWindow_DoesNotCorroborate()
    {
        var stream = new SyntheticStream();

        stream.At(0, SyntheticStream.EnemyEvidence("region:center"));
        var late = stream.At(11, SyntheticStream.EnemyEvidence("region:upper-left"));

        Assert.Equal(AttentionEventDisposition.HeldForCorroboration, late.Record!.Disposition);
        Assert.Equal(AttentionState.Noticing, stream.Engine.Current.State);
    }

    [Theory]
    [InlineData(DecisiveReason.Boss)]
    [InlineData(DecisiveReason.Miniboss)]
    [InlineData(DecisiveReason.LargeEnemyGroup)]
    [InlineData(DecisiveReason.MajorDeath)]
    [InlineData(DecisiveReason.Credits)]
    [InlineData(DecisiveReason.RareAchievement)]
    [InlineData(DecisiveReason.WatchTaskComplete)]
    [InlineData(DecisiveReason.ExplicitLookRequest)]
    [InlineData(DecisiveReason.ExceptionalCuriosity)]
    public void DecisiveEvent_EntersHighAttentionImmediately(DecisiveReason reason)
    {
        var stream = new SyntheticStream();

        var update = stream.At(0, SyntheticStream.Decisive(reason));

        Assert.Equal(AttentionState.HighAttention, update.Snapshot.State);
        Assert.Equal(AttentionState.Noticing, update.PreviousState);
        Assert.Contains(AttentionIntentKind.Urgent, update.Intents.Select(intent => intent.Kind));
        var started = Assert.Single(update.Intents, intent => intent.Kind == AttentionIntentKind.HighAttentionStarted);
        Assert.Equal([AttentionRegionKind.FullContext, AttentionRegionKind.CenterEnvironment], started.Regions);
        Assert.True(update.Snapshot.SuppressUnrelatedInitiatedConversations);
    }

    [Fact]
    public void DecisiveEvent_BypassesHabituationAndSafeFamiliarity()
    {
        var stream = new SyntheticStream();
        stream.Engine.SetLocationFamiliarity("town.safe", LocationFamiliarity.Safe);
        for (var index = 0; index < 20; index++)
        {
            stream.Engine.CorrectFalseAlarm(stream.Now);
        }

        var update = stream.At(1, SyntheticStream.Decisive() with { LocationKey = "town.safe" });

        Assert.Equal(AttentionState.HighAttention, update.Snapshot.State);
    }

    [Fact]
    public void LoadingScreenChanges_Deduplicate_AndANewTransitionAppliesAgain()
    {
        var stream = new SyntheticStream();

        var first = stream.At(0, SyntheticStream.Loading());
        var repeats = Enumerable.Range(1, 20)
            .Select(index => stream.At(index * 0.5, SyntheticStream.Loading()))
            .ToArray();

        Assert.Equal(AttentionEventDisposition.Applied, first.Record!.Disposition);
        Assert.True(first.Record.Contribution <= AttentionConfiguration.Default.TransitionContributionCap);
        Assert.All(repeats, update => Assert.Equal(AttentionEventDisposition.DuplicateTransition, update.Record!.Disposition));
        Assert.Equal(AttentionState.Noticing, stream.Engine.Current.State);

        var later = stream.At(45, SyntheticStream.Loading());
        var different = stream.At(46, SyntheticStream.Loading("transition.loading.2"));
        Assert.Equal(AttentionEventDisposition.Applied, later.Record!.Disposition);
        Assert.Equal(AttentionEventDisposition.Applied, different.Record!.Disposition);
    }

    [Fact]
    public void FamiliarHarmlessActivity_Habituates_WhileFamiliarUrgentDangerStillAlerts()
    {
        var stream = new SyntheticStream();
        var contributions = new List<double>();
        for (var index = 0; index < 30; index++)
        {
            // 30 s apart: score decays back between exposures, habituation does not recover.
            var update = stream.At(index * 30, SyntheticStream.Harmless());
            contributions.Add(update.Record!.Contribution);
        }

        Assert.True(contributions[0] > contributions[5]);
        Assert.True(contributions[^1] <= contributions[0] * AttentionConfiguration.Default.HabituationFloor + 1e-9 || contributions[^1] == 0);
        Assert.InRange(stream.Engine.HabituationOf("npc.villager-walk"), 0, 0.3);
        Assert.Equal(AttentionState.Noticing, stream.Engine.Current.State);

        var urgent = stream.At(901, SyntheticStream.UrgentDanger());
        var fresh = new SyntheticStream().At(0, SyntheticStream.UrgentDanger("threat.never-seen"));
        Assert.Equal(AttentionEventDisposition.Applied, urgent.Record!.Disposition);
        Assert.Contains(AttentionIntentKind.Urgent, urgent.Intents.Select(intent => intent.Kind));
        Assert.Equal(fresh.Record!.Contribution, urgent.Record.Contribution, 9);
        stream.At(902, SyntheticStream.UrgentDanger());
        Assert.Equal(AttentionState.Engaged, stream.Engine.Current.State);
    }

    [Fact]
    public void Habituation_RecoversOverTime()
    {
        var stream = new SyntheticStream();
        // 20 s apart: faster than recovery (10-minute half-life), so the floor is reached.
        for (var index = 0; index < 12; index++)
        {
            stream.At(index * 20, SyntheticStream.Harmless());
        }

        var habituated = stream.Engine.HabituationOf("npc.villager-walk");
        stream.AdvanceTo(220 + TimeSpan.FromHours(2).TotalSeconds);

        Assert.True(stream.Engine.HabituationOf("npc.villager-walk") > 0.99);
        Assert.True(habituated < 0.3);
    }

    [Fact]
    public void Afterglow_FollowsEveryCompletedHighEpisodeWithExactlyOneOpening()
    {
        var stream = new SyntheticStream();
        stream.At(0, SyntheticStream.Decisive());

        // Decay out of High Attention after the minimum dwell.
        for (var second = 1; second <= 30; second++)
        {
            stream.AdvanceTo(second);
        }

        Assert.Equal(AttentionState.Afterglow, stream.Engine.Current.State);
        Assert.Single(stream.IntentKinds, kind => kind == AttentionIntentKind.AfterglowOpening);
        Assert.Single(stream.IntentKinds, kind => kind == AttentionIntentKind.HighAttentionEnded);
        Assert.True(stream.Engine.Current.SuppressUnrelatedInitiatedConversations);

        var until = stream.Engine.Current.AfterglowUntil!.Value;
        stream.AdvanceTo((until - SyntheticStream.Start).TotalSeconds - 0.5);
        Assert.Equal(AttentionState.Afterglow, stream.Engine.Current.State);
        stream.AdvanceTo((until - SyntheticStream.Start).TotalSeconds);
        Assert.Equal(AttentionState.Noticing, stream.Engine.Current.State);
        Assert.False(stream.Engine.Current.SuppressUnrelatedInitiatedConversations);
        Assert.Single(stream.IntentKinds, kind => kind == AttentionIntentKind.AfterglowOpening);
    }

    [Fact]
    public void AfterglowDuration_AdaptsToPeakAndDwell_WithinItsBound()
    {
        static TimeSpan AfterglowAfter(int sustainSeconds)
        {
            var stream = new SyntheticStream();
            stream.At(0, SyntheticStream.Decisive());
            for (var second = 1; second <= sustainSeconds; second++)
            {
                stream.At(second, SyntheticStream.Decisive());
            }

            for (var second = sustainSeconds + 1; stream.Engine.Current.State == AttentionState.HighAttention; second++)
            {
                SyntheticStream.Guard(second);
                stream.AdvanceTo(second);
            }

            return stream.Engine.Current.AfterglowUntil!.Value - stream.Engine.Current.StateSince;
        }

        var brief = AfterglowAfter(0);
        var sustained = AfterglowAfter(120);
        var marathon = AfterglowAfter(3600);

        Assert.True(sustained > brief);
        Assert.Equal(AttentionConfiguration.Default.AfterglowMaximum, marathon);
    }

    [Fact]
    public void RenewedDangerDuringAfterglow_ReturnsToHighAttention()
    {
        var stream = new SyntheticStream();
        stream.At(0, SyntheticStream.Decisive());
        for (var second = 1; stream.Engine.Current.State != AttentionState.Afterglow; second++)
        {
            SyntheticStream.Guard(second);
            stream.AdvanceTo(second);
        }

        var renewed = stream.At(stream.Engine.Current.AsOf.Subtract(SyntheticStream.Start).TotalSeconds + 1, SyntheticStream.Decisive(DecisiveReason.LargeEnemyGroup, "encounter.ambush"));

        Assert.Equal(AttentionState.HighAttention, renewed.Snapshot.State);
        Assert.Null(renewed.Snapshot.AfterglowUntil);
    }

    [Fact]
    public void Hysteresis_PreventsFlappingAroundTheEngagedThreshold()
    {
        var stream = new SyntheticStream();
        stream.At(0, SyntheticStream.EnemyEvidence("region:center"));
        stream.At(1, SyntheticStream.EnemyEvidence("region:upper-left"));
        Assert.Equal(AttentionState.Engaged, stream.Engine.Current.State);

        var states = new List<AttentionState>();
        for (var tenth = 11; tenth <= 400; tenth++)
        {
            states.Add(stream.AdvanceTo(tenth / 10.0).Snapshot.State);
        }

        var changes = states.Zip(states.Skip(1)).Count(pair => pair.First != pair.Second);
        Assert.True(changes <= 1, "Decay crosses the engaged band at most once.");
        Assert.Equal(AttentionState.Noticing, states[^1]);
    }

    [Fact]
    public void HigherInterest_DecaysMoreSlowly()
    {
        var high = new SyntheticStream();
        high.At(0, SyntheticStream.Decisive());
        var highStart = high.Engine.Current.Score;
        high.AdvanceTo(5);
        var highRetained = high.Engine.Current.Score / highStart;

        var low = new SyntheticStream();
        low.At(0, SyntheticStream.Harmless());
        var lowStart = low.Engine.Current.Score;
        low.AdvanceTo(5);
        var lowRetained = low.Engine.Current.Score / lowStart;

        Assert.True(highRetained > lowRetained);
    }

    [Fact]
    public void LongGaps_DecayInBoundedWork()
    {
        var stream = new SyntheticStream();
        stream.At(0, SyntheticStream.Decisive());

        var update = stream.AdvanceTo(TimeSpan.FromDays(365).TotalSeconds);

        Assert.Equal(0, update.Snapshot.Score);
        Assert.Equal(AttentionState.Noticing, update.Snapshot.State);
    }

    [Fact]
    public void FalseAlarmCorrection_EndsPromptlyAdjustsOnlyTriggeringTopics_AndNeverBlocksUrgentDanger()
    {
        var stream = new SyntheticStream();
        stream.At(0, SyntheticStream.Suspicious());
        stream.At(1, SyntheticStream.Suspicious());
        Assert.NotEqual(AttentionState.Noticing, stream.Engine.Current.State);
        var untouched = stream.Engine.HabituationOf("npc.unrelated");
        var before = stream.Engine.HabituationOf("shadow.flicker");

        var corrected = stream.Engine.CorrectFalseAlarm(SyntheticStream.Start + TimeSpan.FromSeconds(2));

        Assert.Equal(AttentionState.Noticing, corrected.Snapshot.State);
        Assert.Null(corrected.Snapshot.AfterglowUntil);
        Assert.Equal([AttentionIntentKind.AllClear], corrected.Intents.Select(intent => intent.Kind));
        Assert.DoesNotContain(AttentionIntentKind.AfterglowOpening, corrected.Intents.Select(intent => intent.Kind));
        Assert.True(stream.Engine.HabituationOf("shadow.flicker") < before);
        Assert.Equal(untouched, stream.Engine.HabituationOf("npc.unrelated"));

        var urgent = stream.At(3, SyntheticStream.UrgentDanger("shadow.flicker"));
        stream.At(4, SyntheticStream.UrgentDanger("shadow.flicker"));
        Assert.Contains(AttentionIntentKind.Urgent, urgent.Intents.Select(intent => intent.Kind));
        Assert.NotEqual(AttentionState.Noticing, stream.Engine.Current.State);
    }

    [Fact]
    public void FalseAlarmCorrectionWhileNoticing_IsANoOp()
    {
        var stream = new SyntheticStream();

        var update = stream.Engine.CorrectFalseAlarm(SyntheticStream.Start);

        Assert.Empty(update.Intents);
        Assert.Equal(AttentionState.Noticing, update.Snapshot.State);
    }

    [Fact]
    public void LocationFamiliarity_ScalesHarmlessEvidence_ButNeverSuppressesUrgentDanger()
    {
        static double Contribution(LocationFamiliarity familiarity, InterestEvent interestEvent)
        {
            var stream = new SyntheticStream();
            stream.Engine.SetLocationFamiliarity("area", familiarity);
            return stream.At(0, interestEvent with { LocationKey = "area" }).Record!.Contribution;
        }

        var harmless = SyntheticStream.Harmless("npc.fresh");
        var urgent = SyntheticStream.UrgentDanger("threat.fresh");

        Assert.True(Contribution(LocationFamiliarity.Safe, harmless) < Contribution(LocationFamiliarity.Cleared, harmless));
        Assert.True(Contribution(LocationFamiliarity.Cleared, harmless) < Contribution(LocationFamiliarity.Unknown, harmless));
        Assert.True(Contribution(LocationFamiliarity.Unknown, harmless) < Contribution(LocationFamiliarity.Hazardous, harmless));
        Assert.Equal(Contribution(LocationFamiliarity.Unknown, urgent), Contribution(LocationFamiliarity.Safe, urgent), 9);
        Assert.Equal(Contribution(LocationFamiliarity.Unknown, urgent), Contribution(LocationFamiliarity.Cleared, urgent), 9);
        Assert.True(Contribution(LocationFamiliarity.Hazardous, urgent) > Contribution(LocationFamiliarity.Unknown, urgent));
    }

    [Fact]
    public void Locations_AreLearnedHazardousAfterUrgentEpisodes_AndClearedAfterSustainedQuiet()
    {
        var stream = new SyntheticStream();
        stream.At(0, SyntheticStream.Harmless("npc.guard", "dungeon.hall"));
        stream.At(1, SyntheticStream.UrgentDanger("threat.ambush", "dungeon.hall"));
        stream.At(2, SyntheticStream.Decisive(DecisiveReason.LargeEnemyGroup) with { LocationKey = "dungeon.hall" });
        for (var second = 3; stream.Engine.Current.State == AttentionState.HighAttention; second++)
        {
            SyntheticStream.Guard(second);
            stream.AdvanceTo(second);
        }

        Assert.Equal(LocationFamiliarity.Hazardous, stream.Engine.FamiliarityOf("dungeon.hall"));

        // Return to quiet, then stay quiet in the same place.
        var until = stream.Engine.Current.AfterglowUntil!.Value;
        stream.AdvanceTo((until - SyntheticStream.Start).TotalSeconds);
        Assert.Equal(AttentionState.Noticing, stream.Engine.Current.State);
        var quietFrom = (stream.Now - SyntheticStream.Start).TotalSeconds;
        stream.AdvanceTo(quietFrom + TimeSpan.FromMinutes(5).TotalSeconds - 1);
        Assert.Equal(LocationFamiliarity.Hazardous, stream.Engine.FamiliarityOf("dungeon.hall"));
        stream.AdvanceTo(quietFrom + TimeSpan.FromMinutes(5).TotalSeconds);
        Assert.Equal(LocationFamiliarity.Cleared, stream.Engine.FamiliarityOf("dungeon.hall"));
    }

    [Fact]
    public void ExplicitFamiliarity_IsNeverOverriddenByLearning()
    {
        var stream = new SyntheticStream();
        stream.Engine.SetLocationFamiliarity("town.square", LocationFamiliarity.Safe);
        stream.At(0, SyntheticStream.UrgentDanger("threat.raid", "town.square"));
        stream.At(1, SyntheticStream.Decisive() with { LocationKey = "town.square" });
        for (var second = 2; stream.Engine.Current.State == AttentionState.HighAttention; second++)
        {
            SyntheticStream.Guard(second);
            stream.AdvanceTo(second);
        }

        Assert.Equal(LocationFamiliarity.Safe, stream.Engine.FamiliarityOf("town.square"));
    }

    [Fact]
    public void WrongSessionEvidence_IsIgnored()
    {
        var stream = new SyntheticStream();

        var update = stream.Engine.Observe(SyntheticStream.Decisive() with
        {
            TargetSessionId = Guid.NewGuid(),
            Timestamp = SyntheticStream.Start,
        });

        Assert.Equal(AttentionEventDisposition.WrongSession, update.Record!.Disposition);
        Assert.Equal(AttentionState.Noticing, update.Snapshot.State);
        Assert.Empty(update.Intents);
    }

    [Fact]
    public void OutOfOrderEvidence_IsAppliedWithoutMovingTimeBackwards()
    {
        var stream = new SyntheticStream();
        stream.AdvanceTo(30);

        var late = stream.Engine.Observe(SyntheticStream.Harmless() with { Timestamp = SyntheticStream.Start + TimeSpan.FromSeconds(10) });

        Assert.Equal(SyntheticStream.Start + TimeSpan.FromSeconds(30), late.Snapshot.AsOf);
        Assert.Equal(AttentionEventDisposition.Applied, late.Record!.Disposition);
        Assert.Equal(SyntheticStream.Start + TimeSpan.FromSeconds(30), stream.Engine.Advance(SyntheticStream.Start).Snapshot.AsOf);
    }

    public static TheoryData<string> InvalidEvents => new()
    {
        "nan", "negative", "above-one", "empty-session", "no-timestamp", "decisive-without-reason",
        "reason-without-decisive", "transition-without-key", "blank-topic", "long-topic", "too-many-references",
        "invalid-region",
    };

    [Theory]
    [MemberData(nameof(InvalidEvents))]
    public void InvalidEvidence_IsRejectedNotClamped(string scenario)
    {
        var engine = new SyntheticStream().Engine;
        var valid = SyntheticStream.Harmless();
        var invalid = scenario switch
        {
            "nan" => valid with { Signals = valid.Signals with { Salience = double.NaN } },
            "negative" => valid with { Signals = valid.Signals with { Novelty = -0.1 } },
            "above-one" => valid with { Signals = valid.Signals with { Confidence = 1.01 } },
            "empty-session" => valid with { TargetSessionId = Guid.Empty },
            "no-timestamp" => valid with { Timestamp = default },
            "decisive-without-reason" => valid with { Kind = AttentionEventKind.Decisive },
            "reason-without-decisive" => valid with { DecisiveReason = DecisiveReason.Boss },
            "transition-without-key" => valid with { Kind = AttentionEventKind.GlobalTransition },
            "blank-topic" => valid with { TopicKey = " " },
            "long-topic" => valid with { TopicKey = new string('t', 257) },
            "too-many-references" => valid with { EvidenceReferences = Enumerable.Range(0, 17).Select(index => $"ref.{index}").ToArray() },
            "invalid-region" => valid with { Region = new NormalizedRegion(0.9, 0.9, 0.5, 0.5) },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        Assert.ThrowsAny<ArgumentException>(() => engine.Observe(invalid));
        Assert.Equal(AttentionState.Noticing, engine.Current.State);
    }

    [Fact]
    public void InvalidConfiguration_IsRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => new AttentionEngine(SyntheticStream.Session, SyntheticStream.Start, new AttentionConfiguration { EngagedExit = 45 }));
        Assert.ThrowsAny<ArgumentException>(() => new AttentionEngine(SyntheticStream.Session, SyntheticStream.Start, new AttentionConfiguration { HighExit = 75 }));
        Assert.ThrowsAny<ArgumentException>(() => new AttentionEngine(SyntheticStream.Session, SyntheticStream.Start, new AttentionConfiguration { MinimumIndependentSources = 1 }));
        Assert.ThrowsAny<ArgumentException>(() => new AttentionEngine(SyntheticStream.Session, SyntheticStream.Start, new AttentionConfiguration { NoveltyWeight = double.NaN }));
        Assert.ThrowsAny<ArgumentException>(() => new AttentionEngine(SyntheticStream.Session, SyntheticStream.Start, new AttentionConfiguration { AfterglowMaximum = TimeSpan.FromSeconds(1) }));
        Assert.ThrowsAny<ArgumentException>(() => new AttentionEngine(Guid.Empty, SyntheticStream.Start));
    }

    [Fact]
    public void TopicAndLocationTables_StayBounded()
    {
        var stream = new SyntheticStream(new AttentionConfiguration { MaximumTopics = 16, MaximumLocations = 8 });

        for (var index = 0; index < 200; index++)
        {
            stream.At(index, SyntheticStream.Harmless($"topic.{index}", $"location.{index}"));
        }

        Assert.Equal(16, stream.Engine.TrackedTopicCount);
        Assert.Equal(8, stream.Engine.TrackedLocationCount);
    }

    [Fact]
    public void IdenticalSyntheticStreams_ProduceIdenticalUpdates()
    {
        static IReadOnlyList<string> Run()
        {
            var stream = new SyntheticStream();
            stream.At(0, SyntheticStream.WeakMotion());
            stream.At(1, SyntheticStream.EnemyEvidence("region:center"));
            stream.At(2, SyntheticStream.EnemyEvidence("region:upper-left"));
            stream.At(3, SyntheticStream.Loading());
            stream.At(4, SyntheticStream.Loading());
            stream.At(5, SyntheticStream.Decisive());
            for (var second = 6; second < 300; second += 3)
            {
                stream.AdvanceTo(second);
            }

            stream.At(301, SyntheticStream.Harmless());
            stream.Engine.CorrectFalseAlarm(SyntheticStream.Start + TimeSpan.FromSeconds(302));
            return stream.Updates
                .Select(update => $"{update.Snapshot.State}|{update.Snapshot.Score:R}|{update.Snapshot.AfterglowUntil:O}|"
                    + string.Join(',', update.Intents.Select(intent => $"{intent.Kind}:{intent.TopicKey}"))
                    + $"|{update.Record?.Disposition}|{update.Record?.Contribution:R}")
                .ToArray();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Score_NeverExceedsItsCeiling()
    {
        var stream = new SyntheticStream();

        for (var index = 0; index < 50; index++)
        {
            stream.At(index * 0.1, SyntheticStream.Decisive());
            stream.At(index * 0.1, SyntheticStream.UrgentDanger("threat.swarm"));
        }

        Assert.All(stream.Updates, update => Assert.InRange(update.Snapshot.Score, 0, 100));
    }

    [Fact]
    public void HighAttention_HoldsForItsMinimumDwell_EvenWhenScoreFallsFast()
    {
        var configuration = new AttentionConfiguration
        {
            HighMinimumDwell = TimeSpan.FromSeconds(20),
            BaseHalfLife = TimeSpan.FromSeconds(1),
            HalfLifePerScorePoint = TimeSpan.Zero,
        };
        var stream = new SyntheticStream(configuration);
        stream.At(0, SyntheticStream.Decisive());

        stream.AdvanceTo(19);
        Assert.Equal(AttentionState.HighAttention, stream.Engine.Current.State);
        Assert.True(stream.Engine.Current.Score < configuration.HighExit);

        stream.AdvanceTo(20);
        Assert.Equal(AttentionState.Afterglow, stream.Engine.Current.State);
    }

    [Fact]
    public void AfterglowExpiry_ReturnsToEngaged_WhenInterestRemainsInTheEngagedBand()
    {
        var configuration = new AttentionConfiguration { AfterglowBase = TimeSpan.FromSeconds(1), AfterglowPerPeakPoint = TimeSpan.Zero, AfterglowPerHighSecond = 0, AfterglowMaximum = TimeSpan.FromSeconds(1) };
        var stream = new SyntheticStream(configuration);
        stream.At(0, SyntheticStream.Decisive());
        for (var tenth = 1; stream.Engine.Current.State == AttentionState.HighAttention; tenth++)
        {
            SyntheticStream.Guard(tenth);
            stream.AdvanceTo(tenth / 10.0);
        }

        var afterglowAt = (stream.Now - SyntheticStream.Start).TotalSeconds;
        stream.AdvanceTo(afterglowAt + 1);

        Assert.InRange(stream.Engine.Current.Score, configuration.EngagedExit, configuration.HighEnter);
        Assert.Equal(AttentionState.Engaged, stream.Engine.Current.State);
        Assert.False(stream.Engine.Current.SuppressUnrelatedInitiatedConversations);
    }

    [Fact]
    public void EngagedState_HoldsInsideTheHysteresisBand()
    {
        var stream = new SyntheticStream();
        stream.At(0, SyntheticStream.EnemyEvidence("region:center"));
        stream.At(1, SyntheticStream.EnemyEvidence("region:upper-left"));
        Assert.Equal(AttentionState.Engaged, stream.Engine.Current.State);

        var tenth = 11;
        while (stream.Engine.Current.Score >= AttentionConfiguration.Default.EngagedEnter)
        {
            SyntheticStream.Guard(tenth);
            stream.AdvanceTo(tenth++ / 10.0);
        }

        // Below the entry threshold but above the exit threshold: still Engaged.
        Assert.InRange(stream.Engine.Current.Score, AttentionConfiguration.Default.EngagedExit, AttentionConfiguration.Default.EngagedEnter);
        Assert.Equal(AttentionState.Engaged, stream.Engine.Current.State);
    }
}
