using System.IO;
using System.Windows.Threading;
using CompanionCore.Api;
using CompanionCore.Memory;
using CompanionCore.Orchestration;
using CompanionCore.Platform.Windows;
using CompanionCore.Privacy;
using CompanionCore.Runtime.Diagnostics;
using CompanionCore.TargetAuth;
using CompanionCore.TargetAuth.Windows;
using CompanionCore.Watchbun;
using Microsoft.Win32;

namespace CompanionCore.App;

/// <summary>
/// Composes the accepted orchestration core into the App: one <see cref="CompanionHost"/>
/// on the development data root (or an isolated test root in test mode), the offline
/// Braincase shell, real Windows platform signals, a bounded dispatcher tick, and the
/// system lock/sleep notifications. Nothing here holds a credential or opens the network.
/// </summary>
internal sealed class CompanionComposition : IAsyncDisposable
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private const string TestDataRootArgument = "--test-data-root=";
    private const string TestRunArgument = "--test-run=";
    internal const string CalibrationArgument = "--calibration-log";
    private const string CalibrationIntervalArgument = "--calibration-interval-ms=";

    private readonly DispatcherTimer _timer;
    private readonly InMemoryCredentialStore _credentials;
    private bool _ticking;
    private bool _disposed;

    private CompanionComposition(CompanionHost host, WindowsPlatformSignals platform, InMemoryCredentialStore credentials, Dispatcher dispatcher)
    {
        Host = host;
        _credentials = credentials;
        Platform = platform;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _timer = new DispatcherTimer(TickInterval, DispatcherPriority.Background, OnTick, dispatcher);
        _timer.Start();
    }

    public CompanionHost Host { get; }

    public WindowsPlatformSignals Platform { get; }

    /// <summary>Present only when the App was started with <c>--calibration-log</c>.</summary>
    public CalibrationSampler? Calibration { get; private set; }

    /// <summary>Tick failures contained so far (a failed tick never stops the timer).</summary>
    public long TickFailures { get; private set; }

    /// <summary>
    /// Development runs use the fixed development root. Test-mode runs use an isolated test
    /// root and never touch development data: <c>--test-data-root=&lt;absolute path&gt;</c>
    /// and <c>--test-run=&lt;guid&gt;</c>, or a fresh temporary root.
    /// </summary>
    public static MemoryStoreLocation ResolveLocation(IReadOnlyList<string> args, bool testMode)
    {
        if (!testMode)
        {
            var localApplicationData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify);
            return new DevelopmentDataRootPolicy(localApplicationData).Resolve();
        }

        var root = Argument(args, TestDataRootArgument)
            ?? Path.Combine(Path.GetTempPath(), "CompanionCore.App.TestMode");
        var run = Guid.TryParse(Argument(args, TestRunArgument), out var id) && id != Guid.Empty ? id : Guid.NewGuid();
        return TestDataRootPolicy.Create(root, run);
    }

    public static CompanionComposition Open(
        MemoryStoreLocation location,
        RuntimePrivacyState privacy,
        TargetSessionController controller,
        IDiagnosticsSink diagnostics,
        Action<CompanionNotice> onNotice,
        Dispatcher dispatcher)
    {
        var credentials = new InMemoryCredentialStore();
        var platform = new WindowsPlatformSignals(new WindowsPlatformNative(), new WindowsTargetDiscovery());
        try
        {
            var options = new CompanionHostOptions(location, privacy, controller, new RealSemanticProviderShell(credentials), credentials)
            {
                Platform = platform,
                Diagnostics = diagnostics,
                Notice = (_, notice) => onNotice(notice),
            };

            // Off the dispatcher's synchronization context, so the start can never wait on this thread.
            var host = Task.Run(() => CompanionHost.OpenAsync(options)).GetAwaiter().GetResult();
            return new CompanionComposition(host, platform, credentials, dispatcher);
        }
        catch
        {
            platform.DisposeAsync().AsTask().GetAwaiter().GetResult();
            credentials.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Stage 11 calibration: with <c>--calibration-log</c>, samples resource numbers and typed
    /// states beside the memory root (privacy-safe and bounded). Otherwise does nothing.
    /// </summary>
    public void StartCalibration(IReadOnlyList<string> args, Capture.Contracts.ICaptureWorker worker)
    {
        if (Calibration is not null || !args.Contains(CalibrationArgument, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var interval = int.TryParse(Argument(args, CalibrationIntervalArgument), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var milliseconds)
            ? TimeSpan.FromMilliseconds(milliseconds)
            : CalibrationSampler.DefaultInterval;
        Calibration = new CalibrationSampler(
            new Calibration.CalibrationRecorder(CalibrationSampler.DirectoryFor(Host.Location)),
            Host,
            worker,
            interval);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Calibration is not null)
        {
            await Calibration.DisposeAsync().ConfigureAwait(false);
        }

        _timer.Stop();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        try
        {
            await Host.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await Platform.DisposeAsync().ConfigureAwait(false);
            _credentials.Dispose();
        }
    }

    internal static string? Argument(IReadOnlyList<string> args, string prefix) =>
        args.Where(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(argument => argument[prefix.Length..])
            .FirstOrDefault(value => value.Length > 0);

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_ticking || _disposed)
        {
            return;
        }

        _ticking = true;
        try
        {
            await Host.Orchestrator.TickAsync();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Contained: a disposed orchestrator during shutdown, or a contained fault.
            TickFailures++;
        }
        finally
        {
            _ticking = false;
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                Platform.ReportSuspended(SuspendReason.Lock);
                break;
            case SessionSwitchReason.SessionUnlock:
                Platform.ReportResumed(SuspendReason.Lock);
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                Platform.ReportSuspended(SuspendReason.Sleep);
                break;
            case PowerModes.Resume:
                Platform.ReportResumed(SuspendReason.Sleep);
                break;
        }
    }
}
