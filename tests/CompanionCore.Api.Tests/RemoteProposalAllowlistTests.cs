using System.Text.Json.Nodes;
using CompanionCore.Memory;

namespace CompanionCore.Api.Tests;

/// <summary>
/// Remote output may only append through the local allowlist. Any forbidden proposal
/// rejects the response's whole batch, committed memory is untouched, and the neutral
/// interpretation is still delivered once.
/// </summary>
public sealed class RemoteProposalAllowlistTests
{
    private const string Seeded = "synthetic.subject.seeded";

    public static TheoryData<string, RemoteProposalRejection> ForbiddenProposals => new()
    {
        { "update", RemoteProposalRejection.OperationNotAllowlisted },
        { "delete", RemoteProposalRejection.OperationNotAllowlisted },
        { "overwrite", RemoteProposalRejection.OperationNotAllowlisted },
        { "replaceCheckpoint", RemoteProposalRejection.OperationNotAllowlisted },
        { "appendWithTarget", RemoteProposalRejection.InvalidShape },
        { "appendMissingRecollection", RemoteProposalRejection.InvalidShape },
        { "appendConfidenceOutOfRange", RemoteProposalRejection.InvalidShape },
        { "told", RemoteProposalRejection.SourceKindNotPermitted },
        { "remembered", RemoteProposalRejection.SourceKindNotPermitted },
        { "userCorrection", RemoteProposalRejection.SourceKindNotPermitted },
        { "integration", RemoteProposalRejection.SourceKindNotPermitted },
        { "foreignLink", RemoteProposalRejection.LinkTargetNotInPacket },
        { "crossSubjectCorrection", RemoteProposalRejection.LinkSubjectMismatch },
        { "validPlusDelete", RemoteProposalRejection.OperationNotAllowlisted },
    };

    [Theory]
    [MemberData(nameof(ForbiddenProposals))]
    public async Task ForbiddenRemoteProposals_AreRejectedLocallyAndNothingIsCommitted(
        string scenario,
        RemoteProposalRejection expected)
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var seededId = await harness.SeedMemoryAsync(Seeded, "Seeded synthetic recollection.");
        var seededBefore = Assert.Single(await harness.RetrieveAsync(Seeded));
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: Proposals(scenario, seededId))),
        ]);
        var bridge = harness.OpenBridge(mock);
        var published = 0;
        bridge.InterpretationProduced += (_, _) => published++;
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(
            ApiTestHarness.Sheet(grant),
            grant,
            new ResumeContext([Seeded]));

        Assert.Equal(BridgeOutcomeKind.Interpreted, outcome.Kind);
        Assert.Equal(expected, outcome.Memory.Rejection);
        Assert.Null(outcome.Memory.GateResult);
        Assert.Equal(1, published);
        var seededAfter = Assert.Single(await harness.RetrieveAsync(Seeded));
        Assert.Equal(seededBefore.Record.RecordId, seededAfter.Record.RecordId);
        Assert.Equal(seededBefore.RecordChecksum, seededAfter.RecordChecksum);
        Assert.True(seededAfter.IsCurrent);
        Assert.Empty(await harness.RetrieveAsync("synthetic.subject.remote"));
        Assert.Equal(1, bridge.GetDiagnosticsSnapshot().RejectedProposalBatches);
    }

    [Fact]
    public async Task SameSubjectCorrectionOfAPacketRecord_IsAppendedAndTheOriginalSurvives()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var seededId = await harness.SeedMemoryAsync(Seeded, "Seeded synthetic recollection.");
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals:
                [
                    SyntheticResponses.Append(
                        Seeded,
                        "Corrected synthetic recollection.",
                        sourceKind: "inferred",
                        links: new JsonArray(SyntheticResponses.Link(seededId, "corrects"))),
                ])),
        ]);
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(
            ApiTestHarness.Sheet(grant),
            grant,
            new ResumeContext([Seeded]));

        Assert.Equal(WriteGateStatus.Committed, outcome.Memory.GateResult!.Status);
        var retrieved = await harness.RetrieveAsync(Seeded);
        Assert.Equal(2, retrieved.Count);
        Assert.True(retrieved[0].IsCurrent);
        Assert.Equal("Corrected synthetic recollection.", retrieved[0].Record.VisibleRecollection);
        Assert.Equal(seededId, retrieved[1].Record.RecordId);
        Assert.False(retrieved[1].IsCurrent);
        Assert.Contains(seededId.ToString("D"), Assert.Single(mock.Requests).RequestJson);
    }

    [Fact]
    public async Task OversizedRemoteRecollection_IsRejectedByTheWriteGateNotCommitted()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        var mock = new MockSemanticProvider([
            MockSemanticProvider.Respond(request => SyntheticResponses.Interpretation(
                request.OperationId,
                proposals: [SyntheticResponses.Append("synthetic.subject.remote", new string('x', 17 * 1024))])),
        ]);
        var bridge = harness.OpenBridge(mock);
        var grant = harness.Grant();

        var outcome = await bridge.InterpretAttentionSheetAsync(ApiTestHarness.Sheet(grant), grant);

        Assert.Equal(WriteGateStatus.Rejected, outcome.Memory.GateResult!.Status);
        Assert.Equal(WriteGateRejectionReason.InvalidProposal, outcome.Memory.GateResult.RejectionReason);
        Assert.Empty(await harness.RetrieveAsync("synthetic.subject.remote"));
    }

    private static JsonObject[] Proposals(string scenario, Guid seededId) => scenario switch
    {
        "update" => [Operation("memory.update.v1", seededId)],
        "delete" => [Operation("memory.delete.v1", seededId)],
        "overwrite" => [Operation("memory.overwriteStore.v1", null)],
        "replaceCheckpoint" => [Operation("journal.replaceCheckpoint.v1", null)],
        "appendWithTarget" => [WithTarget(Remote(), seededId)],
        "appendMissingRecollection" => [Without(Remote(), "recollection")],
        "appendConfidenceOutOfRange" => [SyntheticResponses.Append("synthetic.subject.remote", "Remote.", confidence: 1.5)],
        "told" or "remembered" or "userCorrection" or "integration" =>
            [SyntheticResponses.Append("synthetic.subject.remote", "Remote.", sourceKind: scenario)],
        "foreignLink" =>
            [SyntheticResponses.Append("synthetic.subject.remote", "Remote.", links: new JsonArray(SyntheticResponses.Link(Guid.NewGuid(), "source")))],
        "crossSubjectCorrection" =>
            [SyntheticResponses.Append("synthetic.subject.remote", "Remote.", links: new JsonArray(SyntheticResponses.Link(seededId, "corrects")))],
        "validPlusDelete" => [Remote(), Operation("memory.delete.v1", seededId)],
        _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
    };

    private static JsonObject Remote() => SyntheticResponses.Append("synthetic.subject.remote", "Remote synthetic recollection.");

    private static JsonObject Operation(string operation, Guid? target)
    {
        var proposal = new JsonObject { ["operation"] = operation };
        if (target is { } id)
        {
            proposal["targetRecordId"] = id.ToString("D");
        }

        return proposal;
    }

    private static JsonObject WithTarget(JsonObject proposal, Guid target)
    {
        proposal["targetRecordId"] = target.ToString("D");
        return proposal;
    }

    private static JsonObject Without(JsonObject proposal, string member)
    {
        proposal.Remove(member);
        return proposal;
    }
}
