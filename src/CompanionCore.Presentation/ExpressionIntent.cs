namespace CompanionCore.Presentation;

/// <summary>
/// Typed expression intents. Task 1's scope only ever emits <see cref="None"/> or
/// <see cref="Recovering"/> — the fuller vocabulary (observing, investigating, urgent,
/// taking_note, privacy_paused, ...) belongs to <c>AttentionEngine</c>/
/// <c>ConversationCoordinator</c>, which do not exist until later tasks.
/// </summary>
public enum ExpressionIntent
{
    None,
    Recovering
}
