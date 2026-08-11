using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace CompanionCore.Capture.Worker;

internal static class WindowsCaptureInterop
{
    private const uint D3D11CreateDeviceBgraSupport = 0x20;
    private const uint D3D11SdkVersion = 7;
    private const int D3DDriverTypeHardware = 1;
    private static readonly Guid GraphicsCaptureItemGuid =
        new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid DxgiDeviceGuid =
        new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");

    internal static GraphicsCaptureItem CreateItemForWindow(IntPtr window)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var iid = GraphicsCaptureItemGuid;
        interop.CreateForWindow(window, ref iid, out var itemPointer);
        if (itemPointer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Windows Graphics Capture returned no target item.");
        }

        try
        {
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer)
                ?? throw new InvalidOperationException(
                    "Windows Graphics Capture returned an invalid target item.");
        }
        finally
        {
            MarshalInterface<GraphicsCaptureItem>.DisposeAbi(itemPointer);
        }
    }

    internal static WinRtD3DDeviceLease CreateDirect3DDevice()
    {
        var result = NativeMethods.D3D11CreateDevice(
            adapter: IntPtr.Zero,
            driverType: D3DDriverTypeHardware,
            software: IntPtr.Zero,
            flags: D3D11CreateDeviceBgraSupport,
            featureLevels: IntPtr.Zero,
            featureLevelCount: 0,
            sdkVersion: D3D11SdkVersion,
            out var d3dDevice,
            out _,
            out var d3dContext);
        Marshal.ThrowExceptionForHR(result);

        IntPtr dxgiDevice = IntPtr.Zero;
        IntPtr inspectableDevice = IntPtr.Zero;
        try
        {
            var iid = DxgiDeviceGuid;
            result = Marshal.QueryInterface(d3dDevice, in iid, out dxgiDevice);
            Marshal.ThrowExceptionForHR(result);

            result = NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(
                dxgiDevice,
                out inspectableDevice);
            Marshal.ThrowExceptionForHR(result);
            var projected = MarshalInterface<IDirect3DDevice>.FromAbi(inspectableDevice)
                ?? throw new InvalidOperationException(
                    "The Direct3D device could not be projected into Windows Runtime.");
            return new WinRtD3DDeviceLease(projected);
        }
        finally
        {
            if (inspectableDevice != IntPtr.Zero)
            {
                MarshalInterface<IDirect3DDevice>.DisposeAbi(inspectableDevice);
            }

            Release(dxgiDevice);
            Release(d3dContext);
            Release(d3dDevice);
        }
    }

    internal static void DisposeProjectedObject(object? value)
    {
        if (value is IWinRTObject winRtObject)
        {
            winRtObject.NativeObject.Dispose();
        }
    }

    private static void Release(IntPtr value)
    {
        if (value != IntPtr.Zero)
        {
            _ = Marshal.Release(value);
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        void CreateForWindow(IntPtr window, ref Guid iid, out IntPtr result);
    }

    private static class NativeMethods
    {
        [DllImport("d3d11.dll")]
        internal static extern int D3D11CreateDevice(
            IntPtr adapter,
            int driverType,
            IntPtr software,
            uint flags,
            IntPtr featureLevels,
            uint featureLevelCount,
            uint sdkVersion,
            out IntPtr device,
            out int selectedFeatureLevel,
            out IntPtr immediateContext);

        [DllImport("d3d11.dll")]
        internal static extern int CreateDirect3D11DeviceFromDXGIDevice(
            IntPtr dxgiDevice,
            out IntPtr graphicsDevice);
    }
}

internal sealed class WinRtD3DDeviceLease : IDisposable
{
    private IDirect3DDevice? _device;

    internal WinRtD3DDeviceLease(IDirect3DDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    internal IDirect3DDevice Device =>
        _device ?? throw new ObjectDisposedException(nameof(WinRtD3DDeviceLease));

    public void Dispose()
    {
        var device = Interlocked.Exchange(ref _device, null);
        WindowsCaptureInterop.DisposeProjectedObject(device);
    }
}
