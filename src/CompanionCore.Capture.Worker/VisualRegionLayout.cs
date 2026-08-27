using CompanionCore.Capture.Contracts;

namespace CompanionCore.Capture.Worker;

internal readonly record struct VisualRegion(
    AttentionRegionKind Kind,
    NormalizedRegion Bounds);

internal static class VisualRegionLayout
{
    internal static readonly NormalizedRegion FullContext = new(0, 0, 1, 1);

    internal static readonly IReadOnlyList<VisualRegion> DefaultFocusRegions =
    [
        new(AttentionRegionKind.CenterEnvironment, new(0.18, 0.12, 0.64, 0.56)),
        new(AttentionRegionKind.LowerDialogueInventory, new(0.05, 0.65, 0.90, 0.32)),
        new(AttentionRegionKind.UpperLeft, new(0.00, 0.00, 0.35, 0.32)),
        new(AttentionRegionKind.UpperRight, new(0.65, 0.00, 0.35, 0.32)),
        new(AttentionRegionKind.LeftSide, new(0.00, 0.18, 0.28, 0.64)),
        new(AttentionRegionKind.RightSide, new(0.72, 0.18, 0.28, 0.64)),
    ];

    internal static PixelRect MapToPixels(
        NormalizedRegion region,
        int sourceWidth,
        int sourceHeight)
    {
        region.Validate(nameof(region));
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                sourceWidth <= 0 ? nameof(sourceWidth) : nameof(sourceHeight));
        }

        var left = Math.Clamp((int)Math.Floor(region.X * sourceWidth), 0, sourceWidth - 1);
        var top = Math.Clamp((int)Math.Floor(region.Y * sourceHeight), 0, sourceHeight - 1);
        var right = Math.Clamp(
            (int)Math.Ceiling((region.X + region.Width) * sourceWidth),
            left + 1,
            sourceWidth);
        var bottom = Math.Clamp(
            (int)Math.Ceiling((region.Y + region.Height) * sourceHeight),
            top + 1,
            sourceHeight);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    internal static string GetVisibleLabel(AttentionRegionKind kind) => kind switch
    {
        AttentionRegionKind.FullContext => "FULL CONTEXT",
        AttentionRegionKind.CenterEnvironment => "CENTER",
        AttentionRegionKind.LowerDialogueInventory => "LOWER / DIALOGUE",
        AttentionRegionKind.UpperLeft => "UPPER LEFT",
        AttentionRegionKind.UpperRight => "UPPER RIGHT",
        AttentionRegionKind.LeftSide => "LEFT SIDE",
        AttentionRegionKind.RightSide => "RIGHT SIDE",
        AttentionRegionKind.ManualFocus => "MANUAL FOCUS",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

internal sealed class StaggeredRegionScheduler
{
    private int _next;

    internal IReadOnlyList<VisualRegion> TakeNext(NormalizedRegion? manualRegion)
    {
        var selected = new List<VisualRegion>(2);
        if (manualRegion is { } manual)
        {
            manual.Validate(nameof(manualRegion));
            selected.Add(new VisualRegion(AttentionRegionKind.ManualFocus, manual));
        }

        while (selected.Count < 2)
        {
            selected.Add(VisualRegionLayout.DefaultFocusRegions[_next]);
            _next = (_next + 1) % VisualRegionLayout.DefaultFocusRegions.Count;
        }

        return selected;
    }

    internal void Reset() => _next = 0;
}
