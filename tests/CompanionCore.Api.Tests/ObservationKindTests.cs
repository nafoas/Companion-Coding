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

    [Fact]
    public void Encounters_AreAskedFor_ReadWhenPresent_AndOptionalWhenNot()
    {
        var interpretation = AnthropicSemanticProvider.ResponseSchemaFor(Regions)
            .GetProperty("properties").GetProperty("interpretation");
        var encounter = interpretation.GetProperty("properties").GetProperty("encounters").GetProperty("items");
        Assert.Equal(["ongoing", "bossWon", "bossHurt", "bossLost"], encounter.GetProperty("properties").GetProperty("outcome").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("encounters", interpretation.GetProperty("required").EnumerateArray().Select(item => item.GetString()));

        var operation = Guid.NewGuid();
        Assert.True(SemanticResponseParser.TryParse(Answer(operation, encounters: new JsonArray(new JsonObject { ["threat"] = "Zombie", ["outcome"] = "bossWon" })), operation, Regions, out var fight, out _));
        Assert.Equal([new SemanticEncounter("Zombie", EncounterOutcome.BossWon)], fight!.Encounters);

        Assert.True(SemanticResponseParser.TryParse(Answer(operation), operation, Regions, out var older, out _));
        Assert.Empty(older!.Encounters);

        Assert.False(SemanticResponseParser.TryParse(Answer(operation, encounters: new JsonArray(new JsonObject { ["threat"] = "Zombie", ["outcome"] = "bossSulked" })), operation, Regions, out _, out _));
        Assert.False(SemanticResponseParser.TryParse(Answer(operation, encounters: new JsonArray(new JsonObject { ["threat"] = new string('z', 500), ["outcome"] = "ongoing" })), operation, Regions, out _, out _));
        var tooMany = new JsonArray([.. Enumerable.Range(0, 20).Select(index => (JsonNode)new JsonObject { ["threat"] = $"t{index}", ["outcome"] = "ongoing" })]);
        Assert.False(SemanticResponseParser.TryParse(Answer(operation, encounters: tooMany), operation, Regions, out _, out _));
    }

    private static string Answer(Guid operation, string? kind = null, bool? danger = null, JsonArray? encounters = null)
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
            ["interpretation"] = encounters is null
                ? new JsonObject { ["summary"] = "s", ["observations"] = new JsonArray(observation) }
                : new JsonObject { ["summary"] = "s", ["observations"] = new JsonArray(observation), ["encounters"] = encounters },
            ["memoryProposals"] = new JsonArray(),
        }.ToJsonString();
    }
}
