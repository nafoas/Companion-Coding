using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace CompanionCore.Api.Tests;

public sealed class CredentialTests
{
    private const string Marker = "SYNTHETIC-CREDENTIAL-d41f-TEST-ONLY";

    [Fact]
    public void ProtectedCredential_RedactsFormattingAndSerialization_AndZeroesOnDispose()
    {
        var credential = ProtectedCredential.FromCharacters(Marker);

        Assert.DoesNotContain(Marker, credential.ToString());
        Assert.DoesNotContain(Marker, JsonSerializer.Serialize(credential));
        Assert.DoesNotContain(Marker, $"{credential}");
        Assert.Equal(Marker, credential.Use(0, static (secret, _) => Encoding.UTF8.GetString(secret)));

        credential.Dispose();

        Assert.True(credential.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => credential.Use(0, static (_, _) => 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ProtectedCredential_RejectsBlankValues(string value)
    {
        Assert.Throws<ArgumentException>(() => ProtectedCredential.FromCharacters(value));
    }

    [Fact]
    public void CredentialStore_StartsUnconfigured_AndSetReplaceRemoveZeroOwnedValues()
    {
        using var store = new InMemoryCredentialStore();
        const string name = RealSemanticProviderShell.PrimaryCredentialName;
        Assert.Equal(CredentialState.Unconfigured, store.GetState(name));
        Assert.False(store.TryUse(name, 0, static (_, _) => 1, out _));

        var first = ProtectedCredential.FromCharacters("first-synthetic");
        store.Set(name, first);
        Assert.Equal(CredentialState.Configured, store.GetState(name));
        Assert.True(store.TryUse(name, 0, static (secret, _) => secret.Length, out var length));
        Assert.Equal("first-synthetic".Length, length);

        store.Set(name, ProtectedCredential.FromCharacters(Marker));
        Assert.True(first.IsDisposed);
        Assert.True(store.AnyAppearsIn($"prefix {Marker} suffix"));
        Assert.False(store.AnyAppearsIn("first-synthetic"));

        Assert.True(store.Remove(name));
        Assert.False(store.Remove(name));
        Assert.Equal(CredentialState.Unconfigured, store.GetState(name));
        Assert.False(store.AnyAppearsIn(Marker));
        Assert.DoesNotContain(Marker, store.ToString());
    }

    [Theory]
    [InlineData("Upper")]
    [InlineData("with space")]
    [InlineData("../escape")]
    [InlineData("")]
    public void CredentialStore_RejectsUnsafeNames(string name)
    {
        using var store = new InMemoryCredentialStore();
        Assert.Throws<ArgumentException>(() => store.GetState(name));
    }

    [Fact]
    public async Task SyntheticCredential_NeverReachesLogsDiagnosticsJournalMemoryBackupOrOutcomes()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        harness.Credentials.Set(RealSemanticProviderShell.PrimaryCredentialName, ProtectedCredential.FromCharacters(Marker));
        var mock = new MockSemanticProvider([
            // Exception text echoing the secret, thrown synchronously and as a faulted task.
            (_, _) => throw new InvalidOperationException($"synthetic failure {Marker}"),
            (_, _) => Task.FromException<ProviderReply>(new IOException($"synthetic failure {Marker}")),
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(request.OperationId)),

            // A remote response echoing the secret in its summary or in a proposed memory.
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(request.OperationId, summary: $"echo {Marker}")),
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: [SyntheticResponses.Append($"subject.{Marker}", $"recollection {Marker}")])),

            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: [SyntheticResponses.Append("synthetic.subject.clean", "Clean synthetic recollection.")])),
        ]);
        var bridge = harness.OpenBridge(mock);
        var published = new List<SemanticInterpretation>();
        bridge.InterpretationProduced += (_, interpretation) => published.Add(interpretation);
        var outcomes = new List<BridgeOutcome>();
        var grant = harness.Grant();

        for (var index = 0; index < 4; index++)
        {
            outcomes.Add(await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant));
        }

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcomes[0].Kind);
        Assert.Equal(3, outcomes[0].Attempts);
        Assert.Equal(SemanticResponseInvalidReason.CredentialEcho, outcomes[1].InvalidReason);
        Assert.Equal(SemanticResponseInvalidReason.CredentialEcho, outcomes[2].InvalidReason);
        Assert.Equal(BridgeOutcomeKind.Interpreted, outcomes[3].Kind);
        Assert.Equal(2, published.Count);
        var diagnostics = bridge.GetDiagnosticsSnapshot();
        await harness.Repository.CreateBackupAsync();
        await harness.CloseBridgesAsync();
        await harness.Repository.DisposeAsync();

        Assert.DoesNotContain(harness.Diagnostics.Lines, line => line.Contains(Marker, StringComparison.Ordinal));
        Assert.DoesNotContain(Marker, diagnostics.ToString());
        Assert.All(outcomes, outcome => Assert.DoesNotContain(Marker, outcome.ToString()));
        Assert.All(published, interpretation => Assert.DoesNotContain(Marker, interpretation.ToString()));
        Assert.All(mock.Requests, request => Assert.DoesNotContain(Marker, request.RequestJson));

        var scanned = 0;
        foreach (var file in Directory.EnumerateFiles(harness.BasePath, "*", SearchOption.AllDirectories))
        {
            var bytes = await File.ReadAllBytesAsync(file);
            AssertNoMarker(bytes, file);
            if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ZipFile.OpenRead(file);
                foreach (var entry in archive.Entries)
                {
                    using var stream = entry.Open();
                    using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer);
                    AssertNoMarker(buffer.ToArray(), $"{file}!{entry.FullName}");
                    scanned++;
                }
            }

            scanned++;
        }

        Assert.True(scanned >= 4, "The memory store, memory journal, backup archive, and bridge journal were scanned.");
        Assert.True(File.Exists(harness.StateLocation.JournalPath));

        // Reopen so the harness can dispose cleanly; then remove the credential.
        await harness.ReopenRepositoryAsync();
        Assert.True(harness.Credentials.Remove(RealSemanticProviderShell.PrimaryCredentialName));
        Assert.Equal(CredentialState.Unconfigured, harness.Credentials.GetState(RealSemanticProviderShell.PrimaryCredentialName));
    }

    [Fact]
    public async Task JsonEscapedSecret_IsStillDetectedBeforeMemoryOrOutput()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        harness.Credentials.Set(RealSemanticProviderShell.PrimaryCredentialName, ProtectedCredential.FromCharacters(Marker));
        var escaped = "\\u0053" + Marker[1..];
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Respond(request => SyntheticResponses
                .Interpretation(
                    request.OperationId,
                    proposals: [SyntheticResponses.Append("synthetic.subject.escaped", "ESCAPED-PLACEHOLDER")])
                .Replace("ESCAPED-PLACEHOLDER", escaped, StringComparison.Ordinal)),
        ]);
        var bridge = harness.OpenBridge(mock);
        var published = 0;
        bridge.InterpretationProduced += (_, _) => published++;
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.DoesNotContain(Marker, Assert.Single(mock.Requests).RequestJson);
        Assert.Equal(BridgeOutcomeKind.InvalidResponse, outcome.Kind);
        Assert.Equal(SemanticResponseInvalidReason.CredentialEcho, outcome.InvalidReason);
        Assert.Equal(0, published);
        Assert.Empty(await harness.RetrieveAsync("synthetic.subject.escaped"));
    }

    [Fact]
    public async Task ASecretEchoedInAFieldOutsideTheDecodedText_StillInvalidatesTheWholeResponse()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        harness.Credentials.Set(RealSemanticProviderShell.PrimaryCredentialName, ProtectedCredential.FromCharacters(Marker));
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: [SyntheticResponses.Append("synthetic.subject.kind", "[neutral] recollection", sourceKind: Marker)])),
        ]);
        var bridge = harness.OpenBridge(mock);
        var published = 0;
        bridge.InterpretationProduced += (_, _) => published++;
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.InvalidResponse, outcome.Kind);
        Assert.Equal(SemanticResponseInvalidReason.CredentialEcho, outcome.InvalidReason);
        Assert.Equal(0, published);
        Assert.Empty(await harness.RetrieveAsync("synthetic.subject.kind"));
    }

    [Fact]
    public async Task RealProviderShell_WithoutCredentials_IsNeutrallyUnavailableNotACrash()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var selection = SemanticProviderFactory.Create(BraincaseConfiguration.Parse(null), harness.Credentials);
        var bridge = harness.OpenBridge(selection.Provider);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.IsType<RealSemanticProviderShell>(selection.Provider);
        Assert.Equal(BridgeOutcomeKind.Unavailable, outcome.Kind);
        Assert.Equal(ProviderUnavailableReason.CredentialsMissing, outcome.UnavailableReason);
        Assert.Null(outcome.Interpretation);
    }

    [Fact]
    public async Task RealProviderShell_WithACredential_StillCannotMakeALiveCall()
    {
        using var store = new InMemoryCredentialStore();
        store.Set(RealSemanticProviderShell.PrimaryCredentialName, ProtectedCredential.FromCharacters(Marker));
        var shell = new RealSemanticProviderShell(store);
        await using var harness = await ApiTestHarness.CreateAsync();
        var grant = harness.Grant();
        using var sheet = ApiTestHarness.Sheet(grant);
        var request = new SemanticRequest(
            Guid.NewGuid(),
            SemanticOperationKind.InterpretAttentionSheet,
            1,
            new ResumePacket(Guid.NewGuid(), ApiTestHarness.BaselineUtc, null, null, [], [], 0, false),
            new AttentionSheetDescription(
                sheet.Metadata.Kind,
                [.. sheet.Metadata.Regions.Select(region => region.Kind)],
                64,
                64,
                64,
                32,
                "image/png",
                sheet.Length,
                new string('0', 64)),
            sheet.EncodedImage);

        var reply = await shell.InterpretAsync(request, CancellationToken.None);

        Assert.False(reply.IsSuccess);
        Assert.Equal(ProviderUnavailableReason.LiveCallsDisabled, reply.UnavailableReason);
    }

    [Fact]
    public void BridgeAssemblies_ReferenceNoNetworkingAssembly()
    {
        string[] forbidden =
        [
            "System.Net.Http",
            "System.Net.Sockets",
            "System.Net.Requests",
            "System.Net.WebClient",
            "System.Net.WebSockets",
            "System.Net.WebSockets.Client",
            "System.Net.Security",
            "System.Net.Quic",

            // The live Claude adapter lives in its own assembly (LIVE-01); the bridge never references it.
            "Anthropic",
            "CompanionCore.Braincase.Anthropic",
        ];
        var assemblies = new[]
        {
            typeof(ApiBridge).Assembly,
            typeof(CompanionCore.Memory.LocalWriteGate).Assembly,
            typeof(CompanionCore.Privacy.RuntimePrivacyState).Assembly,
            typeof(CompanionCore.Capture.Contracts.AttentionSheet).Assembly,
            typeof(CompanionCore.Runtime.Diagnostics.IDiagnosticsSink).Assembly,
        };

        foreach (var assembly in assemblies)
        {
            var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
            Assert.DoesNotContain(references, name => forbidden.Contains(name, StringComparer.Ordinal));
        }
    }

    private static void AssertNoMarker(byte[] bytes, string source)
    {
        Assert.True(
            bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Marker)) < 0
            && bytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes(Marker)) < 0,
            $"The synthetic credential appeared in {source}.");
    }
}
