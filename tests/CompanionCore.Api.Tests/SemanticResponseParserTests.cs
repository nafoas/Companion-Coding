using System.Text.Json.Nodes;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Api.Tests;

public sealed class SemanticResponseParserTests
{
    private static readonly Guid Operation = Guid.Parse("0192a7f0-0000-7000-8000-000000000001");
    private static readonly AttentionRegionKind[] FullOnly = [AttentionRegionKind.FullContext];

    public static TheoryData<string, SemanticResponseInvalidReason> InvalidResponses()
    {
        var data = new TheoryData<string, SemanticResponseInvalidReason>
        {
            { "{", SemanticResponseInvalidReason.Malformed },
            { "null", SemanticResponseInvalidReason.Malformed },
            { "[]", SemanticResponseInvalidReason.Malformed },
            { Mutate(root => root["remoteSessionId"] = "capsule"), SemanticResponseInvalidReason.Malformed },
            { Mutate(root => root["resumeCapsule"] = new JsonObject()), SemanticResponseInvalidReason.Malformed },
            { Mutate(root => Proposal(root)["recordId"] = Guid.NewGuid().ToString()), SemanticResponseInvalidReason.Malformed },
            { Mutate(root => Proposal(root)["createdAtUtc"] = "2026-10-10T00:00:00Z"), SemanticResponseInvalidReason.Malformed },
            { Mutate(root => Proposal(root)["sourceKind"] = 1), SemanticResponseInvalidReason.Malformed },
            { Mutate(root => Proposal(root)["sourceKind"] = "authoritative"), SemanticResponseInvalidReason.Malformed },
            { Mutate(root => root.Remove("interpretation")), SemanticResponseInvalidReason.Malformed },
            { Mutate(root => Interpretation(root)["summary"] = null), SemanticResponseInvalidReason.Malformed },
            { Mutate(root => root["schemaVersion"] = "1"), SemanticResponseInvalidReason.Malformed },
            { Valid().Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal), SemanticResponseInvalidReason.Malformed },
            { Valid() + "//", SemanticResponseInvalidReason.Malformed },
            { Mutate(root => root["schemaVersion"] = 2), SemanticResponseInvalidReason.UnsupportedSchemaVersion },
            { Mutate(root => root["operationId"] = Guid.NewGuid().ToString()), SemanticResponseInvalidReason.OperationMismatch },
            { Mutate(root => Interpretation(root)["summary"] = " "), SemanticResponseInvalidReason.OutOfBounds },
            { Mutate(root => Interpretation(root)["summary"] = new string('s', 2001)), SemanticResponseInvalidReason.OutOfBounds },
            { Mutate(root => Interpretation(root)["summary"] = "bell\u0007"), SemanticResponseInvalidReason.OutOfBounds },
            { Mutate(root => Observation(root)["confidence"] = 1.5), SemanticResponseInvalidReason.OutOfBounds },
            { Mutate(root => Observation(root)["label"] = new string('l', 201)), SemanticResponseInvalidReason.OutOfBounds },
            { Mutate(root => Observation(root)["region"] = "upperLeft"), SemanticResponseInvalidReason.UnknownRegion },
            { Mutate(root => Interpretation(root)["observations"] = Repeat(17, ObservationNode)), SemanticResponseInvalidReason.OutOfBounds },
            { Mutate(root => root["memoryProposals"] = Repeat(9, ProposalNode)), SemanticResponseInvalidReason.OutOfBounds },
            { Mutate(root => root["usage"] = new JsonObject { ["inputUnits"] = -1, ["outputUnits"] = 0 }), SemanticResponseInvalidReason.OutOfBounds },
            { Mutate(root => Interpretation(root)["summary"] = new string('s', 70_000)), SemanticResponseInvalidReason.TooLarge },
        };
        return data;
    }

    [Theory]
    [MemberData(nameof(InvalidResponses))]
    public void InvalidResponses_AreRejectedWhole(string json, SemanticResponseInvalidReason expected)
    {
        Assert.False(SemanticResponseParser.TryParse(json, Operation, FullOnly, out var parsed, out var reason));
        Assert.Null(parsed);
        Assert.Equal(expected, reason);
    }

    [Fact]
    public void ValidResponse_ParsesEveryField()
    {
        Assert.True(SemanticResponseParser.TryParse(Valid(), Operation, FullOnly, out var parsed, out var reason));
        Assert.Equal(SemanticResponseInvalidReason.None, reason);
        Assert.Equal("Neutral synthetic summary.", parsed!.Summary);
        Assert.Equal(AttentionRegionKind.FullContext, Assert.Single(parsed.Observations).Region);
        Assert.Equal("synthetic.subject.parsed", Assert.Single(parsed.Proposals).SubjectKey);
        Assert.Equal(10, parsed.InputUnits);
        Assert.Equal(5, parsed.OutputUnits);
    }

    [Fact]
    public async Task InvalidResponse_EndToEnd_PublishesNothingWritesNothingAndIsNotRetried()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Respond(request =>
            {
                var root = JsonNode.Parse(SyntheticResponses.Interpretation(
                    request.OperationId,
                    proposals: [SyntheticResponses.Append("synthetic.subject.invalid", "Invalid synthetic recollection.")]))!.AsObject();
                root["remoteSessionId"] = "capsule";
                return root.ToJsonString();
            }),
        ]);
        var bridge = harness.OpenBridge(mock);
        var published = 0;
        bridge.InterpretationProduced += (_, _) => published++;
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(BridgeOutcomeKind.InvalidResponse, outcome.Kind);
        Assert.Equal(SemanticResponseInvalidReason.Malformed, outcome.InvalidReason);
        Assert.Equal(0, published);
        Assert.Equal(1, mock.CallCount);
        Assert.Empty(await harness.RetrieveAsync("synthetic.subject.invalid"));
        Assert.False(bridge.NapStatus.IsNapping);
    }

    private static string Valid() =>
        SyntheticResponses.Interpretation(
            Operation,
            proposals: [ProposalNode()],
            usage: new JsonObject { ["inputUnits"] = 10, ["outputUnits"] = 5 });

    private static JsonObject ProposalNode() =>
        SyntheticResponses.Append("synthetic.subject.parsed", "Parsed synthetic recollection.");

    private static JsonObject ObservationNode() =>
        new() { ["region"] = "fullContext", ["label"] = "synthetic", ["confidence"] = 0.5 };

    private static JsonArray Repeat(int count, Func<JsonObject> create) =>
        new([.. Enumerable.Range(0, count).Select(_ => (JsonNode)create())]);

    private static string Mutate(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(Valid())!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static JsonObject Interpretation(JsonObject root) => root["interpretation"]!.AsObject();

    private static JsonObject Observation(JsonObject root) => Interpretation(root)["observations"]![0]!.AsObject();

    private static JsonObject Proposal(JsonObject root) => root["memoryProposals"]![0]!.AsObject();
}
