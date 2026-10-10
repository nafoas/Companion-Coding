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
    /// <summary>
    /// Liveness bound for a cold launch to reach its first marker line. A first WPF
    /// launch on a shared runner reads the runtime from disk and competes with other
    /// suites' disk syncs; that cost belongs here, not in the exit bound.
    /// </summary>
    internal static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Bound for the process to exit after its marker. Every test mode prints its one
    /// marker immediately before stopping, closing, or shutting down, so this times
    /// exactly the stop/close/exit work.
    /// </summary>
    internal static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    internal readonly record struct Result(
        int ExitCode,
        string StdOut,
        string StdErr,
        TimeSpan StartupDuration,
        TimeSpan ExitDuration);

    /// <summary>
    /// Runs the app with the given test-mode scenario to completion in two bounded
    /// phases: launch until its first stdout marker (<see cref="StartupTimeout"/>), then
    /// marker until exit (<see cref="ExitTimeout"/>). A process that overruns either
    /// phase is killed and the call throws naming that phase, rather than hanging the
    /// test suite.
    /// </summary>
    internal static Result Run(string testMode, string extraArguments = "")
    {
        using var process = Start($"--test-mode={testMode} {extraArguments}".TrimEnd());
        var stdErrTask = process.StandardError.ReadToEndAsync();
        var clock = Stopwatch.StartNew();

        string? marker;
        try
        {
            marker = ReadLineWithTimeout(process.StandardOutput, StartupTimeout, allowEndOfStream: true);
        }
        catch (TimeoutException)
        {
            TryKill(process);
            throw new TimeoutException(
                $"CompanionCore.App --test-mode={testMode} did not reach its first marker within the {StartupTimeout} startup bound.");
        }

        var startupDuration = clock.Elapsed;
        var stdOutRestTask = process.StandardOutput.ReadToEndAsync();
        clock.Restart();
        if (!process.WaitForExit((int)ExitTimeout.TotalMilliseconds))
        {
            TryKill(process);
            throw new TimeoutException(
                $"CompanionCore.App --test-mode={testMode} did not exit within {ExitTimeout} of its marker.");
        }

        var exitDuration = clock.Elapsed;
        if (!Task.WaitAll([stdOutRestTask, stdErrTask], ExitTimeout))
        {
            throw new TimeoutException(
                $"CompanionCore.App --test-mode={testMode} exited but its output streams did not close within {ExitTimeout}.");
        }

        var stdOut = marker is null
            ? stdOutRestTask.Result
            : marker + Environment.NewLine + stdOutRestTask.Result;
        return new Result(process.ExitCode, stdOut, stdErrTask.Result, startupDuration, exitDuration);
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
    /// anything (e.g. because it crashed before reaching the expected marker). The first
    /// line of a launch is a startup marker, so the default is <see cref="StartupTimeout"/>.
    /// </summary>
    internal static string? ReadLineWithTimeout(
        StreamReader reader,
        TimeSpan? timeout = null,
        bool allowEndOfStream = false)
    {
        var effectiveTimeout = timeout ?? StartupTimeout;
        var readTask = reader.ReadLineAsync();
        if (!readTask.Wait(effectiveTimeout))
        {
            throw new TimeoutException($"Timed out after {effectiveTimeout} waiting for a line of output.");
        }

        return readTask.Result
            ?? (allowEndOfStream ? null : throw new InvalidOperationException("Stream ended before producing a line."));
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
