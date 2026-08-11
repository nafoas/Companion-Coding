using CompanionCore.Presentation;
using CompanionCore.Runtime;
using CompanionCore.TargetAuth;

namespace CompanionCore.Presentation.Tests;

/// <summary>
/// Table-driven verification that <see cref="NeutralPersonalityAdapter"/> matches
/// architecture §6.2.1 exactly, row for row.
/// </summary>
public sealed class NeutralPersonalityAdapterTests
{
    private readonly NeutralPersonalityAdapter _adapter = new();

    [Fact]
    public void Start_Cold_MapsToStartedWithNoIntent()
    {
        var transition = new LifecycleTransitionResult(
            LifecycleEvent.Start, IsValid: true, RuntimeState.NotStarted, RuntimeState.Running, CheckpointRecovered: false);

        var content = _adapter.Map(transition);

        Assert.Equal(NeutralPersonalityAdapter.StartedKey, content.ContentKey);
        Assert.Equal(ExpressionIntent.None, content.Intent);
    }

    [Fact]
    public void Start_CheckpointRecovered_MapsToRecoveringWithRecoveringIntent()
    {
        var transition = new LifecycleTransitionResult(
            LifecycleEvent.Start, IsValid: true, RuntimeState.NotStarted, RuntimeState.Running, CheckpointRecovered: true);

        var content = _adapter.Map(transition);

        Assert.Equal(NeutralPersonalityAdapter.RecoveringKey, content.ContentKey);
        Assert.Equal(ExpressionIntent.Recovering, content.Intent);
    }

    [Fact]
    public void Nap_MapsToNappingWithNoIntent()
    {
        var transition = new LifecycleTransitionResult(
            LifecycleEvent.Nap, IsValid: true, RuntimeState.Running, RuntimeState.Napping, CheckpointRecovered: false);

        var content = _adapter.Map(transition);

        Assert.Equal(NeutralPersonalityAdapter.NappingKey, content.ContentKey);
        Assert.Equal(ExpressionIntent.None, content.Intent);
    }

    [Fact]
    public void Wake_Valid_MapsToWakingWithNoIntent()
    {
        var transition = new LifecycleTransitionResult(
            LifecycleEvent.Wake, IsValid: true, RuntimeState.Napping, RuntimeState.Running, CheckpointRecovered: false);

        var content = _adapter.Map(transition);

        Assert.Equal(NeutralPersonalityAdapter.WakingKey, content.ContentKey);
        Assert.Equal(ExpressionIntent.None, content.Intent);
    }

    [Fact]
    public void Stop_MapsToStoppedWithNoIntent()
    {
        var transition = new LifecycleTransitionResult(
            LifecycleEvent.Stop, IsValid: true, RuntimeState.Running, RuntimeState.Stopped, CheckpointRecovered: false);

        var content = _adapter.Map(transition);

        Assert.Equal(NeutralPersonalityAdapter.StoppedKey, content.ContentKey);
        Assert.Equal(ExpressionIntent.None, content.Intent);
    }

    [Theory]
    [InlineData(LifecycleEvent.Start)]
    [InlineData(LifecycleEvent.Nap)]
    [InlineData(LifecycleEvent.Wake)]
    [InlineData(LifecycleEvent.Stop)]
    public void InvalidTransition_AlwaysMapsToUnknownWithNoIntent_RegardlessOfEvent(LifecycleEvent @event)
    {
        // The fallback is required to be deterministic across every event, not only
        // ones the mapping otherwise recognizes as valid — an invalid Start is just as
        // unknown as an invalid Nap.
        var transition = new LifecycleTransitionResult(
            @event, IsValid: false, RuntimeState.NotStarted, RuntimeState.NotStarted, CheckpointRecovered: false);

        var content = _adapter.Map(transition);

        Assert.Equal(NeutralPersonalityAdapter.UnknownKey, content.ContentKey);
        Assert.Equal(ExpressionIntent.None, content.Intent);
    }

    [Fact]
    public void InvalidTransition_IgnoresCheckpointRecoveredFlag()
    {
        // CheckpointRecovered only distinguishes the two *valid* Start rows; it must not
        // leak into the invalid-transition fallback.
        var transition = new LifecycleTransitionResult(
            LifecycleEvent.Start, IsValid: false, RuntimeState.Running, RuntimeState.Running, CheckpointRecovered: true);

        var content = _adapter.Map(transition);

        Assert.Equal(NeutralPersonalityAdapter.UnknownKey, content.ContentKey);
        Assert.Equal(ExpressionIntent.None, content.Intent);
    }

