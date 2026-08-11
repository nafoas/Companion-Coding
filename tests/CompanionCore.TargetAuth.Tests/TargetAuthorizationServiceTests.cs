using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth.Tests;

public sealed class TargetAuthorizationServiceTests
{
    [Fact]
    public async Task Discovery_ReturnsMinimalCandidatesWithoutChangingAuthorizationState()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();
        harness.Discovery.Candidates = [target];

        var result = await harness.Authorization.DiscoverAsync();

        Assert.Equal(TargetDiscoveryStatus.Ready, result.Status);
        Assert.Equal(target, Assert.Single(result.Candidates));
        Assert.Equal(TargetSessionPhase.None, harness.Authorization.CurrentSession.Phase);
        Assert.Equal(0, harness.Discovery.ValidationCalls);
    }

    [Fact]
    public async Task UnknownTarget_RequiresExplicitConsentBeforeValidationOrGrant()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();

        var pending = await harness.Authorization.AuthorizeAsync(target, explicitConsent: false);

        Assert.Equal(TargetAuthorizationStatus.ConsentRequired, pending.Status);
        Assert.True(pending.ShouldPrompt);
        Assert.Null(pending.Grant);
        Assert.Equal(0, harness.Discovery.ValidationCalls);
        Assert.Equal(TargetSessionPhase.None, harness.Authorization.CurrentSession.Phase);

        var authorized = await harness.Authorization.AuthorizeAsync(target, explicitConsent: true);
        Assert.True(authorized.IsAuthorized);
        Assert.Equal(1, harness.Discovery.ValidationCalls);
    }

    [Fact]
    public async Task FamiliarTarget_StillRequiresPerSessionConsent()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();
        await harness.Catalog.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(AuthorizationCategory.FamiliarAsk, TargetContentPolicy.Standard));

        var result = await harness.Authorization.AuthorizeAsync(target, explicitConsent: false);

        Assert.Equal(TargetAuthorizationStatus.ConsentRequired, result.Status);
        Assert.True(result.ShouldPrompt);
        Assert.Equal(0, harness.Discovery.ValidationCalls);
    }

    [Fact]
    public async Task DeniedTarget_ProducesNeitherPromptNorValidationNorGrant()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate(
            'B',
            102,
            202,
            "bitwarden.exe",
            ApplicationCategory.PasswordManager);

        var invitation = harness.Authorization.Inspect(target);
        var result = await harness.Authorization.AuthorizeAsync(target, explicitConsent: true);

        Assert.Equal(TargetInvitationDisposition.DeniedWithoutPrompt, invitation.Disposition);
        Assert.Equal(TargetAuthorizationStatus.Denied, result.Status);
        Assert.False(result.ShouldPrompt);
        Assert.Null(result.Grant);
        Assert.Equal(0, harness.Discovery.ValidationCalls);
    }

    [Fact]
    public async Task StandingAuthorization_WorksOnlyAfterExplicitPersistedPolicyChange()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();
        var before = await harness.Authorization.AuthorizeAsync(target, explicitConsent: false);
        Assert.Equal(TargetAuthorizationStatus.ConsentRequired, before.Status);

        await harness.Catalog.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(
                AuthorizationCategory.StandingAuthorized,
                TargetContentPolicy.Standard));
        var after = await harness.Authorization.AuthorizeAsync(target, explicitConsent: false);

        Assert.True(after.IsAuthorized);
        Assert.False(after.ShouldPrompt);
    }

    [Fact]
    public async Task ExactlyOneTarget_RemainsStableAcrossDiscoveryAndForegroundChanges()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var first = TargetAuthTestHarness.Candidate();
        var second = TargetAuthTestHarness.Candidate('B', 102, 202, "other-game.exe");
        var firstResult = await harness.Authorization.AuthorizeAsync(first, explicitConsent: true);
        Assert.True(firstResult.IsAuthorized);

        harness.Discovery.SimulatedForegroundWindowId = second.Identity.WindowId;
        harness.Discovery.Candidates = [second];
        await harness.Authorization.DiscoverAsync();
        var secondResult = await harness.Authorization.AuthorizeAsync(second, explicitConsent: true);

        Assert.Equal(TargetAuthorizationStatus.AnotherTargetActive, secondResult.Status);
        Assert.Equal(first.Identity, harness.Authorization.CurrentSession.Candidate?.Identity);
        Assert.Equal(firstResult.Grant?.TargetSessionId, harness.Authorization.CurrentSession.TargetSessionId);
    }

    [Fact]
    public async Task StaleOrReusedWindow_FailsBeforeGrantCreation()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        harness.Discovery.IsValid = false;

        var result = await harness.Authorization.AuthorizeAsync(
            TargetAuthTestHarness.Candidate(),
            explicitConsent: true);

        Assert.Equal(TargetAuthorizationStatus.StaleTarget, result.Status);
        Assert.Null(result.Grant);
        Assert.Equal(TargetSessionPhase.None, harness.Authorization.CurrentSession.Phase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task AnythingOtherThanOneDisplay_BlocksDiscoveryAndAuthorization(int displayCount)
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        harness.Topology.DisplayCount = displayCount;
        var target = TargetAuthTestHarness.Candidate();
        harness.Discovery.Candidates = [target];

        var discovery = await harness.Authorization.DiscoverAsync();
        var authorization = await harness.Authorization.AuthorizeAsync(target, explicitConsent: true);

        Assert.Equal(TargetDiscoveryStatus.UnsupportedDisplayTopology, discovery.Status);
        Assert.Empty(discovery.Candidates);
        Assert.Equal(TargetAuthorizationStatus.UnsupportedDisplayTopology, authorization.Status);
        Assert.Equal(0, harness.Discovery.DiscoveryCalls);
        Assert.Equal(0, harness.Discovery.ValidationCalls);
    }

    [Fact]
    public async Task UnknownDisplayCountFailure_FailsClosed()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        harness.Topology.ThrowOnRead = true;

        var result = await harness.Authorization.AuthorizeAsync(
            TargetAuthTestHarness.Candidate(),
            explicitConsent: true);

        Assert.Equal(TargetAuthorizationStatus.UnsupportedDisplayTopology, result.Status);
    }

    [Fact]
    public async Task CancellationObservedAtValidationBoundary_CreatesNoGrant()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        harness.Discovery.ValidationHandler = (_, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult(true);
        };

        var result = await harness.Authorization.AuthorizeAsync(
            TargetAuthTestHarness.Candidate(),
            explicitConsent: true,
            cancellation.Token);

        Assert.Equal(TargetAuthorizationStatus.Cancelled, result.Status);
        Assert.Null(result.Grant);
        Assert.Equal(TargetSessionPhase.None, harness.Authorization.CurrentSession.Phase);
    }

    [Fact]
    public async Task UnexpectedValidationError_FailsClosedWithoutGrant()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        harness.Discovery.ValidationHandler = (_, _) =>
            throw new NotSupportedException("Synthetic validation failure.");

        var result = await harness.Authorization.AuthorizeAsync(
            TargetAuthTestHarness.Candidate(),
            explicitConsent: true);

        Assert.Equal(TargetAuthorizationStatus.Failed, result.Status);
        Assert.Null(result.Grant);
        Assert.Equal(TargetSessionPhase.None, harness.Authorization.CurrentSession.Phase);
    }
}
