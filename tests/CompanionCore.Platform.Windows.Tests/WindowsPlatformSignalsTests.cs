using CompanionCore.Capture.Contracts;
using CompanionCore.Watchbun;
using static CompanionCore.Platform.Windows.Tests.Synthetic;

namespace CompanionCore.Platform.Windows.Tests;

public sealed class WindowsPlatformSignalsTests
{
    private sealed class Recorder
    {
        public List<ForegroundWindow> Foreground { get; } = [];

        public int Inputs { get; set; }

        public List<TargetExit> Exits { get; } = [];

        public List<CaptureTargetIdentity> Launches { get; } = [];

        public List<(bool Suspended, SuspendReason Reason)> Power { get; } = [];

        public void Attach(WindowsPlatformSignals signals)
        {
            signals.ForegroundChanged += (_, window) => Foreground.Add(window);
            signals.InputObserved += (_, _) => Inputs++;
            signals.TargetExited += (_, exit) => Exits.Add(exit);
            signals.TargetLaunched += (_, identity) => Launches.Add(identity);
            signals.Suspended += (_, reason) => Power.Add((true, reason));
            signals.Resumed += (_, reason) => Power.Add((false, reason));
        }
    }

    private static (WindowsPlatformSignals Signals, FakeNative Native, FakeDiscovery Discovery, Recorder Recorder, ManualTime Time) Create()
    {
        var native = new FakeNative();
        var discovery = new FakeDiscovery();
        var time = new ManualTime(T0);
        var signals = new WindowsPlatformSignals(native, discovery, time, startLoop: false);
        var recorder = new Recorder();
        recorder.Attach(signals);
        return (signals, native, discovery, recorder, time);
    }

    private static FakeProbe Watch(WindowsPlatformSignals signals, FakeNative native, CaptureTargetIdentity target)
    {
        var probe = new FakeProbe();
        native.Windows[target.WindowId] = target.ProcessId;
        native.Processes[target.ProcessId] = probe;
        signals.WatchTarget(target);
        return probe;
    }

    // ---- foreground and input --------------------------------------------------------

    [Fact]
    public async Task Foreground_IsMinimizedToTheTargetOrAZeroSentinel()
    {
        var (signals, native, _, recorder, _) = Create();
        var target = Identity();
        Watch(signals, native, target);

        native.Foreground = (0x99, 55);
        await signals.PollOnceAsync();
        native.Foreground = (0x98, 56);
        await signals.PollOnceAsync();
        native.Foreground = (target.WindowId, target.ProcessId);
        await signals.PollOnceAsync();
        await signals.PollOnceAsync();
        native.Foreground = (target.WindowId, 8);
        await signals.PollOnceAsync();

        Assert.Equal(
            [new ForegroundWindow(0, 0), new ForegroundWindow(target.WindowId, target.ProcessId), new ForegroundWindow(0, 0)],
            recorder.Foreground);
        Assert.DoesNotContain(recorder.Foreground, window => window.WindowId == 0x99 || window.ProcessId == 55 || window.ProcessId == 8);
    }

    [Fact]
    public async Task Input_IsRaisedOnlyWhenTheLastInputTickAdvances()
    {
        var (signals, native, _, recorder, _) = Create();
        Watch(signals, native, Identity());

        await signals.PollOnceAsync();
        await signals.PollOnceAsync();
        Assert.Equal(0, recorder.Inputs);

        native.LastInput = 1500;
        await signals.PollOnceAsync();
        await signals.PollOnceAsync();
        Assert.Equal(1, recorder.Inputs);
        native.LastInput = null;
        await signals.PollOnceAsync();
        Assert.Equal(1, recorder.Inputs);
        native.LastInput = 1600;
        await signals.PollOnceAsync();

        Assert.Equal(2, recorder.Inputs);
    }

    // ---- exits -----------------------------------------------------------------------

