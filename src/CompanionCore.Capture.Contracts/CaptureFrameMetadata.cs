namespace CompanionCore.Capture.Contracts;

/// <summary>
/// Metadata only — never a pixel buffer. Every frame is inseparably tagged with the
/// exact authorized target session and privacy generation that produced it. A later
/// admission gate rejects anything that is no longer current before downstream work.
/// </summary>
public sealed record CaptureFrameMetadata
{
    public CaptureFrameMetadata(
        CaptureAuthorizationGrant authorization,
        long sequenceNumber,
        DateTimeOffset timestamp,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        if (sequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                width <= 0 ? nameof(width) : nameof(height),
                "Synthetic frame dimensions must be positive.");
        }

        TargetSessionId = authorization.TargetSessionId;
        Generation = authorization.Generation;
        Target = authorization.Target;
        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;
        Width = width;
        Height = height;
    }

    public Guid TargetSessionId { get; }

    public long Generation { get; }

    public CaptureTargetIdentity Target { get; }

    public long SequenceNumber { get; }

    public DateTimeOffset Timestamp { get; }

    public int Width { get; }

    public int Height { get; }
}
