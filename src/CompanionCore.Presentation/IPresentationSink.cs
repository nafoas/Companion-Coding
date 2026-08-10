namespace CompanionCore.Presentation;

/// <summary>
/// The UI-facing contract. Renders whatever <see cref="PresentationContent"/> it's
/// handed — it does not interpret, generate, or filter content, and it never sees the
/// typed events/context that produced it.
/// </summary>
public interface IPresentationSink
{
    void Render(PresentationContent content);
}
