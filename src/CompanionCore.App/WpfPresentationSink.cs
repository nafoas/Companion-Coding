using System.Windows.Controls;
using CompanionCore.Presentation;

namespace CompanionCore.App;

/// <summary>
/// The only place a rendered <see cref="PresentationContent"/> becomes visible pixels.
/// It resolves the neutral placeholder string for the content key and displays it —
/// nothing here generates or interprets content; that already happened in
/// <see cref="NeutralPersonalityAdapter"/>.
/// </summary>
public sealed class WpfPresentationSink : IPresentationSink
{
    private readonly TextBlock _statusText;

    public WpfPresentationSink(TextBlock statusText)
    {
        _statusText = statusText;
    }

    public void Render(PresentationContent content)
    {
        _statusText.Text = PlaceholderStrings.Resolve(content);
    }
}
