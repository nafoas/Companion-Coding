using CompanionCore.Api;
using CompanionCore.Capture.Contracts;
using CompanionCore.Memory;
using CompanionCore.Privacy;
using CompanionCore.Recall;
using CompanionCore.TargetAuth;
using CompanionCore.Watchbun;
using static CompanionCore.Orchestration.Tests.OrchestrationHarness;

namespace CompanionCore.Orchestration.Tests;

public sealed class OrchestrationMechanicsTests
{
    [Fact]
    public async Task LockAndSleep_SuspendCaptureAndSpending_ThenResumeExactly()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        harness.Platform.Suspend(SuspendReason.Lock);
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.CaptureSuspended));

        await harness.SheetAsync();
        Assert.Equal(0, harness.Provider.CallCount);
        await harness.AdvanceAndTickAsync(TimeSpan.FromHours(5));
        Assert.False(harness.HasWatchbun(WatchbunIntentKind.QuietCheckAsked));

        harness.Platform.Resume(SuspendReason.Lock);
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.CaptureRestored));
        await harness.SheetAsync();
        Assert.Equal(1, harness.Provider.CallCount);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task IndefiniteWatch_KeepsTheSessionOpenWithSpendingPaused_UntilTargetInput()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        await harness.AdvanceAndTickAsync(TimeSpan.FromMinutes(61));
        Assert.Equal(WatchbunRefusal.None, await harness.Orchestrator.AnswerQuietCheckAsync(QuietAnswer.BackEventually));

        for (var hour = 0; hour < 6; hour++)
        {
            await harness.AdvanceAndTickAsync(TimeSpan.FromHours(1));
        }

        Assert.False(harness.HasWatchbun(WatchbunIntentKind.SessionClosed));
        await harness.SheetAsync();
        Assert.Equal(0, harness.Provider.CallCount);

        harness.Platform.Foreground(grant.Target.WindowId, grant.Target.ProcessId);
        harness.Platform.Input();
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.IndefiniteWatchEnded));
        await harness.SheetAsync();
        Assert.Equal(1, harness.Provider.CallCount);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task CrashThenConsolidate_SummarizesEverySessionInTheSpan_AndBacksUp()
    {
        await using var harness = await CreateAsync();
        var first = await harness.AuthorizeAsync();
        harness.Provider.Enqueue(MockSemanticProvider.Respond(request => ResponseWithMemory(request.OperationId, "one", "synthetic.one", "[neutral memory] one.")));
        await harness.SheetAsync();
        harness.Platform.Exit(new TargetExit(first.Target.ProcessId, 0, WindowClosedFirst: true, WasHung: false));
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.ExitPromptRaised));
        Assert.Equal(TargetExitKind.DeliberateClose, harness.Notices.Single(n => n.Watchbun?.Kind == WatchbunIntentKind.ExitPromptRaised).Watchbun!.Exit);

        Assert.Equal(WatchbunRefusal.None, await harness.Orchestrator.DecideAfterExitAsync(ExitDecision.Consolidate));
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.VaultBackedUp));

        Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary($"target-session:{first.TargetSessionId:N}")));
        var snapshot = await harness.Orchestrator.GetSnapshotAsync();
        Assert.Null(snapshot.Watchbun);
        Assert.Empty(snapshot.UnconsolidatedSessions);
        Assert.Equal(0, snapshot.Faults);
    }

    [Fact]
    public async Task PausedAdventure_IsRecordedAppendOnlyInMemory()
    {
        await using var harness = await CreateAsync();
        var grant = await harness.AuthorizeAsync();
        harness.Platform.Exit(new TargetExit(grant.Target.ProcessId, 3, WindowClosedFirst: false, WasHung: false));
        await harness.WaitForAsync(() => harness.HasWatchbun(WatchbunIntentKind.ExitPromptRaised));

        await harness.Orchestrator.DecideAfterExitAsync(ExitDecision.PreservePaused);

        var adventure = Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Adventure("synthetic-game", CompanionOrchestrator.DefaultSave)));
        Assert.Equal(AdventureStatus.Paused, RecallMetadata.Parse(adventure.Record.RetrievalMetadataJson).Adventure);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task OfflineBraincase_ReportsUnavailableOnce_AndWritesNothing()
    {
        await using var harness = await CreateAsync();
        await harness.Host.DisposeAsync();
        var offline = new RealSemanticProviderShell(new InMemoryCredentialStore());
        await using var host = await CompanionHost.OpenAsync(new CompanionHostOptions(harness.Location, harness.Privacy, harness.Controller, offline, new InMemoryCredentialStore())
        {
            Time = harness.Time,
            Platform = harness.Platform,
            Notice = (_, notice) => harness.Notices.Enqueue(notice),
            Bridge = new BridgeOptions { InitialBackoff = TimeSpan.Zero, MaximumBackoff = TimeSpan.Zero },
        });
        harness.Discovery.Candidates.Add(Candidate());
        await harness.Controller.SetExplicitPolicyAsync(Candidate(), new TargetPolicy(AuthorizationCategory.StandingAuthorized, TargetContentPolicy.TrustedGame));
        await harness.Controller.AuthorizeAsync(Candidate(), explicitConsent: true);
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionStarted), host.Orchestrator);

        for (var index = 0; index < 3; index++)
        {
            harness.Worker.EmitSheet(harness.Time.GetUtcNow());
            for (var attempt = 0; attempt < 100 && (await host.Orchestrator.GetSnapshotAsync()).BridgeInFlight; attempt++)
            {
                await Task.Delay(10);
            }
        }

        await harness.WaitForAsync(() => harness.Notices.Any(n => n.Kind == CompanionNoticeKind.Braincase), host.Orchestrator);
        var braincase = harness.Notices.Where(n => n.Kind == CompanionNoticeKind.Braincase).ToArray();
        Assert.Single(braincase);
        Assert.Contains(braincase[0].Braincase, new BridgeOutcomeKind?[] { BridgeOutcomeKind.Unavailable, BridgeOutcomeKind.Napping });
        Assert.Empty(await host.Repository.RetrieveSubjectPrefixPageAsync("synthetic", null, 10));
        Assert.Equal(0, host.Orchestrator.Faults);
        await harness.Controller.EndSessionAsync();
        await harness.WaitForAsync(() => harness.Has(CompanionNoticeKind.SessionEnded), host.Orchestrator);
    }

    [Fact]
    public async Task SessionsLeftByAnInterruptedClose_AreConsolidatedBeforeAFreshWatch()
    {
        await using var harness = await CreateAsync();
        const string leftover = "target-session:leftover";
        await harness.CommitSessionOriginalAsync(leftover);
        await harness.Host.DisposeAsync();
        await harness.Host.State.PutAsync(CompanionOrchestrator.SessionsStateName, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[] { leftover }));
        await harness.OpenHostAsync();
        Assert.Equal([leftover], (await harness.Orchestrator.GetSnapshotAsync()).UnconsolidatedSessions);

        await harness.AuthorizeAsync();

        Assert.Single(await harness.Host.Repository.RetrieveBySubjectAsync(RecallSubjects.Summary(leftover)));
        Assert.DoesNotContain(leftover, (await harness.Orchestrator.GetSnapshotAsync()).UnconsolidatedSessions);
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task SheetBurst_KeepsAtMostOneBridgeCallInFlight_AndZeroesEverySheet()
    {
        await using var harness = await CreateAsync();
        await harness.AuthorizeAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Provider.Enqueue(async (request, cancellationToken) =>
        {
            await release.Task.WaitAsync(cancellationToken);
            return ProviderReply.Success(MockSemanticProvider.DefaultResponseJson(request));
        });

        // Hold the first call in flight, then deliver more sheets one at a time, each fully
        // processed by the mailbox, so a missing single-flight guard deterministically starts
        // a second call (a burst alone could collapse into the latest-sheet slot).
        harness.Worker.EmitSheet(harness.Time.GetUtcNow(), shade: 0);
        await harness.WaitForAsync(() => harness.Provider.CallCount == 1);
        for (var index = 1; index < 20; index++)
        {
            harness.Worker.EmitSheet(harness.Time.GetUtcNow(), shade: (byte)index);
            await harness.Orchestrator.GetSnapshotAsync();
        }

        await Task.Delay(50);
        Assert.Equal(1, harness.Provider.CallCount);
        Assert.True((await harness.Orchestrator.GetSnapshotAsync()).BridgeInFlight);
        release.SetResult();
        await harness.SettleAsync();

        Assert.Equal(1, harness.Provider.CallCount);
        Assert.All(harness.Worker.Emitted, sheet => Assert.Equal(0, sheet.Length));
        Assert.Equal(0, harness.Orchestrator.Faults);
    }

    [Fact]
    public async Task AThrowingNoticeHandler_NeverBreaksThePipeline()
    {
        await using var harness = await CreateAsync();
        harness.Orchestrator.Notice += (_, notice) =>
        {
            if (notice.Kind != CompanionNoticeKind.Fault)
            {
                throw new InvalidOperationException("synthetic presentation failure");
            }
        };

        await harness.AuthorizeAsync();
        await harness.SheetAsync();

        Assert.Equal(1, harness.Provider.CallCount);
        Assert.True(harness.Orchestrator.Faults > 0);
        Assert.Contains(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Fault && notice.Key == nameof(InvalidOperationException));
        Assert.Contains(harness.Notices, notice => notice.Kind == CompanionNoticeKind.Braincase);
    }

    [Fact]
    public async Task ADisposedOrchestrator_RefusesCommands_AndDisposeIsIdempotent()
    {
        await using var harness = await CreateAsync();
        var orchestrator = harness.Orchestrator;
        await harness.Host.DisposeAsync();
        await orchestrator.DisposeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.TickAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.StartAsync());
    }

    [Theory]
    [InlineData("Glowing Door", "glowing-door")]
    [InlineData("  ---Boss!!  Fight  ", "boss-fight")]
    [InlineData("", "observation")]
    [InlineData(null, "observation")]
    [InlineData("!!!", "observation")]
    [InlineData("Ünïcödé", "unicode")]
    [InlineData("日本語", "observation")]
    [InlineData("a.b_c-d", "a.b_c-d")]
    public void Topics_AreBoundedWatchbunCompatibleKeys(string? label, string expected)
    {
        var topic = SemanticEvidenceMapper.Topic(label);

        Assert.Equal(expected, topic);
        Assert.True(WatchbunEngine.ValidEventKey(topic));
    }

    [Fact]
    public void Topics_AreLengthBounded()
    {
        var topic = SemanticEvidenceMapper.Topic(new string('x', 500));
        Assert.Equal(SemanticEvidenceMapper.MaximumTopicCharacters, topic.Length);
    }

    [Fact]
    public void SheetDecoder_AcceptsOnlyTheStrictWorkerFormat()
    {
        var good = ScriptedCaptureWorker.EncodeUniform(8, 4, 50);
        Assert.True(SheetPhotographSource.TryDecodeRgba(good, out var width, out var height, out var rgba));
        Assert.Equal((8, 4, 8 * 4 * 4), (width, height, rgba.Length));
        Assert.Equal(new byte[] { 50, 50, 40, 255 }, rgba[..4]);

        var badCrc = good.ToArray();
        badCrc[20] ^= 1;
        Assert.False(SheetPhotographSource.TryDecodeRgba(badCrc, out _, out _, out _));
        Assert.False(SheetPhotographSource.TryDecodeRgba(good[..^12], out _, out _, out _));
        Assert.False(SheetPhotographSource.TryDecodeRgba([1, 2, 3], out _, out _, out _));
        Assert.False(SheetPhotographSource.TryDecodeRgba([], out _, out _, out _));
    }

    [Fact]
    public void Ids_AndGameReferences_AreStableAndSafe()
    {
        Assert.Equal(CompanionOrchestrator.DeriveId("x", "y"), CompanionOrchestrator.DeriveId("x", "y"));
        Assert.NotEqual(CompanionOrchestrator.DeriveId("x", "y"), CompanionOrchestrator.DeriveId("x", "z"));
        Assert.Equal("synthetic-game", CompanionOrchestrator.GameReference("Synthetic-Game.EXE"));
        Assert.Equal("c-x", CompanionOrchestrator.GameReference("c:x.exe"));
        Assert.Equal("game", CompanionOrchestrator.GameReference(@"C:\Games\Game.exe"));
        Assert.Equal("game", CompanionOrchestrator.GameReference("/opt/games/game.exe"));
        Assert.Equal("my.game", CompanionOrchestrator.GameReference("My.Game.exe"));
        Assert.Equal("noextension", CompanionOrchestrator.GameReference("NoExtension"));
        Assert.Equal("application", CompanionOrchestrator.GameReference(".exe"));
        Assert.Equal(120, CompanionOrchestrator.GameReference(new string('g', 200) + ".exe").Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrchestratorOptions { CheckpointInterval = TimeSpan.Zero }.Validate());
    }
}
