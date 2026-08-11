namespace CompanionCore.Privacy;

/// <summary>
/// Policy input from a future local pixel classifier. Task 4 intentionally supplies
/// only synthetic assessments; it establishes the admission boundary without reading
/// or classifying real pixels.
/// </summary>
public sealed record PrivacyAssessment
{
    private PrivacyAssessment(PrivacyAssessmentKind kind, SensitiveContentKind sensitiveKind)
    {
        Kind = kind;
        SensitiveKind = sensitiveKind;
    }

    public PrivacyAssessmentKind Kind { get; }

    public SensitiveContentKind SensitiveKind { get; }

    public static PrivacyAssessment Clear { get; } =
        new(PrivacyAssessmentKind.Clear, SensitiveContentKind.None);

    public static PrivacyAssessment Unavailable { get; } =
        new(PrivacyAssessmentKind.Unavailable, SensitiveContentKind.None);

    public static PrivacyAssessment ClearlySensitive(SensitiveContentKind kind)
    {
        if (kind == SensitiveContentKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new PrivacyAssessment(PrivacyAssessmentKind.ClearlySensitive, kind);
    }
}
