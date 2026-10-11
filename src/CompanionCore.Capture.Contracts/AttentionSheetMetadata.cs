namespace CompanionCore.Capture.Contracts;

public enum AttentionSheetKind
{
    Orientation,
    Regional,

    /// <summary>
    /// One keepsake photograph, requested explicitly while the camera action is shown: the
    /// whole authorized target at its native resolution (halved only to fit the bounds).
    /// </summary>
    Photograph,
}

public enum AttentionRegionKind
{
    FullContext,
    CenterEnvironment,
    LowerDialogueInventory,
    UpperLeft,
    UpperRight,
    LeftSide,
    RightSide,
    ManualFocus,
}

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public bool IsValidWithin(int width, int height) =>
        X >= 0
        && Y >= 0
        && Width > 0
        && Height > 0
        && X <= width - Width
        && Y <= height - Height;
}

public sealed record AttentionSheetRegionMetadata
{
    public AttentionRegionKind Kind { get; init; }

    public NormalizedRegion NormalizedSource { get; init; }

    public PixelRect SourcePixels { get; init; }

    public PixelRect SheetPixels { get; init; }
}

/// <summary>
/// Strict, privacy-safe description of one RAM-only attention sheet. Labels are an
/// enum rather than arbitrary text, and all geometry is relative to the authorized
/// target's content surface.
/// </summary>
public sealed record AttentionSheetMetadata
{
    public const int MaximumRegions = 3;

    public Guid TargetSessionId { get; init; }

    public long Generation { get; init; }

    public CaptureTargetIdentity? Target { get; init; }

    public long SourceSequenceNumber { get; init; }

    public DateTimeOffset SourceTimestamp { get; init; }

    public int SourceWidth { get; init; }

    public int SourceHeight { get; init; }

    public int SheetWidth { get; init; }

    public int SheetHeight { get; init; }

    public int EncodedByteLength { get; init; }

    public AttentionSheetKind Kind { get; init; }

    public double ChangeScore { get; init; }

    public AttentionSheetRegionMetadata[] Regions { get; init; } = [];

    public bool Matches(CaptureAuthorizationGrant grant) =>
        grant is not null
        && TargetSessionId == grant.TargetSessionId
        && Generation == grant.Generation
        && Target == grant.Target;

    public bool IsProtocolSafe()
    {
        if (TargetSessionId == Guid.Empty
            || Generation <= 0
            || Target is null
            || SourceSequenceNumber <= 0
            || SourceTimestamp == default
            || SourceWidth <= 0
            || SourceHeight <= 0
            || !FitsBgraBudget(
                SourceWidth,
                SourceHeight,
                CaptureWorkerMetrics.ScreenshotBudgetBytes)
            || SheetWidth <= 0
            || SheetHeight <= 0
            || !FitsBgraBudget(
                SheetWidth,
                SheetHeight,
                CaptureWorkerMetrics.VisualWorkingBudgetBytes)
            || EncodedByteLength is <= 0 or > AttentionSheet.MaximumEncodedBytes
            || !Enum.IsDefined(Kind)
            || !double.IsFinite(ChangeScore)
            || ChangeScore is < 0 or > 1
            || Regions is null
            || Regions.Length is < 1 or > MaximumRegions
            || Regions[0].Kind != AttentionRegionKind.FullContext
            || Regions[0].NormalizedSource != new NormalizedRegion(0, 0, 1, 1)
            || Regions[0].SourcePixels != new PixelRect(0, 0, SourceWidth, SourceHeight)
            || (Kind == AttentionSheetKind.Orientation && Regions.Length != 1)
            || (Kind == AttentionSheetKind.Regional && Regions.Length < 2)
            || (Kind == AttentionSheetKind.Photograph
                && (Regions.Length != 1
                    || Regions[0].SheetPixels != new PixelRect(0, 0, SheetWidth, SheetHeight)
                    || SheetWidth > SourceWidth
                    || SheetHeight > SourceHeight
                    || Math.Max(SheetWidth, SheetHeight) > AttentionSheet.MaximumPhotographEdge)))
        {
            return false;
        }

        foreach (var region in Regions)
        {
            if (region is null
                || !Enum.IsDefined(region.Kind)
                || !region.NormalizedSource.IsValid
                || !region.SourcePixels.IsValidWithin(SourceWidth, SourceHeight)
                || !region.SheetPixels.IsValidWithin(SheetWidth, SheetHeight))
            {
                return false;
            }
        }

        return Regions.Select(region => region.Kind).Distinct().Count() == Regions.Length;
    }

    private static bool FitsBgraBudget(int width, int height, long byteBudget)
    {
        if (width <= 0 || height <= 0 || byteBudget <= 0)
        {
            return false;
        }

        var rowBytes = (long)width * 4;
        return rowBytes <= byteBudget && height <= byteBudget / rowBytes;
    }
}
