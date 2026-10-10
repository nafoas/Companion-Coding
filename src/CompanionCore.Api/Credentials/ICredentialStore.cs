namespace CompanionCore.Api;

/// <summary>
/// Protected credential-storage seam. The development state is unconfigured; a missing
/// credential is a supported state, never a startup failure. Persistent OS-protected
/// storage for real credentials belongs to the final API gate.
/// </summary>
public interface ICredentialStore
{
    CredentialState GetState(string credentialName);

    /// <summary>Stores the credential, taking ownership and disposing any replaced value.</summary>
    void Set(string credentialName, ProtectedCredential credential);

    /// <summary>Removes and zeroes the credential. Returns false when none was configured.</summary>
    bool Remove(string credentialName);

    /// <summary>Scoped borrow for a future live adapter. Returns false when unconfigured.</summary>
    bool TryUse<TState, TResult>(
        string credentialName,
        TState state,
        ProtectedCredential.SecretFunc<TState, TResult> use,
        out TResult? result);

    /// <summary>True when any configured secret appears verbatim in <paramref name="text"/>.</summary>
    bool AnyAppearsIn(string text);
}
