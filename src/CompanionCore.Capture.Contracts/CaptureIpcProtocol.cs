using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanionCore.Capture.Contracts;

/// <summary>
/// Bounded, versioned transport for the dedicated local capture process. These data
/// shapes carry no grant-issuance authority; only an already-issued sealed grant can
/// be converted by the client into a start message.
/// </summary>
public static class CaptureIpcProtocol
{
    public const int Version = 1;
    public const int MaximumMessageBytes = 64 * 1024;
    public const int HandshakeNonceHexLength = 64;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static async Task WriteAsync(
        Stream stream,
        CaptureIpcMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);

        if (message.ProtocolVersion != Version)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.UnsupportedProtocol);
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if (payload.Length is <= 0 or > MaximumMessageBytes)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.OversizedMessage);
        }

        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CaptureIpcMessage> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var prefix = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is <= 0 or > MaximumMessageBytes)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.OversizedMessage);
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        CaptureIpcMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<CaptureIpcMessage>(payload, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new CaptureProtocolException(
                CaptureWorkerErrorCode.MalformedMessage,
                exception);
        }

        if (message is null)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
        }

        if (message.ProtocolVersion != Version)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.UnsupportedProtocol);
        }

        return message;
    }
}

public enum CaptureIpcMessageKind
{
    Hello,
    HelloAccepted,
    Start,
    Stop,
    StopAndClear,
    GetMetrics,
    Shutdown,
    CommandSucceeded,
    CommandFailed,
    FrameProduced,
    StatusChanged,
}

public enum CaptureWorkerErrorCode
{
    None,
    MalformedMessage,
    OversizedMessage,
    UnsupportedProtocol,
    InvalidHandshake,
    InvalidState,
    InvalidAuthorization,
    TargetUnavailable,
    TargetIdentityMismatch,
    CaptureUnavailable,
    CaptureFault,
    Cancelled,
    Timeout,
}

public sealed record CaptureIpcAuthorization
{
    public Guid TargetSessionId { get; init; }

    public long Generation { get; init; }

    public long WindowId { get; init; }

    public int ProcessId { get; init; }

    public string ExecutableFileName { get; init; } = string.Empty;

    public string ExecutablePathFingerprint { get; init; } = string.Empty;

    public static CaptureIpcAuthorization FromGrant(CaptureAuthorizationGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        return new CaptureIpcAuthorization
        {
            TargetSessionId = grant.TargetSessionId,
            Generation = grant.Generation,
            WindowId = grant.Target.WindowId,
            ProcessId = grant.Target.ProcessId,
            ExecutableFileName = grant.Target.ExecutableFileName,
            ExecutablePathFingerprint = grant.Target.ExecutablePathFingerprint,
        };
    }

    public bool Matches(CaptureAuthorizationGrant grant) =>
        TargetSessionId == grant.TargetSessionId
        && Generation == grant.Generation
        && WindowId == grant.Target.WindowId
        && ProcessId == grant.Target.ProcessId
        && string.Equals(
            ExecutableFileName,
            grant.Target.ExecutableFileName,
            StringComparison.OrdinalIgnoreCase)
        && string.Equals(
            ExecutablePathFingerprint,
            grant.Target.ExecutablePathFingerprint,
            StringComparison.OrdinalIgnoreCase);
}

public sealed record CaptureIpcMessage
{
    public int ProtocolVersion { get; init; } = CaptureIpcProtocol.Version;

    public CaptureIpcMessageKind Kind { get; init; }

    public Guid CorrelationId { get; init; }

    public long ControlSequence { get; init; }

    public string? HandshakeNonce { get; init; }

    public CaptureIpcAuthorization? Authorization { get; init; }

    public long SequenceNumber { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public long AccountedBytes { get; init; }

    public CaptureWorkerStatus Status { get; init; }

    public CaptureWorkerStatusReason StatusReason { get; init; }

    public CaptureWorkerMetrics? Metrics { get; init; }

    public int ClearedFrameCount { get; init; }

    public long ClearedBytes { get; init; }

    public CaptureWorkerErrorCode ErrorCode { get; init; }
}

public sealed class CaptureProtocolException : Exception
{
    public CaptureProtocolException(
        CaptureWorkerErrorCode errorCode,
        Exception? innerException = null)
        : base($"Capture worker protocol failure ({errorCode}).", innerException)
    {
        ErrorCode = errorCode;
    }

    public CaptureWorkerErrorCode ErrorCode { get; }
}
