using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CompanionCore.Platform.Windows;

/// <summary>
/// The Win32 implementation. It deliberately imports no text, title, hook, accessibility,
/// or capture API: foreground identity, the last-input tick, window existence and hung
/// state, and a synchronize/limited-query process handle are all it can see.
/// </summary>
public sealed class WindowsPlatformNative : IWindowsPlatformNative
{
    private const uint Synchronize = 0x00100000;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 0x102;

    public (long WindowId, int ProcessId) GetForeground()
    {
        if (!OperatingSystem.IsWindows())
        {
            return (0, 0);
        }

        var window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return (0, 0);
        }

        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        return (window.ToInt64(), unchecked((int)processId));
    }

    public uint? GetLastInputTick()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var info = new NativeMethods.LastInputInfo { Size = (uint)Marshal.SizeOf<NativeMethods.LastInputInfo>() };
        return NativeMethods.GetLastInputInfo(ref info) ? info.Time : null;
    }

    public bool IsWindow(long windowId) =>
        OperatingSystem.IsWindows() && windowId != 0 && NativeMethods.IsWindow(new IntPtr(windowId));

    public int GetWindowProcessId(long windowId)
    {
        if (!IsWindow(windowId))
        {
            return 0;
        }

        NativeMethods.GetWindowThreadProcessId(new IntPtr(windowId), out var processId);
        return unchecked((int)processId);
    }

    public bool IsHung(long windowId) =>
        IsWindow(windowId) && NativeMethods.IsHungAppWindow(new IntPtr(windowId));

    public IProcessProbe? OpenProcess(int processId)
    {
        if (!OperatingSystem.IsWindows() || processId <= 0)
        {
            return null;
        }

        var handle = NativeMethods.OpenProcess(Synchronize | ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        return new Probe(handle);
    }

    private sealed class Probe(SafeProcessHandle handle) : IProcessProbe
    {
        public ProcessProbeState Poll(out int exitCode)
        {
            exitCode = -1;
            if (handle.IsClosed)
            {
                return ProcessProbeState.Unknown;
            }

            switch (NativeMethods.WaitForSingleObject(handle, 0))
            {
                case WaitTimeout:
                    return ProcessProbeState.Running;
                case WaitObject0:
                    if (NativeMethods.GetExitCodeProcess(handle, out var code))
                    {
                        exitCode = unchecked((int)code);
                    }

                    return ProcessProbeState.Exited;
                default:
                    return ProcessProbeState.Unknown;
            }
        }

        public void Dispose() => handle.Dispose();
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct LastInputInfo
        {
            public uint Size;
            public uint Time;
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetLastInputInfo(ref LastInputInfo info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsHungAppWindow(IntPtr window);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetExitCodeProcess(SafeProcessHandle handle, out uint exitCode);
    }
}