    [Fact]
    public async Task AWindowClosedBeforeACleanExit_IsADeliberateClose()
    {
        var (signals, native, _, recorder, _) = Create();
        var target = Identity();
        var probe = Watch(signals, native, target);
        await signals.PollOnceAsync();

        native.Windows.Remove(target.WindowId);
        await signals.PollOnceAsync();
        Assert.Empty(recorder.Exits);

        probe.State = ProcessProbeState.Exited;
        probe.ExitCode = 0;
        await signals.PollOnceAsync();

        var exit = Assert.Single(recorder.Exits);
        Assert.Equal(new TargetExit(target.ProcessId, 0, WindowClosedFirst: true, WasHung: false), exit);
        Assert.Equal(TargetExitKind.DeliberateClose, WatchbunEngine.Classify(exit));
        Assert.True(probe.Disposed);
    }

    [Fact]
    public async Task AWindowGoneInTheSamePollAsTheExit_ErrsTowardASuspectedCrash()
    {
        var (signals, native, _, recorder, _) = Create();
        var target = Identity();
        var probe = Watch(signals, native, target);
        await signals.PollOnceAsync();

        native.Windows.Remove(target.WindowId);
        probe.State = ProcessProbeState.Exited;
        await signals.PollOnceAsync();

        var exit = Assert.Single(recorder.Exits);
        Assert.False(exit.WindowClosedFirst);
        Assert.Equal(TargetExitKind.SuspectedCrash, WatchbunEngine.Classify(exit));
    }

    [Fact]
    public async Task AHungWindowBeforeTheExit_IsASuspectedCrash()
    {
        var (signals, native, _, recorder, _) = Create();
        var target = Identity();
        var probe = Watch(signals, native, target);
        native.Hung.Add(target.WindowId);
        await signals.PollOnceAsync();

        native.Windows.Remove(target.WindowId);
        await signals.PollOnceAsync();
        probe.State = ProcessProbeState.Exited;
        await signals.PollOnceAsync();

        var exit = Assert.Single(recorder.Exits);
        Assert.True(exit.WasHung);
        Assert.Equal(TargetExitKind.SuspectedCrash, WatchbunEngine.Classify(exit));
    }

    [Fact]
    public async Task ANonZeroExitCode_IsASuspectedCrash()
    {
        var (signals, native, _, recorder, _) = Create();
        var target = Identity();
        var probe = Watch(signals, native, target);
        native.Windows.Remove(target.WindowId);
        await signals.PollOnceAsync();
        probe.State = ProcessProbeState.Exited;
        probe.ExitCode = unchecked((int)0xC0000005);

        await signals.PollOnceAsync();

        var exit = Assert.Single(recorder.Exits);
        Assert.Equal(unchecked((int)0xC0000005), exit.ExitCode);
        Assert.Equal(TargetExitKind.SuspectedCrash, WatchbunEngine.Classify(exit));
    }

    [Fact]
    public async Task AnUnopenableProcess_ExitsWhenItsWindowIsGone_WithAnUnknownCode()
    {
        var (signals, native, _, recorder, _) = Create();
        var target = Identity();
        native.Windows[target.WindowId] = target.ProcessId;
        signals.WatchTarget(target);

        await signals.PollOnceAsync();
        Assert.Empty(recorder.Exits);
        native.Windows.Remove(target.WindowId);
        await signals.PollOnceAsync();
        await signals.PollOnceAsync();

        var exit = Assert.Single(recorder.Exits);
        Assert.Equal(-1, exit.ExitCode);
        Assert.Equal(TargetExitKind.SuspectedCrash, WatchbunEngine.Classify(exit));
    }

