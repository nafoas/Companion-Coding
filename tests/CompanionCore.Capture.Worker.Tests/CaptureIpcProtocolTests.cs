using System.Buffers.Binary;
using System.Text;
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
            "{\"ProtocolVersion\":1,\"Kind\":3,\"CorrelationId\":\"25252525-2525-2525-2525-252525252525\",\"ControlSequence\":1,\"Unexpected\":true}";
        await using var stream = FrameJson(json);

        var exception = await Assert.ThrowsAsync<CaptureProtocolException>(() =>
            CaptureIpcProtocol.ReadAsync(stream, CancellationToken.None));

        Assert.Equal(CaptureWorkerErrorCode.MalformedMessage, exception.ErrorCode);
    }

    [Fact]
    public async Task UnsupportedVersion_FailsClosed()
    {
        const string json = "{\"ProtocolVersion\":2,\"Kind\":3}";
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

    private static MemoryStream FrameJson(string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var bytes = new byte[sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, payload.Length);
        payload.CopyTo(bytes.AsSpan(sizeof(int)));
        return new MemoryStream(bytes, writable: false);
    }
}
