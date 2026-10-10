using CompanionCore.Capture.Contracts;
using CompanionCore.TargetAuth;

namespace CompanionCore.Platform.Windows.Tests;

internal sealed class ManualTime(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

internal sealed class FakeProbe : IProcessProbe
{
    public ProcessProbeState State { get; set; } = ProcessProbeState.Running;

    public int ExitCode { get; set; }

    public bool Disposed { get; private set; }

    public ProcessProbeState Poll(out int exitCode)
    {
        exitCode = State == ProcessProbeState.Exited ? ExitCode : -1;
        return State;
    }

    public void Dispose() => Disposed = true;
}

/// <summary>A scripted native surface; every call is counted so idle polling is observable.</summary>
internal sealed class FakeNative : IWindowsPlatformNative
{
    public (long WindowId, int ProcessId) Foreground { get; set; }

    public uint? LastInput { get; set; } = 1000;

    public Dictionary<long, int> Windows { get; } = [];

    public HashSet<long> Hung { get; } = [];

    public Dictionary<int, FakeProbe> Processes { get; } = [];

    public int Calls { get; private set; }

    public (long WindowId, int ProcessId) GetForeground()
    {
        Calls++;
        return Foreground;
    }

    public uint? GetLastInputTick()
    {
        Calls++;
        return LastInput;
    }

    public bool IsWindow(long windowId)
    {
        Calls++;
        return Windows.ContainsKey(windowId);
    }

    public int GetWindowProcessId(long windowId)
    {
        Calls++;
        return Windows.TryGetValue(windowId, out var process) ? process : 0;
    }

    public bool IsHung(long windowId)
    {
        Calls++;
        return Hung.Contains(windowId);
    }

    public IProcessProbe? OpenProcess(int processId)
    {
        Calls++;
        return Processes.TryGetValue(processId, out var probe) ? probe : null;
    }
}

internal sealed class FakeDiscovery : ITargetDiscovery
{
    public List<TargetCandidate> Candidates { get; } = [];

    public int Calls { get; private set; }

    public Exception? Failure { get; set; }

    public Task<IReadOnlyList<TargetCandidate>> DiscoverAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Failure is { } failure
            ? Task.FromException<IReadOnlyList<TargetCandidate>>(failure)
            : Task.FromResult<IReadOnlyList<TargetCandidate>>([.. Candidates]);
    }

    public Task<bool> IsStillValidAsync(TargetCandidate target, CancellationToken cancellationToken) => Task.FromResult(true);
}

internal static class Synthetic
{
    internal static readonly DateTimeOffset T0 = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    internal static CaptureTargetIdentity Identity(long window = 0x10, int process = 7, string exe = "synthetic-game.exe", char fingerprint = 'A') =>
        new(window, process, exe, new string(fingerprint, 64));

    internal static TargetCandidate Candidate(CaptureTargetIdentity identity) => new(identity, ApplicationCategory.Game);
}
