using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth;

public sealed class TargetPolicyCatalog
{
    private readonly AuthorizationPolicyStore _store;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);
    private readonly object _gate = new();
    private IReadOnlyDictionary<string, AuthorizationPolicyEntry> _entries;

    private TargetPolicyCatalog(
        AuthorizationPolicyStore store,
        AuthorizationPolicyLoadResult loadResult)
    {
        _store = store;
        _entries = loadResult.Entries;
        WasLoadedValidly = loadResult.IsValid;
    }

    public bool WasLoadedValidly { get; }

    public static async Task<TargetPolicyCatalog> OpenDevelopmentAsync(
        CancellationToken cancellationToken = default) =>
        await OpenAsync(
                AuthorizationPolicyLocation.CreateDevelopment(),
                testHook: null,
                cancellationToken)
            .ConfigureAwait(false);

    internal static async Task<TargetPolicyCatalog> OpenTestAsync(
        string testRoot,
        IAuthorizationPolicyTestHook? testHook = null,
        CancellationToken cancellationToken = default) =>
        await OpenAsync(
                AuthorizationPolicyLocation.CreateTest(testRoot),
                testHook,
                cancellationToken)
            .ConfigureAwait(false);

    public TargetPolicy Resolve(TargetCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var filenameCategory = DefaultApplicationClassifier.Classify(
            candidate.Identity.ExecutableFileName);
        if (candidate.ApplicationCategory == ApplicationCategory.Browser
            || filenameCategory == ApplicationCategory.Browser)
        {
            // Ordinary window capture can never make a browser safe; a separately
            // approved tab-specific integration is required before this can change.
            return new TargetPolicy(AuthorizationCategory.Denied, TargetContentPolicy.Standard);
        }

        lock (_gate)
        {
            if (_entries.TryGetValue(
                    candidate.Identity.ExecutablePathFingerprint,
                    out var entry)
                && string.Equals(
                    entry.ExecutableFileName,
                    candidate.Identity.ExecutableFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new TargetPolicy(entry.AuthorizationCategory, entry.ContentPolicy);
            }
        }

        var category = (DefaultApplicationClassifier.IsSensitiveByDefault(candidate.ApplicationCategory)
                || DefaultApplicationClassifier.IsSensitiveByDefault(filenameCategory))
            ? AuthorizationCategory.Denied
            : AuthorizationCategory.UnknownAsk;
        return new TargetPolicy(category, TargetContentPolicy.Standard);
    }

    internal async Task SetExplicitPolicyAsync(
        TargetCandidate candidate,
        TargetPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policy);
        if (!Enum.IsDefined(policy.AuthorizationCategory)
            || !Enum.IsDefined(policy.ContentPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy));
        }

        if ((candidate.ApplicationCategory == ApplicationCategory.Browser
                || DefaultApplicationClassifier.Classify(candidate.Identity.ExecutableFileName)
                    == ApplicationCategory.Browser)
            && (policy.AuthorizationCategory != AuthorizationCategory.Denied
                || policy.ContentPolicy != TargetContentPolicy.Standard))
        {
            throw new InvalidOperationException(
                "Browser windows remain denied until a separately approved tab-specific integration exists.");
        }

        var normalizedPolicy = policy.AuthorizationCategory == AuthorizationCategory.Denied
            ? policy with { ContentPolicy = TargetContentPolicy.Standard }
            : policy;

        await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, AuthorizationPolicyEntry> candidateEntries;
            lock (_gate)
            {
                candidateEntries = new Dictionary<string, AuthorizationPolicyEntry>(
                    _entries,
                    StringComparer.Ordinal);
            }

            candidateEntries[candidate.Identity.ExecutablePathFingerprint] =
                new AuthorizationPolicyEntry(
                    candidate.Identity.ExecutablePathFingerprint,
                    candidate.Identity.ExecutableFileName,
                    normalizedPolicy.AuthorizationCategory,
                    normalizedPolicy.ContentPolicy);

            // Save and validate before changing live in-memory authority. A failed or
            // cancelled save cannot grant standing authorization for this process.
            await _store.SaveAsync(candidateEntries.Values, cancellationToken)
                .ConfigureAwait(false);
            lock (_gate)
            {
                _entries = candidateEntries;
            }
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private static async Task<TargetPolicyCatalog> OpenAsync(
        AuthorizationPolicyLocation location,
        IAuthorizationPolicyTestHook? testHook,
        CancellationToken cancellationToken)
    {
        var store = new AuthorizationPolicyStore(location, testHook);
        var loadResult = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return new TargetPolicyCatalog(store, loadResult);
    }
}
