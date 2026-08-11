using System.Runtime.InteropServices;

namespace CompanionCore.TargetAuth.Windows;

public sealed class WindowsDisplayTopology : IDisplayTopology
{
    private const int SmCmonitors = 80;

    public int GetAttachedDisplayCount()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        var count = NativeMethods.GetSystemMetrics(SmCmonitors);
        return count > 0 ? count : 0;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int index);
    }
}
