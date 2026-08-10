using CompanionCore.Runtime;

namespace CompanionCore.Presentation;

/// <summary>
/// The content-producing contract. Maps typed events/context to opaque
/// <see cref="PresentationContent"/> for <see cref="IPresentationSink"/> to render. The
/// two contracts intentionally have different input/output shapes — an adapter never
/// hands its typed input straight to a sink.
/// </summary>
public interface IPersonalityAdapter
{
    PresentationContent Map(LifecycleTransitionResult transition);
}
