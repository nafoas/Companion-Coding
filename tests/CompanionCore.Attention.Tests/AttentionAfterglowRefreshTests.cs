namespace CompanionCore.Attention.Tests;

/// <summary>
/// ATT-01 — while things keep happening, Afterglow stays. Its length is still the episode's
/// adaptive 30 s to 3 min, but each interesting event restarts the countdown, so a long
/// quest, match, or cutscene doesn't flip him between "watching intently" and "never mind".
/// </summary>
public sealed class AttentionAfterglowRefreshTests
{
    /// <summary>Just below Bnuy Mode, like every local reflex's evidence.</summary>
    private static readonly double Ceiling = AttentionConfiguration.Default.HighEnter - 1;

    [Fact]
    public void InterestingEvents_KeepHimInAfterglow_AndItEndsOneLengthAfterTheLast()
    {
        var (stream, entered, length) = EnterAfterglow();
        var originalDeadline = stream.Engine.Current.AfterglowUntil!.Value;

        // New developments every 20 s, well past the original deadline.
        var at = entered;
        var developments = 0;
        while (stream.Now < originalDeadline + (length * 2))
        {
            at += 20;
            stream.At(at, Development(developments++));
            Assert.Equal(AttentionState.Afterglow, stream.Engine.Current.State);
            Assert.Equal(stream.Now + length, stream.Engine.Current.AfterglowUntil);
        }

        // Then it goes quiet: Afterglow lasts exactly one length after the last development.
        var last = at;
        stream.AdvanceTo(last + length.TotalSeconds - 0.5);
        Assert.Equal(AttentionState.Afterglow, stream.Engine.Current.State);
        stream.AdvanceTo(last + length.TotalSeconds);
        Assert.NotEqual(AttentionState.Afterglow, stream.Engine.Current.State);
        Assert.Null(stream.Engine.Current.AfterglowUntil);

        // However long it was extended, the episode had exactly one Afterglow opening.
        Assert.Single(stream.IntentKinds, kind => kind == AttentionIntentKind.AfterglowOpening);
        Assert.True(developments > 3);
    }

    [Fact]
    public void TheLengthStaysTheEpisodes_ARestartNeverMakesItLonger()
    {
        var (stream, entered, length) = EnterAfterglow();
        Assert.InRange(length, AttentionConfiguration.Default.AfterglowBase, AttentionConfiguration.Default.AfterglowMaximum);

        stream.At(entered + 5, Development(0));
        Assert.Equal(stream.Now + length, stream.Engine.Current.AfterglowUntil);
        stream.At(entered + 6, Development(1));
        Assert.Equal(stream.Now + length, stream.Engine.Current.AfterglowUntil);
    }

    [Fact]
    public void NoiseDoesNotExtendIt()
    {
        var (stream, entered, _) = EnterAfterglow();

        // A first loading screen is a real development (it extends Afterglow); seeing it again is not.
        stream.At(entered + 1, SyntheticStream.Loading("transition.repeat") with { ScoreCeiling = Ceiling });
        var deadline = stream.Engine.Current.AfterglowUntil!.Value;
        var duplicate = stream.At(entered + 2, SyntheticStream.Loading("transition.repeat") with { ScoreCeiling = Ceiling });
        Assert.Equal(AttentionEventDisposition.DuplicateTransition, duplicate.Record!.Disposition);

        // Weak evidence that is only held for corroboration.
        var weak = stream.At(entered + 3, SyntheticStream.WeakMotion() with { ScoreCeiling = Ceiling });
        Assert.Equal(AttentionEventDisposition.HeldForCorroboration, weak.Record!.Disposition);

        // Evidence for some other session.
        var foreign = stream.At(entered + 4, Development(0) with { TargetSessionId = Guid.NewGuid() });
        Assert.Equal(AttentionEventDisposition.WrongSession, foreign.Record!.Disposition);

        // A faint transition is applied (transitions skip the weak-evidence hold) but is too small to count.
        var faint = stream.At(entered + 5, SyntheticStream.Loading("transition.faint") with
        {
            Signals = new AttentionSignals(Novelty: 0.1, Change: 0.2, Salience: 0.1, Urgency: 0, Persistence: 0.1, Confidence: 0.6),
            ScoreCeiling = Ceiling,
        });
        Assert.Equal(AttentionEventDisposition.Applied, faint.Record!.Disposition);
        Assert.InRange(faint.Record.Contribution, 0.0, AttentionConfiguration.Default.WeakContribution);

        Assert.Equal(deadline, stream.Engine.Current.AfterglowUntil);
    }

    [Fact]
    public void AFamiliarRepeatingThing_StopsExtendingIt()
    {
        var (stream, entered, _) = EnterAfterglow();

        // A HUD clock ticks every second: the first few ticks are news, then it is familiar.
        DateTimeOffset? lastExtension = null;
        var ticks = 0;
        for (var second = 1; second <= 60; second++)
        {
            var before = stream.Engine.Current.AfterglowUntil;
            stream.At(entered + second, SyntheticStream.Harmless("hud.clock") with { ScoreCeiling = Ceiling });
            if (stream.Engine.Current.State != AttentionState.Afterglow)
            {
                break;
            }

            if (stream.Engine.Current.AfterglowUntil != before)
            {
                lastExtension = stream.Now;
                ticks++;
            }
        }

        Assert.InRange(ticks, 1, 15);
        Assert.NotNull(lastExtension);
        Assert.True(lastExtension < SyntheticStream.Start + TimeSpan.FromSeconds(entered + 60));
    }

    [Fact]
    public void RealDangerStillReturnsHimToBnuyMode()
    {
        var (stream, entered, _) = EnterAfterglow();
        stream.At(entered + 5, Development(0));
        var renewed = stream.At(entered + 6, SyntheticStream.Decisive(DecisiveReason.LargeEnemyGroup, "encounter.ambush"));
        Assert.Equal(AttentionState.HighAttention, renewed.Snapshot.State);
        Assert.Null(renewed.Snapshot.AfterglowUntil);
    }

    /// <summary>A distinct, interesting development (no two share a topic, so none habituates).</summary>
    private static InterestEvent Development(int index) =>
        SyntheticStream.Suspicious($"quest.development-{index}") with { ScoreCeiling = Ceiling };

    private static (SyntheticStream Stream, double EnteredSeconds, TimeSpan Length) EnterAfterglow()
    {
        var stream = new SyntheticStream();
        stream.At(0, SyntheticStream.Decisive());
        var second = 1;
        for (; stream.Engine.Current.State != AttentionState.Afterglow; second++)
        {
            SyntheticStream.Guard(second);
            stream.AdvanceTo(second);
        }

        var snapshot = stream.Engine.Current;
        return (stream, second - 1, snapshot.AfterglowUntil!.Value - snapshot.StateSince);
    }
}
