using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CompanionCore.Braincase;

namespace CompanionCore.Api.Tests;

/// <summary>
/// LIVE-02: Builder Prince's sign-in, settings file, Bun Budget pricing, provider selection,
/// and the first-run live check. Fake transport only; nothing here reaches the network.
/// </summary>
public sealed class BuilderBraincaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CompanionCore.Api.Tests.Builder", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeTransport(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        internal List<HttpRequestMessage> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Answer(string text = "{\"interpretation\":{\"summary\":\"A sky, grass, and a red square.\",\"observations\":[{\"region\":\"fullContext\",\"label\":\"red square\",\"confidence\":0.9}]},\"memoryProposals\":[]}") =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                new JsonObject
                {
                    ["id"] = "msg_synthetic",
                    ["type"] = "message",
                    ["role"] = "assistant",
                    ["model"] = AnthropicProviderOptions.DefaultModel,
                    ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
                    ["stop_reason"] = "end_turn",
                    ["usage"] = new JsonObject { ["input_tokens"] = 900, ["output_tokens"] = 60 },
                }.ToJsonString(),
                Encoding.UTF8,
                "application/json"),
        };

    private static Dictionary<string, string?> Env(params (string Key, string? Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value);

    // ---- sign-in ---------------------------------------------------------------------

    [Fact]
    public void SignIn_IsFound_FromTheKeyVariable_AProfile_OrASavedLogin()
    {
        static bool Detect(Dictionary<string, string?> env, bool saved = false) =>
            AnthropicSignIn.IsAvailable(key => env.GetValueOrDefault(key), _ => saved);

        Assert.True(Detect(Env(("ANTHROPIC_API_KEY", "sk-ant-synthetic"))));
        Assert.True(Detect(Env(("ANTHROPIC_AUTH_TOKEN", "synthetic"))));
        Assert.True(Detect(Env(("ANTHROPIC_PROFILE", "default"))));
        Assert.True(Detect(Env(("ANTHROPIC_CONFIG_DIR", "/synthetic/config")), saved: true));
        Assert.True(Detect(Env(("HOME", "/synthetic/home"), ("APPDATA", "C:\\Synthetic\\AppData")), saved: true));

        // An empty key is not a sign-in, and no saved login means none.
        Assert.False(Detect(Env(("ANTHROPIC_API_KEY", "  "), ("HOME", "/synthetic/home"), ("APPDATA", "C:\\Synthetic\\AppData"))));
        Assert.False(Detect(Env()));
    }

    [Fact]
    public void TheSavedLoginFolder_FollowsTheSdk()
    {
        Assert.Equal("/synthetic/config", AnthropicSignIn.ConfigDirectory(key => key == "ANTHROPIC_CONFIG_DIR" ? "/synthetic/config" : null));
        var expected = OperatingSystem.IsWindows()
            ? Path.Combine("C:\\Synthetic\\AppData", "Anthropic")
            : Path.Combine("/synthetic/home", ".config", "anthropic");
        Assert.Equal(expected, AnthropicSignIn.ConfigDirectory(key => key switch
        {
            "APPDATA" => "C:\\Synthetic\\AppData",
            "HOME" => "/synthetic/home",
            _ => null,
        }));
    }

    // ---- settings --------------------------------------------------------------------

    [Fact]
    public void Settings_AreCreatedOff_WithTheDefaultBudget_AndRoundTrip()
    {
        var (created, issue) = BuilderBraincaseSettings.LoadOrCreate(_root);
        Assert.Equal(BuilderBraincaseIssue.None, issue);
        Assert.False(created.LiveCalls);
        Assert.Equal(5m, created.DollarsPerDay);
        Assert.True(File.Exists(Path.Combine(_root, BuilderBraincaseSettings.FileName)));
        Assert.DoesNotContain("key", File.ReadAllText(Path.Combine(_root, BuilderBraincaseSettings.FileName)), StringComparison.OrdinalIgnoreCase);

        (created with { LiveCalls = true, DollarsPerDay = 2.5m, Model = "claude-haiku-5-5" }).Save(_root);
        var (loaded, _) = BuilderBraincaseSettings.LoadOrCreate(_root);
        Assert.Equal((true, 2.5m, "claude-haiku-5-5"), (loaded.LiveCalls, loaded.DollarsPerDay, loaded.Model));
    }

    [Theory]
    [InlineData("{ not json", BuilderBraincaseIssue.Invalid)]
    [InlineData("{\"liveCalls\":true,\"dollarsPerDay\":0}", BuilderBraincaseIssue.Invalid)]
    [InlineData("{\"liveCalls\":true,\"effort\":\"turbo\"}", BuilderBraincaseIssue.Invalid)]
    [InlineData("{\"liveCalls\":true,\"apiKey\":\"sk-ant-synthetic\"}", BuilderBraincaseIssue.Invalid)]
    [InlineData("{\"liveCalls\":true,\"model\":\"claude-unknown-9\"}", BuilderBraincaseIssue.UnknownModel)]
    public void DamagedOrUnknownSettings_AlwaysResolveToOff(string json, BuilderBraincaseIssue expected)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, BuilderBraincaseSettings.FileName), json);

        var (settings, issue) = BuilderBraincaseSettings.LoadOrCreate(_root);

        Assert.Equal(expected, issue);
        Assert.False(settings.LiveCalls);
    }

    // ---- pricing and selection -----------------------------------------------------

    [Fact]
    public void TheBunBudget_PricesTheDefaultModelAtItsFallbacksHigherRates()
    {
        Assert.Equal(new DailyCostBudget(5m, 5m, 25m), AnthropicPricing.BudgetFor(AnthropicProviderOptions.DefaultModel, 5m));
        Assert.Equal(new DailyCostBudget(3m, 0.10m, 0.50m), AnthropicPricing.BudgetFor("claude-haiku-5-5", 3m));
        Assert.Null(AnthropicPricing.BudgetFor("claude-unknown-9", 5m));
        Assert.Null(AnthropicPricing.BudgetFor(AnthropicProviderOptions.DefaultModel, 0m));
    }

    [Fact]
    public void Settings_UseTheSdksOwnSignIn_AndNeverHoldAKey()
    {
        var options = (BuilderBraincaseSettings.Off with { LiveCalls = true, Model = "claude-sonnet-5-5", Effort = "medium" }).ToProviderOptions();
        Assert.True(options.AllowAmbientCredentials);
        Assert.True(options.LiveCallsEnabled);
        Assert.Equal(("claude-sonnet-5-5", "medium"), (options.Model, options.Effort));
        Assert.False(BuilderBraincaseSettings.Off.ToProviderOptions().LiveCallsEnabled);
    }

    [Fact]
    public void Selection_IsTheOfflineShell_UntilLiveCallsAreTurnedOn_ThenClaudeUnderTheBudget()
    {
        using var store = new InMemoryCredentialStore();
        var off = BuilderBraincase.Compose(_root, store);
        Assert.IsType<RealSemanticProviderShell>(off.Provider);
        Assert.Null(off.Bridge);
        Assert.False(off.LiveCallsEnabled);

        (BuilderBraincaseSettings.Off with { LiveCalls = true, DollarsPerDay = 4m }).Save(_root);
        var on = BuilderBraincase.Compose(_root, store);
        using (on.Provider as IDisposable)
        {
            Assert.IsType<AnthropicSemanticProvider>(on.Provider);
            Assert.True(on.LiveCallsEnabled);
            Assert.Equal(new DailyCostBudget(4m, 5m, 25m), on.Bridge!.DailyCostBudget);
        }
    }

    // ---- signed-in calls and the live check ---------------------------------------

    [Fact]
    public async Task WithoutAStoredKey_TheSdksOwnSignInIsUsed_OnlyWhenAllowedAndPresent()
    {
        using var store = new InMemoryCredentialStore();
        using var sheet = ApiTestHarness.Sheet(Capture.Contracts.CaptureAuthorizationGrant.Issue(Guid.NewGuid(), 1, new Capture.Contracts.CaptureTargetIdentity(0x10, 42, "synthetic.exe", new string('A', 64))));
        var request = new SemanticRequest(
            Guid.NewGuid(),
            SemanticOperationKind.InterpretAttentionSheet,
            1,
            new ResumePacket(Guid.NewGuid(), ApiTestHarness.BaselineUtc, null, null, [], [], 0, false),
            new AttentionSheetDescription(sheet.Metadata.Kind, [.. sheet.Metadata.Regions.Select(region => region.Kind)], 64, 64, 64, 32, "image/png", sheet.Length, new string('0', 64)),
            sheet.EncodedImage);

        AnthropicSemanticProvider Provider(bool allowed, bool present, FakeTransport transport) => new(
            store,
            new AnthropicProviderOptions { LiveCallsEnabled = true, AllowAmbientCredentials = allowed },
            new HttpClient(transport),
            ownsHttp: true,
            baseUrl: new Uri("https://fake.invalid"),
            ambientCredentials: () => present);

        var notAllowed = new FakeTransport(_ => Answer());
        var absent = new FakeTransport(_ => Answer());
        var signedIn = new FakeTransport(_ => Answer());
        using (var provider = Provider(allowed: false, present: true, notAllowed))
        {
            Assert.Equal(ProviderUnavailableReason.CredentialsMissing, (await provider.InterpretAsync(request, CancellationToken.None)).UnavailableReason);
        }

        using (var provider = Provider(allowed: true, present: false, absent))
        {
            Assert.Equal(ProviderUnavailableReason.CredentialsMissing, (await provider.InterpretAsync(request, CancellationToken.None)).UnavailableReason);
        }

        Assert.Empty(notAllowed.Calls);
        Assert.Empty(absent.Calls);

        // A detector that saw a sign-in the SDK then cannot resolve: the request goes out with no
        // credential, the service answers 401, and that is a typed rejection, never an escape.
        var stale = new FakeTransport(http => http.Headers.Contains("x-api-key") || http.Headers.Authorization is not null
            ? Answer()
            : new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"x\"}}", Encoding.UTF8, "application/json"),
            });
        var previous = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        try
        {
            using var provider = Provider(allowed: true, present: true, stale);
            var reply = await provider.InterpretAsync(request, CancellationToken.None);
            Assert.Equal(ProviderUnavailableReason.CredentialsRejected, reply.UnavailableReason);
            Assert.False(Assert.Single(stale.Calls).Headers.Contains("x-api-key"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previous);
        }

        // Signed in through the SDK's environment variable: the call is made with that key.
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-synthetic-environment-only");
        try
        {
            using var provider = Provider(allowed: true, present: true, signedIn);
            Assert.True((await provider.InterpretAsync(request, CancellationToken.None)).IsSuccess);
            Assert.Equal("sk-ant-synthetic-environment-only", Assert.Single(Assert.Single(signedIn.Calls).Headers.GetValues("x-api-key")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previous);
        }
    }

    [Fact]
    public async Task TheLiveCheck_SendsOneSyntheticPicture_AndReportsTheParsedAnswerAndItsCost()
    {
        using var store = new InMemoryCredentialStore();
        store.Set(RealSemanticProviderShell.PrimaryCredentialName, ProtectedCredential.FromCharacters("sk-ant-synthetic-test-only"));
        var transport = new FakeTransport(_ => Answer());
        var provider = new AnthropicSemanticProvider(store, new AnthropicProviderOptions { LiveCallsEnabled = true }, new HttpClient(transport), ownsHttp: true, baseUrl: new Uri("https://fake.invalid"));

        var result = await BraincaseLiveCheck.RunWithAsync(provider, BuilderBraincaseSettings.Off, CancellationToken.None);

        Assert.Equal(LiveCheckVerdict.Passed, result.Verdict);
        Assert.Equal("A sky, grass, and a red square.", result.Summary);
        Assert.Equal(1, result.Observations);
        Assert.Equal((900, 60), (result.InputUnits, result.OutputUnits));
        Assert.Equal(new DailyCostBudget(1m, 5m, 25m).CostOf(900, 60), result.EstimatedCost);
        Assert.Single(transport.Calls);
        Assert.StartsWith("\u0089PNG", Encoding.Latin1.GetString(BraincaseLiveCheck.SyntheticScene().AsSpan(0, 4)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLiveCheck_ReportsAFailedCall_AndARefusedAnswer()
    {
        using var store = new InMemoryCredentialStore();
        store.Set(RealSemanticProviderShell.PrimaryCredentialName, ProtectedCredential.FromCharacters("sk-ant-synthetic-test-only"));
        AnthropicSemanticProvider With(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            new(store, new AnthropicProviderOptions { LiveCallsEnabled = true }, new HttpClient(new FakeTransport(respond)), ownsHttp: true, baseUrl: new Uri("https://fake.invalid"));

        var rejected = await BraincaseLiveCheck.RunWithAsync(
            With(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"x\"}}", Encoding.UTF8, "application/json") }),
            BuilderBraincaseSettings.Off,
            CancellationToken.None);
        var invalid = await BraincaseLiveCheck.RunWithAsync(With(_ => Answer("{\"interpretation\":{\"summary\":\"A scene.\",\"observations\":[{\"region\":\"lowerDialogueInventory\",\"label\":\"x\",\"confidence\":0.5}]},\"memoryProposals\":[]}")), BuilderBraincaseSettings.Off, CancellationToken.None);

        Assert.Equal((LiveCheckVerdict.ProviderFailed, ProviderUnavailableReason.CredentialsRejected), (rejected.Verdict, rejected.UnavailableReason));
        Assert.Equal(LiveCheckVerdict.InvalidResponse, invalid.Verdict);
        Assert.Equal(SemanticResponseInvalidReason.UnknownRegion, invalid.InvalidReason);
    }
}
