using CompanionCore.TargetAuth.Windows;

namespace CompanionCore.App.IntegrationTests;

public sealed class GlobalPrivacyHotkeyRegistrationTests
{
    [Fact]
    public void Success_RegistersExactStopOnlyChordOnce_AndUnregistersDeterministically()
    {
        var native = new FakeHotkeyNativeApi
        {
            RegistrationResult = new GlobalHotkeyRegistrationResult(true, 0),
        };
        var registration = new GlobalPrivacyHotkeyRegistration(native);

        var first = registration.Register((nint)123);
        var repeated = registration.Register((nint)999);

        Assert.True(first.Succeeded);
        Assert.Equal(first, repeated);
        Assert.Equal(1, native.RegisterCalls);
        Assert.Equal((nint)123, native.RegisteredWindow);
        Assert.Equal(GlobalPrivacyHotkeyRegistration.DefaultIdentifier, native.RegisteredIdentifier);
        Assert.Equal(GlobalPrivacyHotkeyRegistration.RequiredModifiers, native.RegisteredModifiers);
        Assert.Equal(GlobalPrivacyHotkeyRegistration.VirtualKeyF12, native.RegisteredVirtualKey);

        registration.Dispose();
        registration.Dispose();
        Assert.Equal(1, native.UnregisterCalls);
    }

    [Fact]
    public void CollisionFailure_IsTypedAndVisible_AndDoesNotPretendToOwnRegistration()
    {
        var native = new FakeHotkeyNativeApi
        {
            RegistrationResult = new GlobalHotkeyRegistrationResult(false, 1409),
        };
        using var registration = new GlobalPrivacyHotkeyRegistration(native);

        var result = registration.Register((nint)123);

        Assert.False(result.Succeeded);
        Assert.Equal(1409, result.ErrorCode);
        Assert.Equal(0, native.UnregisterCalls);
    }

    [Fact]
    public void MessageMatch_RequiresExactWmHotkeyAndOwnedIdentifier()
    {
        using var registration = new GlobalPrivacyHotkeyRegistration(new FakeHotkeyNativeApi());
        Assert.True(registration.Register((nint)123).Succeeded);

        Assert.True(registration.IsMessageForPrivacyHotkey(
            0x0312,
            (nint)GlobalPrivacyHotkeyRegistration.DefaultIdentifier));
        Assert.False(registration.IsMessageForPrivacyHotkey(
            0x0312,
            (nint)(GlobalPrivacyHotkeyRegistration.DefaultIdentifier + 1)));
        Assert.False(registration.IsMessageForPrivacyHotkey(
            0x007E,
            (nint)GlobalPrivacyHotkeyRegistration.DefaultIdentifier));
    }

    [Fact]
    public void FailedRegistration_NeverClaimsAHotkeyMessage()
    {
        using var registration = new GlobalPrivacyHotkeyRegistration(
            new FakeHotkeyNativeApi
            {
                RegistrationResult = new GlobalHotkeyRegistrationResult(false, 1409),
            });
        Assert.False(registration.Register((nint)123).Succeeded);

        Assert.False(registration.IsMessageForPrivacyHotkey(
            0x0312,
            (nint)GlobalPrivacyHotkeyRegistration.DefaultIdentifier));
    }

    [Fact]
    public void ZeroWindowHandle_FailsBeforeCallingNativeRegistration()
    {
        var native = new FakeHotkeyNativeApi();
        using var registration = new GlobalPrivacyHotkeyRegistration(native);

        var result = registration.Register(0);

        Assert.False(result.Succeeded);
        Assert.Equal(1400, result.ErrorCode);
        Assert.Equal(0, native.RegisterCalls);
    }

    [Fact]
    public void NativeRegistrationException_BecomesUnavailableResultWithoutThrowing()
    {
        var native = new FakeHotkeyNativeApi
        {
            ThrowOnRegister = true,
        };
        using var registration = new GlobalPrivacyHotkeyRegistration(native);

        var result = registration.Register((nint)123);

        Assert.False(result.Succeeded);
        Assert.Equal(-1, result.ErrorCode);
        Assert.Equal(1, native.RegisterCalls);
        Assert.Equal(0, native.UnregisterCalls);
    }

    [Fact]
    public void NativeUnregistrationException_DoesNotEscapeDeterministicDispose()
    {
        var native = new FakeHotkeyNativeApi
        {
            ThrowOnUnregister = true,
        };
        var registration = new GlobalPrivacyHotkeyRegistration(native);
        Assert.True(registration.Register((nint)123).Succeeded);

        var exception = Record.Exception(registration.Dispose);

        Assert.Null(exception);
        Assert.Equal(1, native.UnregisterCalls);
    }

    [Fact]
    public void WindowsDiscoveryAdapter_DeclaresNoTitleForegroundOrCaptureNativeEntryPoint()
    {
        var methodNames = typeof(WindowsTargetDiscovery)
            .GetNestedTypes(System.Reflection.BindingFlags.NonPublic)
            .SelectMany(type => type.GetMethods(
                System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public))
            .Select(method => method.Name)
            .ToArray();

        Assert.DoesNotContain(methodNames, name =>
            name.Contains("WindowText", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Foreground", StringComparison.OrdinalIgnoreCase)
            || name.Contains("PrintWindow", StringComparison.OrdinalIgnoreCase)
            || name.Contains("BitBlt", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Capture", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Thumbnail", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeHotkeyNativeApi : IGlobalHotkeyNativeApi
    {
        internal GlobalHotkeyRegistrationResult RegistrationResult { get; init; } =
            new(true, 0);

        internal int RegisterCalls { get; private set; }

        internal int UnregisterCalls { get; private set; }

        internal bool ThrowOnRegister { get; init; }

        internal bool ThrowOnUnregister { get; init; }

        internal nint RegisteredWindow { get; private set; }

        internal int RegisteredIdentifier { get; private set; }

        internal GlobalHotkeyModifiers RegisteredModifiers { get; private set; }

        internal uint RegisteredVirtualKey { get; private set; }

        public GlobalHotkeyRegistrationResult TryRegister(
            nint windowHandle,
            int identifier,
            GlobalHotkeyModifiers modifiers,
            uint virtualKey)
        {
            RegisterCalls++;
            if (ThrowOnRegister)
            {
                throw new InvalidOperationException("Synthetic registration failure.");
            }

            RegisteredWindow = windowHandle;
            RegisteredIdentifier = identifier;
            RegisteredModifiers = modifiers;
            RegisteredVirtualKey = virtualKey;
            return RegistrationResult;
        }

        public bool TryUnregister(nint windowHandle, int identifier)
        {
            UnregisterCalls++;
            if (ThrowOnUnregister)
            {
                throw new InvalidOperationException("Synthetic unregistration failure.");
            }

            return true;
        }
    }
}
