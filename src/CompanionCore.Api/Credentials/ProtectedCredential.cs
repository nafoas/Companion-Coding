using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace CompanionCore.Api;

/// <summary>
/// One RAM-only secret. The value is held masked in pinned buffers, is exposed only to a
/// scoped callback through a zeroed temporary buffer, and is zeroed on disposal. The type
/// has no value-bearing property, so formatting, serialization, and diagnostics see only
/// a redaction marker.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public sealed class ProtectedCredential : IDisposable
{
    public const int MaximumCharacters = 4096;

    private const string Redacted = "[redacted credential]";

    private readonly object _gate = new();
    private readonly byte[] _masked;
    private readonly byte[] _pad;
    private bool _disposed;

    private ProtectedCredential(byte[] masked, byte[] pad)
    {
        _masked = masked;
        _pad = pad;
    }

    public delegate TResult SecretFunc<in TState, out TResult>(ReadOnlySpan<byte> secretUtf8, TState state);

    public bool IsDisposed
    {
        get
        {
            lock (_gate)
            {
                return _disposed;
            }
        }
    }

    public static ProtectedCredential FromCharacters(ReadOnlySpan<char> secret)
    {
        if (secret.IsEmpty || secret.Length > MaximumCharacters || secret.IsWhiteSpace())
        {
            // The message deliberately never echoes any part of the rejected value.
            throw new ArgumentException("A credential must be non-blank and bounded.", nameof(secret));
        }

        var byteCount = Encoding.UTF8.GetByteCount(secret);
        var masked = GC.AllocateArray<byte>(byteCount, pinned: true);
        var pad = GC.AllocateArray<byte>(byteCount, pinned: true);
        RandomNumberGenerator.Fill(pad);
        Encoding.UTF8.GetBytes(secret, masked);
        for (var index = 0; index < masked.Length; index++)
        {
            masked[index] ^= pad[index];
        }

        return new ProtectedCredential(masked, pad);
    }

    /// <summary>
    /// Exposes the secret only for the duration of <paramref name="use"/>. The clear
    /// buffer is pinned and zeroed before this method returns or throws.
    /// </summary>
    public TResult Use<TState, TResult>(TState state, SecretFunc<TState, TResult> use)
    {
        ArgumentNullException.ThrowIfNull(use);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var clear = GC.AllocateArray<byte>(_masked.Length, pinned: true);
            try
            {
                for (var index = 0; index < clear.Length; index++)
                {
                    clear[index] = (byte)(_masked[index] ^ _pad[index]);
                }

                return use(clear, state);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(clear);
            }
        }
    }

    /// <summary>
    /// True when the exact UTF-8 secret occurs anywhere in <paramref name="text"/>. Used to
    /// keep a secret echoed by remote output out of memory, output, logs, and the journal.
    /// </summary>
    internal bool AppearsIn(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var haystack = Encoding.UTF8.GetBytes(text);
        try
        {
            return Use(haystack, static (secret, candidate) => candidate.AsSpan().IndexOf(secret) >= 0);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(haystack);
        }
    }

    public override string ToString() => Redacted;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CryptographicOperations.ZeroMemory(_masked);
            CryptographicOperations.ZeroMemory(_pad);
        }
    }
}
