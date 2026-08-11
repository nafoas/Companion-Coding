using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CompanionCore.Capture.Client;
using CompanionCore.Capture.Contracts;
using CompanionCore.Privacy;
using CompanionCore.TargetAuth;
using CompanionCore.TargetAuth.Windows;

namespace CompanionCore.Capture.Spike;

internal static class Program
{
    private static readonly TimeSpan EvidenceTimeout = TimeSpan.FromSeconds(10);

    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("SPIKE:UNSUPPORTED PLATFORM:NON_WINDOWS");
            return 2;
        }

        return args.Contains("--fixture", StringComparer.Ordinal)
            ? RunFixture()
            : RunHarnessAsync(args).GetAwaiter().GetResult();
    }

    private static async Task<int> RunHarnessAsync(string[] args)
    {
        var testMinimized = args.Contains("--minimized", StringComparer.Ordinal);
        var testExclusive = args.Contains("--exclusive", StringComparer.Ordinal);
        using var fixture = StartFixture();
        try
        {
            var ready = await fixture.StandardOutput.ReadLineAsync()
                .WaitAsync(EvidenceTimeout)
                .ConfigureAwait(false);
            if (!TryParseFixtureReady(ready, out var fixtureProcessId))
            {
                Console.WriteLine("SPIKE:FAILED FIXTURE:START");
                return 3;
            }

            var privacyState = new RuntimePrivacyState();
            var policyCatalog = await TargetPolicyCatalog.OpenDevelopmentAsync()
                .ConfigureAwait(false);
            var authorization = new TargetAuthorizationService(
                new WindowsTargetDiscovery(),
                new WindowsDisplayTopology(),
                policyCatalog,
                privacyState);
            var discovery = await authorization.DiscoverAsync().ConfigureAwait(false);
            if (discovery.Status != TargetDiscoveryStatus.Ready)
            {
                Console.WriteLine(
                    $"SPIKE:UNSUPPORTED DISCOVERY:{discovery.Status} DISPLAYS:{discovery.AttachedDisplayCount}");
                return 4;
            }

            var candidate = discovery.Candidates.SingleOrDefault(
                item => item.Identity.ProcessId == fixtureProcessId);
            if (candidate is null)
            {
                Console.WriteLine("SPIKE:FAILED FIXTURE:NOT_DISCOVERED");
                return 5;
            }

            using var worker = new OutOfProcessCaptureWorker();
            await using var controller = new TargetSessionController(
                authorization,
                worker,
                privacyState,
                new LocalPrivacyGuard());
            var admittedSequence = 0L;
            var status = CaptureWorkerStatus.Stopped;
            controller.FrameAdmitted += (_, frame) =>
                Interlocked.Exchange(ref admittedSequence, frame.SequenceNumber);
            worker.StatusChanged += (_, change) => status = change.Status;
            var authorized = await controller.AuthorizeAsync(
                    candidate,
                    explicitConsent: true)
                .ConfigureAwait(false);
            if (!authorized.Succeeded)
            {
                Console.WriteLine($"SPIKE:FAILED AUTHORIZE:{authorized.EventKind}");
                return 6;
            }

            await WaitForAsync(() => Volatile.Read(ref admittedSequence) > 0)
                .ConfigureAwait(false);
            Console.WriteLine("VISIBLE:FRAME_ARRIVED");
            var beforeOcclusion = Volatile.Read(ref admittedSequence);
            await SendFixtureCommandAsync(fixture, "occlude").ConfigureAwait(false);
            await WaitForAsync(() => Volatile.Read(ref admittedSequence) > beforeOcclusion)
                .ConfigureAwait(false);
            Console.WriteLine("OCCLUDED:FRAME_ARRIVED");
            await SendFixtureCommandAsync(fixture, "visible").ConfigureAwait(false);

            if (testMinimized)
            {
                await SendFixtureCommandAsync(fixture, "minimize").ConfigureAwait(false);
                await WaitForAsync(() => status is
                        CaptureWorkerStatus.PausedMinimized
                        or CaptureWorkerStatus.NoSignal)
                    .ConfigureAwait(false);
                Console.WriteLine($"MINIMIZED:{status}");
                await SendFixtureCommandAsync(fixture, "visible").ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine("MINIMIZED:UNSUPPORTED_NOT_SPIKED");
            }

            if (testExclusive)
            {
                var response = await SendFixtureCommandAsync(fixture, "exclusive")
                    .ConfigureAwait(false);
                if (response == "EXCLUSIVE:ENTERED")
                {
                    var beforeExclusive = Volatile.Read(ref admittedSequence);
                    var produced = await TryWaitForAsync(
                            () => Volatile.Read(ref admittedSequence) > beforeExclusive)
                        .ConfigureAwait(false);
                    Console.WriteLine(produced
                        ? "EXCLUSIVE:FRAME_ARRIVED"
                        : $"EXCLUSIVE:NO_FRAME STATUS:{status}");
                    await SendFixtureCommandAsync(fixture, "exit-exclusive").ConfigureAwait(false);
                }
                else
                {
                    Console.WriteLine("EXCLUSIVE:UNAVAILABLE");
                }
            }
            else
            {
                Console.WriteLine("EXCLUSIVE:UNSUPPORTED_NOT_SPIKED");
            }

            var stop = await controller.PrivacyStopAsync().ConfigureAwait(false);
            Console.WriteLine(
                $"STOP:CLEANUP_{(stop.CleanupComplete ? "COMPLETE" : "FAILED")} WORKER_PID:{worker.WorkerProcessId}");
            return stop.CleanupComplete ? 0 : 7;
        }
        catch (TimeoutException)
        {
            Console.WriteLine("SPIKE:FAILED TIMEOUT");
            return 8;
        }
        finally
        {
            try
            {
                if (!fixture.HasExited)
                {
                    fixture.StandardInput.WriteLine("exit");
                    fixture.StandardInput.Flush();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await fixture.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                if (!fixture.HasExited)
                {
                    fixture.Kill(entireProcessTree: true);
                }
            }
        }
    }

    private static Process StartFixture()
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The spike executable path is unavailable.");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("--fixture");
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("The private-safe capture fixture did not start.");
        }

        return process;
    }

    private static async Task<string?> SendFixtureCommandAsync(
        Process fixture,
        string command)
    {
        await fixture.StandardInput.WriteLineAsync(command).ConfigureAwait(false);
        await fixture.StandardInput.FlushAsync().ConfigureAwait(false);
        return await fixture.StandardOutput.ReadLineAsync()
            .WaitAsync(EvidenceTimeout)
            .ConfigureAwait(false);
    }

    private static bool TryParseFixtureReady(string? line, out int processId)
    {
        processId = 0;
        const string prefix = "FIXTURE:READY PID:";
        return line is not null
            && line.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(
                line.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out processId)
            && processId > 0;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        if (!await TryWaitForAsync(condition).ConfigureAwait(false))
        {
            throw new TimeoutException();
        }
    }

    private static async Task<bool> TryWaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + EvidenceTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        return true;
    }

    private static int RunFixture()
    {
        var application = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };
        var target = CreateTargetWindow();
        var occluder = CreateOccluder(target);
        ExclusiveSwapChain? exclusive = null;
        target.Loaded += (_, _) =>
        {
            Console.WriteLine($"FIXTURE:READY PID:{Environment.ProcessId}");
            Console.Out.Flush();
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    var command = await Console.In.ReadLineAsync().ConfigureAwait(false);
                    if (command is null or "exit")
                    {
                        await target.Dispatcher.InvokeAsync(() =>
                        {
                            exclusive?.Dispose();
                            application.Shutdown(0);
                        });
                        return;
                    }

                    await target.Dispatcher.InvokeAsync(() =>
                    {
                        switch (command)
                        {
                            case "occlude":
                                RestoreTarget(target);
                                occluder.Show();
                                occluder.Activate();
                                Console.WriteLine("OCCLUDER:SHOWN");
                                break;
                            case "visible":
                                exclusive?.Dispose();
                                exclusive = null;
                                occluder.Hide();
                                RestoreTarget(target);
                                Console.WriteLine("TARGET:VISIBLE");
                                break;
                            case "minimize":
                                occluder.Hide();
                                target.WindowState = WindowState.Minimized;
                                Console.WriteLine("TARGET:MINIMIZED");
                                break;
                            case "exclusive":
                                occluder.Hide();
                                RestoreTarget(target);
                                exclusive?.Dispose();
                                exclusive = ExclusiveSwapChain.TryEnter(
                                    new WindowInteropHelper(target).Handle);
                                Console.WriteLine(exclusive is null
                                    ? "EXCLUSIVE:UNAVAILABLE"
                                    : "EXCLUSIVE:ENTERED");
                                break;
                            case "exit-exclusive":
                                exclusive?.Dispose();
                                exclusive = null;
                                Console.WriteLine("EXCLUSIVE:EXITED");
                                break;
                            default:
                                Console.WriteLine("FIXTURE:UNKNOWN_COMMAND");
                                break;
                        }

                        Console.Out.Flush();
                    });
                }
            });
        };
        target.Show();
        application.Run();
        exclusive?.Dispose();
        return 0;
    }

    private static Window CreateTargetWindow()
    {
        var grid = new Grid
        {
            Width = 720,
            Height = 420,
            Background = Brushes.DarkSlateBlue,
        };
        var pulse = new Rectangle
        {
            Width = 180,
            Height = 180,
            Fill = Brushes.Gold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(pulse);
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Render, (_, _) =>
        {
            pulse.Fill = ReferenceEquals(pulse.Fill, Brushes.Gold)
                ? Brushes.MediumTurquoise
                : Brushes.Gold;
        }, Dispatcher.CurrentDispatcher);
        timer.Start();
        var window = new Window
        {
            Title = "Private-safe capture fixture",
            Width = 720,
            Height = 420,
            Content = grid,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        window.Closed += (_, _) => timer.Stop();
        return window;
    }

    private static Window CreateOccluder(Window target) => new()
    {
        Title = "Private-safe occluder",
        Owner = target,
        Width = target.Width,
        Height = target.Height,
        Background = Brushes.Black,
        Content = new TextBlock
        {
            Text = "Synthetic occlusion",
            Foreground = Brushes.White,
            FontSize = 36,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };

    private static void RestoreTarget(Window target)
    {
        target.WindowState = WindowState.Normal;
        target.Show();
        target.Activate();
    }
}

internal sealed class ExclusiveSwapChain : IDisposable
{
    private const int D3DDriverTypeHardware = 1;
    private const uint D3D11SdkVersion = 7;
    private const uint DxgiUsageRenderTargetOutput = 0x20;
    private const uint DxgiSwapChainFlagAllowModeSwitch = 0x2;
    private const int DxgiFormatB8G8R8A8Unorm = 87;
    private IntPtr _swapChain;
    private IntPtr _device;
    private IntPtr _context;
    private DispatcherTimer? _presentTimer;

    private ExclusiveSwapChain(IntPtr swapChain, IntPtr device, IntPtr context)
    {
        _swapChain = swapChain;
        _device = device;
        _context = context;
    }

    internal static ExclusiveSwapChain? TryEnter(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return null;
        }

        var description = new DxgiSwapChainDescription
        {
            BufferDescription = new DxgiModeDescription
            {
                Format = DxgiFormatB8G8R8A8Unorm,
                Scaling = 0,
                ScanlineOrdering = 0,
            },
            SampleDescription = new DxgiSampleDescription { Count = 1 },
            BufferUsage = DxgiUsageRenderTargetOutput,
            BufferCount = 2,
            OutputWindow = window,
            Windowed = true,
            SwapEffect = 0,
            Flags = DxgiSwapChainFlagAllowModeSwitch,
        };
        var result = NativeMethods.D3D11CreateDeviceAndSwapChain(
            adapter: IntPtr.Zero,
            driverType: D3DDriverTypeHardware,
            software: IntPtr.Zero,
            flags: 0,
            featureLevels: IntPtr.Zero,
            featureLevelCount: 0,
            sdkVersion: D3D11SdkVersion,
            ref description,
            out var swapChain,
            out var device,
            out _,
            out var context);
        if (result < 0 || swapChain == IntPtr.Zero)
        {
            Release(context);
            Release(device);
            Release(swapChain);
            return null;
        }

        var lease = new ExclusiveSwapChain(swapChain, device, context);
        if (lease.SetFullscreenState(true) < 0)
        {
            lease.Dispose();
            return null;
        }

        lease._presentTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(100),
            DispatcherPriority.Render,
            (_, _) => lease.Present(),
            Dispatcher.CurrentDispatcher);
        lease._presentTimer.Start();
        return lease;
    }

    private int SetFullscreenState(bool fullscreen)
    {
        var method = GetVtableMethod(_swapChain, 10);
        var invoke = Marshal.GetDelegateForFunctionPointer<SetFullscreenStateDelegate>(method);
        return invoke(_swapChain, fullscreen, IntPtr.Zero);
    }

    private void Present()
    {
        if (_swapChain == IntPtr.Zero)
        {
            return;
        }

        var method = GetVtableMethod(_swapChain, 8);
        var invoke = Marshal.GetDelegateForFunctionPointer<PresentDelegate>(method);
        _ = invoke(_swapChain, 1, 0);
    }

    private static IntPtr GetVtableMethod(IntPtr instance, int slot)
    {
        var vtable = Marshal.ReadIntPtr(instance);
        return Marshal.ReadIntPtr(vtable, checked(slot * IntPtr.Size));
    }

    public void Dispose()
    {
        _presentTimer?.Stop();
        _presentTimer = null;
        if (_swapChain != IntPtr.Zero)
        {
            _ = SetFullscreenState(false);
        }

        Release(Interlocked.Exchange(ref _context, IntPtr.Zero));
        Release(Interlocked.Exchange(ref _device, IntPtr.Zero));
        Release(Interlocked.Exchange(ref _swapChain, IntPtr.Zero));
    }

    private static void Release(IntPtr value)
    {
        if (value != IntPtr.Zero)
        {
            _ = Marshal.Release(value);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetFullscreenStateDelegate(
        IntPtr self,
        [MarshalAs(UnmanagedType.Bool)] bool fullscreen,
        IntPtr target);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int PresentDelegate(IntPtr self, uint syncInterval, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct DxgiRational
    {
        internal uint Numerator;
        internal uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DxgiModeDescription
    {
        internal uint Width;
        internal uint Height;
        internal DxgiRational RefreshRate;
        internal int Format;
        internal int ScanlineOrdering;
        internal int Scaling;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DxgiSampleDescription
    {
        internal uint Count;
        internal uint Quality;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DxgiSwapChainDescription
    {
        internal DxgiModeDescription BufferDescription;
        internal DxgiSampleDescription SampleDescription;
        internal uint BufferUsage;
        internal uint BufferCount;
        internal IntPtr OutputWindow;
        [MarshalAs(UnmanagedType.Bool)]
        internal bool Windowed;
        internal int SwapEffect;
        internal uint Flags;
    }

    private static class NativeMethods
    {
        [DllImport("d3d11.dll")]
        internal static extern int D3D11CreateDeviceAndSwapChain(
            IntPtr adapter,
            int driverType,
            IntPtr software,
            uint flags,
            IntPtr featureLevels,
            uint featureLevelCount,
            uint sdkVersion,
            ref DxgiSwapChainDescription swapChainDescription,
            out IntPtr swapChain,
            out IntPtr device,
            out int selectedFeatureLevel,
            out IntPtr immediateContext);
    }
}
