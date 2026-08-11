using System.Reflection;
using CompanionCore.Capture.Contracts;
using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth.Tests;

public sealed class PublicSurfaceIsolationTests
{
    [Fact]
    public void AuthorizationCategories_AreExactlyTheFourAcceptedCategories()
    {
        Assert.Equal(
            ["FamiliarAsk", "UnknownAsk", "Denied", "StandingAuthorized"],
            Enum.GetNames<AuthorizationCategory>());
    }

    [Fact]
    public void DiscoveredTargetModels_ExposeNoPrivateWindowOrExecutableText()
    {
        var publicNames = typeof(TargetCandidate)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Concat(typeof(CaptureTargetIdentity)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name))
            .ToArray();

        Assert.DoesNotContain(publicNames, ContainsForbiddenMetadataName);
        Assert.Contains(nameof(CaptureTargetIdentity.WindowId), publicNames);
        Assert.Contains(nameof(CaptureTargetIdentity.ProcessId), publicNames);
        Assert.Contains(nameof(CaptureTargetIdentity.ExecutableFileName), publicNames);
        Assert.Contains(nameof(CaptureTargetIdentity.ExecutablePathFingerprint), publicNames);
    }

    [Fact]
    public void WorkerCannotStartWithoutSealedAuthorizationGrant()
    {
        var starts = typeof(ICaptureWorker)
            .GetMethods()
            .Where(method => method.Name == nameof(ICaptureWorker.StartAsync))
            .ToArray();
        var start = Assert.Single(starts);

        Assert.Equal(typeof(CaptureAuthorizationGrant), start.GetParameters()[0].ParameterType);
        Assert.Empty(typeof(CaptureAuthorizationGrant).GetConstructors());
        Assert.DoesNotContain(
            typeof(CaptureAuthorizationGrant).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.ReturnType == typeof(CaptureAuthorizationGrant));
        Assert.Null(typeof(TargetAuthorizationResult).GetProperty(
            "Grant",
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(TargetSessionSnapshot).GetProperty(
            "Grant",
            BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void CaptureContract_ExposesNoFullScreenForegroundTitlePixelOrMemoryCapability()
    {
        var memberNames = typeof(ICaptureWorker)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(member => member.Name)
            .Concat(typeof(CaptureFrameMetadata)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Select(member => member.Name))
            .ToArray();

        Assert.DoesNotContain(memberNames, ContainsForbiddenMetadataName);
        Assert.DoesNotContain(memberNames, name =>
            name.Contains("Memory", StringComparison.OrdinalIgnoreCase)
            || name.Contains("IdentityConstruction", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AuthorizationService_HasNoForegroundRetargetOperation()
    {
        Assert.DoesNotContain(
            typeof(TargetAuthorizationService).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Foreground", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("Retarget", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExplicitResumeAndRevocationCannotBypassTheControllerPublicly()
    {
        var privacyMethods = typeof(RuntimePrivacyState)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name)
            .ToArray();
        Assert.DoesNotContain("PauseAndRevoke", privacyMethods);
        Assert.DoesNotContain("ResumeExplicitly", privacyMethods);
        Assert.DoesNotContain("RevokeActiveGeneration", privacyMethods);

        var authorizationMethods = typeof(TargetAuthorizationService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name)
            .ToArray();
        Assert.DoesNotContain("PauseForPrivacy", authorizationMethods);
        Assert.DoesNotContain("AuthorizeAsync", authorizationMethods);
        Assert.DoesNotContain("ResumeExplicitlyAsync", authorizationMethods);
        Assert.DoesNotContain("ResumePrivacyWithoutTargetExplicitly", authorizationMethods);
        Assert.DoesNotContain("EndSession", authorizationMethods);

        Assert.DoesNotContain(
            typeof(TargetPolicyCatalog)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(method => method.Name),
            name => name.Contains("Set", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Change", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Update", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsForbiddenMetadataName(string name) =>
        name.Contains("Title", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Command", StringComparison.OrdinalIgnoreCase)
        || name.Contains("RawPath", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Foreground", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Notification", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Pixel", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Screen", StringComparison.OrdinalIgnoreCase)
        || name.Contains("DisplayCapture", StringComparison.OrdinalIgnoreCase);
}
