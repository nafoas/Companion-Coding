using CompanionCore.Privacy;

namespace CompanionCore.Privacy.Tests;

public sealed class LocalPrivacyGuardTests
{
    private readonly LocalPrivacyGuard _guard = new();

    [Fact]
    public void StandardTarget_AllowsClearAssessment()
    {
        Assert.Equal(
            PrivacyGuardDecision.Allowed,
            _guard.Evaluate(TargetContentPolicy.Standard, PrivacyAssessment.Clear));
    }

    [Theory]
    [InlineData(SensitiveContentKind.Credential)]
    [InlineData(SensitiveContentKind.Payment)]
    [InlineData(SensitiveContentKind.Financial)]
    public void StandardTarget_RejectsOnlyExplicitClearlySensitiveAssessment(
        SensitiveContentKind sensitiveKind)
    {
        Assert.Equal(
            PrivacyGuardDecision.RejectedSensitive,
            _guard.Evaluate(
                TargetContentPolicy.Standard,
                PrivacyAssessment.ClearlySensitive(sensitiveKind)));
    }

    [Fact]
    public void StandardTarget_FailsClosedWhenAssessmentIsUnavailable()
    {
        Assert.Equal(
            PrivacyGuardDecision.RejectedUnavailable,
            _guard.Evaluate(TargetContentPolicy.Standard, PrivacyAssessment.Unavailable));
    }

    [Fact]
    public void ExplicitTrustedGame_BypassesOnlyContentPolicy()
    {
        Assert.Equal(
            PrivacyGuardDecision.TrustedGameBypass,
            _guard.Evaluate(
                TargetContentPolicy.TrustedGame,
                PrivacyAssessment.ClearlySensitive(SensitiveContentKind.Credential)));
    }

    [Fact]
    public void UnknownContentPolicy_FailsClosedEvenForClearAssessment()
    {
        Assert.Equal(
            PrivacyGuardDecision.RejectedUnavailable,
            _guard.Evaluate((TargetContentPolicy)999, PrivacyAssessment.Clear));
    }
}
