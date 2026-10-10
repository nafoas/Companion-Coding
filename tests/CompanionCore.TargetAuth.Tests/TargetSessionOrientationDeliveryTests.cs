using CompanionCore.Capture.Contracts;
using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth.Tests;

/// <summary>
/// The one orientation sheet per visual epoch must reach the consumer: it is never
/// displaced by newer regional sheets, and when it cannot be delivered the controller
/// asks the worker to retake it a bounded number of times.
/// </summary>
public sealed class TargetSessionOrientationDeliveryTests
{
    private const int Budget = TargetSessionController.OrientationDeliveryFrameBudget;

    [Fact]
    public async Task Orientation_IsTakenBeforeANewerRegionalSheet()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var grant = worker.LastGrant!;

        var orientation = worker.EmitSheet(grant, sourceSequenceNumber: 1);
        worker.Emit(grant);
        var regional = worker.EmitSheet(
            grant,
            sourceSequenceNumber: 2,
            kind: AttentionSheetKind.Regional);

        using (var first = controller.TakeLatestAttentionSheet())
        {
            Assert.Equal(orientation, first!.Metadata);
        }

        using var second = controller.TakeLatestAttentionSheet();
        Assert.Equal(regional, second!.Metadata);
    }

    [Fact]
    public async Task UnadmittedOrientation_IsNotDisplacedByANewerRegionalSheet()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var available = new List<AttentionSheetMetadata>();
        controller.AttentionSheetAvailable += (_, metadata) => available.Add(metadata);
        var grant = worker.LastGrant!;

        // The orientation's source frame (2) has not been admitted yet, so the
        // controller takes and holds it.
        var orientation = worker.EmitSheet(grant, sourceSequenceNumber: 2);
        Assert.Null(controller.TakeLatestAttentionSheet());

        // A newer regional sheet arrives and is pulled before the orientation's frame.
        var regional = worker.EmitSheet(
            grant,
            sourceSequenceNumber: 3,
            kind: AttentionSheetKind.Regional);
        Assert.Null(controller.TakeLatestAttentionSheet());

        worker.Emit(grant);
        worker.Emit(grant);
        Assert.Equal([orientation, regional], available);

        using (var first = controller.TakeLatestAttentionSheet())
        {
            Assert.Equal(orientation, first!.Metadata);
        }

        using var second = controller.TakeLatestAttentionSheet();
        Assert.Equal(regional, second!.Metadata);
    }

    [Fact]
    public async Task RegionalSheets_KeepFlowingWhileAnOrientationAwaitsAdmission()
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

        worker.EmitSheet(grant, sourceSequenceNumber: 2);
        worker.Emit(grant);
        Assert.Null(controller.TakeLatestAttentionSheet());

        var regional = worker.EmitSheet(
            grant,
            sourceSequenceNumber: 3,
            kind: AttentionSheetKind.Regional);
        worker.Emit(grant);

        using var taken = controller.TakeLatestAttentionSheet();
        Assert.Equal(regional, taken!.Metadata);
    }

    [Fact]
    public async Task DeliveredOrientation_NeverTriggersARetake()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var grant = worker.LastGrant!;

        worker.EmitSheet(grant, sourceSequenceNumber: 1);
        await EmitFramesAsync(controller, worker, grant, Budget * (TargetSessionController.MaximumOrientationRetakes + 2));

        Assert.Equal(0, worker.OrientationRequestCount);
        Assert.Equal(0, controller.OrientationRetakeRequests);
        Assert.Equal(0, controller.OrientationDeliveryFailures);
    }

    [Fact]
    public async Task UndeliveredOrientation_IsRetakenAfterEachBudgetAndThenStopsHonestly()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var grant = worker.LastGrant!;

        // Start already admitted frame 1, so the first budget needs Budget - 1 more.
        await EmitFramesAsync(controller, worker, grant, Budget - 2);
        Assert.Equal(0, worker.OrientationRequestCount);
        await EmitFramesAsync(controller, worker, grant, 1);
        Assert.Equal(1, worker.OrientationRequestCount);

        for (var attempt = 2; attempt <= TargetSessionController.MaximumOrientationRetakes; attempt++)
        {
            await EmitFramesAsync(controller, worker, grant, Budget - 1);
            Assert.Equal(attempt - 1, worker.OrientationRequestCount);
            await EmitFramesAsync(controller, worker, grant, 1);
            Assert.Equal(attempt, worker.OrientationRequestCount);
        }

        await EmitFramesAsync(controller, worker, grant, Budget * 4);
        Assert.Equal(TargetSessionController.MaximumOrientationRetakes, worker.OrientationRequestCount);
        Assert.Equal(TargetSessionController.MaximumOrientationRetakes, controller.OrientationRetakeRequests);
        Assert.Equal(1, controller.OrientationDeliveryFailures);
    }

    [Fact]
    public async Task PrivacyRejectedOrientation_IsRetakenAndTheFreshOneEndsTheFailsafe()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(
            harness,
            worker,
            frame => frame.SequenceNumber == 2
                ? PrivacyAssessment.ClearlySensitive(SensitiveContentKind.Credential)
                : PrivacyAssessment.Clear);
        var available = new List<AttentionSheetMetadata>();
        controller.AttentionSheetAvailable += (_, metadata) => available.Add(metadata);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var grant = worker.LastGrant!;

        // The orientation's source frame is privacy-rejected, so it can never be
        // delivered. Rejected frames do not count toward the budget.
        worker.EmitSheet(grant, sourceSequenceNumber: 2);
        worker.Emit(grant);
        await EmitFramesAsync(controller, worker, grant, Budget - 1);
        Assert.Equal(1, worker.OrientationRequestCount);
        Assert.Empty(available);

        // The retaken orientation comes from a later, admitted frame.
        var fresh = worker.EmitSheet(grant, sourceSequenceNumber: Budget + 2);
        worker.Emit(grant);
        Assert.Equal(fresh, Assert.Single(available));
        using (var taken = controller.TakeLatestAttentionSheet())
        {
            Assert.Equal(fresh, taken!.Metadata);
        }

        await EmitFramesAsync(controller, worker, grant, Budget * 3);
        Assert.Equal(1, worker.OrientationRequestCount);
        Assert.Equal(0, controller.OrientationDeliveryFailures);
    }

    [Fact]
    public async Task ResizeRecalibration_OwesAFreshOrientationWithAFreshBudget()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var grant = worker.LastGrant!;

        worker.EmitSheet(grant, sourceSequenceNumber: 1);
        await EmitFramesAsync(controller, worker, grant, Budget * 2);
        Assert.Equal(0, worker.OrientationRequestCount);

        worker.EmitStatus(CaptureWorkerStatus.Running, CaptureWorkerStatusReason.SourceResized);
        await EmitFramesAsync(controller, worker, grant, Budget - 1);
        Assert.Equal(0, worker.OrientationRequestCount);
        await EmitFramesAsync(controller, worker, grant, 1);
        Assert.Equal(1, worker.OrientationRequestCount);
    }

    [Fact]
    public async Task FailedRetakeRequests_AreContainedAndStillBounded()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker
        {
            OrientationRequestFailure = new IOException("Synthetic retake failure."),
        };
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var grant = worker.LastGrant!;

        await EmitFramesAsync(controller, worker, grant, Budget * (TargetSessionController.MaximumOrientationRetakes + 3));

        Assert.Equal(TargetSessionController.MaximumOrientationRetakes, worker.OrientationRequestCount);
        Assert.Equal(1, controller.OrientationDeliveryFailures);
        Assert.Equal(TargetSessionPhase.Authorized, controller.CurrentSession.Phase);
    }

    [Fact]
    public async Task StoppedWorker_OwesNoOrientation()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var grant = worker.LastGrant!;

        worker.EmitStatus(CaptureWorkerStatus.NoSignal, CaptureWorkerStatusReason.None);
        await EmitFramesAsync(controller, worker, grant, Budget * 2);

        Assert.Equal(0, worker.OrientationRequestCount);
    }

    private static async Task EmitFramesAsync(
        TargetSessionController controller,
        RecordingCaptureWorker worker,
        CaptureAuthorizationGrant grant,
        int count)
    {
        for (var index = 0; index < count; index++)
        {
            worker.Emit(grant);

            // Retakes run off the admission callback; settle each one so counts
            // are deterministic.
            await controller.OrientationRetakeSettled().WaitAsync(TimeSpan.FromSeconds(5));
        }
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
