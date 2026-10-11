using System.Text.Json.Nodes;
using CompanionCore.Api;
using CompanionCore.Capture.Contracts;
using CompanionCore.Memory;
using CompanionCore.Vault;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// Stage 11 failure-mode hardening (Roadmap: capture failure, disk full, corrupted state,
/// crash loop, upgrade rollback). Each failure is contained, no committed memory is lost,
/// and the one Conversation Thread lineage survives whenever it can be read.
/// </summary>
public sealed class OrchestrationFailureTests
{
    private const string Conversation = CompanionOrchestrator.ConversationStateName;
    private static readonly string Preserved = Conversation + CompanionOrchestrator.UnreadableSuffix;

    [Fact]
    public async Task ACheckpointFromANewerBuild_IsPreservedAside_AndTheLineageIsKept()
    {
        await using var harness = await CreateAsync();
        var lineage = (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage;
        await harness.Host.DisposeAsync();

        // A rollback: the checkpoint was written by a newer build this one cannot restore.
        var current = (await harness.Host.State.GetAsync(Conversation)).Payload.ToArray();
        var newer = JsonNode.Parse(current)!.AsObject();
        newer["Version"] = 99;
        var newerBytes = System.Text.Encoding.UTF8.GetBytes(newer.ToJsonString());
        await harness.Host.State.PutAsync(Conversation, newerBytes);
        await harness.OpenHostAsync();

        Assert.Equal(lineage, (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage);
        var preserved = await harness.Host.State.GetAsync(Preserved);
        Assert.Equal(StateStatus.Verified, preserved.Status);
        Assert.Equal(newerBytes, preserved.Payload.ToArray());
        Assert.Contains(harness.Notices, n => n.Kind == CompanionNoticeKind.Fault && n.Key == $"UnreadableState:{Conversation}");
        Assert.Equal(1, harness.Orchestrator.Faults);

        // The next start reads its own checkpoint; the preserved copy stays exactly as it was.
        await harness.ReopenHostAsync();
        Assert.Equal(lineage, (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage);
        Assert.Equal(newerBytes, (await harness.Host.State.GetAsync(Preserved)).Payload.ToArray());
        Assert.Equal(0, harness.Orchestrator.Faults);

        // A later unreadable checkpoint never replaces the first preserved copy.
        await harness.Host.DisposeAsync();
        await File.WriteAllBytesAsync(harness.Host.State.PathFor(Conversation), "a second damage"u8.ToArray());
        await harness.OpenHostAsync();
        Assert.Equal(newerBytes, (await harness.Host.State.GetAsync(Preserved)).Payload.ToArray());
        Assert.Equal(1, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task AnUnreadableCheckpointWithAnEmptyLineage_StartsAFreshLineage()
    {
        await using var harness = await CreateAsync();
        await harness.Host.DisposeAsync();
        var current = JsonNode.Parse((await harness.Host.State.GetAsync(Conversation)).Payload.ToArray())!.AsObject();
        current["Version"] = 99;
        current["CoordinatorId"] = Guid.Empty.ToString();
        await harness.Host.State.PutAsync(Conversation, System.Text.Encoding.UTF8.GetBytes(current.ToJsonString()));

        await harness.OpenHostAsync();

        Assert.NotEqual(Guid.Empty, (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage);
        Assert.Equal(1, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task ADamagedCheckpoint_IsPreservedRaw_AndVaultBackupsKeepWorking()
    {
        await using var harness = await CreateAsync();
        await harness.Host.DisposeAsync();
        var garbage = "damaged state envelope"u8.ToArray();
        await File.WriteAllBytesAsync(harness.Host.State.PathFor(Conversation), garbage);

        await harness.OpenHostAsync();

        Assert.Equal(garbage, (await harness.Host.State.GetAsync(Preserved)).Payload.ToArray());
        Assert.NotEqual(Guid.Empty, (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage);
        await harness.AuthorizeAsync();
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded));
        Assert.True(harness.Has(CompanionNoticeKind.VaultBackedUp));
        Assert.False(harness.Has(CompanionNoticeKind.VaultBackupFailed));
    }

    [Fact]
    public async Task AnUnparseableSessionsList_IsPreserved_AndStartupContinues()
    {
        await using var harness = await CreateAsync();
        await harness.Host.DisposeAsync();
        await harness.Host.State.PutAsync(CompanionOrchestrator.SessionsStateName, "not json"u8.ToArray());

        await harness.OpenHostAsync();

        var preserved = await harness.Host.State.GetAsync(CompanionOrchestrator.SessionsStateName + CompanionOrchestrator.UnreadableSuffix);
        Assert.Equal("not json"u8.ToArray(), preserved.Payload.ToArray());
        Assert.Empty((await harness.Orchestrator.GetSnapshotAsync()).UnconsolidatedSessions);
        await harness.AuthorizeAsync();
        await harness.SheetAsync();
        Assert.Equal(1, harness.Provider.CallCount);
    }

    [Fact]
    public async Task AFailedCheckpointWrite_IsContained_AndMemoryKeepsWorking()
    {
        await using var harness = await CreateAsync(new OrchestratorOptions { SemanticCadence = SemanticCadence.Unthrottled });
        await harness.AuthorizeAsync();

        // Simulate a full or failing disk for the checkpoint: its path cannot be replaced.
        var path = harness.Host.State.PathFor(Conversation);
        File.Delete(path);
        Directory.CreateDirectory(path);
        harness.Time.Advance(TimeSpan.FromSeconds(31));
        await Assert.ThrowsAnyAsync<Exception>(() => harness.Orchestrator.TickAsync());
        Assert.True(harness.Orchestrator.Faults > 0);

        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "disk", "synthetic.disk", "[neutral memory] still remembered.")));
        await harness.SheetAsync();
        Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = "synthetic.disk" }));

        // Space returns: the next checkpoint succeeds.
        Directory.Delete(path);
        harness.Time.Advance(TimeSpan.FromSeconds(31));
        await harness.Orchestrator.TickAsync();
        Assert.Equal(StateStatus.Verified, (await harness.Host.State.GetAsync(Conversation)).Status);
    }

    [Fact]
    public async Task ACrashLoop_NeverLosesOrDuplicatesMemory_AndKeepsOneLineage()
    {
        await using var harness = await CreateAsync();
        var lineage = (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage;

        for (var cycle = 0; cycle < 5; cycle++)
        {
            await harness.AuthorizeAsync();
            var subject = $"synthetic.cycle{cycle}";
            harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, $"cycle{cycle}", subject, $"[neutral memory] cycle {cycle}.")));
            await harness.SheetAsync();

            // An abrupt restart while the target is still authorized.
            await harness.ReopenHostAsync();
            Assert.Equal(lineage, (await harness.Orchestrator.GetSnapshotAsync()).ConversationLineage);
            await harness.Controller.EndSessionAsync();
            await harness.SettleAsync();
        }

        for (var cycle = 0; cycle < 5; cycle++)
        {
            Assert.Single(await harness.Host.Repository.RetrieveAsync(new MemoryQuery { SubjectPrefix = $"synthetic.cycle{cycle}" }));
        }

        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task ACaptureWorkerFault_IsAbsorbed_AndCaptureResumesCleanly()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        await harness.SheetAsync();
        Assert.Equal(1, harness.Provider.CallCount);

        // A faulted worker produces nothing; the pipeline stays healthy and idle.
        harness.Worker.RaiseStatus(CaptureWorkerStatus.Faulted);
        await harness.SettleAsync();
        Assert.Equal(1, harness.Provider.CallCount);
        Assert.Equal(0, harness.Orchestrator.Faults);
        Assert.NotNull((await harness.Orchestrator.GetSnapshotAsync()).TargetSessionId);

        harness.Worker.RaiseStatus(CaptureWorkerStatus.Running);
        await harness.SheetAsync();
        Assert.Equal(2, harness.Provider.CallCount);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }
}
