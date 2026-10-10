namespace CompanionCore.Api.Tests;

public sealed class ConfigurationTests
{
    [Theory]
    [InlineData(null, BraincaseProviderKind.Real, BraincaseConfigurationIssue.None)]
    [InlineData("", BraincaseProviderKind.Real, BraincaseConfigurationIssue.None)]
    [InlineData(" real ", BraincaseProviderKind.Real, BraincaseConfigurationIssue.None)]
    [InlineData("MOCK", BraincaseProviderKind.Mock, BraincaseConfigurationIssue.None)]
    [InlineData("disabled", BraincaseProviderKind.Disabled, BraincaseConfigurationIssue.None)]
    [InlineData("replay", BraincaseProviderKind.Disabled, BraincaseConfigurationIssue.ReplayDirectoryMissing)]
    [InlineData("gpt-live-please", BraincaseProviderKind.Disabled, BraincaseConfigurationIssue.UnknownProvider)]
    [InlineData("\u0000‮", BraincaseProviderKind.Disabled, BraincaseConfigurationIssue.UnknownProvider)]
    public void Parse_NeverThrowsAndDegradesToNeutralStates(
        string? setting,
        BraincaseProviderKind expectedKind,
        BraincaseConfigurationIssue expectedIssue)
    {
        var configuration = BraincaseConfiguration.Parse(setting);

        Assert.Equal(expectedKind, configuration.ProviderKind);
        Assert.Equal(expectedIssue, configuration.Issue);
    }

    [Fact]
    public void Factory_BuildsEachConfiguredProvider()
    {
        using var credentials = new InMemoryCredentialStore();
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "replay");

