namespace CompanionCore.Runtime;

/// <summary>
/// The four lifecycle events Task 1 must handle deterministically, matching the
/// architecture's §6.2.1 normative mapping.
/// </summary>
public enum LifecycleEvent
{
    Start,
    Nap,
    Wake,
    Stop
}
