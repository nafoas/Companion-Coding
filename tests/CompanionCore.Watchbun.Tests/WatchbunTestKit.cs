using CompanionCore.Capture.Contracts;

namespace CompanionCore.Watchbun.Tests;

internal static class WatchbunTestKit
{
    internal static readonly DateTimeOffset T0 = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    internal static readonly Guid EngineId = Guid.Parse("00000000-0000-4000-8000-00000000b0b0");

    internal static CaptureTargetIdentity Target(int processId = 4321, long windowId = 0x1234, string executable = "synthetic-game.exe", char fingerprint = 'A') =>
        new(windowId, processId, executable, new string(fingerprint, 64));

    internal static CaptureAuthorizationGrant Grant(CaptureTargetIdentity? target = null, Guid? session = null) =>
        CaptureAuthorizationGrant.Issue(session ?? Guid.NewGuid(), 1, target ?? Target());

    internal static WatchbunEngine Engine(out CaptureAuthorizationGrant grant, WatchbunConfiguration? configuration = null)
    {
        grant = Grant();
        return new WatchbunEngine(grant, T0, configuration, EngineId);
    }

    internal static DateTimeOffset At(double minutes) => T0.AddMinutes(minutes);

    internal static StructuredGameEvent Event(CaptureAuthorizationGrant grant, GameEventKind kind, string key = "synthetic.event") =>
        new(grant.TargetSessionId, kind, key);

    internal static WatchbunIntentKind[] Kinds(WatchbunUpdate update) => update.Intents.Select(intent => intent.Kind).ToArray();

    /// <summary>The bound target or nothing: capture never names any other application.</summary>
    internal static void AssertCaptureBound(WatchbunUpdate update, CaptureTargetIdentity bound)
    {
        Assert.True(update.Snapshot.CaptureTarget is null || update.Snapshot.CaptureTarget == bound);
        Assert.Equal(update.Snapshot.Capture == CaptureMode.None, update.Snapshot.CaptureTarget is null);
        Assert.All(update.Intents.Where(intent => intent.Target is not null), intent =>
            Assert.True(WatchbunEngine.SameExecutable(intent.Target!, bound)));
        Assert.All(update.Intents, intent => Assert.True(intent.NoFocusSteal));
    }
}
