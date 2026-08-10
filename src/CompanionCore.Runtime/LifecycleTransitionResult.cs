namespace CompanionCore.Runtime;

/// <summary>
/// The outcome of attempting a lifecycle transition. <see cref="IsValid"/> is false for
/// an attempted transition that violates the state machine's preconditions (e.g. napping
/// before starting); the runtime does not silently coerce those into something else, and
/// does not throw either — the caller (the presentation mapping) decides what an invalid
/// transition displays. <see cref="CheckpointRecovered"/> only ever applies to
/// <see cref="LifecycleEvent.Start"/> and is false for every other event.
/// </summary>
public readonly record struct LifecycleTransitionResult(
    LifecycleEvent Event,
    bool IsValid,
    RuntimeState PriorState,
    RuntimeState ResultingState,
    bool CheckpointRecovered);
