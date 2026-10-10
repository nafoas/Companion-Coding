using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CompanionCore.Memory;

namespace CompanionCore.Api;

public enum RemoteProposalRejection
{
    None = 0,
    OperationNotAllowlisted = 1,
    InvalidShape = 2,
    SourceKindNotPermitted = 3,
    LinkTargetNotInPacket = 4,
    LinkSubjectMismatch = 5,
}

internal sealed record RemoteProposalDecision(
    AppendMemoryProposal? Proposal,
    IReadOnlyList<string> Subjects,
    RemoteProposalRejection Rejection,
    int RejectedIndex)
{
    internal static RemoteProposalDecision Empty { get; } =
        new(null, Array.Empty<string>(), RemoteProposalRejection.None, -1);

    internal static RemoteProposalDecision Reject(RemoteProposalRejection rejection, int index) =>
        new(null, Array.Empty<string>(), rejection, index);
}

/// <summary>
/// Local allowlist between remote proposals and <see cref="LocalWriteGate"/>. Remote
/// output can only describe an append; every authority-bearing field (record ID,
/// timestamp, operation ID, session/application reference, provenance) is assigned
/// locally. Any forbidden proposal rejects the response's whole batch.
/// </summary>
internal static class RemoteProposalAllowlist
{
    private static readonly HashSet<MemorySourceKind> RemoteSourceKinds =
    [
        MemorySourceKind.Observed,
        MemorySourceKind.Read,
        MemorySourceKind.Inferred,
        MemorySourceKind.Guess,
    ];

    internal static RemoteProposalDecision Evaluate(
        Guid operationId,
        IReadOnlyList<MemoryProposalWire> proposals,
        ResumePacket packet,
        DateTimeOffset nowUtc,
        string providerName)
    {
        if (proposals.Count == 0)
        {
            return RemoteProposalDecision.Empty;
        }

        var packetItems = packet.Items.ToDictionary(item => item.RecordId);
        var drafts = new MemoryRecordDraft[proposals.Count];
        var subjects = new List<string>();
        for (var index = 0; index < proposals.Count; index++)
        {
            var proposal = proposals[index];
            if (!string.Equals(proposal.Operation, SemanticSchema.AppendOperationName, StringComparison.Ordinal))
            {
                return RemoteProposalDecision.Reject(RemoteProposalRejection.OperationNotAllowlisted, index);
            }

            if (proposal.TargetRecordId is not null
                || proposal.Scope is not { } scope
                || !Enum.IsDefined(scope)
                || proposal.SourceKind is not { } sourceKind
                || !Enum.IsDefined(sourceKind)
                || proposal.Confidence is not { } confidence
                || !SemanticResponseParser.IsUnitInterval(confidence)
                || string.IsNullOrWhiteSpace(proposal.SubjectKey)
                || string.IsNullOrWhiteSpace(proposal.Recollection)
                || proposal.EntityReferences?.Any(entity => entity is null) == true
                || proposal.Links?.Any(link => link is null) == true)
            {
                return RemoteProposalDecision.Reject(RemoteProposalRejection.InvalidShape, index);
            }

            if (!RemoteSourceKinds.Contains(sourceKind))
            {
                return RemoteProposalDecision.Reject(RemoteProposalRejection.SourceKindNotPermitted, index);
            }

            var links = new List<MemoryLink>();
            foreach (var link in proposal.Links ?? [])
            {
                if (!packetItems.TryGetValue(link.TargetRecordId, out var target))
                {
                    return RemoteProposalDecision.Reject(RemoteProposalRejection.LinkTargetNotInPacket, index);
                }

                if (link.Kind is MemoryLinkKind.Corrects or MemoryLinkKind.Supersedes or MemoryLinkKind.RecursWith
                    && !string.Equals(target.SubjectKey, proposal.SubjectKey, StringComparison.Ordinal))
                {
                    return RemoteProposalDecision.Reject(RemoteProposalRejection.LinkSubjectMismatch, index);
                }

                links.Add(new MemoryLink(link.TargetRecordId, link.Kind));
            }

            drafts[index] = new MemoryRecordDraft
            {
                RecordId = DeriveRecordId(operationId, index),
                CreatedAtUtc = nowUtc.ToUniversalTime(),
                Scope = scope,
                SourceKind = sourceKind,
                Confidence = confidence,
                SubjectKey = proposal.SubjectKey,
                EntityReferences = (proposal.EntityReferences ?? []).ToArray(),
                ApplicationReference = packet.ApplicationReference,
                SessionReference = packet.SessionReference,
                VisibleRecollection = proposal.Recollection,
                RetrievalMetadataJson = ProvenanceJson(operationId, providerName, index),
                Links = links.ToArray(),
            };

            if (!subjects.Contains(proposal.SubjectKey, StringComparer.Ordinal))
            {
                subjects.Add(proposal.SubjectKey);
            }
        }

        return new RemoteProposalDecision(
            new AppendMemoryProposal(operationId, drafts),
            subjects.AsReadOnly(),
            RemoteProposalRejection.None,
            -1);
    }

    /// <summary>
    /// Deterministic record identity: the same operation and proposal index always map to
    /// the same record, so a replayed append is AlreadyCommitted rather than a duplicate.
    /// </summary>
    internal static Guid DeriveRecordId(Guid operationId, int index)
    {
        Span<byte> input = stackalloc byte[64];
        var prefix = "companion.braincase.record.v1"u8;
        prefix.CopyTo(input);
        operationId.TryWriteBytes(input[prefix.Length..], bigEndian: true, out _);
        BinaryPrimitives.WriteInt32BigEndian(input[(prefix.Length + 16)..], index);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input[..(prefix.Length + 20)], hash);
        var bytes = hash[..16].ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private static string ProvenanceJson(Guid operationId, string providerName, int index)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("braincase");
            writer.WriteString("operationId", operationId.ToString("D"));
            writer.WriteNumber("proposalIndex", index);
            writer.WriteString("provider", providerName);
            writer.WriteNumber("schemaVersion", SemanticSchema.Version);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
