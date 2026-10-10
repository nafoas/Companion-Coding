namespace CompanionCore.Api;

/// <summary>
/// Contract-complete shell for the one primary remote provider. It has no transport and
/// references no networking assembly, so no live or paid call is possible before the
/// final API gate deliberately adds one. Without a credential it is neutrally
/// unavailable; with one, live calls are still disabled.
/// </summary>
public sealed class RealSemanticProviderShell : ISemanticProvider
{
    public const string PrimaryCredentialName = "braincase.primary";

    private readonly ICredentialStore _credentials;

    public RealSemanticProviderShell(ICredentialStore credentials)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
    }

    public string ProviderName => "primary";

    public Task<ProviderReply> InterpretAsync(SemanticRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var reason = _credentials.GetState(PrimaryCredentialName) == CredentialState.Configured
            ? ProviderUnavailableReason.LiveCallsDisabled
            : ProviderUnavailableReason.CredentialsMissing;
        return Task.FromResult(ProviderReply.Unavailable(reason));
    }
}

/// <summary>Neutral provider used when the Braincase is disabled or misconfigured.</summary>
public sealed class DisabledSemanticProvider : ISemanticProvider
{
    public DisabledSemanticProvider(ProviderUnavailableReason reason = ProviderUnavailableReason.Disabled)
    {
        if (reason == ProviderUnavailableReason.None || !Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Reason = reason;
    }

    public ProviderUnavailableReason Reason { get; }

    public string ProviderName => "disabled";

    public Task<ProviderReply> InterpretAsync(SemanticRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(ProviderReply.Unavailable(Reason));
    }
}
