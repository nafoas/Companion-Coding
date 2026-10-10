namespace CompanionCore.Transcript;

/// <summary>Per-session bounds. Provisional until Stage 11 profiling.</summary>
public sealed record TranscriptOptions
{
    public int MaximumEvents { get; init; } = 20_000;

    public long MaximumBytes { get; init; } = 8 * 1024 * 1024;

    public int MaximumTextCharacters { get; init; } = 4_000;

    public int MaximumTopicCharacters { get; init; } = 256;

    internal void Validate()
    {
        if (MaximumEvents is < 4 or > 1_000_000
            || MaximumBytes is < 4096 or > 256L * 1024 * 1024
            || MaximumTextCharacters is < 1 or > 64_000
            || MaximumTopicCharacters is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(TranscriptOptions), "Transcript bounds are invalid.");
        }
    }
}
