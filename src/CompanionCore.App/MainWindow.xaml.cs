using System.Windows;
using CompanionCore.Presentation;
using CompanionCore.Privacy;
using CompanionCore.Runtime;
using CompanionCore.TargetAuth;
using CompanionCore.TargetAuth.Windows;

namespace CompanionCore.App;

/// <summary>
/// The neutral shell needed to exercise lifecycle and Task 4 target-consent controls.
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
        _targetAuthorization = targetAuthorization;
        _targetController = targetController;
        AuthorizationCategoryCombo.ItemsSource = Enum.GetValues<AuthorizationCategory>();
        _targetController.SessionEvent += TargetController_SessionEvent;

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
            _privacyHotkey?.Dispose();
        };
    }

    public void RenderTransition(LifecycleTransitionResult transition)
    {
        _sink.Render(_adapter.Map(transition));
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

    private void TargetController_SessionEvent(object? sender, TargetSessionEvent targetEvent)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => RenderTargetEvent(targetEvent));
            return;
        }

        RenderTargetEvent(targetEvent);
    }

    private void RenderTargetEvent(TargetSessionEvent targetEvent) =>
        _targetSink.Render(_adapter.Map(targetEvent));

    private void RenderSelectionEvent(TargetSessionEvent targetEvent) =>
        _selectionSink.Render(_adapter.Map(targetEvent));
}
