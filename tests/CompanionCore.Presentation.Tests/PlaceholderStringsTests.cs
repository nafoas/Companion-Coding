using CompanionCore.Presentation;

namespace CompanionCore.Presentation.Tests;

public sealed class PlaceholderStringsTests
{
    [Theory]
    [InlineData(NeutralPersonalityAdapter.StartedKey)]
    [InlineData(NeutralPersonalityAdapter.RecoveringKey)]
    [InlineData(NeutralPersonalityAdapter.NappingKey)]
    [InlineData(NeutralPersonalityAdapter.WakingKey)]
    [InlineData(NeutralPersonalityAdapter.StoppedKey)]
    [InlineData(NeutralPersonalityAdapter.UnknownKey)]
    [InlineData(NeutralPersonalityAdapter.TargetDiscoveryReadyKey)]
    [InlineData(NeutralPersonalityAdapter.TargetDiscoveryBlockedKey)]
    [InlineData(NeutralPersonalityAdapter.TargetDiscoveryFailedKey)]
    [InlineData(NeutralPersonalityAdapter.TargetConsentRequiredKey)]
    [InlineData(NeutralPersonalityAdapter.TargetDeniedKey)]
    [InlineData(NeutralPersonalityAdapter.StandingAuthorizationAvailableKey)]
    [InlineData(NeutralPersonalityAdapter.AnotherTargetActiveKey)]
    [InlineData(NeutralPersonalityAdapter.TargetAuthorizedKey)]
    [InlineData(NeutralPersonalityAdapter.TargetPrivacyPausedKey)]
    [InlineData(NeutralPersonalityAdapter.TargetPrivacyPausedNoTargetKey)]
    [InlineData(NeutralPersonalityAdapter.TargetPrivacyResumedKey)]
    [InlineData(NeutralPersonalityAdapter.TargetResumedKey)]
    [InlineData(NeutralPersonalityAdapter.TargetEndedKey)]
    [InlineData(NeutralPersonalityAdapter.TargetUnavailableKey)]
    [InlineData(NeutralPersonalityAdapter.TargetFailedKey)]
    public void EveryContentKeyTheAdapterCanProduce_HasAPlaceholderString(string contentKey)
    {
        Assert.True(PlaceholderStrings.ByContentKey.ContainsKey(contentKey));
        Assert.False(string.IsNullOrWhiteSpace(PlaceholderStrings.ByContentKey[contentKey]));
    }

    [Fact]
    public void Resolve_UnrecognizedContentKey_FallsBackToUnknownRatherThanThrowing()
    {
        var content = new PresentationContent("something-not-in-the-table", ExpressionIntent.None);

        var text = PlaceholderStrings.Resolve(content);

        Assert.Equal(PlaceholderStrings.ByContentKey[NeutralPersonalityAdapter.UnknownKey], text);
    }

    [Fact]
    public void TargetTemplate_UsesOnlyProvidedNeutralTitleFreeDetail()
    {
        var content = new PresentationContent(
            NeutralPersonalityAdapter.TargetAuthorizedKey,
            ExpressionIntent.None,
            "synthetic-game.exe (PID 20, window 0x2A)");

        var text = PlaceholderStrings.Resolve(content);

        Assert.Equal("Authorized target: synthetic-game.exe (PID 20, window 0x2A).", text);
    }
}
