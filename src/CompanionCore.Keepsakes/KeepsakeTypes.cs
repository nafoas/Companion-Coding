using CompanionCore.Capture.Contracts;

namespace CompanionCore.Keepsakes;

public enum KeepsakeRefusal
{
    None = 0,
    PrivacyStale = 1,
    PrivacyRejected = 2,
    TooSoon = 3,
    DailyLimit = 4,
    UnknownAction = 5,
    ActionExpired = 6,
    WrongTarget = 7,
    OutsideWindow = 8,
    InvalidFrame = 9,
    EncodedTooLarge = 10,
    StoredFileMismatch = 11,
    RecordRejected = 12,
    RecordConflict = 13,
}

public enum KeepsakeIntentKind
{
    /// <summary>The visible camera action that discloses the coming durable write.</summary>
    CameraShown = 1,

    /// <summary>The durable write, paired with its camera action.</summary>
    PhotographSaved = 2,

    PhotographDeleted = 3,
}

/// <summary>One typed output; the camera animation and wording belong to presentation.</summary>
public sealed record KeepsakeIntent(KeepsakeIntentKind Kind, Guid ActionId, Guid? PhotographId = null, string? Sha256 = null);

/// <summary>A single-use, short-lived camera action for one authorized target session.</summary>
public sealed record CameraAction(
    Guid ActionId,
    Guid TargetSessionId,
    long Generation,
    CaptureTargetIdentity Target,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt);

/// <summary>An in-RAM BGRA32 frame, tagged with the authorized capture metadata that produced it.</summary>
public sealed record PhotographFrame(CaptureFrameMetadata Metadata, ReadOnlyMemory<byte> Bgra32, int Stride);

/// <summary>Where the photograph belongs; used only for memory scope.</summary>
public sealed record KeepsakeContext(string? Game = null, string? Save = null, string? Session = null);

public sealed record CameraActionResult(CameraAction? Action, IReadOnlyList<KeepsakeIntent> Intents, KeepsakeRefusal Refusal);

public sealed record PhotographResult(
    Guid? PhotographId,
    string? Sha256,
    IReadOnlyList<KeepsakeIntent> Intents,
    KeepsakeRefusal Refusal,
    bool AlreadySaved = false);

public enum InspectionStatus
{
    Verified = 1,
    Missing = 2,
    Tampered = 3,
    Deleted = 4,
    Unknown = 5,
}

public sealed record KeepsakeInspection(InspectionStatus Status, ReadOnlyMemory<byte> Png);

public sealed record PhotographEntry(
    Guid PhotographId,
    Guid ActionId,
    string Sha256,
    long ByteLength,
    int Width,
    int Height,
    DateTimeOffset TakenAt,
    string Caption,
    string? Game,
    string? Save,
    string? Session,
    bool Deleted);

public enum DeletionStatus
{
    Deleted = 1,
    AlreadyDeleted = 2,
    Unknown = 3,
    RecordRejected = 4,
}

public sealed record DeletionResult(DeletionStatus Status, IReadOnlyList<KeepsakeIntent> Intents);

public sealed record DiskGrowthReport(
    long KeepsakeFiles,
    long KeepsakeBytes,
    long MemoryRootFiles,
    long MemoryRootBytes);
