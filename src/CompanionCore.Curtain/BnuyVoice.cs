namespace CompanionCore.Curtain;

/// <summary>Where Prince's words come from for one Bnuy request. Stage 13 provides his real voice.</summary>
public interface IBnuyVoiceSource
{
    Task<string?> SpeakAsync(BnuyRequest request, CancellationToken cancellationToken);
}

/// <summary>What came of one request: the line he says, or nothing.</summary>
public sealed record VoiceOutcome(string? Line, int Rejected);

/// <summary>
/// The only path for Prince's words to come out. A line that leaks behind the curtain is
/// rejected and asked for again, a few times at most; if every attempt leaks, he simply says
/// nothing. Rejected words are never returned, shown, or stored, and never reach his state:
/// the regenerated line is simply what he said. Only Boss's diagnostics learn of a rejection,
/// by kind, never by its words.
/// </summary>
public sealed class BnuyVoice
{
    public const int MaximumAttempts = 3;

    private readonly IBnuyVoiceSource _source;
    private readonly ICharacterValidator _validator;
    private readonly Action<CurtainLeakKind>? _diagnostics;

    public BnuyVoice(IBnuyVoiceSource source, ICharacterValidator? validator = null, Action<CurtainLeakKind>? diagnostics = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _validator = validator ?? CurtainValidator.Instance;
        _diagnostics = diagnostics;
    }

    public async Task<VoiceOutcome> SpeakAsync(BnuyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rejected = 0;
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var line = await _source.SpeakAsync(request, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                return new VoiceOutcome(null, rejected);
            }

            var verdict = _validator.Check(line);
            if (verdict.Accepted)
            {
                return new VoiceOutcome(line, rejected);
            }

            rejected++;
            foreach (var leak in verdict.Leaks)
            {
                _diagnostics?.Invoke(leak.Kind);
            }
        }

        return new VoiceOutcome(null, rejected);
    }
}
