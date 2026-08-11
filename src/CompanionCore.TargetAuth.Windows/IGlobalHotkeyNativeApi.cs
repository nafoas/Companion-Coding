namespace CompanionCore.TargetAuth.Windows;

public interface IGlobalHotkeyNativeApi
{
    GlobalHotkeyRegistrationResult TryRegister(
        nint windowHandle,
        int identifier,
        GlobalHotkeyModifiers modifiers,
        uint virtualKey);

    bool TryUnregister(nint windowHandle, int identifier);
}
