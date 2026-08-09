namespace CompanionCore.App.IntegrationTests;

/// <summary>
/// Covers the four acceptance items from <c>tasks/active/task-01-skeleton.md</c> that
/// cannot be closed by unit tests or code review alone: a real launch with no key,
/// network, or capture; real multiple windows sharing one runtime construction; a
/// genuine second OS process being rejected without ever building a runtime; and clean
/// shutdown leaving no process behind. Each test launches the actual compiled
/// <c>CompanionCore.App.exe</c> via <see cref="AppProcess"/>.
/// </summary>
public sealed class AppProcessTests
{
    [Fact]
    public void Ready_LaunchesWithNoKeyNetworkOrCapture_AndReachesReadyState()
    {
        // Nothing in CompanionCore.App's Task 1 code path touches the network or a
        // credential store — there is no such code to bypass. This test's job is to
        // prove the process actually launches, constructs its one runtime, reaches the
        // ready lifecycle state, and exits cleanly, none of which code review alone can
        // confirm about the real compiled artifact.
        var result = AppProcess.Run("ready");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("READY", result.StdOut);
        Assert.Contains("CONSTRUCTIONS:1", result.StdOut);
    }

    [Fact]
    public void MultiWindow_ThreeRealWindows_ShareExactlyOneRuntimeConstruction()
    {
        var result = AppProcess.Run("multiwindow");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("WINDOWS:3", result.StdOut);
        Assert.Contains("CONSTRUCTIONS:1", result.StdOut);
    }

    [Fact]
    public void Shutdown_StopThenClose_ExitsCleanlyWithStoppedStateAndNoLeftoverProcess()
    {
        var result = AppProcess.Run("shutdown");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SHUTDOWN:Stopped", result.StdOut);
        // AppProcess.Run itself already proves the process exited (WaitForExit
        // succeeded rather than timing out into a kill) — a hang here would have
        // thrown TimeoutException before this assertion is ever reached.
    }

    [Fact]
    public void SecondProcess_NeverConstructsARuntime_AndFirstProcessStaysUnaffected()
    {
        using var first = AppProcess.Start("--test-mode=hold");
        try
        {
            var holdingLine = AppProcess.ReadLineWithTimeout(first.StandardOutput);
            Assert.StartsWith("HOLDING", holdingLine);
            Assert.Contains("CONSTRUCTIONS:1", holdingLine);

            var second = AppProcess.Run("ready");

            Assert.Equal(2, second.ExitCode);
            Assert.Contains("SECOND_INSTANCE:REJECTED", second.StdOut);
            // The construction count in the *second process* — a fresh, independent
            // static field, since it's a different OS process — must be 0: proof this
            // process never reached CompanionRuntime.ClaimConstructionAuthority at all,
            // not just that it exited with a particular code.
            Assert.Contains("CONSTRUCTIONS:0", second.StdOut);
        }
        finally
        {
            // Release the held guard so the first process exits on its own clean
            // shutdown path rather than being killed — this also exercises "hold"'s own
            // shutdown behavior as a side effect.
            first.StandardInput.WriteLine();
            first.StandardInput.Flush();
            if (!first.WaitForExit((int)AppProcess.DefaultTimeout.TotalMilliseconds))
            {
                AppProcess.TryKill(first);
            }
        }

        Assert.Equal(0, first.ExitCode);
        Assert.True(first.HasExited);
    }
}