    [Fact]
    public async Task AnUnknownProbe_KeepsWatchingWhileTheWindowLives_ThenReportsAnUnknownExit()
    {
        var (signals, native, _, recorder, _) = Create();
        var target = Identity();
        var probe = Watch(signals, native, target);
        probe.State = ProcessProbeState.Unknown;

        await signals.PollOnceAsync();
        Assert.Empty(recorder.Exits);

        native.Windows.Remove(target.WindowId);
        await signals.PollOnceAsync();

        Assert.Equal(-1, Assert.Single(recorder.Exits).ExitCode);
    }

    [Fact]
    public async Task AWindowNoLongerOwnedByTheProcess_IsAnImmediateUnknownExit()
    {
        var (signals, native, _, recorder, _) = Create();
        var target = Identity();
        native.Windows[target.WindowId] = 999;
        native.Processes[target.ProcessId] = new FakeProbe();
        signals.WatchTarget(target);

        await signals.PollOnceAsync();

        var exit = Assert.Single(recorder.Exits);
        Assert.Equal(new TargetExit(target.ProcessId, -1, WindowClosedFirst: false, WasHung: false), exit);
    }

    [Fact]
    public async Task AfterAnExit_NothingIsPolledOrRaisedAgain()
    {
        var (signals, native, _, recorder, _) = Create();
        var probe = Watch(signals, native, Identity());
        probe.State = ProcessProbeState.Exited;
        await signals.PollOnceAsync();
        var calls = native.Calls;

        await signals.PollOnceAsync();
        await signals.PollOnceAsync();

        Assert.Single(recorder.Exits);
        Assert.Equal(calls, native.Calls);
        Assert.False(signals.Watching);
    }

    [Fact]
    public async Task Rewatching_DisposesTheEarlierProbe_AndResetsExitFacts()
    {
        var (signals, native, _, recorder, _) = Create();
        var first = Identity();
        var firstProbe = Watch(signals, native, first);
        native.Hung.Add(first.WindowId);
        await signals.PollOnceAsync();

        var second = Identity(window: 0x20, process: 9);
        var secondProbe = Watch(signals, native, second);
        Assert.True(firstProbe.Disposed);
        native.Windows.Remove(second.WindowId);
        await signals.PollOnceAsync();
        secondProbe.State = ProcessProbeState.Exited;
        await signals.PollOnceAsync();

        Assert.Equal(new TargetExit(second.ProcessId, 0, WindowClosedFirst: true, WasHung: false), Assert.Single(recorder.Exits));
    }

    [Fact]
    public async Task WatchingNothing_ClearsTheTarget()
    {
        var (signals, native, _, recorder, _) = Create();
        var probe = Watch(signals, native, Identity());
        signals.WatchTarget(null);
        Assert.True(probe.Disposed);
        var calls = native.Calls;

        await signals.PollOnceAsync();

        Assert.Equal(calls, native.Calls);
        Assert.Empty(recorder.Exits);
    }

    // ---- relaunch --------------------------------------------------------------------

    [Fact]
    public async Task Relaunch_RaisesOncePerNewSameExecutableProcess_OnABoundedInterval()
    {
        var (signals, _, discovery, recorder, time) = Create();
        var previous = Identity();
        signals.WatchForRelaunch(previous);
        discovery.Candidates.Add(Candidate(previous));
        discovery.Candidates.Add(Candidate(Identity(window: 0x30, process: 30, exe: "other.exe", fingerprint: 'B')));
        discovery.Candidates.Add(Candidate(Identity(window: 0x31, process: 31, fingerprint: 'C')));
        discovery.Candidates.Add(Candidate(Identity(window: 0x32, process: 32, exe: "renamed.exe")));
        discovery.Candidates.Add(Candidate(Identity(window: 0x40, process: 40, exe: "SYNTHETIC-GAME.EXE")));

        await signals.PollOnceAsync();
        Assert.Equal(40, Assert.Single(recorder.Launches).ProcessId);
        Assert.Equal(1, discovery.Calls);

        await signals.PollOnceAsync();
        Assert.Equal(1, discovery.Calls);

        time.Advance(WindowsPlatformSignals.RelaunchInterval);
        discovery.Candidates.Add(Candidate(Identity(window: 0x41, process: 41)));
        await signals.PollOnceAsync();

        Assert.Equal([40, 41], recorder.Launches.Select(identity => identity.ProcessId));
        Assert.Equal(2, discovery.Calls);

        signals.WatchForRelaunch(null);
        time.Advance(WindowsPlatformSignals.RelaunchInterval);
        await signals.PollOnceAsync();
        Assert.Equal(2, discovery.Calls);
    }

