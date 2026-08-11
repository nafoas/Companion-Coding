using CompanionCore.Capture.Contracts;
using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth.Tests;

public sealed class TargetSessionControllerTests
{
    [Fact]
    public async Task DiscoveryAlone_ProducesNoWorkerStartOrFrame()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        var frames = 0;
        controller.FrameAdmitted += (_, _) => frames++;
        harness.Discovery.Candidates = [TargetAuthTestHarness.Candidate()];

        var discovery = await harness.Authorization.DiscoverAsync();

        Assert.Equal(TargetDiscoveryStatus.Ready, discovery.Status);
        Assert.Single(discovery.Candidates);
        Assert.Equal(0, worker.StartCount);
        Assert.Equal(0, frames);
    }

    [Fact]
    public async Task DeniedTarget_ProducesNoWorkerStartOrFrame()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        var frames = 0;
        controller.FrameAdmitted += (_, _) => frames++;
        var denied = TargetAuthTestHarness.Candidate(
            'B',
            102,
            202,
            "bitwarden.exe",
            ApplicationCategory.PasswordManager);

        var result = await controller.AuthorizeAsync(denied, explicitConsent: true);

        Assert.False(result.Succeeded);
        Assert.Equal(TargetSessionEventKind.Denied, result.EventKind);
        Assert.Equal(0, worker.StartCount);
        Assert.Equal(0, frames);
    }

    [Fact]
    public async Task AuthorizationGrant_IsRequiredBeforeWorkerStartAndAdmittedFrameMatchesExactTarget()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        var target = TargetAuthTestHarness.Candidate();
        var admitted = new List<CaptureFrameMetadata>();
        controller.FrameAdmitted += (_, frame) => admitted.Add(frame);

        Assert.Equal(0, worker.StartCount);
        Assert.Empty(admitted);

        var result = await controller.AuthorizeAsync(target, explicitConsent: true);

        Assert.True(result.Succeeded);
        Assert.Equal(1, worker.StartCount);
        var frame = Assert.Single(admitted);
        Assert.Equal(target.Identity, frame.Target);
        Assert.Equal(controller.CurrentSession.TargetSessionId, frame.TargetSessionId);
        Assert.Equal(controller.CurrentSession.Generation, frame.Generation);
    }

    [Fact]
    public async Task PrivacyStop_RevokesFirst_CancelsWork_ClearsBuffer_DropsLateFrame_AndIsIdempotent()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        var admitted = new List<CaptureFrameMetadata>();
        controller.FrameAdmitted += (_, frame) => admitted.Add(frame);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var oldGrant = worker.LastGrant!;
        var oldGeneration = oldGrant.Generation;
        var workToken = controller.CurrentTargetWorkToken;
        Assert.Single(admitted);

        var firstStop = await controller.PrivacyStopAsync();

        Assert.False(firstStop.WasAlreadyPaused);
        Assert.True(firstStop.HadActiveTarget);
        Assert.True(firstStop.CleanupComplete);
        Assert.True(firstStop.ClearedMetadataCount >= 1);
        Assert.True(workToken.IsCancellationRequested);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.False(harness.Privacy.IsCurrent(oldGeneration));
        Assert.Equal(0, worker.BufferedCount);

        worker.Emit(oldGrant);
        Assert.Single(admitted);

        var secondStop = await controller.PrivacyStopAsync();
        Assert.True(secondStop.WasAlreadyPaused);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(TargetSessionPhase.PrivacyPaused, controller.CurrentSession.Phase);
    }

    [Fact]
    public async Task PrivacyStopWithoutTarget_RequiresExplicitResumeAndNeverStartsWorker()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);

        var stop = await controller.PrivacyStopAsync();
        var blockedAuthorization = await controller.AuthorizeAsync(
            TargetAuthTestHarness.Candidate(),
            explicitConsent: true);
        var resume = await controller.ResumeExplicitlyAsync();

        Assert.False(stop.HadActiveTarget);
        Assert.False(blockedAuthorization.Succeeded);
        Assert.True(resume.Succeeded);
        Assert.Equal(TargetSessionEventKind.PrivacyResumed, resume.EventKind);
        Assert.False(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(TargetSessionPhase.None, controller.CurrentSession.Phase);
        Assert.Equal(0, worker.StartCount);
    }

    [Fact]
    public async Task ExplicitResume_RevalidatesAndCreatesStrictlyNewGenerationWithoutOldMetadata()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        var admitted = new List<CaptureFrameMetadata>();
        controller.FrameAdmitted += (_, frame) => admitted.Add(frame);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        var oldGrant = worker.LastGrant!;
        await controller.PrivacyStopAsync();

        var resumed = await controller.ResumeExplicitlyAsync();

        Assert.True(resumed.Succeeded);
        Assert.Equal(TargetSessionEventKind.Resumed, resumed.EventKind);
        Assert.True(worker.LastGrant!.Generation > oldGrant.Generation);
        Assert.Equal(oldGrant.TargetSessionId, worker.LastGrant.TargetSessionId);
        Assert.Equal(2, worker.StartCount);
        Assert.Equal(2, admitted.Count);
        Assert.All(admitted, frame => Assert.Equal(oldGrant.Target, frame.Target));

        worker.Emit(oldGrant);
        Assert.Equal(2, admitted.Count);
    }

    [Fact]
    public async Task StopArrivingDuringResume_InvalidatesResumeBeforeWorkerRestart()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        await controller.PrivacyStopAsync();

        var validationEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseValidation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Discovery.ValidationHandler = async (_, cancellationToken) =>
        {
            validationEntered.TrySetResult();
            await releaseValidation.Task.WaitAsync(cancellationToken);
            return true;
        };

        var resumeTask = controller.ResumeExplicitlyAsync();
        await validationEntered.Task;
        var laterStopTask = controller.PrivacyStopAsync();
        releaseValidation.TrySetResult();

        var resume = await resumeTask;
        var laterStop = await laterStopTask;

        Assert.False(resume.Succeeded);
        Assert.True(laterStop.WasAlreadyPaused);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(TargetSessionPhase.PrivacyPaused, controller.CurrentSession.Phase);
        Assert.Equal(1, worker.StartCount);
    }

    [Fact]
    public async Task StopClearFailure_RemainsPausedAndBlocksResumeUntilCleanupSucceeds()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        worker.FailStopAndClear = true;

        var stop = await controller.PrivacyStopAsync();
        var blockedResume = await controller.ResumeExplicitlyAsync();

        Assert.False(stop.CleanupComplete);
        Assert.False(blockedResume.Succeeded);
        Assert.False(blockedResume.CleanupComplete);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(TargetSessionPhase.PrivacyPaused, controller.CurrentSession.Phase);

        worker.FailStopAndClear = false;
        var recoveredResume = await controller.ResumeExplicitlyAsync();
        Assert.True(recoveredResume.Succeeded);
        Assert.False(harness.Privacy.Snapshot.IsPaused);
    }

    [Fact]
    public async Task UnexpectedWorkerStartFailure_PausesAndCreatesNoAdmittedFrame()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker
        {
            FailStart = true,
        };
        await using var controller = CreateController(harness, worker);
        var admitted = 0;
        controller.FrameAdmitted += (_, _) => admitted++;

        var result = await controller.AuthorizeAsync(
            TargetAuthTestHarness.Candidate(),
            explicitConsent: true);

        Assert.False(result.Succeeded);
        Assert.Equal(TargetSessionEventKind.PrivacyPaused, result.EventKind);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(TargetSessionPhase.PrivacyPaused, controller.CurrentSession.Phase);
        Assert.Equal(0, worker.StartCount);
        Assert.Equal(0, admitted);
    }

    [Fact]
    public async Task CancellationDuringWorkerStart_RevokesBeforeAnIgnoringWorkerCanEmit()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var enteredStart = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStart = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new RecordingCaptureWorker
        {
            StartHandler = async (_, _) =>
            {
                enteredStart.TrySetResult();
                await releaseStart.Task;
            },
        };
        await using var controller = CreateController(harness, worker);
        var admitted = 0;
        var lateFrameProduced = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        controller.FrameAdmitted += (_, _) => admitted++;
        worker.FrameProduced += (_, _) => lateFrameProduced.TrySetResult();
        using var cancellation = new CancellationTokenSource();

        var authorizationTask = controller.AuthorizeAsync(
            TargetAuthTestHarness.Candidate(),
            explicitConsent: true,
            cancellation.Token);
        await enteredStart.Task;
        await cancellation.CancelAsync();
        var result = await authorizationTask;
        releaseStart.TrySetResult();
        await lateFrameProduced.Task;

        Assert.False(result.Succeeded);
        Assert.Equal(TargetSessionEventKind.PrivacyPaused, result.EventKind);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(TargetSessionPhase.PrivacyPaused, controller.CurrentSession.Phase);
        Assert.Equal(0, admitted);
    }

    [Fact]
    public async Task DenyingCurrentExecutable_RevokesAndClearsBeforePolicyBecomesActive()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        var target = TargetAuthTestHarness.Candidate();
        var admitted = new List<CaptureFrameMetadata>();
        controller.FrameAdmitted += (_, frame) => admitted.Add(frame);
        await controller.AuthorizeAsync(target, explicitConsent: true);
        var oldGrant = worker.LastGrant!;
        Assert.Single(admitted);

        var resolved = await controller.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(AuthorizationCategory.Denied, TargetContentPolicy.TrustedGame));

        Assert.Equal(AuthorizationCategory.Denied, resolved.AuthorizationCategory);
        Assert.Equal(TargetContentPolicy.Standard, resolved.ContentPolicy);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(TargetSessionPhase.PrivacyPaused, controller.CurrentSession.Phase);
        Assert.Equal(CaptureWorkerStatus.Stopped, worker.Status);
        Assert.Equal(0, worker.BufferedCount);

        worker.Emit(oldGrant);
        Assert.Single(admitted);
    }

    [Fact]
    public async Task DisplayExpansion_PrivacyStopsActiveSessionAndNeverAutoResumes()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        harness.Topology.DisplayCount = 2;

        var stopped = await controller.HandleDisplayTopologyChangedAsync();

        Assert.True(stopped);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(TargetSessionPhase.PrivacyPaused, controller.CurrentSession.Phase);
        Assert.Equal(CaptureWorkerStatus.Stopped, worker.Status);
        Assert.Equal(0, harness.Discovery.DiscoveryCalls);

        harness.Topology.DisplayCount = 1;
        var automaticallyResumed = await controller.HandleDisplayTopologyChangedAsync();
        Assert.False(automaticallyResumed);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(1, worker.StartCount);
    }

    [Fact]
    public async Task ResumeWithStaleTarget_RemainsPausedAndDoesNotRestartWorker()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        await controller.PrivacyStopAsync();
        harness.Discovery.IsValid = false;

        var resume = await controller.ResumeExplicitlyAsync();

        Assert.False(resume.Succeeded);
        Assert.Equal(TargetSessionEventKind.TargetUnavailable, resume.EventKind);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(1, worker.StartCount);
    }

    [Fact]
    public async Task ResumeWithAdditionalDisplay_RemainsPausedWithoutValidationOrRestart()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        await controller.PrivacyStopAsync();
        harness.Topology.DisplayCount = 2;
        var validationCalls = harness.Discovery.ValidationCalls;

        var resume = await controller.ResumeExplicitlyAsync();

        Assert.False(resume.Succeeded);
        Assert.Equal(TargetSessionEventKind.DiscoveryBlocked, resume.EventKind);
        Assert.True(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(validationCalls, harness.Discovery.ValidationCalls);
        Assert.Equal(1, worker.StartCount);
    }

    [Fact]
    public async Task DisplayExpansionWithoutTarget_BlocksDiscoveryButDoesNotCreatePrivacyPause()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        harness.Topology.DisplayCount = 2;
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);

        var stopped = await controller.HandleDisplayTopologyChangedAsync();

        Assert.False(stopped);
        Assert.False(harness.Privacy.Snapshot.IsPaused);
        Assert.Equal(0, harness.Discovery.DiscoveryCalls);
        Assert.Equal(0, worker.StopAndClearCount);
    }

    [Fact]
    public async Task EndTarget_RevokesAuthorityClearsWorkerAndAllowsExplicitReplacement()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        var first = TargetAuthTestHarness.Candidate();
        var second = TargetAuthTestHarness.Candidate('B', 102, 202, "other-game.exe");
        await controller.AuthorizeAsync(first, explicitConsent: true);
        var firstGrant = worker.LastGrant!;

        await controller.EndSessionAsync();
        var replacement = await controller.AuthorizeAsync(second, explicitConsent: true);

        Assert.True(replacement.Succeeded);
        Assert.Equal(second.Identity, controller.CurrentSession.Candidate?.Identity);
        Assert.False(harness.Authorization.IsCurrent(firstGrant));
        Assert.Equal(2, worker.StartCount);
    }

    [Fact]
    public async Task SecondTargetAttempt_ReportsTheExactExistingAuthorizedTarget()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        var first = TargetAuthTestHarness.Candidate();
        var second = TargetAuthTestHarness.Candidate('B', 102, 202, "other-game.exe");
        TargetSessionEvent? latest = null;
        controller.SessionEvent += (_, targetEvent) => latest = targetEvent;
        await controller.AuthorizeAsync(first, explicitConsent: true);

        var rejected = await controller.AuthorizeAsync(second, explicitConsent: true);

        Assert.False(rejected.Succeeded);
        Assert.Equal(TargetSessionEventKind.AnotherTargetActive, rejected.EventKind);
        Assert.Equal(TargetSessionEventKind.AnotherTargetActive, latest?.Kind);
        Assert.Equal(first.Identity, latest?.Candidate?.Identity);
        Assert.Equal(first.Identity, controller.CurrentSession.Candidate?.Identity);
        Assert.Equal(1, worker.StartCount);
    }

    [Fact]
    public async Task EndArrivingDuringAuthorization_CancelsTheInFlightAuthorityAttempt()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        var validationEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseValidation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Discovery.ValidationHandler = async (_, cancellationToken) =>
        {
            validationEntered.TrySetResult();
            await releaseValidation.Task.WaitAsync(cancellationToken);
            return true;
        };

        var authorizationTask = controller.AuthorizeAsync(
            TargetAuthTestHarness.Candidate(),
            explicitConsent: true);
        await validationEntered.Task;
        var endTask = controller.EndSessionAsync();
        releaseValidation.TrySetResult();

        var authorization = await authorizationTask;
        await endTask;

        Assert.False(authorization.Succeeded);
        Assert.Equal(TargetSessionPhase.None, controller.CurrentSession.Phase);
        Assert.Equal(0, worker.StartCount);
        Assert.False(harness.Privacy.Snapshot.IsPaused);
    }

    [Fact]
    public async Task StandardSensitiveFrame_IsRejectedBeforeDownstream()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(
            harness,
            worker,
            _ => PrivacyAssessment.ClearlySensitive(SensitiveContentKind.Credential));
        var admitted = 0;
        controller.FrameAdmitted += (_, _) => admitted++;

        await controller.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);

        Assert.Equal(0, admitted);
        Assert.Equal(1, worker.StartCount);
    }

    [Fact]
    public async Task TrustedGameBypass_DoesNotBypassExactTargetOrGenerationChecks()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();
        await harness.Catalog.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(AuthorizationCategory.FamiliarAsk, TargetContentPolicy.TrustedGame));
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(
            harness,
            worker,
            _ => PrivacyAssessment.ClearlySensitive(SensitiveContentKind.Credential));
        var admitted = new List<CaptureFrameMetadata>();
        controller.FrameAdmitted += (_, frame) => admitted.Add(frame);
        await controller.AuthorizeAsync(target, explicitConsent: true);
        var currentGrant = worker.LastGrant!;

        Assert.Single(admitted);

        harness.Topology.DisplayCount = 2;
        worker.Emit(currentGrant);
        Assert.Single(admitted);

        await controller.PrivacyStopAsync();
        worker.Emit(currentGrant);
        Assert.Single(admitted);
    }

    [Fact]
    public async Task PolicyChangedToDenied_RejectsCurrentGrantBeforeDownstreamAdmission()
    {
        await using var harness = await TargetAuthTestHarness.CreateAsync();
        var target = TargetAuthTestHarness.Candidate();
        var worker = new RecordingCaptureWorker();
        await using var controller = CreateController(harness, worker);
        var admitted = new List<CaptureFrameMetadata>();
        controller.FrameAdmitted += (_, frame) => admitted.Add(frame);
        await controller.AuthorizeAsync(target, explicitConsent: true);
        var grant = worker.LastGrant!;
        Assert.Single(admitted);

        await harness.Catalog.SetExplicitPolicyAsync(
            target,
            new TargetPolicy(AuthorizationCategory.Denied, TargetContentPolicy.Standard));
        worker.Emit(grant);

        Assert.Single(admitted);
        Assert.False(harness.Authorization.IsCurrent(grant));
    }

    [Fact]
    public async Task FrameFromAnotherTarget_IsRejectedEvenWhenSessionAndGenerationMatch()
    {
        await using var firstHarness = await TargetAuthTestHarness.CreateAsync();
        await using var secondHarness = await TargetAuthTestHarness.CreateAsync();
        var firstWorker = new RecordingCaptureWorker();
        await using var firstController = CreateController(firstHarness, firstWorker);
        var secondWorker = new RecordingCaptureWorker();
        await using var secondController = CreateController(secondHarness, secondWorker);
        var admitted = new List<CaptureFrameMetadata>();
        firstController.FrameAdmitted += (_, frame) => admitted.Add(frame);
        await firstController.AuthorizeAsync(TargetAuthTestHarness.Candidate(), explicitConsent: true);
        await secondController.AuthorizeAsync(
            TargetAuthTestHarness.Candidate('B', 102, 202, "other.exe"),
            explicitConsent: true);

        firstWorker.Emit(secondWorker.LastGrant!);

        Assert.Single(admitted);
    }

    [Fact]
    public async Task FrameFromAnotherSession_IsRejectedEvenWhenTargetAndGenerationMatch()
    {
        await using var firstHarness = await TargetAuthTestHarness.CreateAsync();
        await using var secondHarness = await TargetAuthTestHarness.CreateAsync(sessionSeed: 1);
        var firstWorker = new RecordingCaptureWorker();
        await using var firstController = CreateController(firstHarness, firstWorker);
        var secondWorker = new RecordingCaptureWorker();
        await using var secondController = CreateController(secondHarness, secondWorker);
        var target = TargetAuthTestHarness.Candidate();
        var admitted = new List<CaptureFrameMetadata>();
        firstController.FrameAdmitted += (_, frame) => admitted.Add(frame);
        await firstController.AuthorizeAsync(target, explicitConsent: true);
        await secondController.AuthorizeAsync(target, explicitConsent: true);

        Assert.Equal(firstWorker.LastGrant?.Generation, secondWorker.LastGrant?.Generation);
        Assert.Equal(firstWorker.LastGrant?.Target, secondWorker.LastGrant?.Target);
        Assert.NotEqual(firstWorker.LastGrant?.TargetSessionId, secondWorker.LastGrant?.TargetSessionId);

        firstWorker.Emit(secondWorker.LastGrant!);

        Assert.Single(admitted);
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
