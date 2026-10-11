using System.Text.Json.Serialization;

namespace CompanionCore.Capture.Contracts;

/// <summary>
/// One local Sniff: what changed in a single authorized frame, computed in the capture
/// worker before any sheet work, within the ≤100 ms noticing budget (VIS-01). It carries
/// no pixels — only bounded numbers and a target-relative region — and is published only
/// after its source frame passes the same privacy admission as attention sheets.
/// </summary>
public sealed record SniffSignal
{
    /// <summary>The change grid over the frame signature (columns × rows).</summary>
    public const int GridColumns = 8;

    public const int GridRows = 6;

    /// <summary>A hot cell at or above this change is a trigger: Prince looks at once.</summary>
    public const double TriggerChange = 0.15;

    /// <summary>A scene cut needs this much global change …</summary>
    public const double SceneCutChange = 0.30;

    /// <summary>… spread over at least this fraction of the grid.</summary>
    public const double SceneCutMotion = 0.6;

    /// <summary>The sniff asks for an immediate look.</summary>
    [JsonIgnore]
    public bool IsTrigger => SceneCut || HotChange >= TriggerChange;

    /// <summary>A sniff whose compute time exceeds this is protocol-unsafe (a stalled worker).</summary>
    public const long MaximumComputeMicroseconds = 10_000_000;

    public Guid TargetSessionId { get; init; }

    public long Generation { get; init; }

    public CaptureTargetIdentity? Target { get; init; }

    public long SourceSequenceNumber { get; init; }

    public DateTimeOffset SourceTimestamp { get; init; }

    /// <summary>The source frame's geometry and accounting, so the sniff passes privacy admission on its own.</summary>
    public int SourceWidth { get; init; }

    public int SourceHeight { get; init; }

    public long SourceAccountedBytes { get; init; }

    /// <summary>Mean change across the whole frame, in [0, 1].</summary>
    public double Change { get; init; }

    /// <summary>Change of the hottest grid cell, in [0, 1].</summary>
    public double HotChange { get; init; }

    /// <summary>Fraction of grid cells that changed noticeably (motion spread), in [0, 1].</summary>
    public double Motion { get; init; }

    /// <summary>Most of the frame changed at once: a cut, a load, or a menu opening.</summary>
    public bool SceneCut { get; init; }

    /// <summary>The bounding box of the changed cells (the hottest cell when none passes the cell threshold).</summary>
    public NormalizedRegion HotRegion { get; init; }

    /// <summary>The named layout region nearest the hot region.</summary>
    public AttentionRegionKind HotRegionKind { get; init; }

    /// <summary>The hottest cell's index, row-major in the grid.</summary>
    public int HotCell { get; init; }

    /// <summary>Worker time from frame arrival to this sniff, in microseconds.</summary>
    public long ComputeMicroseconds { get; init; }

    /// <summary>The source frame as admission metadata (pixels never leave the worker).</summary>
    public CaptureFrameMetadata ToFrameMetadata(CaptureAuthorizationGrant grant) =>
        new(grant, SourceSequenceNumber, SourceTimestamp, SourceWidth, SourceHeight, SourceAccountedBytes);

    public bool Matches(CaptureAuthorizationGrant grant) =>
        grant is not null
        && TargetSessionId == grant.TargetSessionId
        && Generation == grant.Generation
        && Target == grant.Target;

    public bool IsProtocolSafe() =>
        TargetSessionId != Guid.Empty
        && Generation > 0
        && Target is not null
        && SourceSequenceNumber > 0
        && SourceTimestamp != default
        && SourceWidth > 0
        && SourceHeight > 0
        && SourceAccountedBytes is > 0 and <= CaptureWorkerMetrics.ScreenshotBudgetBytes
        && InUnitRange(Change)
        && InUnitRange(HotChange)
        && InUnitRange(Motion)
        && HotChange >= Change - 1e-9
        && HotRegion.IsValid
        && Enum.IsDefined(HotRegionKind)
        && HotRegionKind is not (AttentionRegionKind.FullContext or AttentionRegionKind.ManualFocus)
        && HotCell >= 0
        && HotCell < GridColumns * GridRows
        && ComputeMicroseconds is >= 0 and <= MaximumComputeMicroseconds;

