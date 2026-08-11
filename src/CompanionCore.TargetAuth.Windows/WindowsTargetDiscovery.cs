using System.Runtime.InteropServices;
using System.Text;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.TargetAuth.Windows;

/// <summary>
/// Enumerates title-free eligible top-level application windows. This adapter contains
/// deliberately no import for GetWindowText, foreground-window APIs, capture APIs,
/// accessibility APIs, thumbnails, or pixels.
/// </summary>
public sealed class WindowsTargetDiscovery : ITargetDiscovery
{
    private const int GwlExstyle = -20;
    private const long WsExToolwindow = 0x00000080L;
    private const uint GwOwner = 4;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint DwmwaCloaked = 14;
    private const int MaximumExecutablePathCharacters = 32768;

    public Task<IReadOnlyList<TargetCandidate>> DiscoverAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyList<TargetCandidate>>([]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var currentProcessId = Environment.ProcessId;
        var candidates = new List<TargetCandidate>();
        var cancelled = false;
        var failed = false;

        var callback = new NativeMethods.EnumWindowsProc((window, _) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                return false;
            }

            TargetCandidate candidate;
            try
            {
                if (!TryCreateCandidate(window, currentProcessId, out candidate))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                failed = true;
                return false;
            }

            candidates.Add(candidate);
            return true;
        });

        if (!NativeMethods.EnumWindows(callback, IntPtr.Zero))
        {
            if (cancelled)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (failed)
            {
                throw new InvalidOperationException(
                    "Window discovery failed while validating title-free target metadata.");
            }

            throw new InvalidOperationException(
                $"Window discovery failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }

        GC.KeepAlive(callback);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<TargetCandidate>>(candidates);
    }

    public Task<bool> IsStillValidAsync(TargetCandidate target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(false);
        }

        try
        {
            var window = new IntPtr(target.Identity.WindowId);
            if (!NativeMethods.IsWindow(window)
                || !TryCreateCandidate(window, Environment.ProcessId, out var current))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(current.Identity == target.Identity);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }

    private static bool TryCreateCandidate(
        IntPtr window,
        int currentProcessId,
        out TargetCandidate candidate)
    {
        candidate = null!;
        if (window == IntPtr.Zero
            || !NativeMethods.IsWindowVisible(window)
            || NativeMethods.GetWindow(window, GwOwner) != IntPtr.Zero)
        {
            return false;
        }

        var extendedStyle = NativeMethods.GetWindowLongPtr(window, GwlExstyle).ToInt64();
        if ((extendedStyle & WsExToolwindow) != 0)
        {
            return false;
        }

        var cloaked = 0;
        var cloakedResult = NativeMethods.DwmGetWindowAttribute(
            window,
            DwmwaCloaked,
            out cloaked,
            Marshal.SizeOf<int>());
        if (cloakedResult != 0 || cloaked != 0)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(window, out var rawProcessId);
        if (rawProcessId == 0
            || rawProcessId > int.MaxValue
            || rawProcessId == currentProcessId)
        {
            return false;
        }

        var processId = checked((int)rawProcessId);
        if (!TryReadExecutableIdentity(processId, out var fileName, out var fingerprint))
        {
            return false;
        }

        var windowId = window.ToInt64();
        if (windowId <= 0)
        {
            return false;
        }

        candidate = new TargetCandidate(
            new CaptureTargetIdentity(windowId, processId, fileName, fingerprint),
            DefaultApplicationClassifier.Classify(fileName));
        return true;
    }

    private static bool TryReadExecutableIdentity(
        int processId,
        out string fileName,
        out string fingerprint)
    {
        fileName = string.Empty;
        fingerprint = string.Empty;
        var process = NativeMethods.OpenProcess(
            ProcessQueryLimitedInformation,
            inheritHandle: false,
            checked((uint)processId));
        if (process == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var capacity = MaximumExecutablePathCharacters;
            var path = new StringBuilder(capacity);
            if (!NativeMethods.QueryFullProcessImageName(process, flags: 0, path, ref capacity)
                || capacity <= 0)
            {
                return false;
            }

            var rawPath = path.ToString(0, capacity);
            fileName = Path.GetFileName(rawPath);
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 260)
            {
                return false;
            }

            fingerprint = ExecutablePathFingerprint.Create(rawPath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    private static class NativeMethods
    {
        internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        internal static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(
            IntPtr window,
            uint attribute,
            out int value,
            int valueSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(
            uint desiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryFullProcessImageName(
            IntPtr process,
            uint flags,
            StringBuilder executableName,
            ref int size);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
