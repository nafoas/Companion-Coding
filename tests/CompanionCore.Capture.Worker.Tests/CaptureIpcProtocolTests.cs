using System.Buffers.Binary;
using System.Text;
using System.Security.Cryptography;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class CaptureIpcProtocolTests
{
    [Fact]
    public async Task RoundTrip_PreservesVersionCorrelationSequenceAndExactAuthorization()
    {
        var expected = new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.Start,
            CorrelationId = Guid.Parse("25252525-2525-2525-2525-252525252525"),
            ControlSequence = 1,
            Authorization = CaptureWorkerTestSupport.CreateAuthorization(),
        };
        await using var stream = new MemoryStream();

        await CaptureIpcProtocol.WriteAsync(stream, expected, CancellationToken.None);
        stream.Position = 0;
        var actual = await CaptureIpcProtocol.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(expected, actual);
        Assert.Equal(CaptureIpcProtocol.Version, actual.ProtocolVersion);
    }

    [Fact]
    public async Task OversizedPrefix_IsRejectedBeforePayloadAllocation()
    {
        await using var stream = new MemoryStream();
        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(
            prefix,
            CaptureIpcProtocol.MaximumMessageBytes + 1);
        await stream.WriteAsync(prefix);
        stream.Position = 0;

        var exception = await Assert.ThrowsAsync<CaptureProtocolException>(() =>
            CaptureIpcProtocol.ReadAsync(stream, CancellationToken.None));

        Assert.Equal(CaptureWorkerErrorCode.OversizedMessage, exception.ErrorCode);
    }

    [Fact]
    public async Task UnknownMember_IsRejectedByStrictDeserializer()
    {
        const string json =
            "{\"ProtocolVersion\":2,\"Kind\":3,\"CorrelationId\":\"25252525-2525-2525-2525-252525252525\",\"ControlSequence\":1,\"Unexpected\":true}";
        await using var stream = FrameJson(json);

        var exception = await Assert.ThrowsAsync<CaptureProtocolException>(() =>
            CaptureIpcProtocol.ReadAsync(stream, CancellationToken.None));

        Assert.Equal(CaptureWorkerErrorCode.MalformedMessage, exception.ErrorCode);
    }

    [Fact]
    public async Task UnsupportedVersion_FailsClosed()
    {
        const string json = "{\"ProtocolVersion\":3,\"Kind\":3}";
        await using var stream = FrameJson(json);

        var exception = await Assert.ThrowsAsync<CaptureProtocolException>(() =>
            CaptureIpcProtocol.ReadAsync(stream, CancellationToken.None));

        Assert.Equal(CaptureWorkerErrorCode.UnsupportedProtocol, exception.ErrorCode);
    }

    [Fact]
    public void WorkerHandshakeNonce_UsesTheShared256BitHexLength()
    {
        Assert.Equal(64, CaptureIpcProtocol.HandshakeNonceHexLength);
        Assert.Equal(CaptureIpcProtocol.HandshakeNonceHexLength, WorkerHostOptions.NonceLength);
    }

    [Fact]
    public async Task AttentionPayload_RoundTripsSeparatelyWithIntegrity()
    {
        var payload = Enumerable.Range(0, 1024).Select(index => (byte)index).ToArray();
        var message = CreateAttentionMessage(payload);
        await using var stream = new MemoryStream();

        await CaptureIpcProtocol.WriteAsync(stream, message, payload, CancellationToken.None);
        stream.Position = 0;
        using var envelope = await CaptureIpcProtocol.ReadEnvelopeAsync(stream, CancellationToken.None);

        Assert.Equal(message, envelope.Message);
        Assert.Equal(payload, envelope.Payload.ToArray());
        Assert.True(stream.Position <= sizeof(int) + CaptureIpcProtocol.MaximumMessageBytes + payload.Length);
    }

    [Fact]
    public async Task CorruptedAttentionPayload_FailsIntegrityCheck()
    {
        var payload = Enumerable.Range(0, 512).Select(index => (byte)index).ToArray();
        var message = CreateAttentionMessage(payload);
        await using var stream = new MemoryStream();
        await CaptureIpcProtocol.WriteAsync(stream, message, payload, CancellationToken.None);
        var bytes = stream.ToArray();
        bytes[^1] ^= 0xff;
        await using var corrupted = new MemoryStream(bytes, writable: false);

        var exception = await Assert.ThrowsAsync<CaptureProtocolException>(() =>
            CaptureIpcProtocol.ReadEnvelopeAsync(corrupted, CancellationToken.None));

        Assert.Equal(CaptureWorkerErrorCode.PayloadIntegrityFailure, exception.ErrorCode);
    }

    [Fact]
    public async Task PayloadOnNonAttentionShape_IsRejectedBeforeWrite()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var message = new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.FrameProduced,
            PayloadLength = payload.Length,
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(payload)),
        };
        await using var stream = new MemoryStream();

        var exception = await Assert.ThrowsAsync<CaptureProtocolException>(() =>
            CaptureIpcProtocol.WriteAsync(stream, message, payload, CancellationToken.None));

        Assert.Equal(CaptureWorkerErrorCode.UnexpectedPayload, exception.ErrorCode);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task OversizedPayloadDescriptor_IsRejectedBeforeAllocationOrWrite()
    {
        var message = new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.AttentionSheetProduced,
            PayloadLength = AttentionSheet.MaximumEncodedBytes + 1,
            PayloadSha256 = new string('0', 64),
        };
        await using var stream = new MemoryStream();

        var exception = await Assert.ThrowsAsync<CaptureProtocolException>(() =>
            CaptureIpcProtocol.WriteAsync(stream, message, CancellationToken.None));

        Assert.Equal(CaptureWorkerErrorCode.OversizedPayload, exception.ErrorCode);
        Assert.Equal(0, stream.Length);
    }

    private static CaptureIpcMessage CreateAttentionMessage(byte[] payload)
    {
        var authorization = CaptureWorkerTestSupport.CreateAuthorization();
        var target = new CaptureTargetIdentity(
            authorization.WindowId,
            authorization.ProcessId,
            authorization.ExecutableFileName,
            authorization.ExecutablePathFingerprint);
        var metadata = new AttentionSheetMetadata
        {
            TargetSessionId = authorization.TargetSessionId,
            Generation = authorization.Generation,
            Target = target,
            SourceSequenceNumber = 1,
            SourceTimestamp = CaptureWorkerTestSupport.FixedTime,
            SourceWidth = 2,
            SourceHeight = 2,
            SheetWidth = 2,
            SheetHeight = 2,
            EncodedByteLength = payload.Length,
            Kind = AttentionSheetKind.Orientation,
            ChangeScore = 1,
            Regions =
            [
                new AttentionSheetRegionMetadata
                {
                    Kind = AttentionRegionKind.FullContext,
                    NormalizedSource = new NormalizedRegion(0, 0, 1, 1),
                    SourcePixels = new PixelRect(0, 0, 2, 2),
                    SheetPixels = new PixelRect(0, 0, 2, 2),
                },
            ],
        };
        return new CaptureIpcMessage
        {
            Kind = CaptureIpcMessageKind.AttentionSheetProduced,
            Authorization = authorization,
            AttentionSheet = metadata,
            PayloadLength = payload.Length,
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(payload)),
        };
    }

    private static MemoryStream FrameJson(string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var bytes = new byte[sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, payload.Length);
        payload.CopyTo(bytes.AsSpan(sizeof(int)));
        return new MemoryStream(bytes, writable: false);
    }
}
