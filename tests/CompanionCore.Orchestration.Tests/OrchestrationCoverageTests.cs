using CompanionCore.Api;
using CompanionCore.Attention;
using CompanionCore.Capture.Contracts;
using CompanionCore.Keepsakes;
using CompanionCore.Memory;
using CompanionCore.Recall;
using CompanionCore.TargetAuth;
using CompanionCore.Watchbun;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

/// <summary>
/// Closes the gaps a mutation pass over the orchestrator exposed: each test pins one wiring
/// decision that the scenario tests exercised only on their happy paths.
/// </summary>
public sealed class OrchestrationCoverageTests
{
    // ---- durable consolidation queue -------------------------------------------------

    [Fact]
    public async Task RejectedConsolidationIntent_StaysQueuedAcrossOtherSessions()
    {
        await using var harness = await CreateAsync();
        const string orphan = "target-session:rejected";
        var orphanOperation = CompanionOrchestrator.DeriveId("consolidation", orphan);

        // The orphan's operation id is already taken by different content, so its summary is
        // refused as a conflict every time it is tried.
        await harness.CommitSessionOriginalAsync(orphan, orphanOperation);
        await harness.Host.State.PutAsync(
            CompanionOrchestrator.ConsolidationStateName,
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[] { new CompanionOrchestrator.PendingConsolidation(orphan, orphanOperation, harness.Time.GetUtcNow(), null) }));

