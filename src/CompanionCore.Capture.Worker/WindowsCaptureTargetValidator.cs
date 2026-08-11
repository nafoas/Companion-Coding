using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CompanionCore.Capture.Contracts;
using Microsoft.Win32.SafeHandles;

namespace CompanionCore.Capture.Worker;

internal static class WindowsCaptureTargetValidator
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int MaximumExecutablePathCharacters = 32_767;

    internal static CaptureTargetValidationResult Validate(CaptureIpcAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        if (!OperatingSystem.IsWindows())
        {
            return CaptureTargetValidationResult.Unavailable;
        }

        IntPtr window;
        try
        {
            window = new IntPtr(authorization.WindowId);
        }
        catch (OverflowException)
        {
            return CaptureTargetValidationResult.TargetClosed;
        }

        if (window == IntPtr.Zero || !NativeMethods.IsWindow(window))
        {
            return CaptureTargetValidationResult.TargetClosed;
        }

        _ = NativeMethods.GetWindowThreadProcessId(window, out var rawProcessId);
        if (rawProcessId == 0 || rawProcessId > int.MaxValue)
        {
            return CaptureTargetValidationResult.TargetClosed;
        }

        if (checked((int)rawProcessId) != authorization.ProcessId)
        {
            return CaptureTargetValidationResult.IdentityMismatch;
        }

        using var process = NativeMethods.OpenProcess(
            ProcessQueryLimitedInformation,
            inheritHandle: false,
            rawProcessId);
        if (process.IsInvalid)
        {
            return CaptureTargetValidationResult.Unavailable;
        }

        try
        {
            var capacity = MaximumExecutablePathCharacters;
            var path = new StringBuilder(capacity);
            if (!NativeMethods.QueryFullProcessImageName(
                    process,
                    flags: 0,
                    path,
                    ref capacity)
                || capacity <= 0)
            {
                return CaptureTargetValidationResult.Unavailable;
            }

            var rawPath = path.ToString(0, capacity);
            var fileName = Path.GetFileName(rawPath);
            if (!string.Equals(
                    fileName,
                    authorization.ExecutableFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CaptureTargetValidationResult.IdentityMismatch;
            }

            var normalizedPath = Path.GetFullPath(rawPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
            var fingerprint = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)));
            return string.Equals(
                fingerprint,
                authorization.ExecutablePathFingerprint,
                StringComparison.OrdinalIgnoreCase)
                ? CaptureTargetValidationResult.Valid
                : CaptureTargetValidationResult.IdentityMismatch;
        }
        catch (Exception)
        {
            return CaptureTargetValidationResult.Unavailable;
        }
    }

    internal static bool IsMinimized(long windowId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var window = new IntPtr(windowId);
            return window != IntPtr.Zero
                && NativeMethods.IsWindow(window)
                && NativeMethods.IsIconic(window);
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern SafeProcessHandle OpenProcess(
            uint desiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryFullProcessImageName(
            SafeProcessHandle process,
            uint flags,
            StringBuilder executableName,
            ref int size);
    }
}

internal enum CaptureTargetValidationResult
{
    Valid,
    TargetClosed,
    IdentityMismatch,
    Unavailable,
}
