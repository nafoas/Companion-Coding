namespace CompanionCore.Api;

public enum ProviderFailureKind
{
    None = 0,
    Transient = 1,
    Outage = 2,
    RateLimited = 3,
    Unavailable = 4,
}

public enum ProviderUnavailableReason
{
    None = 0,
    Disabled = 1,
    CredentialsMissing = 2,
    LiveCallsDisabled = 3,
    FixturesUnavailable = 4,
    NoMatchingFixture = 5,

    /// <summary>The provider refused the configured credential (unauthorized or forbidden).</summary>
    CredentialsRejected = 6,

    /// <summary>The provider refused the request itself; retrying the same request cannot help.</summary>
    RequestRejected = 7,
}

/// <summary>
/// A provider's answer: response JSON for the shared strict parser, or a typed failure.
/// Providers never return free-form error text; nothing remote reaches diagnostics.
/// </summary>
public sealed record ProviderReply
{
    private ProviderReply(
        string? responseJson,
        ProviderFailureKind failureKind,
        ProviderUnavailableReason unavailableReason,
        TimeSpan? retryAfter)
    {
        ResponseJson = responseJson;
        FailureKind = failureKind;
        UnavailableReason = unavailableReason;
        RetryAfter = retryAfter;
    }

    public bool IsSuccess => ResponseJson is not null;

    public string? ResponseJson { get; }

    public ProviderFailureKind FailureKind { get; }

    public ProviderUnavailableReason UnavailableReason { get; }

    public TimeSpan? RetryAfter { get; }

    public static ProviderReply Success(string responseJson)
    {
        ArgumentNullException.ThrowIfNull(responseJson);
        return new(responseJson, ProviderFailureKind.None, ProviderUnavailableReason.None, null);
    }

    public static ProviderReply Transient() =>
        new(null, ProviderFailureKind.Transient, ProviderUnavailableReason.None, null);

    public static ProviderReply Outage() =>
        new(null, ProviderFailureKind.Outage, ProviderUnavailableReason.None, null);

    public static ProviderReply RateLimited(TimeSpan? retryAfter = null) =>
        new(null, ProviderFailureKind.RateLimited, ProviderUnavailableReason.None, retryAfter);

    public static ProviderReply Unavailable(ProviderUnavailableReason reason)
    {
        if (reason == ProviderUnavailableReason.None || !Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        return new(null, ProviderFailureKind.Unavailable, reason, null);
    }
}
