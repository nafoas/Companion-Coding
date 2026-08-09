namespace CompanionCore.Runtime;

/// <summary>
/// The resting states a <see cref="CompanionRuntime"/> can occupy. This is deliberately
/// narrow for Task 1: no attention, conversation, or capture states exist yet.
/// </summary>
public enum RuntimeState
{
    NotStarted,
    Running,
    Napping,
    Stopped
}