    [Fact]
    public async Task ARelaunchDiscoveryFailure_IsContained()
    {
        var (signals, _, discovery, recorder, _) = Create();
        signals.WatchForRelaunch(Identity());
        discovery.Failure = new InvalidOperationException("synthetic discovery failure");

        await signals.PollOnceAsync();

        Assert.Empty(recorder.Launches);
        Assert.Equal(1, signals.ContainedFailures);
    }

    [Fact]
    public async Task RelaunchMemory_IsBounded()
    {
        var (signals, _, discovery, recorder, _) = Create();
        signals.WatchForRelaunch(Identity());
        for (var process = 100; process < 100 + WindowsPlatformSignals.MaximumAnnouncedRelaunches + 10; process++)
        {
            discovery.Candidates.Add(Candidate(Identity(window: process, process: process)));
        }

        await signals.PollOnceAsync();

        Assert.Equal(WindowsPlatformSignals.MaximumAnnouncedRelaunches, recorder.Launches.Count);
    }

    // ---- power, idleness, containment, lifetime --------------------------------------

    [Fact]
    public void LockAndSleep_AreForwardedExactly()
    {
        var (signals, _, _, recorder, _) = Create();

        signals.ReportSuspended(SuspendReason.Lock);
        signals.ReportSuspended(SuspendReason.Sleep);
        signals.ReportResumed(SuspendReason.Sleep);
        signals.ReportResumed(SuspendReason.Lock);

        Assert.Equal([(true, SuspendReason.Lock), (true, SuspendReason.Sleep), (false, SuspendReason.Sleep), (false, SuspendReason.Lock)], recorder.Power);
    }

    [Fact]
    public async Task NothingWatched_MeansNoNativeOrDiscoveryCalls()
    {
        var (signals, native, discovery, recorder, _) = Create();

        await signals.PollOnceAsync();
        await signals.PollOnceAsync();

        Assert.Equal(0, native.Calls);
        Assert.Equal(0, discovery.Calls);
        Assert.Empty(recorder.Foreground);
        Assert.False(signals.Watching);
    }

    [Fact]
    public async Task AThrowingHandler_IsContained_AndOtherHandlersStillRun()
    {
        var (signals, native, _, recorder, _) = Create();
        signals.TargetExited += (_, _) => throw new InvalidOperationException("synthetic handler failure");
        var probe = Watch(signals, native, Identity());
        probe.State = ProcessProbeState.Exited;

        await signals.PollOnceAsync();

        Assert.Single(recorder.Exits);
        Assert.Equal(1, signals.ContainedFailures);
    }

    [Fact]
    public async Task TheLoop_ObservesAnExit_AndDisposeIsIdempotent()
    {
        var native = new FakeNative();
        var signals = new WindowsPlatformSignals(native, new FakeDiscovery());
        var exited = new TaskCompletionSource<TargetExit>(TaskCreationOptions.RunContinuationsAsynchronously);
        signals.TargetExited += (_, exit) => exited.TrySetResult(exit);
        var target = Identity();
        var probe = Watch(signals, native, target);

        probe.State = ProcessProbeState.Exited;
        probe.ExitCode = 3;
        var exit = await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(3, exit.ExitCode);
        await signals.DisposeAsync();
        await signals.DisposeAsync();
        signals.WatchTarget(target);
        signals.WatchForRelaunch(target);
        Assert.False(signals.Watching);
    }
}
