using System.Windows;
using CompanionCore.Capture.Contracts;
using CompanionCore.Presentation;
using CompanionCore.Runtime;

namespace CompanionCore.App;

/// <summary>
/// The minimum shell needed to exercise the engine: a blank text box, a neutral
/// placeholder icon, a status display, and controls for the lifecycle transitions a
/// human can trigger (nap, wake, stop — start happens automatically on launch). This
/// window never constructs a <see cref="CompanionRuntime"/>; it only holds a reference
/// to the one the composition root already built.
/// </summary>
public partial class MainWindow : Window
{
    private readonly CompanionRuntime _runtime;
    private readonly IPersonalityAdapter _adapter;
    private readonly ICaptureWorker _captureWorker;
    private readonly IPresentationSink _sink;

    public MainWindow(CompanionRuntime runtime, IPersonalityAdapter adapter, ICaptureWorker captureWorker)
    {
        InitializeComponent();

        _runtime = runtime;
        _adapter = adapter;
        _captureWorker = captureWorker;
        _sink = new WpfPresentationSink(StatusText);

        Closed += (_, _) => _captureWorker.Dispose();
    }

    public void RenderTransition(LifecycleTransitionResult transition)
    {
        _sink.Render(_adapter.Map(transition));
    }

    private void NapButton_Click(object sender, RoutedEventArgs e) => RenderTransition(_runtime.Nap());

    private void WakeButton_Click(object sender, RoutedEventArgs e) => RenderTransition(_runtime.Wake());

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        RenderTransition(_runtime.Stop());
        Close();
    }
}
