using System.Text.Json;
using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth.Tests;

public sealed class TargetPolicyCatalogTests
{
    [Fact]
    public async Task UnknownIsDefault_AndRecognizedSensitiveCategoriesAreDeniedByDefault()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var ordinary = TargetAuthTestHarness.Candidate();
        var passwordManager = TargetAuthTestHarness.Candidate(
            'B',
            102,
            202,
            "bitwarden.exe",
            ApplicationCategory.PasswordManager);

        Assert.Equal(AuthorizationCategory.UnknownAsk, harness.Catalog.Resolve(ordinary).AuthorizationCategory);
        Assert.Equal(AuthorizationCategory.Denied, harness.Catalog.Resolve(passwordManager).AuthorizationCategory);
    }

    [Fact]
    public async Task BrowserCannotBeAuthorizedEvenByExplicitStoredPolicy()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var browser = TargetAuthTestHarness.Candidate(
            'B',
            102,
            202,
            "chrome.exe",
            ApplicationCategory.Browser);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Catalog.SetExplicitPolicyAsync(
                browser,
                new TargetPolicy(
                    AuthorizationCategory.StandingAuthorized,
                    TargetContentPolicy.TrustedGame)));

        Assert.Equal(AuthorizationCategory.Denied, harness.Catalog.Resolve(browser).AuthorizationCategory);
    }

    [Fact]
    public async Task BrowserFilenameCannotBypassHardDenialThroughIncorrectAdapterCategory()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var mislabeledBrowser = TargetAuthTestHarness.Candidate(
            'B',
            102,
            202,
            "chrome.exe",
            ApplicationCategory.Other);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Catalog.SetExplicitPolicyAsync(
                mislabeledBrowser,
                new TargetPolicy(
                    AuthorizationCategory.StandingAuthorized,
                    TargetContentPolicy.Standard)));

        Assert.Equal(
            AuthorizationCategory.Denied,
            harness.Catalog.Resolve(mislabeledBrowser).AuthorizationCategory);
    }

    [Theory]
    [InlineData(AuthorizationCategory.FamiliarAsk)]
    [InlineData(AuthorizationCategory.UnknownAsk)]
    [InlineData(AuthorizationCategory.Denied)]
    [InlineData(AuthorizationCategory.StandingAuthorized)]
    public async Task EveryAcceptedAuthorizationCategory_PersistsExactly(
        AuthorizationCategory category)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "CompanionCore.TargetAuth.Tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var target = TargetAuthTestHarness.Candidate();
            var catalog = await TargetPolicyCatalog.OpenTestAsync(root);
            await catalog.SetExplicitPolicyAsync(
                target,
                new TargetPolicy(category, TargetContentPolicy.Standard));

            var reopened = await TargetPolicyCatalog.OpenTestAsync(root);

            Assert.True(reopened.WasLoadedValidly);
            Assert.Equal(category, reopened.Resolve(target).AuthorizationCategory);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TrustedGameContentPolicy_PersistsOnlyAfterExplicitChange()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();

        Assert.Equal(TargetContentPolicy.Standard, harness.Catalog.Resolve(target).ContentPolicy);

        await harness.Catalog.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(
                AuthorizationCategory.FamiliarAsk,
                TargetContentPolicy.TrustedGame));
        var reopened = await TargetPolicyCatalog.OpenTestAsync(harness.Root);

        Assert.Equal(TargetContentPolicy.TrustedGame, reopened.Resolve(target).ContentPolicy);
    }

    [Fact]
    public async Task PersistedPolicy_ContainsOnlyTheAcceptedVersionedIdentityAndPolicyFields()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();
        await harness.Catalog.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(
                AuthorizationCategory.FamiliarAsk,
                TargetContentPolicy.TrustedGame));
        var location = AuthorizationPolicyLocation.CreateTest(harness.Root);

        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(location.PolicyPath));
        Assert.Equal(
            ["formatVersion", "entries", "checksum"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        var entry = Assert.Single(document.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal(
            [
                "executablePathFingerprint",
                "executableFileName",
                "authorizationCategory",
                "contentPolicy"
            ],
            entry.EnumerateObject().Select(property => property.Name));
        Assert.Equal(target.Identity.ExecutablePathFingerprint, entry.GetProperty("executablePathFingerprint").GetString());
        Assert.Equal(target.Identity.ExecutableFileName, entry.GetProperty("executableFileName").GetString());
    }

    [Fact]
    public async Task CorruptPolicy_FailsClosedWithoutHonoringStandingAuthorization()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();
        await harness.Catalog.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(
                AuthorizationCategory.StandingAuthorized,
                TargetContentPolicy.Standard));
        var location = AuthorizationPolicyLocation.CreateTest(harness.Root);
        await File.WriteAllTextAsync(location.PolicyPath, "{\"formatVersion\":1,\"entries\":[]}");

        var reopened = await TargetPolicyCatalog.OpenTestAsync(harness.Root);

        Assert.False(reopened.WasLoadedValidly);
        Assert.Equal(AuthorizationCategory.UnknownAsk, reopened.Resolve(target).AuthorizationCategory);
        Assert.Equal(
            AuthorizationCategory.Denied,
            reopened.Resolve(TargetAuthTestHarness.Candidate(
                'B',
                102,
                202,
                "bitwarden.exe",
                ApplicationCategory.PasswordManager)).AuthorizationCategory);
    }

    [Fact]
    public async Task OversizedPolicy_FailsClosed()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var location = AuthorizationPolicyLocation.CreateTest(harness.Root);
        Directory.CreateDirectory(location.RootPath);
        await File.WriteAllBytesAsync(location.PolicyPath, new byte[(1024 * 1024) + 1]);

        var reopened = await TargetPolicyCatalog.OpenTestAsync(harness.Root);

        Assert.False(reopened.WasLoadedValidly);
        Assert.Equal(
            AuthorizationCategory.UnknownAsk,
            reopened.Resolve(TargetAuthTestHarness.Candidate()).AuthorizationCategory);
    }

    [Fact]
    public async Task UnsupportedPolicyVersion_FailsClosedWithoutStandingAuthority()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();
        await harness.Catalog.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(
                AuthorizationCategory.StandingAuthorized,
                TargetContentPolicy.Standard));
        var location = AuthorizationPolicyLocation.CreateTest(harness.Root);
        var valid = await File.ReadAllTextAsync(location.PolicyPath);
        await File.WriteAllTextAsync(
            location.PolicyPath,
            valid.Replace("\"formatVersion\":1", "\"formatVersion\":2", StringComparison.Ordinal));

        var reopened = await TargetPolicyCatalog.OpenTestAsync(harness.Root);

        Assert.False(reopened.WasLoadedValidly);
        Assert.Equal(AuthorizationCategory.UnknownAsk, reopened.Resolve(target).AuthorizationCategory);
    }

    [Fact]
    public async Task TamperedPolicyChecksum_FailsClosedWithoutStandingAuthority()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();
        await harness.Catalog.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(
                AuthorizationCategory.StandingAuthorized,
                TargetContentPolicy.Standard));
        var location = AuthorizationPolicyLocation.CreateTest(harness.Root);
        var valid = await File.ReadAllTextAsync(location.PolicyPath);
        var checksumMarker = "\"checksum\":\"";
        var checksumStart = valid.IndexOf(checksumMarker, StringComparison.Ordinal) + checksumMarker.Length;
        var tampered = valid.ToCharArray();
        tampered[checksumStart] = valid[checksumStart] == 'A' ? 'B' : 'A';
        await File.WriteAllTextAsync(location.PolicyPath, new string(tampered));

        var reopened = await TargetPolicyCatalog.OpenTestAsync(harness.Root);

        Assert.False(reopened.WasLoadedValidly);
        Assert.Equal(AuthorizationCategory.UnknownAsk, reopened.Resolve(target).AuthorizationCategory);
    }

    [Fact]
    public async Task PromotionFailure_LeavesPriorFileAndLiveAuthorityUnchanged()
    {
        var hook = new ThrowingPromotionHook();
        await using var harness = await TargetAuthTestHarness.CreateAsync(hook);
        var target = TargetAuthTestHarness.Candidate();
        await harness.Catalog.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(AuthorizationCategory.FamiliarAsk, TargetContentPolicy.Standard));
        var location = AuthorizationPolicyLocation.CreateTest(harness.Root);
        var priorBytes = await File.ReadAllBytesAsync(location.PolicyPath);
        hook.ThrowBeforePromotion = true;

        await Assert.ThrowsAsync<IOException>(() =>
            harness.Catalog.SetExplicitPolicyAsync(
                target,
                new TargetPolicy(
                    AuthorizationCategory.StandingAuthorized,
                    TargetContentPolicy.TrustedGame)));

        Assert.Equal(priorBytes, await File.ReadAllBytesAsync(location.PolicyPath));
        var current = harness.Catalog.Resolve(target);
        Assert.Equal(AuthorizationCategory.FamiliarAsk, current.AuthorizationCategory);
        Assert.Equal(TargetContentPolicy.Standard, current.ContentPolicy);
    }

    [Fact]
    public async Task CancelledSave_CannotCreateStandingAuthorization()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.Catalog.SetExplicitPolicyAsync(
                target,
                new TargetPolicy(
                    AuthorizationCategory.StandingAuthorized,
                    TargetContentPolicy.Standard),
                cancellation.Token));

        Assert.Equal(AuthorizationCategory.UnknownAsk, harness.Catalog.Resolve(target).AuthorizationCategory);
    }
}