    [Fact]
    public void UnrecognizedEventValue_StillMapsToUnknown_ProvingTotality()
    {
        // A value outside the declared LifecycleEvent range (simulating a future enum
        // member reaching an old build) must still produce a defined, renderable result
        // rather than throwing — the mapping is required to be a total function.
        var unrecognized = (LifecycleEvent)999;
        var transition = new LifecycleTransitionResult(
            unrecognized, IsValid: true, RuntimeState.NotStarted, RuntimeState.NotStarted, CheckpointRecovered: false);

        var content = _adapter.Map(transition);

        Assert.Equal(NeutralPersonalityAdapter.UnknownKey, content.ContentKey);
        Assert.Equal(ExpressionIntent.None, content.Intent);
    }

    [Fact]
    public void Map_IsDeterministic_SameInputAlwaysProducesSameOutput()
    {
        var transition = new LifecycleTransitionResult(
            LifecycleEvent.Nap, IsValid: true, RuntimeState.Running, RuntimeState.Napping, CheckpointRecovered: false);

        var first = _adapter.Map(transition);
        var second = _adapter.Map(transition);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(TargetSessionEventKind.DiscoveryReady, NeutralPersonalityAdapter.TargetDiscoveryReadyKey, ExpressionIntent.None)]
    [InlineData(TargetSessionEventKind.DiscoveryBlocked, NeutralPersonalityAdapter.TargetDiscoveryBlockedKey, ExpressionIntent.PrivacyPaused)]
    [InlineData(TargetSessionEventKind.DiscoveryFailed, NeutralPersonalityAdapter.TargetDiscoveryFailedKey, ExpressionIntent.None)]
    [InlineData(TargetSessionEventKind.ConsentRequired, NeutralPersonalityAdapter.TargetConsentRequiredKey, ExpressionIntent.None)]
    [InlineData(TargetSessionEventKind.Denied, NeutralPersonalityAdapter.TargetDeniedKey, ExpressionIntent.None)]
    [InlineData(TargetSessionEventKind.StandingAuthorizationAvailable, NeutralPersonalityAdapter.StandingAuthorizationAvailableKey, ExpressionIntent.None)]
    [InlineData(TargetSessionEventKind.AnotherTargetActive, NeutralPersonalityAdapter.AnotherTargetActiveKey, ExpressionIntent.None)]
    [InlineData(TargetSessionEventKind.Authorized, NeutralPersonalityAdapter.TargetAuthorizedKey, ExpressionIntent.None)]
    [InlineData(TargetSessionEventKind.PrivacyPaused, NeutralPersonalityAdapter.TargetPrivacyPausedNoTargetKey, ExpressionIntent.PrivacyPaused)]
    [InlineData(TargetSessionEventKind.PrivacyResumed, NeutralPersonalityAdapter.TargetPrivacyResumedKey, ExpressionIntent.None)]
    [InlineData(TargetSessionEventKind.Resumed, NeutralPersonalityAdapter.TargetResumedKey, ExpressionIntent.None)]
    [InlineData(TargetSessionEventKind.TargetEnded, NeutralPersonalityAdapter.TargetEndedKey, ExpressionIntent.None)]
    [InlineData(TargetSessionEventKind.TargetUnavailable, NeutralPersonalityAdapter.TargetUnavailableKey, ExpressionIntent.PrivacyPaused)]
    [InlineData(TargetSessionEventKind.Failed, NeutralPersonalityAdapter.TargetFailedKey, ExpressionIntent.None)]
    public void TargetEvent_MapsDeterministicallyToNeutralContent(
        TargetSessionEventKind kind,
        string expectedKey,
        ExpressionIntent expectedIntent)
    {
        var targetEvent = new TargetSessionEvent(kind, Candidate: null);

        var content = _adapter.Map(targetEvent);

        Assert.Equal(expectedKey, content.ContentKey);
        Assert.Equal(expectedIntent, content.Intent);
    }

    [Fact]
    public void PrivacyPausedWithTarget_MapsToExactTargetContent()
    {
        var targetEvent = new TargetSessionEvent(
            TargetSessionEventKind.PrivacyPaused,
            TargetAuthTestCandidate.Create());

        var content = _adapter.Map(targetEvent);

        Assert.Equal(NeutralPersonalityAdapter.TargetPrivacyPausedKey, content.ContentKey);
        Assert.Equal(targetEvent.Candidate?.NeutralDisplayLabel, content.NeutralDetail);
    }

    private static class TargetAuthTestCandidate
    {
        internal static TargetCandidate Create() =>
            new(
                new CompanionCore.Capture.Contracts.CaptureTargetIdentity(
                    42,
                    20,
                    "synthetic-game.exe",
                    new string('A', 64)),
                ApplicationCategory.Other);
    }
}
