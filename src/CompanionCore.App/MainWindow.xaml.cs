using System.Globalization;
using System.IO;
using System.Windows;
using CompanionCore.Capture.Contracts;
using CompanionCore.Orchestration;
using CompanionCore.Presentation;
using CompanionCore.Privacy;
using CompanionCore.Runtime;
using CompanionCore.TargetAuth;
using CompanionCore.TargetAuth.Windows;

namespace CompanionCore.App;

/// <summary>
/// The neutral shell needed to exercise lifecycle, target consent, and Task 6's
/// normalized manual-region control.
/// This window never constructs a <see cref="CompanionRuntime"/>; it only holds a
/// reference to the one the composition root already built.
/// </summary>
public partial class MainWindow : Window
{
    private readonly CompanionRuntime _runtime;
    private readonly IPersonalityAdapter _adapter;
    private readonly IPresentationSink _sink;
    private readonly IPresentationSink _targetSink;
    private readonly IPresentationSink _selectionSink;
    private readonly IPresentationSink _companionSink;
    private readonly TargetAuthorizationService _targetAuthorization;
    private readonly TargetSessionController _targetController;
    private readonly WpfPrivacyHotkey? _privacyHotkey;

    public MainWindow(
        CompanionRuntime runtime,
        IPersonalityAdapter adapter,
        TargetAuthorizationService targetAuthorization,
        TargetSessionController targetController,
        IGlobalHotkeyNativeApi hotkeyNativeApi,
        bool registerGlobalHotkey)
    {
        InitializeComponent();

        _runtime = runtime;
        _adapter = adapter;
        _sink = new WpfPresentationSink(StatusText);
        _targetSink = new WpfPresentationSink(TargetStatusText);
        _selectionSink = new WpfPresentationSink(SelectionStatusText);
        _companionSink = new WpfPresentationSink(CompanionStatusText);
        _targetAuthorization = targetAuthorization;
        _targetController = targetController;
        AuthorizationCategoryCombo.ItemsSource = Enum.GetValues<AuthorizationCategory>();
        _targetController.SessionEvent += TargetController_SessionEvent;
        _targetController.AttentionSheetAvailable += TargetController_AttentionSheetAvailable;

        if (registerGlobalHotkey)
        {
            _privacyHotkey = new WpfPrivacyHotkey(
                this,
                hotkeyNativeApi,
                () => _targetController.PrivacyStopAsync(),
                () => _targetController.HandleDisplayTopologyChangedAsync());
            _privacyHotkey.RegistrationChanged += (_, result) =>
                HotkeyStatusText.Text = result.Succeeded
                    ? "Privacy hotkey ready: Ctrl+Shift+F12 (stop only)."
                    : $"Privacy hotkey unavailable (Win32 error {result.ErrorCode}). Use the Privacy stop button.";
            _privacyHotkey.Attach();
        }
        else
        {
            HotkeyStatusText.Text = "Privacy hotkey is owned by the primary window.";
        }

        Closed += (_, _) =>
        {
            _targetController.SessionEvent -= TargetController_SessionEvent;
            _targetController.AttentionSheetAvailable -= TargetController_AttentionSheetAvailable;
            _privacyHotkey?.Dispose();
        };
    }

    public void RenderTransition(LifecycleTransitionResult transition)
    {
        _sink.Render(_adapter.Map(transition));
    }

    /// <summary>Renders one orchestration notice; called on the dispatcher thread.</summary>
    public void RenderNotice(CompanionNotice notice)
    {
        _companionSink.Render(_adapter.Map(notice));
        _replyTarget.Observe(notice);

        // FIX-10: name which notice this is (an offer to talk, a saved topic, a state change).
        var detail = notice.Attention?.Kind.ToString()
            ?? notice.Conversation?.Kind.ToString()
            ?? notice.Watchbun?.Kind.ToString()
            ?? notice.Keepsake?.Kind.ToString()
            ?? notice.Braincase?.ToString()
            ?? notice.Key;
        NoticeLog.Items.Insert(0, string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:HH:mm:ss}  {notice.Kind}{(detail is { Length: > 0 } ? $"  {detail}" : string.Empty)}"));
        while (NoticeLog.Items.Count > MaximumNoticeLogEntries)
        {
            NoticeLog.Items.RemoveAt(NoticeLog.Items.Count - 1);
        }
    }

    private const int MaximumNoticeLogEntries = 200;

    /// <summary>FIX-10: which of Prince's openings the Reply buttons answer.</summary>
    private readonly ReplyTarget _replyTarget = new();

    /// <summary>True once the placeholder commands are attached to the companion host.</summary>
    internal bool CompanionCommandsEnabled => CompanionCommands.IsEnabled;

    private CompanionComposition? _companion;

