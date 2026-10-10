using System.Text.RegularExpressions;

namespace CompanionCore.Api;

/// <summary>
/// RAM-only, process-lifetime credential store. A new instance is the unconfigured
/// development state. Nothing here is ever written to disk, logged, or serialized.
/// </summary>
public sealed partial class InMemoryCredentialStore : ICredentialStore, IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ProtectedCredential> _credentials = new(StringComparer.Ordinal);
    private bool _disposed;

    public CredentialState GetState(string credentialName)
    {
        ValidateName(credentialName);
        lock (_gate)
        {
            return !_disposed && _credentials.ContainsKey(credentialName)
                ? CredentialState.Configured
                : CredentialState.Unconfigured;
        }
    }

    public void Set(string credentialName, ProtectedCredential credential)
    {
        ValidateName(credentialName);
        ArgumentNullException.ThrowIfNull(credential);
        if (credential.IsDisposed)
        {
            throw new ArgumentException("A disposed credential cannot be stored.", nameof(credential));
        }

        ProtectedCredential? replaced;
        lock (_gate)
        {
            if (_disposed)
            {
                credential.Dispose();
                throw new ObjectDisposedException(nameof(InMemoryCredentialStore));
            }

            _credentials.TryGetValue(credentialName, out replaced);
            _credentials[credentialName] = credential;
        }

        if (!ReferenceEquals(replaced, credential))
        {
            replaced?.Dispose();
        }
    }

    public bool Remove(string credentialName)
    {
        ValidateName(credentialName);
        ProtectedCredential? removed;
        lock (_gate)
        {
            if (!_credentials.Remove(credentialName, out removed))
            {
                return false;
            }
        }

        removed.Dispose();
        return true;
    }

    public bool TryUse<TState, TResult>(
        string credentialName,
        TState state,
        ProtectedCredential.SecretFunc<TState, TResult> use,
        out TResult? result)
    {
        ValidateName(credentialName);
        ArgumentNullException.ThrowIfNull(use);
        lock (_gate)
        {
            if (_disposed || !_credentials.TryGetValue(credentialName, out var credential))
            {
                result = default;
                return false;
            }

            result = credential.Use(state, use);
            return true;
        }
    }

    public bool AnyAppearsIn(string text)
    {
        lock (_gate)
        {
            foreach (var credential in _credentials.Values)
            {
                if (credential.AppearsIn(text))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public override string ToString() => "[credential store]";

    public void Dispose()
    {
        List<ProtectedCredential> toDispose;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            toDispose = [.. _credentials.Values];
            _credentials.Clear();
        }

        foreach (var credential in toDispose)
        {
            credential.Dispose();
        }
    }

    private static void ValidateName(string credentialName)
    {
        if (credentialName is null || !CredentialNamePattern().IsMatch(credentialName))
        {
            throw new ArgumentException("Credential names are short lowercase identifiers.", nameof(credentialName));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex CredentialNamePattern();
}
