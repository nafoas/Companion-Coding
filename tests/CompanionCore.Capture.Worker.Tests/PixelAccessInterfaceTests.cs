using System.Reflection;
using System.Runtime.InteropServices;
using CompanionCore.Capture.Worker;

namespace CompanionCore.Capture.Worker.Tests;

/// <summary>
/// FIX-09: Windows finds IMemoryBufferByteAccess only by its official interface ID. A wrong ID
/// makes every real pixel readback fail with E_NOINTERFACE (0x80004002), which synthetic
/// capture sources never exercise. Boss's first real capture lost every frame to exactly that.
/// </summary>
public sealed class PixelAccessInterfaceTests
{
    // windows.foundation.h: MIDL_INTERFACE("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d") IMemoryBufferByteAccess
    private static readonly Guid OfficialId = new("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d");

    [Theory]
    [InlineData(typeof(WindowsGraphicsCaptureSource))]
    [InlineData(typeof(WindowsOcrTextRecognizer))]
    public void EveryPixelReadback_UsesTheOfficialInterfaceId(Type owner)
    {
        var access = owner.GetNestedType("IMemoryBufferByteAccess", BindingFlags.NonPublic);
        Assert.NotNull(access);
        Assert.True(access.IsInterface);
        Assert.NotNull(access.GetCustomAttribute<ComImportAttribute>());
        Assert.Equal(OfficialId, Guid.Parse(access.GetCustomAttribute<GuidAttribute>()!.Value));
    }
}
