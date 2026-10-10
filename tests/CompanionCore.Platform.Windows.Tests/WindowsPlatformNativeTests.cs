using System.Diagnostics;

namespace CompanionCore.Platform.Windows.Tests;

/// <summary>The real Win32 surface on Windows CI; elsewhere it must degrade to "nothing".</summary>
public sealed class WindowsPlatformNativeTests
{
    [Fact]
    public async Task ARealChildProcess_ReportsItsExitCodeThroughAHeldHandle()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var native = new WindowsPlatformNative();
        using var child = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 2 127.0.0.1 >nul & exit 7")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var probe = native.OpenProcess(child.Id);
        Assert.NotNull(probe);

        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        int exitCode;
        while (probe!.Poll(out exitCode) != ProcessProbeState.Exited && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(ProcessProbeState.Exited, probe.Poll(out exitCode));
        Assert.Equal(7, exitCode);
    }

    [Fact]
    public void ARunningProcess_IsReportedRunning()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var probe = new WindowsPlatformNative().OpenProcess(Environment.ProcessId);

        Assert.NotNull(probe);
        Assert.Equal(ProcessProbeState.Running, probe!.Poll(out _));
    }

    [Fact]
    public void Queries_AreSafeForAbsentWindowsAndProcesses()
    {
        var native = new WindowsPlatformNative();

        Assert.False(native.IsWindow(0));
        Assert.Equal(0, native.GetWindowProcessId(0));
        Assert.False(native.IsHung(0));
        Assert.Null(native.OpenProcess(0));
        Assert.Null(native.OpenProcess(-5));
        _ = native.GetForeground();
        if (OperatingSystem.IsWindows())
        {
            Assert.NotNull(native.GetLastInputTick());
        }
        else
        {
            Assert.Equal((0L, 0), native.GetForeground());
            Assert.Null(native.GetLastInputTick());
            Assert.Null(native.OpenProcess(Environment.ProcessId));
        }
    }
}
