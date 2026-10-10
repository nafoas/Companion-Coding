using System.Security.Cryptography;
using System.Text;
using CompanionCore.Memory;

namespace CompanionCore.Keepsakes;

/// <summary>Deterministic, append-only memory records for photographs and their deletion notes.</summary>
internal static class KeepsakeRecords
{
    internal const string SubjectPrefix = "photo:";
    internal const string Caption = "[neutral photograph caption]";
    internal const string DeletionNote = "[neutral photograph deletion note]";

    internal static string Subject(Guid actionId) => $"{SubjectPrefix}{actionId:N}";

    internal static string FileName(Guid actionId) => $"{actionId:N}.png";

    internal static Guid DeriveId(Guid actionId, string purpose)
    {
        var input = Encoding.UTF8.GetBytes($"companion.keepsake.v1|{purpose}|{actionId:N}");
        var bytes = SHA256.HashData(input)[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    internal static AppendMemoryProposal Photograph(KeepsakeMetadata metadata, KeepsakeContext context) =>
        new(DeriveId(metadata.ActionId, "photo-operation"),
        [
            new MemoryRecordDraft
            {
                RecordId = DeriveId(metadata.ActionId, "photo-record"),
                CreatedAtUtc = metadata.TakenAt.ToUniversalTime(),
                Scope = context.Save is not null ? MemoryScope.Save
                    : context.Game is not null ? MemoryScope.Game
                    : context.Session is not null ? MemoryScope.Session
                    : MemoryScope.General,
                SourceKind = MemorySourceKind.Observed,
                Confidence = 1.0,
                SubjectKey = Subject(metadata.ActionId),
                GameReference = context.Game,
                SaveReference = context.Save,
                SessionReference = context.Session,
                VisibleRecollection = Caption,
                RetrievalMetadataJson = metadata.ToJson(),
            },
        ]);

    internal static AppendMemoryProposal Deletion(RetrievedMemory photograph, KeepsakeMetadata metadata, DateTimeOffset now) =>
        new(DeriveId(metadata.ActionId, "delete-operation"),
        [
            new MemoryRecordDraft
            {
                RecordId = DeriveId(metadata.ActionId, "delete-record"),
                CreatedAtUtc = now.ToUniversalTime(),
                Scope = photograph.Record.Scope,
                SourceKind = MemorySourceKind.UserCorrection,
                Confidence = 1.0,
                SubjectKey = photograph.Record.SubjectKey,
                GameReference = photograph.Record.GameReference,
                SaveReference = photograph.Record.SaveReference,
                SessionReference = photograph.Record.SessionReference,
                VisibleRecollection = DeletionNote,
                RetrievalMetadataJson = (metadata with { Deleted = true }).ToJson(),
                Links = [new MemoryLink(photograph.Record.RecordId, MemoryLinkKind.Supersedes)],
            },
        ]);
}
