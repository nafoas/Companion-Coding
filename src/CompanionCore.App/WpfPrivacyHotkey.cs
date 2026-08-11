using System.Windows;
using System.Windows.Interop;
using CompanionCore.TargetAuth.Windows;

namespace CompanionCore.App;

/// <summary>
/// Primary-window Win32 message adapter for the stop-only privacy chord and display
/// topology changes. It owns registration lifetime but no authorization state.
/// </summary>
internal sealed class WpfPrivacyHotkey : IDisposable
{
    private const int WmDisplayChange = 0x007E;

    private readonly Window _window;
    private readonly GlobalPrivacyHotkeyRegistration _registration;
    private readonly Func<Task> _privacyStop;
    private readonly Func<Task> _displayChanged;
    private HwndSource? _source;
    private nint _windowHandle;
    private bool _attached;
    private bool _disposed;

    internal WpfPrivacyHotkey(
        Window window,
        IGlobalHotkeyNativeApi nativeApi,
        Func<Task> privacyStop,
        Func<Task> displayChanged)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _registration = new GlobalPrivacyHotkeyRegistration(
            nativeApi ?? throw new ArgumentNullException(nameof(nativeApi)));
        _privacyStop = privacyStop ?? throw new ArgumentNullException(nameof(privacyStop));
        _displayChanged = displayChanged ?? throw new ArgumentNullException(nameof(displayChanged));
    }

    internal event EventHandler<GlobalHotkeyRegistrationResult>? RegistrationChanged;

    internal void Attach()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_attached)
        {
            return;
        }

        _attached = true;
        _window.SourceInitialized += Window_SourceInitialized;
        if (PresentationSource.FromVisual(_window) is HwndSource)
        {
            InitializeSource();
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) => InitializeSource();

    private void InitializeSource()
    {
        if (_source is not null || _disposed)
        {
            return;
        }

        _windowHandle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        if (_source is null)
        {
            RegistrationChanged?.Invoke(this, _registration.Register(0));
            return;
        }

        _source.AddHook(WindowMessageHook);
        var result = _registration.Register(_windowHandle);
        RegistrationChanged?.Invoke(this, result);
    }

    private nint WindowMessageHook(
        nint window,
        int message,
        nint wordParameter,
        nint longParameter,
        ref bool handled)
    {
        if (_registration.IsMessageForPrivacyHotkey(message, wordParameter))
        {
            handled = true;
            _ = InvokeSafelyAsync(_privacyStop);
        }
        else if (message == WmDisplayChange)
        {
            _ = InvokeSafelyAsync(_displayChanged);
        }

        return 0;
    }

    private static async Task InvokeSafelyAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (Exception)
        {
            // The controller is revocation-first and remains fail-closed. UI status is
            // delivered through its event path where possible; the window hook itself
            // must never crash the WPF dispatcher.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.SourceInitialized -= Window_SourceInitialized;
        _registration.Dispose();

        _source?.RemoveHook(WindowMessageHook);
        _source = null;
    }
}
