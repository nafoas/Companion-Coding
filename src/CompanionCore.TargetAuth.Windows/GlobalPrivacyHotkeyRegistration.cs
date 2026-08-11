namespace CompanionCore.TargetAuth.Windows;

/// <summary>
/// Deterministic ownership wrapper for the one stop-only privacy chord. Registration
/// failure is a typed result, never a swallowed startup condition.
/// </summary>
public sealed class GlobalPrivacyHotkeyRegistration : IDisposable
{
    private const int InvalidWindowHandleError = 1400;
    private const int UnexpectedNativeFailureError = -1;
    public const int DefaultIdentifier = 0x5043;
    public const uint VirtualKeyF12 = 0x7B;
    public const GlobalHotkeyModifiers RequiredModifiers =
        GlobalHotkeyModifiers.Control
        | GlobalHotkeyModifiers.Shift
        | GlobalHotkeyModifiers.NoRepeat;

    private readonly IGlobalHotkeyNativeApi _nativeApi;
    private readonly int _identifier;
    private nint _windowHandle;
    private bool _attempted;
    private bool _registered;
    private bool _disposed;
    private GlobalHotkeyRegistrationResult _result;

    public GlobalPrivacyHotkeyRegistration(
        IGlobalHotkeyNativeApi nativeApi,
        int identifier = DefaultIdentifier)
    {
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        _identifier = identifier;
    }

    public GlobalHotkeyRegistrationResult Register(nint windowHandle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_attempted)
        {
            return _result;
        }

        _attempted = true;
        _windowHandle = windowHandle;
        if (windowHandle == 0)
        {
            _result = new GlobalHotkeyRegistrationResult(false, InvalidWindowHandleError);
            return _result;
        }

        try
        {
            _result = _nativeApi.TryRegister(
                windowHandle,
                _identifier,
                RequiredModifiers,
                VirtualKeyF12);
        }
        catch (Exception)
        {
            _result = new GlobalHotkeyRegistrationResult(false, UnexpectedNativeFailureError);
        }

        _registered = _result.Succeeded;
        return _result;
    }

    public bool IsMessageForPrivacyHotkey(int message, nint wordParameter) =>
        _registered
        && message == 0x0312
        && wordParameter.ToInt64() == _identifier;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_registered)
        {
            try
            {
                _nativeApi.TryUnregister(_windowHandle, _identifier);
            }
            catch (Exception)
            {
                // Registration ownership is still dropped locally. Native cleanup is
                // best-effort during teardown and must not crash the application.
            }

            _registered = false;
        }
    }
}
