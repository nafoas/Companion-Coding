namespace CompanionCore.Presentation;

/// <summary>
/// Typed expression intents available in the neutral core. Task 4 adds only the
/// privacy-paused state; the fuller observing/conversation vocabulary belongs to
/// later subsystems that do not exist yet.
/// </summary>
public enum ExpressionIntent
{
    None,
    Recovering,
    PrivacyPaused
}