    /// <summary>
    /// CORE-12: enables the placeholder commands once the one companion host exists, and shows
    /// which Braincase Builder Prince runs with. Every command goes through the orchestrator or
    /// host; this window holds no authority of its own.
    /// </summary>
    internal void AttachCompanion(CompanionComposition companion)
    {
        _companion = companion;
        var braincase = companion.Braincase;
        BraincaseStatusText.Text = braincase.LiveCallsEnabled
            ? $"Braincase: Claude, live calls on, Bun Budget ${braincase.Bridge!.DailyCostBudget!.LimitPerDay.ToString("0.00", CultureInfo.InvariantCulture)} per day."
            : $"Braincase: offline (live calls off{(braincase.Issue == Braincase.BuilderBraincaseIssue.None ? string.Empty : $"; settings issue: {braincase.Issue}")}).";
        CompanionCommands.IsEnabled = true;
    }

    private ICaptureWorker? _pulseWorker;
    private CaptureWorkerStatusChanged? _workerStatus;
    private long _framesProduced;
    private long _framesAdmitted;
    private long _sniffsProduced;
    private System.Windows.Threading.DispatcherTimer? _pulseTimer;
    private Func<CancellationToken, Task<CaptureWorkerMetrics>>? _pulseMetrics;

    /// <summary>
    /// FIX-07: a live pulse line, refreshed every second, so Boss can see every step from
    /// the worker's frames to the Braincase's looks. Counts and state names only.
    /// </summary>
    internal void AttachPulse(ICaptureWorker worker, Func<CancellationToken, Task<CaptureWorkerMetrics>>? workerMetrics = null)
    {
        _pulseWorker = worker;
        _pulseMetrics = workerMetrics;
        worker.StatusChanged += PulseWorker_StatusChanged;
        worker.FrameProduced += PulseWorker_FrameProduced;
        worker.SniffProduced += PulseWorker_SniffProduced;
        _targetController.FrameAdmitted += PulseController_FrameAdmitted;
        _pulseTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _pulseTimer.Tick += async (_, _) => await RefreshPulseAsync();
        _pulseTimer.Start();
        Closed += (_, _) =>
        {
            _pulseTimer.Stop();
            worker.StatusChanged -= PulseWorker_StatusChanged;
            worker.FrameProduced -= PulseWorker_FrameProduced;
            worker.SniffProduced -= PulseWorker_SniffProduced;
            _targetController.FrameAdmitted -= PulseController_FrameAdmitted;
        };
    }

    private void PulseWorker_StatusChanged(object? sender, CaptureWorkerStatusChanged change) => Volatile.Write(ref _workerStatus, change);

    private void PulseWorker_FrameProduced(object? sender, CaptureFrameMetadata frame) => Interlocked.Increment(ref _framesProduced);

    private void PulseWorker_SniffProduced(object? sender, SniffSignal sniff) => Interlocked.Increment(ref _sniffsProduced);

    private void PulseController_FrameAdmitted(object? sender, CaptureFrameMetadata frame) => Interlocked.Increment(ref _framesAdmitted);

    private async Task RefreshPulseAsync()
    {
        if (_pulseWorker is not { } worker)
        {
            return;
        }

        var status = Volatile.Read(ref _workerStatus);
        var capture = string.Create(
            CultureInfo.InvariantCulture,
            $"Capture: {worker.Status}{(status is { Reason: var reason } ? $" ({reason})" : string.Empty)}; frames {Interlocked.Read(ref _framesProduced)}, admitted {Interlocked.Read(ref _framesAdmitted)}; sniffs {Interlocked.Read(ref _sniffsProduced)}");
        if (_pulseMetrics is { } read)
        {
            // FIX-08: the worker's own counts, so a frame that never becomes a sniff or sheet shows why.
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                var m = await read(timeout.Token);
                capture += string.Create(
                    CultureInfo.InvariantCulture,
                    $"\nWorker: accepted {m.AcceptedFrames}, dropped {m.DroppedFrames}; changed {m.ChangedFrames}, same {m.DuplicateFrames}; sheets {m.ProducedAttentionSheets} (dropped {m.DroppedAttentionSheets}); failures {m.VisualFailures}{(m.VisualFailures > 0 ? $" ({m.LastVisualFailureStage}, 0x{m.LastVisualFailureCode:X8})" : string.Empty)}");
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                capture += "\nWorker: counts unavailable";
            }
        }

        if (_companion is not { } companion)
        {
            PulseText.Text = capture;
            return;
        }

