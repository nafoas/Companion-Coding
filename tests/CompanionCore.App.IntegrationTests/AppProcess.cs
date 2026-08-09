using System.Diagnostics;

namespace CompanionCore.App.IntegrationTests;

/// <summary>
/// Launches the real <c>CompanionCore.App.exe</c> as a genuine child process and
/// captures its stdout/exit code. Every scenario this drives is a
/// <c>--test-mode=&lt;scenario&gt;</c> argument the app itself understands — see
/// <c>App.xaml.cs</c> — so this class never needs UI automation, only process and
/// stream plumbing.
/// </summary>
internal static class AppProcess
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    internal readonly record struct Result(int ExitCode, string StdOut, string StdErr);

    /// <summary>
    /// Runs the app with the given test-mode scenario to completion and returns its
    /// exit code and captured output. The app is expected to exit on its own (every
    /// test-mode scenario calls <c>Shutdown</c> deterministically); a process that
    /// doesn't exit within <paramref name="timeout"/> is killed and the call throws,
    /// rather than hanging the test suite.
    /// </summary>
    internal static Result Run(string testMode, TimeSpan? timeout = null)
    {
        using var process = Start($"--test-mode={testMode}");
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        var effectiveTimeout = timeout ?? DefaultTimeout;
        if (!process.WaitForExit((int)effectiveTimeout.TotalMilliseconds))
        {
            TryKill(process);
            throw new TimeoutException(
                $"CompanionCore.App --test-mode={testMode} did not exit within {effectiveTimeout}.");
        }

        return new Result(process.ExitCode, stdOutTask.GetAwaiter().GetResult(), stdErrTask.GetAwaiter().GetResult());
    }

    /// <summary>
    /// Starts the app with the given raw arguments and leaves it running — used by the
    /// second-process test, which needs one process held open (via <c>--test-mode=hold</c>)
    /// while a second, independent launch is attempted against it.
    /// </summary>
    internal static Process Start(string arguments)
    {
        var startInfo = new ProcessStartInfo(Locate(), arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {Locate()} {arguments}.");
    }

    /// <summary>
    /// Blocks for one line of output with a timeout, rather than an unbounded
    /// <c>ReadLine()</c> that could hang the whole test run if the app never writes
    /// anything (e.g. because it crashed before reaching the expected marker).
    /// </summary>
    internal static string ReadLineWithTimeout(StreamReader reader, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var readTask = reader.ReadLineAsync();
        if (!readTask.Wait(effectiveTimeout))
        {
            throw new TimeoutException($"Timed out after {effectiveTimeout} waiting for a line of output.");
        }

        return readTask.Result ?? throw new InvalidOperationException("Stream ended before producing a line.");
    }

    internal static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the kill attempt — fine.
        }
    }

    private static string? _cachedExePath;

    private static string Locate()
    {
        if (_cachedExePath is not null)
        {
            return _cachedExePath;
        }

        var direct = Path.Combine(AppContext.BaseDirectory, "CompanionCore.App.exe");
        if (File.Exists(direct))
        {
            return _cachedExePath = direct;
        }

        // Fallback: the ProjectReference should have copied the exe into this project's
        // own output directory (the common, expected case above). If it didn't — a
        // different SDK/MSBuild behavior than assumed — search upward from this test
        // assembly's location for the App project's own build output as a last resort,
        // rather than failing with a bare "file not found" that gives no diagnostic trail.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var appProjectDir = Path.Combine(dir.FullName, "CompanionCore.App");
            if (Directory.Exists(appProjectDir))
            {
                var found = Directory.GetFiles(appProjectDir, "CompanionCore.App.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (found is not null)
                {
                    return _cachedExePath = found;
                }
            }
        }

        throw new FileNotFoundException(
            "Could not locate CompanionCore.App.exe from the integration test's output directory " +
            $"({AppContext.BaseDirectory}) or by searching upward for a CompanionCore.App build output. " +
            "Expected the ProjectReference to CompanionCore.App.csproj to copy the built exe alongside this test assembly.");
    }
}
