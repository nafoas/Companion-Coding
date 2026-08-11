using System.Runtime.InteropServices;

namespace CompanionCore.TargetAuth.Windows;

public sealed class WindowsGlobalHotkeyNativeApi : IGlobalHotkeyNativeApi
{
    public GlobalHotkeyRegistrationResult TryRegister(
        nint windowHandle,
        int identifier,
        GlobalHotkeyModifiers modifiers,
        uint virtualKey)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new GlobalHotkeyRegistrationResult(false, 0);
        }

        var succeeded = NativeMethods.RegisterHotKey(
            windowHandle,
            identifier,
            (uint)modifiers,
            virtualKey);
        return new GlobalHotkeyRegistrationResult(
            succeeded,
            succeeded ? 0 : Marshal.GetLastWin32Error());
    }

    public bool TryUnregister(nint windowHandle, int identifier) =>
        OperatingSystem.IsWindows()
        && NativeMethods.UnregisterHotKey(windowHandle, identifier);

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(
            nint windowHandle,
            int identifier,
            uint modifiers,
            uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(nint windowHandle, int identifier);
    }
}