        try
        {
            var snapshot = await companion.Host.Orchestrator.GetSnapshotAsync();
            var attention = snapshot.Attention is { } current
                ? string.Create(CultureInfo.InvariantCulture, $"{current.State} {current.Score:0}")
                : "no session";
            PulseText.Text = string.Create(
                CultureInfo.InvariantCulture,
                $"{capture}\nPrince: sniffs used {snapshot.Sniff.Forwarded}/{snapshot.Sniff.Received}; sheets {snapshot.SheetsReceived}; attention {attention}\n"
                + $"Looks: Glances {snapshot.GlancesSent}, Inspects {snapshot.InspectsSent}; last {(snapshot.LastLookOutcome?.ToString() ?? "none")}; faults {snapshot.Faults}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            PulseText.Text = capture;
        }
    }

    private void ShowResult(string command, object result)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{command}: {(result is "None" ? "accepted" : result)}");
        CommandResultText.Text = string.Create(CultureInfo.InvariantCulture, $"Last command ({DateTime.Now:HH:mm:ss}): {text}");
        NoticeLog.Items.Insert(0, string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:HH:mm:ss}  > {text}"));
    }

    private async Task RunCommandAsync(string command, Func<CompanionCore.Orchestration.CompanionOrchestrator, Task<string>> run)
    {
        if (_companion is not { } companion)
        {
            return;
        }

        try
        {
            ShowResult(command, await run(companion.Host.Orchestrator));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            ShowResult(command, "not available now");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A placeholder command never takes the window down; the failure type is shown.
            ShowResult(command, $"failed ({exception.GetType().Name})");
        }
    }

    private async void ReplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<Conversation.UserTurnKind>(tag, out var kind))
        {
            await RunCommandAsync("Reply", async orchestrator => (await _replyTarget.ReplyAsync(orchestrator, kind)).ToString());
        }
    }

    private async void ConversationLockCheckBox_Click(object sender, RoutedEventArgs e)
    {
        var locked = ConversationLockCheckBox.IsChecked == true;
        await RunCommandAsync("Conversation lock", async orchestrator => (await orchestrator.SetConversationLockAsync(locked)).ToString());
    }

    private async void QuietButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<Watchbun.QuietAnswer>(tag, out var answer))
        {
            await RunCommandAsync("Quiet check", async orchestrator => (await orchestrator.AnswerQuietCheckAsync(answer)).ToString());
        }
    }

    private async void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<Watchbun.ExitDecision>(tag, out var decision))
        {
            await RunCommandAsync("After exit", async orchestrator => (await orchestrator.DecideAfterExitAsync(decision)).ToString());
        }
    }

    private async void AddWatchTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var key = WatchTaskKeyBox.Text.Trim();
        await RunCommandAsync("Watch task", async orchestrator => (await orchestrator.AddWatchTaskAsync(key)).ToString());
    }

    private async void TakePhotographButton_Click(object sender, RoutedEventArgs e) =>
        await RunCommandAsync("Photograph", async orchestrator => (await orchestrator.TakePhotographAsync()).ToString());

    private async void ExportVaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (_companion is not { } companion)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Vault",
            Filter = "Vault export (*.zip)|*.zip",
            FileName = string.Create(CultureInfo.InvariantCulture, $"vault-export-{DateTime.Now:yyyyMMdd-HHmmss}.zip"),
            OverwritePrompt = false,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var report = await companion.Host.ExportVaultAsync(dialog.FileName);
            ShowResult("Vault export", string.Create(CultureInfo.InvariantCulture, $"verified, {report.Bytes} bytes"));
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            ShowResult("Vault export", "refused (the file must be new and outside the data folder)");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Includes a Vault that did not validate; nothing was written.
            ShowResult("Vault export", "refused (the Vault did not validate)");
        }
    }

    private async void RepairVaultButton_Click(object sender, RoutedEventArgs e)
    {
        if (_companion is not { } companion)
        {
            return;
        }

        // Repair is opt-in only and only ever continues the current line of memories.
        var confirm = MessageBox.Show(
            this,
            "Run Vault repair now? It only restores what is damaged or missing from the latest backup and never removes newer memories. End the target session first.",
            "Repair Vault",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Cancel);
        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            var report = await companion.Host.RepairAsync();
            ShowResult("Vault repair", string.Create(CultureInfo.InvariantCulture, $"complete={report.Complete}, photographs restored={report.PhotographsRestored}"));
        }
        catch (InvalidOperationException)
        {
            ShowResult("Vault repair", "not available now (end the target session first)");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowResult("Vault repair", "could not run; nothing was changed");
        }
    }

    private async void NapButton_Click(object sender, RoutedEventArgs e)
    {
        await _targetController.PrivacyStopAsync();
        RenderTransition(_runtime.Nap());
    }

    private void WakeButton_Click(object sender, RoutedEventArgs e) => RenderTransition(_runtime.Wake());

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        await _targetController.PrivacyStopAsync();
        await _targetController.EndSessionAsync();
        RenderTransition(_runtime.Stop());
        Close();
    }

    private async void RefreshTargetsButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _targetAuthorization.DiscoverAsync();
        TargetCombo.ItemsSource = result.Candidates;
        var kind = result.Status switch
        {
            TargetDiscoveryStatus.Ready => TargetSessionEventKind.DiscoveryReady,
            TargetDiscoveryStatus.UnsupportedDisplayTopology => TargetSessionEventKind.DiscoveryBlocked,
            _ => TargetSessionEventKind.DiscoveryFailed
        };
        RenderSelectionEvent(new TargetSessionEvent(kind, null));
    }

    private void TargetCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TargetCombo.SelectedItem is not TargetCandidate candidate)
        {
            return;
        }

        var invitation = _targetAuthorization.Inspect(candidate);
        AuthorizationCategoryCombo.SelectedItem = invitation.Policy.AuthorizationCategory;
        TrustedGameCheckBox.IsChecked = invitation.Policy.ContentPolicy == TargetContentPolicy.TrustedGame;
        var kind = invitation.Disposition switch
        {
            TargetInvitationDisposition.DeniedWithoutPrompt => TargetSessionEventKind.Denied,
            TargetInvitationDisposition.ConsentRequired => TargetSessionEventKind.ConsentRequired,
            _ => TargetSessionEventKind.StandingAuthorizationAvailable
        };
        RenderSelectionEvent(new TargetSessionEvent(kind, candidate));
    }

    private async void ApplyPolicyButton_Click(object sender, RoutedEventArgs e)
    {
        if (TargetCombo.SelectedItem is not TargetCandidate candidate
            || AuthorizationCategoryCombo.SelectedItem is not AuthorizationCategory category)
        {
            return;
        }

        try
        {
            var policy = new TargetPolicy(
                category,
                TrustedGameCheckBox.IsChecked == true
                    ? TargetContentPolicy.TrustedGame
                    : TargetContentPolicy.Standard);
            await _targetController.SetExplicitPolicyAsync(candidate, policy);
            TargetCombo_SelectionChanged(this, null!);
        }
        catch (Exception)
        {
            RenderSelectionEvent(new TargetSessionEvent(TargetSessionEventKind.Failed, candidate));
        }
    }

    private async void AuthorizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (TargetCombo.SelectedItem is TargetCandidate candidate)
        {
            await _targetController.AuthorizeAsync(candidate, explicitConsent: true);
        }
    }

    private async void PrivacyStopButton_Click(object sender, RoutedEventArgs e) =>
        await _targetController.PrivacyStopAsync();

    private async void ResumeTargetButton_Click(object sender, RoutedEventArgs e) =>
        await _targetController.ResumeExplicitlyAsync();

    private async void EndTargetButton_Click(object sender, RoutedEventArgs e) =>
        await _targetController.EndSessionAsync();

    private async void ApplyManualRegionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var region = new NormalizedRegion(
                ParseNormalized(ManualXBox.Text),
                ParseNormalized(ManualYBox.Text),
                ParseNormalized(ManualWidthBox.Text),
                ParseNormalized(ManualHeightBox.Text));
            region.Validate(nameof(region));
            await _targetController.SetManualRegionAsync(region);
            AttentionStatusText.Text = "Manual region accepted; waiting for a current sheet.";
        }
        catch (Exception)
        {
            AttentionStatusText.Text =
                "Manual region rejected; use finite fractions contained in 0–1 with an active target.";
        }
    }

    private async void ClearManualRegionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _targetController.SetManualRegionAsync(null);
            AttentionStatusText.Text = "Manual region cleared.";
        }
        catch (Exception)
        {
            AttentionStatusText.Text =
                "Manual region could not be cleared because no target is active.";
        }
    }

    private void TargetController_SessionEvent(object? sender, TargetSessionEvent targetEvent)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => RenderTargetEvent(targetEvent));
            return;
        }

        RenderTargetEvent(targetEvent);
    }

    private void TargetController_AttentionSheetAvailable(
        object? sender,
        AttentionSheetMetadata metadata)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => TargetController_AttentionSheetAvailable(sender, metadata));
            return;
        }

        AttentionStatusText.Text =
            $"Attention sheet ready: {metadata.Kind}; source {metadata.SourceWidth}×{metadata.SourceHeight}; "
            + $"{metadata.Regions.Length} labeled region(s).";
    }

    private static double ParseNormalized(string text) =>
        double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    private void RenderTargetEvent(TargetSessionEvent targetEvent) =>
        _targetSink.Render(_adapter.Map(targetEvent));

    private void RenderSelectionEvent(TargetSessionEvent targetEvent) =>
        _selectionSink.Render(_adapter.Map(targetEvent));
}
