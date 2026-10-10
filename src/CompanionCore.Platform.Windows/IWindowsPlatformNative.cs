namespace CompanionCore.Platform.Windows;

/// <summary>
/// The narrow native surface the platform signals need. Every query returns identifiers,
/// timing, or process state only: no window titles or text, no input content, no hooks,
/// and no pixels.
/// </summary>
public interface IWindowsPlatformNative
{
    /// <summary>The foreground window and its owning process, or (0, 0) when there is none.</summary>
    (long WindowId, int ProcessId) GetForeground();

    /// <summary>The system-wide last-input tick (milliseconds, wrapping), or null when unavailable.</summary>
    uint? GetLastInputTick();

    bool IsWindow(long windowId);

    /// <summary>The process owning the window, or 0 when the window no longer exists.</summary>
    int GetWindowProcessId(long windowId);

    bool IsHung(long windowId);

    /// <summary>Opens the process for exit observation only, or null when it cannot be opened.</summary>
    IProcessProbe? OpenProcess(int processId);
}

public enum ProcessProbeState
{
    Running = 1,
    Exited = 2,
    Unknown = 3,
}

/// <summary>A held process handle; it keeps the exit code observable after the process ends.</summary>
public interface IProcessProbe : IDisposable
{
    ProcessProbeState Poll(out int exitCode);
}
