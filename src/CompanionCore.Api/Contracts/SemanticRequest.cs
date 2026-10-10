using System.Text.Json;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Api;

/// <summary>Privacy-safe description of the attention sheet sent with a request.</summary>
public sealed record AttentionSheetDescription(
    AttentionSheetKind Kind,
    IReadOnlyList<AttentionRegionKind> RegionKinds,
    int SourceWidth,
    int SourceHeight,
    int SheetWidth,
    int SheetHeight,
    string MediaType,
    int ByteLength,
    string Sha256);

/// <summary>
/// One stateless request. <see cref="RequestJson"/> is the canonical structured body
/// (everything except image bytes). <see cref="EncodedImage"/> is RAM-only, belongs to
/// the bridge, and is valid only for the duration of the provider call.
/// </summary>
public sealed class SemanticRequest
{
    internal SemanticRequest(
        Guid operationId,
        SemanticOperationKind kind,
        int attempt,
        ResumePacket resumePacket,
        AttentionSheetDescription sheet,
        ReadOnlyMemory<byte> encodedImage)
    {
        OperationId = operationId;
        Kind = kind;
        Attempt = attempt;
        ResumePacket = resumePacket;
        Sheet = sheet;
        EncodedImage = encodedImage;
        RequestJson = JsonSerializer.Serialize(
            new RequestWire(
                SemanticSchema.Version,
                operationId,
                kind,
                attempt,
                resumePacket,
                sheet),
            SemanticSchema.StrictOptions);
    }

    public int SchemaVersion => SemanticSchema.Version;

    public Guid OperationId { get; }

    public SemanticOperationKind Kind { get; }

    public int Attempt { get; }

    public ResumePacket ResumePacket { get; }

    public AttentionSheetDescription Sheet { get; }

    public ReadOnlyMemory<byte> EncodedImage { get; }

    public string RequestJson { get; }

    internal SemanticRequest WithAttempt(int attempt) =>
        attempt == Attempt
            ? this
            : new SemanticRequest(OperationId, Kind, attempt, ResumePacket, Sheet, EncodedImage);

    private sealed record RequestWire(
        int SchemaVersion,
        Guid OperationId,
        SemanticOperationKind OperationKind,
        int Attempt,
        ResumePacket ResumePacket,
        AttentionSheetDescription Sheet);
}
