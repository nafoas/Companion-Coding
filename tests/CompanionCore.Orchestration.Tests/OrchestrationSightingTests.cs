using CompanionCore.Api;
using CompanionCore.Memory;
using CompanionCore.Sightings;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>M3a end to end: what the eyes keep seeing becomes one memory and a growing impression, never a log.</summary>
public sealed class OrchestrationSightingTests
{
    [Fact]
    public async Task RepeatedSightings_BecomeOneMemory_AndTheRestStayInTheRecord()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        for (var look = 0; look < 6; look++)
        {
            var count = look + 1;
            harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "Notebook Counter", "synthetic.notebooks", $"[neutral memory] the notebook counter read {count}/7.")));
            await harness.SheetAsync(changeScore: 0.6);
        }

        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "Escape Lights", "synthetic.notebooks", "[neutral memory] the hallway turned red and the exit opened.")));
        await harness.SheetAsync(changeScore: 0.6);

        var memories = await harness.Host.Repository.RetrieveBySubjectAsync("synthetic.notebooks");
        Assert.Equal(
            ["[neutral memory] the hallway turned red and the exit opened.", "[neutral memory] the notebook counter read 1/7."],
            memories.Select(memory => memory.Record.VisibleRecollection).Order(StringComparer.Ordinal));

        // Every sighting is in the subconscious record: 7 proposals and 7 observations.
        Assert.Equal(14, harness.Host.Sightings.Count);
        var impressions = harness.Host.Sightings.Impressions(memories[0].Record.ApplicationReference ?? "unknown");
        Assert.Contains(impressions, impression => impression.Thing == "synthetic.notebooks" && impression.Count == 6);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task WhatHeKeepsSeeing_FeelsLessNew_AndAPrivacyStopRecordsNothing()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        for (var look = 0; look < 4; look++)
        {
            harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "Zombie", "synthetic.zombie", "[neutral memory] a zombie shambled.")));
            await harness.SheetAsync(changeScore: 0.6);
        }

        var game = CompanionOrchestrator.GameReference("synthetic-game.exe");
        var zombie = new Sighting(harness.Time.GetUtcNow(), game, null, "zombie", ObservationKind.Other, false, "Zombie", SightingSource.Observation);
        Assert.Equal(4, harness.Host.Sightings.ImpressionOf(game, "zombie")!.Count);
        var fresh = zombie with { Thing = "never seen before" };
        Assert.True(harness.Host.Sightings.NoveltyOf(zombie) < harness.Host.Sightings.NoveltyOf(fresh) / 2);

        var before = harness.Host.Sightings.Count;
        await harness.Controller.PrivacyStopAsync();
        await harness.SettleAsync();
        Assert.Equal(before, harness.Host.Sightings.Count);
    }

    [Fact]
    public async Task APrivacyStopLandingAfterTheLook_RecordsNoSighting()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        harness.Orchestrator.Notice += (_, notice) =>
        {
            if (notice.Kind == CompanionNoticeKind.Braincase && notice.Braincase == BridgeOutcomeKind.Interpreted)
            {
                harness.Privacy.PauseAndRevoke();
            }
        };
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "Zombie", "synthetic.zombie", "[neutral memory] a zombie shambled.")));

        await harness.SheetAsync(changeScore: 0.6);

        // The memory proposal was admitted before the stop; the look's observation lands after it and is not recorded.
        Assert.Equal(1, harness.Host.Sightings.Count);
        Assert.Null(harness.Host.Sightings.ImpressionOf(CompanionOrchestrator.GameReference("synthetic-game.exe"), "zombie"));
    }

    [Fact]
    public async Task TheSituationHeIsIn_IsPartOfEverySighting()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        harness.Worker.EmitSceneSniff(harness.Time.GetUtcNow(), novelty: 0.9, label: "combat");
        await harness.SettleAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "Zombie", "synthetic.zombie", "[neutral memory] a zombie shambled.")));

        await harness.SheetAsync(changeScore: 0.6);

        var record = string.Concat(Directory.GetFiles(harness.Host.Sightings.Location.RootPath).Select(File.ReadAllText));
        Assert.Contains("\"thing\":\"zombie\"", record, StringComparison.Ordinal);
        Assert.Contains("\"situation\":\"combat\"", record, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRecord_IsInDaBunVault_AndComesBackWhenLost()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "Glowing Door", "synthetic.door", "[neutral memory] a door glowed.")));
        await harness.SheetAsync(changeScore: 0.6);
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.VaultBackedUp));
        var count = harness.Host.Sightings.Count;
        Assert.True(count >= 2);

        Directory.Delete(harness.Host.Sightings.Location.RootPath, recursive: true);
        var repair = await harness.Host.RepairAsync();

        Assert.Equal(SightingRestore.Restored, repair.Sightings);
        Assert.Equal(count, harness.Host.Sightings.Count);
    }
}
