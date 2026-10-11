namespace CompanionCore.Attention.Tests;

/// <summary>VIS-01: evidence may carry a score ceiling; local Sniff evidence uses it to stay below Bnuy Mode.</summary>
public sealed class AttentionScoreCeilingTests
{
    [Fact]
    public void CeilingedEvidence_NeverLiftsTheScorePastItsCeiling()
    {
        var stream = new SyntheticStream();
        var ceiling = stream.Engine.Configuration.HighEnter - 1;
        for (var second = 0; second < 20; second++)
        {
            stream.At(second * 0.2, SyntheticStream.Suspicious($"shadow.{second}") with { ScoreCeiling = ceiling });
        }

        Assert.True(stream.Engine.Current.Score <= ceiling);
        Assert.NotEqual(AttentionState.HighAttention, stream.Engine.Current.State);

        // Without a ceiling, the same evidence does enter Bnuy Mode.
        var open = new SyntheticStream();
        for (var second = 0; second < 20; second++)
        {
            open.At(second * 0.2, SyntheticStream.Suspicious($"shadow.{second}"));
        }

        Assert.Equal(AttentionState.HighAttention, open.Engine.Current.State);
    }

    [Fact]
    public void ADecisiveEvent_IgnoresTheCeiling()
    {
        var stream = new SyntheticStream();
        stream.At(0, SyntheticStream.Decisive() with { ScoreCeiling = 10 });
        Assert.Equal(AttentionState.HighAttention, stream.Engine.Current.State);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(double.NaN)]
    public void AnInvalidCeiling_IsRejected(double ceiling)
    {
        var stream = new SyntheticStream();
        Assert.Throws<ArgumentException>(() => stream.At(0, SyntheticStream.Harmless() with { ScoreCeiling = ceiling }));
    }
}