        Assert.IsType<RealSemanticProviderShell>(SemanticProviderFactory.Create(BraincaseConfiguration.Parse("real"), credentials).Provider);
        Assert.IsType<MockSemanticProvider>(SemanticProviderFactory.Create(BraincaseConfiguration.Parse("mock"), credentials).Provider);
        Assert.IsType<DisabledSemanticProvider>(SemanticProviderFactory.Create(BraincaseConfiguration.Parse("disabled"), credentials).Provider);
        var replay = SemanticProviderFactory.Create(BraincaseConfiguration.Parse("replay", fixtures), credentials);
        Assert.Equal(2, Assert.IsType<ReplaySemanticProvider>(replay.Provider).FixtureCount);
        Assert.Equal(BraincaseConfigurationIssue.None, replay.Issue);
    }

    public static TheoryData<string> InvalidFixtureSets => new()
    {
        "missing-directory",
        "relative-directory",
        "empty-directory",
        "not-json",
        "unknown-member",
        "duplicate-shape",
        "success-without-response",
        "success-with-invalid-response",
        "success-with-unknown-region",
        "transient-with-response",
        "unavailable-without-reason",
        "too-many-replies",
        "oversized-file",
    };

    [Theory]
    [MemberData(nameof(InvalidFixtureSets))]
    public async Task InvalidReplayFixtures_DegradeToANeutralDisabledProvider(string scenario)
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var directory = Path.Combine(harness.BasePath, "fixtures-" + scenario);
        var setting = scenario switch
        {
            "missing-directory" => directory,
            "relative-directory" => "fixtures/relative",
            _ => directory,
        };
        if (scenario is not "missing-directory" and not "relative-directory")
        {
            Directory.CreateDirectory(directory);
            WriteFixtures(directory, scenario);
        }

        var selection = SemanticProviderFactory.Create(BraincaseConfiguration.Parse("replay", setting), harness.Credentials);

        Assert.Equal(BraincaseConfigurationIssue.ReplayFixturesInvalid, selection.Issue);
        var disabled = Assert.IsType<DisabledSemanticProvider>(selection.Provider);
        Assert.Equal(ProviderUnavailableReason.FixturesUnavailable, disabled.Reason);

        var bridge = harness.OpenBridge(selection.Provider);
        var grant = harness.Grant();
        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);
        Assert.Equal(BridgeOutcomeKind.Unavailable, outcome.Kind);
        Assert.Equal(ProviderUnavailableReason.FixturesUnavailable, outcome.UnavailableReason);
    }

    [Fact]
    public async Task ReplayWithoutAMatchingShape_IsNeutrallyUnavailable()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var replay = ReplaySemanticProvider.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "replay"));
        var bridge = harness.OpenBridge(replay);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(
            ApiTestHarness.Sheet(grant),
            grant,
            new ResumeContext(["synthetic.subject.unmatched"]));

        Assert.Equal(BridgeOutcomeKind.Unavailable, outcome.Kind);
        Assert.Equal(ProviderUnavailableReason.NoMatchingFixture, outcome.UnavailableReason);
    }

    [Fact]
    public async Task MockConfiguration_KeepsDevelopmentUsableWithNoKey()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var selection = SemanticProviderFactory.Create(BraincaseConfiguration.Parse("mock"), harness.Credentials);
        var bridge = harness.OpenBridge(selection.Provider);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.Equal("Synthetic interpretation.", outcome.Interpretation!.Summary);
        Assert.Equal(CredentialState.Unconfigured, harness.Credentials.GetState(RealSemanticProviderShell.PrimaryCredentialName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task InvalidBridgeBounds_AreRejectedAtOpen(int attempts)
    {
        await using var harness = await ApiTestHarness.CreateAsync();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            harness.OpenBridge(new MockSemanticProvider(), new BridgeOptions { MaximumAttempts = attempts }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            harness.OpenBridge(new MockSemanticProvider(), new BridgeOptions { DailyInputUnitBudget = 0 }));
    }

    private static void WriteFixtures(string directory, string scenario)
    {
        const string valid = """
            {"fixtureVersion":1,"name":"n","match":{"operationKind":"interpretAttentionSheet","sheetKind":"orientation","regionKinds":["fullContext"],"subjects":[]},"replies":[REPLIES]}
            """;
        const string success = """
            {"kind":"success","response":{"schemaVersion":1,"operationId":"00000000-0000-0000-0000-000000000000","interpretation":{"summary":"s","observations":[{"region":"REGION","label":"l","confidence":0.5}]}}}
            """;
        string Fixture(string replies) => valid.Replace("REPLIES", replies, StringComparison.Ordinal);
        string Success(string region = "fullContext") => success.Replace("REGION", region, StringComparison.Ordinal);
        void Write(string name, string content) => File.WriteAllText(Path.Combine(directory, name + ".replay.json"), content);

        switch (scenario)
        {
            case "empty-directory":
                break;
            case "not-json":
                Write("a", "not json");
                break;
            case "unknown-member":
                Write("a", Fixture(Success()).Replace("\"name\":\"n\"", "\"name\":\"n\",\"liveEndpoint\":\"x\"", StringComparison.Ordinal));
                break;
            case "duplicate-shape":
                Write("a", Fixture(Success()));
                Write("b", Fixture(Success()));
                break;
            case "success-without-response":
                Write("a", Fixture("""{"kind":"success"}"""));
                break;
            case "success-with-invalid-response":
                Write("a", Fixture(Success().Replace("\"schemaVersion\":1", "\"schemaVersion\":9", StringComparison.Ordinal)));
                break;
            case "success-with-unknown-region":
                Write("a", Fixture(Success("upperLeft")));
                break;
            case "transient-with-response":
                Write("a", Fixture(Success().Replace("\"kind\":\"success\"", "\"kind\":\"transient\"", StringComparison.Ordinal)));
                break;
            case "unavailable-without-reason":
                Write("a", Fixture("""{"kind":"unavailable"}"""));
                break;
            case "too-many-replies":
                Write("a", Fixture(string.Join(',', Enumerable.Repeat("""{"kind":"transient"}""", 17))));
                break;
            case "oversized-file":
                Write("a", Fixture(Success()) + new string(' ', ReplaySemanticProvider.MaximumFixtureBytes));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }
    }
}
