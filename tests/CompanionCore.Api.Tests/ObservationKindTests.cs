using System.Text.Json.Nodes;
using CompanionCore.Braincase;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Api.Tests;

/// <summary>M3a: the eyes say what kind of thing they saw and whether it looks dangerous; older answers still parse.</summary>
public sealed class ObservationKindTests
{
    private static readonly AttentionRegionKind[] Regions = [AttentionRegionKind.FullContext];

    [Fact]
    public void TheEyes_AreAskedForKindAndDanger()
    {
        var observation = AnthropicSemanticProvider.ResponseSchemaFor(Regions)
            .GetProperty("properties").GetProperty("interpretation").GetProperty("properties")
            .GetProperty("observations").GetProperty("items");

        Assert.Equal(
            ["other", "creature", "character", "item", "place", "text", "interface", "event"],
            observation.GetProperty("properties").GetProperty("kind").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("boolean", observation.GetProperty("properties").GetProperty("danger").GetProperty("type").GetString());
        Assert.Contains("kind", observation.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("danger", observation.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void KindAndDanger_AreRead_AndOlderAnswersStillParse()
    {
        var operation = Guid.NewGuid();

        Assert.True(SemanticResponseParser.TryParse(Answer(operation, kind: "creature", danger: true), operation, Regions, out var modern, out _));
        Assert.Equal((ObservationKind.Creature, true), (modern!.Observations[0].Kind, modern.Observations[0].Danger));

        Assert.True(SemanticResponseParser.TryParse(Answer(operation), operation, Regions, out var older, out _));
        Assert.Equal((ObservationKind.Other, false), (older!.Observations[0].Kind, older.Observations[0].Danger));

        Assert.False(SemanticResponseParser.TryParse(Answer(operation, kind: "dragon"), operation, Regions, out _, out _));
    }

    private static string Answer(Guid operation, string? kind = null, bool? danger = null)
    {
        var observation = new JsonObject { ["region"] = "fullContext", ["label"] = "Zombie", ["confidence"] = 0.9 };
        if (kind is not null)
        {
            observation["kind"] = kind;
        }

        if (danger is not null)
        {
            observation["danger"] = danger;
        }

        return new JsonObject
        {
            ["schemaVersion"] = SemanticSchema.Version,
            ["operationId"] = operation.ToString("D"),
            ["interpretation"] = new JsonObject { ["summary"] = "s", ["observations"] = new JsonArray(observation) },
            ["memoryProposals"] = new JsonArray(),
        }.ToJsonString();
    }
}
