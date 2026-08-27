using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanionCore.Capture.Contracts;

/// <summary>
/// Bounded, versioned transport for the dedicated local capture process. Control JSON
/// retains its 64 KiB ceiling. Task 6 attention pixels use one separately framed,
/// checksummed payload and are never embedded as base64 or accepted on any other shape.
/// </summary>
public static class CaptureIpcProtocol
{
    public const int Version = 2;
    public const int MaximumMessageBytes = 64 * 1024;
    public const int HandshakeNonceHexLength = 64;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static Task WriteAsync(
        Stream stream,
        CaptureIpcMessage message,
        CancellationToken cancellationToken) =>
        WriteCoreAsync(stream, message, ReadOnlyMemory<byte>.Empty, cancellationToken);

    public static Task WriteAsync(
        Stream stream,
        CaptureIpcMessage message,
        ReadOnlyMemory<byte> attachedPayload,
        CancellationToken cancellationToken) =>
        WriteCoreAsync(stream, message, attachedPayload, cancellationToken);

    private static async Task WriteCoreAsync(
        Stream stream,
        CaptureIpcMessage message,
        ReadOnlyMemory<byte> attachedPayload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);
        if (message.ProtocolVersion != Version)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.UnsupportedProtocol);
        }

        ValidatePayloadDescriptor(message, attachedPayload.Length);
        if (!attachedPayload.IsEmpty
            && !PayloadHashMatches(message.PayloadSha256, attachedPayload.Span))
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.PayloadIntegrityFailure);
        }

        var header = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if (header.Length is <= 0 or > MaximumMessageBytes)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.OversizedMessage);
        }

        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, header.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        if (!attachedPayload.IsEmpty)
        {
            await stream.WriteAsync(attachedPayload, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CaptureIpcMessage> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var envelope = await ReadEnvelopeAsync(stream, cancellationToken).ConfigureAwait(false);
        if (envelope.PayloadLength != 0)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.UnexpectedPayload);
        }

        return envelope.Message;
    }

    public static async Task<CaptureIpcEnvelope> ReadEnvelopeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prefix = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        var headerLength = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (headerLength is <= 0 or > MaximumMessageBytes)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.OversizedMessage);
        }

        var header = new byte[headerLength];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        CaptureIpcMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<CaptureIpcMessage>(header, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage, exception);
        }

        if (message is null)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
        }

        if (message.ProtocolVersion != Version)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.UnsupportedProtocol);
        }

        ValidatePayloadDescriptor(message, message.PayloadLength);
        if (message.PayloadLength == 0)
        {
            return new CaptureIpcEnvelope(message, null);
        }

        var attachedPayload = new byte[message.PayloadLength];
        try
        {
            await stream.ReadExactlyAsync(attachedPayload, cancellationToken).ConfigureAwait(false);
            if (!PayloadHashMatches(message.PayloadSha256, attachedPayload))
            {
                throw new CaptureProtocolException(CaptureWorkerErrorCode.PayloadIntegrityFailure);
            }

            return new CaptureIpcEnvelope(message, attachedPayload);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(attachedPayload);
            throw;
        }
    }

    private static bool PayloadHashMatches(string? encodedHash, ReadOnlySpan<byte> payload)
    {
        byte[]? expected = null;
        byte[]? actual = null;
        try
        {
            try
            {
                expected = Convert.FromHexString(encodedHash ?? string.Empty);
            }
            catch (FormatException)
            {
                return false;
            }

            if (expected.Length != SHA256.HashSizeInBytes)
            {
                return false;
            }

            actual = SHA256.HashData(payload);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        finally
        {
            if (expected is not null)
            {
                CryptographicOperations.ZeroMemory(expected);
            }

            if (actual is not null)
            {
                CryptographicOperations.ZeroMemory(actual);
            }
        }
    }

    private static void ValidatePayloadDescriptor(CaptureIpcMessage message, int actualLength)
    {
        if (message.PayloadLength < 0
            || message.PayloadLength > AttentionSheet.MaximumEncodedBytes
            || actualLength != message.PayloadLength)
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.OversizedPayload);
        }

        var hasPayload = message.PayloadLength > 0;
        if (hasPayload != !string.IsNullOrEmpty(message.PayloadSha256)
            || hasPayload != (message.Kind == CaptureIpcMessageKind.AttentionSheetProduced)
            || hasPayload != (message.AttentionSheet is not null))
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.UnexpectedPayload);
        }

        if (hasPayload
            && (message.AttentionSheet!.EncodedByteLength != message.PayloadLength
                || !message.AttentionSheet.IsProtocolSafe()
                || message.PayloadSha256!.Length != SHA256.HashSizeInBytes * 2))
        {
            throw new CaptureProtocolException(CaptureWorkerErrorCode.MalformedMessage);
        }
    }
}