        var grant = await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "key", "synthetic.key", "[neutral memory] a key.")));
        await harness.SheetAsync();
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded));

        var current = $"target-session:{grant.TargetSessionId:N}";
        Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(current)));
        Assert.Empty(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(orphan)));
        var queued = Assert.Single(await harness.PendingConsolidationsAsync());
        Assert.Equal((orphan, orphanOperation), (queued.Session, queued.OperationId));
        Assert.True(harness.Has(CompanionNoticeKind.VaultBackedUp));
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task ACommittedButUndrainedIntent_ReplaysAsADuplicate_AndDrains()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "key", "synthetic.key", "[neutral memory] a key.")));
        await harness.SheetAsync();
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded));
        var reference = $"target-session:{grant.TargetSessionId:N}";
        Assert.Empty(await harness.PendingConsolidationsAsync());
        var summary = Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(reference)));

        // A crash after the commit but before the queue drained: the intent is still there.
        await harness.Host.DisposeAsync();
        var replay = new CompanionOrchestrator.PendingConsolidation(reference, summary.LocalOperationId, summary.Record.CreatedAtUtc, "synthetic-game");
        await harness.Host.State.PutAsync(CompanionOrchestrator.ConsolidationStateName, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[] { replay }));
        await harness.OpenHostAsync();

        // The replay excludes the session's own summary and highlights, so it is identical.
        Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(reference)));
        Assert.Empty(await harness.PendingConsolidationsAsync());
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task AFailedVaultBackup_IsReportedHonestly_AndNeverClaimedAsDone()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        await harness.Host.State.PutAsync("settings", "{\"volume\":2}"u8.ToArray());
        await File.WriteAllBytesAsync(harness.Host.State.PathFor("settings"), "damaged"u8.ToArray());

        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded));

        Assert.True(harness.Has(CompanionNoticeKind.VaultBackupFailed));
        Assert.False(harness.Has(CompanionNoticeKind.VaultBackedUp));
    }

    // ---- privacy, suspension, staleness -----------------------------------------------

    [Fact]
    public async Task APrivacyPause_DropsAPendingCameraAction()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        Assert.Equal(KeepsakeRefusal.None, await harness.Orchestrator.TakePhotographAsync());

        await harness.Controller.PrivacyStopAsync();
        await harness.SettleAsync();
        Assert.False((await harness.Orchestrator.GetSnapshotAsync()).PhotographPending);

        Assert.True((await harness.Controller.ResumeExplicitlyAsync()).Succeeded);
        await harness.SettleAsync();
        await harness.SheetAsync();

        Assert.Empty(await harness.Orchestrator.Keepsakes.ListAsync());
        Assert.Equal(1, harness.Provider.CallCount);
    }

    [Fact]
    public async Task ALockCancelsInFlightBraincaseWork()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Provider.Enqueue(async (request, cancellationToken) =>
        {
            using var registration = cancellationToken.Register(() => cancelled.TrySetResult());
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return ProviderReply.Success(MockSemanticProvider.DefaultResponseJson(request));
        });

        harness.Worker.EmitSheet(harness.Time.GetUtcNow());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        harness.Platform.Suspend(SuspendReason.Lock);

        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await harness.SettleAsync();
        Assert.False((await harness.Orchestrator.GetSnapshotAsync()).BridgeInFlight);
    }

    [Fact]
    public async Task ASheetForAnOlderGeneration_IsNeverInterpreted()
    {
        await using var harness = await CreateAsync();
        var original = await harness.AuthorizeAsync();
        await harness.Controller.PrivacyStopAsync();
        Assert.True((await harness.Controller.ResumeExplicitlyAsync()).Succeeded);
        await harness.SettleAsync();

        // The controller fences it first; the orchestrator checks the grant again regardless.
        harness.Worker.EmitSheet(harness.Time.GetUtcNow(), grant: original);
        await harness.SettleAsync();

        Assert.Equal(0, harness.Provider.CallCount);
    }

    [Fact]
    public async Task AnOutcomeForAnEndedSession_NeverReachesTheNextSession()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Provider.Enqueue(async (request, cancellationToken) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return ProviderReply.Success(MockSemanticProvider.DefaultResponseJson(request));
        });
        harness.Worker.EmitSheet(harness.Time.GetUtcNow());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded));
        await harness.AuthorizeAsync();
        var before = harness.Notices.Count(n => n.Kind is CompanionNoticeKind.Attention or CompanionNoticeKind.Conversation);
        var attention = (await harness.Orchestrator.GetSnapshotAsync()).Attention;

        release.SetResult();
        for (var attempt = 0; attempt < 500 && (await harness.Orchestrator.GetSnapshotAsync()).BridgeInFlight; attempt++)
        {
            await Task.Delay(10);
        }

        await harness.SettleAsync();
        Assert.False((await harness.Orchestrator.GetSnapshotAsync()).BridgeInFlight);
        Assert.Equal(before, harness.Notices.Count(n => n.Kind is CompanionNoticeKind.Attention or CompanionNoticeKind.Conversation));
        Assert.Equal(attention, (await harness.Orchestrator.GetSnapshotAsync()).Attention);
    }

    [Fact]
    public async Task WithoutAProcessMonitor_AnUnavailableTargetIsTreatedAsACrash()
    {
        await using var harness = await CreateAsync(withPlatform: false);
        await harness.AuthorizeAsync();
        await harness.Controller.PrivacyStopAsync();
        harness.Discovery.Valid = false;

        await harness.Controller.ResumeExplicitlyAsync();
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.ExitPromptRaised));

        var prompt = harness.Notices.First(n => n.Watchbun?.Kind == WatchbunIntentKind.ExitPromptRaised).Watchbun!;
        Assert.Equal(TargetExitKind.SuspectedCrash, prompt.Exit);
        Assert.Equal(WatchbunPhase.ExitPending, (await harness.Orchestrator.GetSnapshotAsync()).Watchbun!.Phase);
    }

    [Fact]
    public async Task ACompletedWatchTask_IsADecisiveAttentionMoment()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        await harness.Orchestrator.AddWatchTaskAsync("door.opened", TimeSpan.FromHours(2));

        var verdict = await harness.Orchestrator.SubmitGameEventLineAsync(
            $"{{\"session\":\"{grant.TargetSessionId:D}\",\"kind\":\"meaningful\",\"key\":\"door.opened\"}}");
        await harness.SettleAsync();

        Assert.Equal(AdapterVerdict.Accepted, verdict);
        Assert.True(harness.HasWatchbun(WatchbunIntentKind.WatchTaskCompleted));
        Assert.Equal(AttentionState.HighAttention, (await harness.Orchestrator.GetSnapshotAsync()).Attention!.State);
    }

    // ---- checkpoints -----------------------------------------------------------------

    [Fact]
    public async Task APausedAdventure_CheckpointsImmediately()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        harness.Platform.Exit(new TargetExit(grant.Target.ProcessId, 0, WindowClosedFirst: true, WasHung: false));
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.ExitPromptRaised));

        await harness.Orchestrator.DecideAfterExitAsync(ExitDecision.PreservePaused);

        var read = await harness.Host.State.GetAsync(CompanionOrchestrator.WatchbunStateName);
        var checkpoint = System.Text.Json.JsonSerializer.Deserialize<WatchbunCheckpoint>(read.Payload.Span)!;
        Assert.Equal(WatchbunPhase.PausedAdventure, checkpoint.Phase);
    }

    [Fact]
    public async Task Checkpoints_PersistOnTheConfiguredInterval()
    {
        await using var harness = await CreateAsync(new OrchestratorOptions { CheckpointInterval = TimeSpan.FromMinutes(1) });
        await harness.AuthorizeAsync();
        var initial = await WatchbunCheckpointBytesAsync(harness);

        await harness.AdvanceAndTickAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(initial, await WatchbunCheckpointBytesAsync(harness));

        await harness.AdvanceAndTickAsync(TimeSpan.FromSeconds(31));
        Assert.NotEqual(initial, await WatchbunCheckpointBytesAsync(harness));
    }

    private static async Task<byte[]> WatchbunCheckpointBytesAsync(OrchestrationHarness harness) =>
        (await harness.Host.State.GetAsync(CompanionOrchestrator.WatchbunStateName)).Payload.ToArray();

    // ---- photographs -----------------------------------------------------------------

    [Fact]
    public async Task AnOlderSheet_KeepsTheCameraWaiting_ForOneInsideTheWindow()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var asked = harness.Time.GetUtcNow();
        Assert.Equal(KeepsakeRefusal.None, await harness.Orchestrator.TakePhotographAsync());

        harness.Worker.EmitSheet(asked - TimeSpan.FromSeconds(5));
        await harness.SettleAsync();
        Assert.True((await harness.Orchestrator.GetSnapshotAsync()).PhotographPending);
        Assert.DoesNotContain(harness.Notices, n => n.Kind == CompanionNoticeKind.PhotographRefused);

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.SheetAsync();
        Assert.Contains(harness.Notices, n => n.Keepsake?.Kind == KeepsakeIntentKind.PhotographSaved);
    }

    [Fact]
    public async Task AnUndecodableSheet_RefusesThePhotograph_AndWritesNothing()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        await harness.Orchestrator.TakePhotographAsync();
        harness.Time.Advance(TimeSpan.FromSeconds(1));

        var corrupt = ScriptedCaptureWorker.EncodeUniform(ScriptedCaptureWorker.SheetWidth, ScriptedCaptureWorker.SheetHeight, 90);
        corrupt[^5] ^= 0xFF;
        harness.Worker.EmitSheet(harness.Time.GetUtcNow(), payload: corrupt);
        await harness.SettleAsync();

        Assert.Contains(harness.Notices, n => n.PhotographRefusal == KeepsakeRefusal.InvalidFrame);
        Assert.False((await harness.Orchestrator.GetSnapshotAsync()).PhotographPending);
        Assert.Empty(await harness.Orchestrator.Keepsakes.ListAsync());
    }

    [Fact]
    public async Task APhotographFrame_RequiresTheSheetsOwnGrant()
    {
        await using var harness = await CreateAsync();
        var original = await harness.AuthorizeAsync();
        await harness.Controller.PrivacyStopAsync();
        Assert.True((await harness.Controller.ResumeExplicitlyAsync()).Succeeded);
        var refreshed = harness.Controller.CurrentSession.Grant!;
        await harness.SettleAsync();

        var png = ScriptedCaptureWorker.EncodeUniform(ScriptedCaptureWorker.SheetWidth, ScriptedCaptureWorker.SheetHeight, 90);
        using var sheet = new AttentionSheet(Metadata(original, encodedLength: png.Length), png);
        Assert.NotNull(SheetPhotographSource.TryCreate(sheet, original));
        Assert.Null(SheetPhotographSource.TryCreate(sheet, refreshed));
    }

    [Fact]
    public void SheetDecoder_RejectsEveryMalformedVariant()
    {
        const int width = 8, height = 4;
        var rows = ScriptedCaptureWorker.UniformRows(width, height, 50);
        Assert.True(SheetPhotographSource.TryDecodeRgba(ScriptedCaptureWorker.EncodeRaw(width, height, rows), out _, out _, out _));

        // A CRC flipped in the IHDR checksum itself (bytes 29..32), with the data untouched.
        var badCrc = ScriptedCaptureWorker.EncodeRaw(width, height, rows);
        badCrc[29] ^= 1;
        Assert.False(SheetPhotographSource.TryDecodeRgba(badCrc, out _, out _, out _));

        // More inflated bytes than the header declares.
        Assert.False(SheetPhotographSource.TryDecodeRgba(ScriptedCaptureWorker.EncodeRaw(width, height, [.. rows, 0]), out _, out _, out _));

        // A non-zero (unsupported) scanline filter.
        var filtered = rows.ToArray();
        filtered[(width * 4 + 1) * 2] = 1;
        Assert.False(SheetPhotographSource.TryDecodeRgba(ScriptedCaptureWorker.EncodeRaw(width, height, filtered), out _, out _, out _));

        // A second IHDR.
        Assert.False(SheetPhotographSource.TryDecodeRgba(ScriptedCaptureWorker.EncodeRaw(width, height, rows, duplicateHeader: true), out _, out _, out _));
    }

    // ---- evidence mapping ------------------------------------------------------------

    [Fact]
    public void Evidence_NoveltyFallsForRepeatedTopics_AndTheStrongestObservationLeads()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var interpretation = Interpretation(("Door", 0.4), ("Lantern", 0.9), ("Map", 0.6));

        var first = SemanticEvidenceMapper.Map(interpretation, Metadata(changeScore: 0.1), T0, seen);
        Assert.All(first.Events, e => Assert.Equal(0.8, e.Signals.Novelty));
        Assert.Equal(new GameObservationKey("lantern", 0.9), Key(first.Observation));
        Assert.Equal(("lantern", GameEventKind.Meaningful), (first.WatchbunEvent!.Key, first.WatchbunEvent.Kind));

        var repeat = SemanticEvidenceMapper.Map(interpretation, Metadata(changeScore: 0.1), T0, seen);
        Assert.All(repeat.Events, e => Assert.Equal(0.2, e.Signals.Novelty));
        Assert.Equal(GameEventKind.Change, repeat.WatchbunEvent!.Kind);

        var changed = SemanticEvidenceMapper.Map(interpretation, Metadata(changeScore: SemanticEvidenceMapper.MeaningfulChange), T0, seen);
        Assert.Equal(GameEventKind.Meaningful, changed.WatchbunEvent!.Kind);

        var empty = SemanticEvidenceMapper.Map(Interpretation(), Metadata(), T0, seen);
        Assert.Empty(empty.Events);
        Assert.Null(empty.Observation);
        Assert.Null(empty.WatchbunEvent);
    }

    private sealed record GameObservationKey(string Topic, double Confidence);

    private static GameObservationKey? Key(CompanionCore.Conversation.GameObservation? observation) =>
        observation is null ? null : new GameObservationKey(observation.TopicKey, observation.Significance);

    private static SemanticInterpretation Interpretation(params (string Label, double Confidence)[] observations) =>
        new(Guid.NewGuid(), SessionId, "[neutral summary]", [.. observations.Select(o => new SemanticObservation(AttentionRegionKind.FullContext, o.Label, o.Confidence))]);

    private static readonly Guid SessionId = Guid.NewGuid();

    private static AttentionSheetMetadata Metadata(CaptureAuthorizationGrant? grant = null, double changeScore = 0.5, int encodedLength = 1) => new()
    {
        TargetSessionId = grant?.TargetSessionId ?? SessionId,
        Generation = grant?.Generation ?? 1,
        Target = grant?.Target ?? Candidate().Identity,
        SourceSequenceNumber = 1,
        SourceTimestamp = T0,
        SourceWidth = 640,
        SourceHeight = 480,
        SheetWidth = ScriptedCaptureWorker.SheetWidth,
        SheetHeight = ScriptedCaptureWorker.SheetHeight,
        EncodedByteLength = encodedLength,
        Kind = AttentionSheetKind.Orientation,
        ChangeScore = changeScore,
        Regions =
        [
            new AttentionSheetRegionMetadata
            {
                Kind = AttentionRegionKind.FullContext,
                NormalizedSource = new NormalizedRegion(0, 0, 1, 1),
                SourcePixels = new PixelRect(0, 0, 640, 480),
                SheetPixels = new PixelRect(0, 0, ScriptedCaptureWorker.SheetWidth, ScriptedCaptureWorker.SheetHeight),
            },
        ],
    };
}
