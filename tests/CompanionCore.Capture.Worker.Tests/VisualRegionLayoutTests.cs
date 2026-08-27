using CompanionCore.Capture.Contracts;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

public sealed class VisualRegionLayoutTests
{
    [Fact]
    public void DefaultLayout_IsNormalizedBoundedAndUniquelyLabeled()
    {
        Assert.Equal(6, VisualRegionLayout.DefaultFocusRegions.Count);
        Assert.All(
            VisualRegionLayout.DefaultFocusRegions,
            region => Assert.True(region.Bounds.IsValid));
        Assert.Equal(
            VisualRegionLayout.DefaultFocusRegions.Count,
            VisualRegionLayout.DefaultFocusRegions.Select(region => region.Kind).Distinct().Count());
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1080)]
    [InlineData(1536, 864)]
    [InlineData(320, 240)]
    public void EveryRegion_MapsInsideSupportedContentGeometry(int width, int height)
    {
        foreach (var region in VisualRegionLayout.DefaultFocusRegions)
        {
            var mapped = VisualRegionLayout.MapToPixels(region.Bounds, width, height);
            Assert.True(mapped.IsValidWithin(width, height));
        }
    }

    [Fact]
    public void NormalizedMapping_RemapsProportionallyAcrossResolutionAndScaling()
    {
        var normalized = new NormalizedRegion(0.1, 0.2, 0.4, 0.5);

        var at720 = VisualRegionLayout.MapToPixels(normalized, 1280, 720);
        var at1080 = VisualRegionLayout.MapToPixels(normalized, 1920, 1080);

        Assert.Equal(new PixelRect(128, 144, 512, 360), at720);
        Assert.Equal(new PixelRect(192, 216, 768, 540), at1080);
    }

    [Fact]
    public void Scheduler_StaggersDefaultsAndKeepsManualOverrideFirst()
    {
        var scheduler = new StaggeredRegionScheduler();
        var first = scheduler.TakeNext(null);
        var second = scheduler.TakeNext(null);
        var manual = new NormalizedRegion(0.2, 0.25, 0.3, 0.35);
        var withManual = scheduler.TakeNext(manual);

        Assert.Equal(
            [AttentionRegionKind.CenterEnvironment, AttentionRegionKind.LowerDialogueInventory],
            first.Select(region => region.Kind));
        Assert.Equal(
            [AttentionRegionKind.UpperLeft, AttentionRegionKind.UpperRight],
            second.Select(region => region.Kind));
        Assert.Equal(AttentionRegionKind.ManualFocus, withManual[0].Kind);
        Assert.Equal(manual, withManual[0].Bounds);
        Assert.Equal(2, withManual.Count);
    }
}