public sealed class CaptureIpcEnvelope : IDisposable
{
    private byte[]? _payload;

    internal CaptureIpcEnvelope(CaptureIpcMessage message, byte[]? payload)
    {
        Message = message;
        _payload = payload;
    }

    public CaptureIpcMessage Message { get; }

    public int PayloadLength => Volatile.Read(ref _payload)?.Length ?? 0;

    internal ReadOnlyMemory<byte> Payload =>
        Volatile.Read(ref _payload) ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Atomically transfers the validated attention payload into its RAM-only owner.
    /// The envelope no longer owns bytes after this succeeds.
    /// </summary>
    public AttentionSheet TakeAttentionSheet()
    {
        var payload = Interlocked.Exchange(ref _payload, null)
            ?? throw new InvalidOperationException("The IPC envelope has no owned payload.");
        try
        {
            if (Message.Kind != CaptureIpcMessageKind.AttentionSheetProduced
                || Message.AttentionSheet is null)
            {
                throw new CaptureProtocolException(CaptureWorkerErrorCode.UnexpectedPayload);
            }

            return new AttentionSheet(Message.AttentionSheet, payload);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(payload);
            throw;
        }
    }

    public void Dispose()
    {
        var payload = Interlocked.Exchange(ref _payload, null);
        if (payload is not null)
        {
            CryptographicOperations.ZeroMemory(payload);
        }
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
    SetManualRegion,
    CommandSucceeded,
    CommandFailed,
    FrameProduced,
    StatusChanged,
    AttentionSheetProduced,
}

public enum CaptureWorkerErrorCode
{
    None,
    MalformedMessage,
    OversizedMessage,
    OversizedPayload,
    UnexpectedPayload,
    PayloadIntegrityFailure,
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
        && string.Equals(ExecutableFileName, grant.Target.ExecutableFileName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(ExecutablePathFingerprint, grant.Target.ExecutablePathFingerprint, StringComparison.OrdinalIgnoreCase);

    public bool Matches(CaptureIpcAuthorization other) =>
        other is not null
        && TargetSessionId == other.TargetSessionId
        && Generation == other.Generation
        && WindowId == other.WindowId
        && ProcessId == other.ProcessId
        && string.Equals(ExecutableFileName, other.ExecutableFileName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(ExecutablePathFingerprint, other.ExecutablePathFingerprint, StringComparison.OrdinalIgnoreCase);
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
    public NormalizedRegion? ManualRegion { get; init; }
    public bool ClearManualRegion { get; init; }
    public AttentionSheetMetadata? AttentionSheet { get; init; }
    public int PayloadLength { get; init; }
    public string? PayloadSha256 { get; init; }
}

public sealed class CaptureProtocolException : Exception
{
    public CaptureProtocolException(CaptureWorkerErrorCode errorCode, Exception? innerException = null)
        : base($"Capture worker protocol failure ({errorCode}).", innerException)
    {
        ErrorCode = errorCode;
    }

    public CaptureWorkerErrorCode ErrorCode { get; }
}
