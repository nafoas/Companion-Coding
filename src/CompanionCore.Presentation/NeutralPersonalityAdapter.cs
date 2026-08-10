using CompanionCore.Runtime;

namespace CompanionCore.Presentation;

/// <summary>
/// The only <see cref="IPersonalityAdapter"/> implementation wired in during neutral-core
/// stages. This is a direct, literal implementation of architecture §6.2.1's normative
/// mapping table — deterministic, total, and independent of wall-clock time or any other
/// source of non-determinism. Prince's real adapter replaces this on resettable Builder
/// Prince during the Stage 13 personality phase; that is not Companion Awakening, and
/// nothing else in the core depends on which adapter is wired in.
/// </summary>
public sealed class NeutralPersonalityAdapter : IPersonalityAdapter
{
    public const string StartedKey = "lifecycle.started";
    public const string RecoveringKey = "lifecycle.recovering";
    public const string NappingKey = "lifecycle.napping";
    public const string WakingKey = "lifecycle.waking";
    public const string StoppedKey = "lifecycle.stopped";
    public const string UnknownKey = "lifecycle.unknown";

    public PresentationContent Map(LifecycleTransitionResult transition)
    {
        // Deterministic fallback: any invalid transition — an unrecognized event, or a
        // recognized event attempted from an invalid prior state (e.g. Wake when not
        // napping) — renders the same neutral "unknown" content. The raw details behind
        // an invalid transition are diagnostics-only (see CompanionRuntime.Invalid) and
        // never reach this mapping's output.
        if (!transition.IsValid)
        {
            return new PresentationContent(UnknownKey, ExpressionIntent.None);
        }

        return transition.Event switch
        {
            LifecycleEvent.Start when transition.CheckpointRecovered =>
                new PresentationContent(RecoveringKey, ExpressionIntent.Recovering),
            LifecycleEvent.Start =>
                new PresentationContent(StartedKey, ExpressionIntent.None),
            LifecycleEvent.Nap =>
                new PresentationContent(NappingKey, ExpressionIntent.None),
            LifecycleEvent.Wake =>
                new PresentationContent(WakingKey, ExpressionIntent.None),
            LifecycleEvent.Stop =>
                new PresentationContent(StoppedKey, ExpressionIntent.None),
            // Total function: any event value this switch doesn't otherwise name (e.g. a
            // future enum member reaching this old build) still returns a defined,
            // renderable result rather than throwing.
            _ => new PresentationContent(UnknownKey, ExpressionIntent.None)
        };
    }
}
