using CompanionCore.Capture.Contracts;
using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth.Tests;

/// <summary>
/// Full-resolution keepsake photographs (deferred WIRE-01 D2): a photograph is requested
/// only for the current authorized target, holds its own slot so newer regional sheets
/// never displace it, and passes the same privacy frame admission as every other sheet.
/// </summary>
public sealed class TargetSessionPhotographTests
{
    [Fact]
    public async Task AHeldPhotograph_IsDeliveredAfterTheOrientation_AndBeforeANewerRegionalSheet()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var grant = worker.LastGrant!;

        // None of these sheets' source frames (2, 3, 4) is admitted yet: each is held.
        var orientation = worker.EmitSheet(grant, sourceSequenceNumber: 2);
        Assert.Null(controller.TakeLatestAttentionSheet());
        var photograph = worker.EmitSheet(grant, sourceSequenceNumber: 3, kind: AttentionSheetKind.Photograph);
        Assert.Null(controller.TakeLatestAttentionSheet());
        var regional = worker.EmitSheet(grant, sourceSequenceNumber: 4, kind: AttentionSheetKind.Regional);
        Assert.Null(controller.TakeLatestAttentionSheet());

        worker.Emit(grant);
        worker.Emit(grant);
        worker.Emit(grant);

        using (var first = controller.TakeLatestAttentionSheet())
        {
            Assert.Equal(orientation, first!.Metadata);
        }

        using (var second = controller.TakeLatestAttentionSheet())
        {
            Assert.Equal(photograph, second!.Metadata);
        }

        using var third = controller.TakeLatestAttentionSheet();
        Assert.Equal(regional, third!.Metadata);
    }

    [Fact]
    public async Task APhotographWhoseFrameIsPrivacyRejected_IsNeverDelivered()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(
            harness,
            worker,
            frame => frame.SequenceNumber == 2
                ? PrivacyAssessment.ClearlySensitive(SensitiveContentKind.Credential)
                : PrivacyAssessment.Clear);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var grant = worker.LastGrant!;

        worker.EmitSheet(grant, sourceSequenceNumber: 2, kind: AttentionSheetKind.Photograph);
        worker.Emit(grant);
        Assert.Null(controller.TakeLatestAttentionSheet());
        Assert.Null(controller.TakeLatestAttentionSheet());
    }

    [Fact]
    public async Task AManualRegionChange_ReleasesAHeldPhotograph()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var grant = worker.LastGrant!;

        worker.EmitSheet(grant, sourceSequenceNumber: 2, kind: AttentionSheetKind.Photograph);
        Assert.Null(controller.TakeLatestAttentionSheet());
        await controller.SetManualRegionAsync(new NormalizedRegion(0.1, 0.1, 0.5, 0.5));
        worker.Emit(grant);

        Assert.Null(controller.TakeLatestAttentionSheet());
    }

    [Fact]
    public async Task APhotographIsRequested_OnlyForTheCurrentAuthorizedTarget()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.RequestPhotographAsync());
        Assert.Equal(0, worker.PhotographRequestCount);

        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        await controller.RequestPhotographAsync();
        Assert.Equal(1, worker.PhotographRequestCount);

        // Never while privacy-paused, and never after the session ends.
        await controller.PrivacyStopAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.RequestPhotographAsync());
        await controller.EndSessionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.RequestPhotographAsync());
        Assert.Equal(1, worker.PhotographRequestCount);
    }

    private static TargetSessionController CreateController(
        TargetAuthTestHarness harness,
        RecordingCaptureWorker worker,
        Func<CaptureFrameMetadata, PrivacyAssessment>? assessmentProvider = null) =>
        new(
            harness.Authorization,
            worker,
            harness.Privacy,
            new LocalPrivacyGuard(),
            assessmentProvider);
}
