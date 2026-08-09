using System.Windows;
using CompanionCore.Capture.Fake;
using CompanionCore.Presentation;
using CompanionCore.Runtime;
using CompanionCore.Runtime.Diagnostics;

namespace CompanionCore.App;

/// <summary>
/// The application composition root. This is the only place a <see cref="CompanionRuntime"/>
/// is constructed — everything else (windows, view models) resolves the existing
/// instance. Single-instance enforcement happens here, before any subsystem
/// initializes, per Task 1's requirement.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = "Local\\CompanionCore.Dev.SingleInstance";

    private SingleInstanceGuard? _instanceGuard;
    private CompanionRuntime? _runtime;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceGuard = new SingleInstanceGuard(SingleInstanceMutexName);
        if (!_instanceGuard.TryAcquire())
        {
            // A second process: exit deterministically before constructing anything
            // that would look like a second companion identity.
            _instanceGuard.Dispose();
            Shutdown(0);
            return;
        }

        var diagnosticsEnabled = e.Args.Contains("--diagnostics", StringComparer.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("COMPANIONCORE_DIAGNOSTICS"), "1", StringComparison.Ordinal);
        IDiagnosticsSink diagnostics = diagnosticsEnabled
            ? new ConsoleDiagnosticsSink()
            : NullDiagnosticsSink.Instance;

        _runtime = new CompanionRuntime(diagnostics);

        var adapter = new NeutralPersonalityAdapter();
        var captureWorker = new FakeCaptureWorker();

        var window = new MainWindow(_runtime, adapter, captureWorker);
        MainWindow = window;
        window.Show();

        var startResult = _runtime.Start(checkpointRecovered: false);
        window.RenderTransition(startResult);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Idempotent: OnExit can run after an already-explicit Stop, or after none at
        // all if the window was closed directly.
        _runtime?.Dispose();
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }
}
