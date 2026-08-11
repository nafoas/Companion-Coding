using System.Windows;
using CompanionCore.Capture.Fake;
using CompanionCore.Presentation;
using CompanionCore.Privacy;
using CompanionCore.Runtime;
using CompanionCore.Runtime.Diagnostics;
using CompanionCore.TargetAuth;
using CompanionCore.TargetAuth.Windows;

namespace CompanionCore.App;

/// <summary>
/// The application composition root. This is the only place a <see cref="CompanionRuntime"/>
/// is constructed — everything else (windows, view models) resolves the existing
/// instance. Single-instance enforcement happens here, before any subsystem
/// initializes, per Task 1's requirement.
/// </summary>
/// <remarks>
/// Also implements the <c>--test-mode=&lt;scenario&gt;</c> bounded test harness the
/// architecture review asked for: a way to prove the real, compiled, Windows-launched
/// app behaves correctly (no key/network/capture needed to reach ready, one runtime
/// across multiple real windows, genuine second-process rejection, clean shutdown)
/// without needing UI automation tooling. Each scenario runs unattended, prints a
/// single-line machine-readable marker to stdout, and exits with a distinct code —
/// see <c>tests/CompanionCore.App.IntegrationTests</c>, which is the only consumer of
/// these arguments. Outside test mode, none of this runs; the app behaves exactly as a
/// human launching it would see.
/// </remarks>
public partial class App : Application
{
    private const string SingleInstanceMutexName = "Local\\CompanionCore.Dev.SingleInstance";

    private SingleInstanceGuard? _instanceGuard;
    private CompanionRuntime? _runtime;
    private TargetSessionController? _targetController;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var diagnosticsEnabled = e.Args.Contains("--diagnostics", StringComparer.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("COMPANIONCORE_DIAGNOSTICS"), "1", StringComparison.Ordinal);
        IDiagnosticsSink diagnostics = diagnosticsEnabled
            ? new ConsoleDiagnosticsSink()
            : NullDiagnosticsSink.Instance;

        var testMode = e.Args
            .Select(a => a.StartsWith("--test-mode=", StringComparison.OrdinalIgnoreCase) ? a["--test-mode=".Length..] : null)
            .FirstOrDefault(v => v is not null);

        _instanceGuard = new SingleInstanceGuard(SingleInstanceMutexName);
        if (!_instanceGuard.TryAcquire())
        {
            // A second process: exit deterministically before constructing anything
            // that would look like a second companion identity. CompanionRuntime.ConstructionCount
            // is process-local, so printing it here (always 0, since we return before
            // ever reaching CompanionRuntime.ClaimConstructionAuthority) is what proves
            // this process never built one, not just that it exited.
            _instanceGuard.Dispose();
            if (testMode is not null)
            {
                Console.WriteLine($"SECOND_INSTANCE:REJECTED CONSTRUCTIONS:{CompanionRuntime.ConstructionCount}");
                Shutdown(2);
                return;
            }

            Shutdown(0);
            return;
        }

        _runtime = CompanionRuntime.ClaimConstructionAuthority().Construct(diagnostics);
        var adapter = new NeutralPersonalityAdapter();
        var privacyState = new RuntimePrivacyState();
        var policyCatalog = TargetPolicyCatalog.OpenDevelopmentAsync()
            .GetAwaiter()
            .GetResult();
        var targetAuthorization = new TargetAuthorizationService(
            new WindowsTargetDiscovery(),
            new WindowsDisplayTopology(),
            policyCatalog,
            privacyState);
        var captureWorker = new FakeCaptureWorker();
        _targetController = new TargetSessionController(
            targetAuthorization,
            captureWorker,
            privacyState,
            new LocalPrivacyGuard());

        var window = new MainWindow(
            _runtime,
            adapter,
            targetAuthorization,
            _targetController,
            new WindowsGlobalHotkeyNativeApi(),
            registerGlobalHotkey: true);
        MainWindow = window;
        window.Show();

        var startResult = _runtime.Start(checkpointRecovered: false);
        window.RenderTransition(startResult);

        if (testMode is not null)
        {
            RunTestMode(
                testMode,
                window,
                adapter,
                targetAuthorization,
                _targetController);
        }
    }

    private void RunTestMode(
        string scenario,
        MainWindow firstWindow,
        IPersonalityAdapter adapter,
        TargetAuthorizationService targetAuthorization,
        TargetSessionController targetController)
    {
        switch (scenario)
        {
            case "ready":
                Console.WriteLine($"READY CONSTRUCTIONS:{CompanionRuntime.ConstructionCount}");
                Shutdown(0);
                break;

            case "multiwindow":
                // Two additional real windows, deliberately sharing the same _runtime
                // reference rather than constructing their own — proving the "one
                // runtime across windows" property end-to-end in a real process, not
                // just via the construction-authority unit tests.
                {
                    var hotkeyApi = new WindowsGlobalHotkeyNativeApi();
                    var second = new MainWindow(
                        _runtime!,
                        adapter,
                        targetAuthorization,
                        targetController,
                        hotkeyApi,
                        registerGlobalHotkey: false);
                    var third = new MainWindow(
                        _runtime!,
                        adapter,
                        targetAuthorization,
                        targetController,
                        hotkeyApi,
                        registerGlobalHotkey: false);
                    second.Show();
                    third.Show();
                    Console.WriteLine($"WINDOWS:3 CONSTRUCTIONS:{CompanionRuntime.ConstructionCount}");
                    second.Close();
                    third.Close();
                }

                Shutdown(0);
                break;

            case "shutdown":
                var stopResult = _runtime!.Stop();
                firstWindow.RenderTransition(stopResult);
                Console.WriteLine($"SHUTDOWN:{_runtime.State} CONSTRUCTIONS:{CompanionRuntime.ConstructionCount}");
                firstWindow.Close();
                Shutdown(0);
                break;

            case "hold":
                // Used by the second-process integration test: signal that the guard is
                // held, then block until the harness tells us to release it, so the
                // harness can launch a genuine second process while this one is still
                // running.
                Console.WriteLine($"HOLDING CONSTRUCTIONS:{CompanionRuntime.ConstructionCount}");
                Console.Out.Flush();
                Console.In.ReadLine();
                var heldStopResult = _runtime!.Stop();
                firstWindow.RenderTransition(heldStopResult);
                Console.WriteLine($"SHUTDOWN:{_runtime.State}");
                firstWindow.Close();
                Shutdown(0);
                break;

            default:
                Console.WriteLine($"UNKNOWN_TEST_MODE:{scenario}");
                Shutdown(64);
                break;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Idempotent: OnExit can run after an already-explicit Stop, or after none at
        // all if the window was closed directly.
        try
        {
            if (_targetController is not null)
            {
                _targetController.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        finally
        {
            _runtime?.Dispose();
            _instanceGuard?.Dispose();
            base.OnExit(e);
        }
    }
}