    private static bool InUnitRange(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
}

/// <summary>
/// How often the capture worker looks (frame spacing) and how often it composes an
/// attention sheet (sheet spacing). Set from Prince's attention state (VIS-01); a sniff
/// trigger always composes a sheet at once.
/// </summary>
public sealed record CaptureCadence
{
    public static readonly TimeSpan MinimumFrameSpacing = TimeSpan.FromMilliseconds(50);

    public static readonly TimeSpan MaximumFrameSpacing = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan MaximumSheetSpacing = TimeSpan.FromSeconds(10);

    /// <summary>Noticing: 4 looks per second, a sheet each second.</summary>
    public static CaptureCadence Noticing { get; } = new()
    {
        FrameSpacing = TimeSpan.FromMilliseconds(250),
        SheetSpacing = TimeSpan.FromSeconds(1),
    };

    /// <summary>Engaged and Afterglow: about 7 looks per second, a sheet each half second.</summary>
    public static CaptureCadence Engaged { get; } = new()
    {
        FrameSpacing = TimeSpan.FromMilliseconds(150),
        SheetSpacing = TimeSpan.FromMilliseconds(500),
    };

    /// <summary>Bnuy Mode: 10 looks per second, a sheet each quarter second.</summary>
    public static CaptureCadence BnuyMode { get; } = new()
    {
        FrameSpacing = TimeSpan.FromMilliseconds(100),
        SheetSpacing = TimeSpan.FromMilliseconds(250),
    };

    public TimeSpan FrameSpacing { get; init; }

    public TimeSpan SheetSpacing { get; init; }

    [JsonIgnore]
    public bool IsValid =>
        FrameSpacing >= MinimumFrameSpacing
        && FrameSpacing <= MaximumFrameSpacing
        && SheetSpacing >= FrameSpacing
        && SheetSpacing <= MaximumSheetSpacing;
}

/// <summary>
/// One Text Sniff (VIS-03): Windows' built-in text recognition read a text-likely region
/// after the Sniff saw it change. It carries no text, only bounded counts and whether the
/// words changed since that region was last read; the recognized text never leaves the
/// capture worker. It passes the same privacy admission as sniffs and sheets.
/// </summary>
public sealed record TextSniffSignal
{
    public const int MaximumLines = 128;

    public const int MaximumCharacters = 16_384;

    public Guid TargetSessionId { get; init; }

    public long Generation { get; init; }

    public CaptureTargetIdentity? Target { get; init; }

    public long SourceSequenceNumber { get; init; }

    public DateTimeOffset SourceTimestamp { get; init; }

    public int SourceWidth { get; init; }

    public int SourceHeight { get; init; }

    public long SourceAccountedBytes { get; init; }

    /// <summary>The text-likely layout region that was read.</summary>
    public AttentionRegionKind RegionKind { get; init; }

    public NormalizedRegion Region { get; init; }

    public int Lines { get; init; }

    public int Characters { get; init; }

    /// <summary>The words differ from the last reading of this region (appeared, changed, or vanished).</summary>
    public bool TextChanged { get; init; }

    public long ComputeMicroseconds { get; init; }

    /// <summary>New or different words are present (not merely vanished).</summary>
    [JsonIgnore]
    public bool NewText => TextChanged && Lines > 0;

    public CaptureFrameMetadata ToFrameMetadata(CaptureAuthorizationGrant grant) =>
        new(grant, SourceSequenceNumber, SourceTimestamp, SourceWidth, SourceHeight, SourceAccountedBytes);

    public bool Matches(CaptureAuthorizationGrant grant) =>
        grant is not null
        && TargetSessionId == grant.TargetSessionId
        && Generation == grant.Generation
        && Target == grant.Target;

    public bool IsProtocolSafe() =>
        TargetSessionId != Guid.Empty
        && Generation > 0
        && Target is not null
        && SourceSequenceNumber > 0
        && SourceTimestamp != default
        && SourceWidth > 0
        && SourceHeight > 0
        && SourceAccountedBytes is > 0 and <= CaptureWorkerMetrics.ScreenshotBudgetBytes
        && Enum.IsDefined(RegionKind)
        && RegionKind is not (AttentionRegionKind.FullContext or AttentionRegionKind.ManualFocus)
        && Region.IsValid
        && Lines is >= 0 and <= MaximumLines
        && Characters is >= 0 and <= MaximumCharacters
        && (Lines > 0 || Characters == 0)
        && ComputeMicroseconds is >= 0 and <= SniffSignal.MaximumComputeMicroseconds;
}
