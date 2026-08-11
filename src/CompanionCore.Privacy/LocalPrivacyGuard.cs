namespace CompanionCore.Privacy;

/// <summary>
/// The local content-policy boundary. Trusted-game mode may bypass content filtering
/// only; target authorization and generation checks remain separate mandatory gates.
/// </summary>
public sealed class LocalPrivacyGuard
{
    public PrivacyGuardDecision Evaluate(
        TargetContentPolicy contentPolicy,
        PrivacyAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        if (contentPolicy == TargetContentPolicy.TrustedGame)
        {
            return PrivacyGuardDecision.TrustedGameBypass;
        }

        if (contentPolicy != TargetContentPolicy.Standard)
        {
            return PrivacyGuardDecision.RejectedUnavailable;
        }

        return assessment.Kind switch
        {
            PrivacyAssessmentKind.Clear => PrivacyGuardDecision.Allowed,
            PrivacyAssessmentKind.ClearlySensitive => PrivacyGuardDecision.RejectedSensitive,
            PrivacyAssessmentKind.Unavailable => PrivacyGuardDecision.RejectedUnavailable,
            _ => PrivacyGuardDecision.RejectedUnavailable
        };
    }
}
