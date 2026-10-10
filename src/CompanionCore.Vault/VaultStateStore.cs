using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CompanionCore.Memory;

namespace CompanionCore.Vault;

/// <summary>
/// Validated state directory: a sibling <c>State</c> directory of an already validated
/// development or test memory location. No raw-path constructor exists.
/// </summary>
public sealed class VaultStateLocation
{
    internal const string DirectoryName = "State";

    private VaultStateLocation(DataRootKind kind, string rootPath)
    {
        Kind = kind;
        RootPath = rootPath;
    }

    public DataRootKind Kind { get; }

    public string RootPath { get; }

    internal string DamagedDirectoryPath => Path.Combine(RootPath, "damaged-v1");

    public static VaultStateLocation For(MemoryStoreLocation memoryLocation)
    {
        ArgumentNullException.ThrowIfNull(memoryLocation);
        var expectedNamespace = memoryLocation.Kind switch
        {
            DataRootKind.Development => DevelopmentDataRootPolicy.DevelopmentApplicationNamespace,
            DataRootKind.Test => TestDataRootPolicy.TestApplicationNamespace,
            _ => null,
        };

        if (expectedNamespace is null
            || !string.Equals(memoryLocation.ApplicationNamespace, expectedNamespace, StringComparison.Ordinal))
        {
            throw new DataRootViolationException("Vault state requires a validated development or test memory location.");
        }

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(memoryLocation.RootPath))
            ?? throw new DataRootViolationException("The memory location has no parent directory.");
        return new VaultStateLocation(memoryLocation.Kind, Path.Combine(parent, DirectoryName));
    }
}

public enum StateStatus
{
    Verified = 1,
    Missing = 2,
    Damaged = 3,
}

public sealed record StateRead(StateStatus Status, ReadOnlyMemory<byte> Payload);

/// <summary>
/// Named, checksummed, atomically written state entries, such as settings and the active
/// checkpoint. Each file is <c>companion-state-v1\n&lt;sha256&gt;\n&lt;payload&gt;</c>, so damage is
/// always detected.
/// </summary>
public sealed partial class VaultStateStore
{
    public const int MaximumEntries = 64;
    public const int MaximumPayloadBytes = 1024 * 1024;
    internal const string Extension = ".state";
    private const string Header = "companion-state-v1\n";

    private readonly SemaphoreSlim _gate = new(1, 1);

    public VaultStateStore(VaultStateLocation location)
    {
        Location = location ?? throw new ArgumentNullException(nameof(location));
    }

    public VaultStateLocation Location { get; }

    public async Task PutAsync(string name, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        RequireName(name);
        if (payload.Length > MaximumPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "State payloads are bounded.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Names().Contains(name, StringComparer.Ordinal) && Names().Count >= MaximumEntries)
            {
                throw new InvalidOperationException("The state store is full.");
            }

            await WriteEnvelopeAsync(name, Encode(payload.Span), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<StateRead> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        RequireName(name);
        var path = PathFor(name);
        if (!File.Exists(path))
        {
            return new StateRead(StateStatus.Missing, ReadOnlyMemory<byte>.Empty);
        }

        if (new FileInfo(path).Length > Header.Length + 65 + MaximumPayloadBytes)
        {
            return new StateRead(StateStatus.Damaged, ReadOnlyMemory<byte>.Empty);
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return TryDecode(bytes, out var payload)
            ? new StateRead(StateStatus.Verified, payload)
            : new StateRead(StateStatus.Damaged, ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>Canonical entry names present on disk, in ordinal order.</summary>
    public IReadOnlyList<string> Names()
    {
        if (!Directory.Exists(Location.RootPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(Location.RootPath, "*" + Extension)
            .Select(Path.GetFileName)
            .Select(file => file![..^Extension.Length])
            .Where(ValidName)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    internal static bool ValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    internal string PathFor(string name) => Path.Combine(Location.RootPath, name + Extension);

    internal static byte[] Encode(ReadOnlySpan<byte> payload)
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(payload));
        var prefix = Encoding.ASCII.GetBytes($"{Header}{digest}\n");
        var envelope = new byte[prefix.Length + payload.Length];
        prefix.CopyTo(envelope, 0);
        payload.CopyTo(envelope.AsSpan(prefix.Length));
        return envelope;
    }

    internal static bool TryDecode(ReadOnlySpan<byte> envelope, out byte[] payload)
    {
        payload = [];
        var prefixLength = Header.Length + 65;
        if (envelope.Length < prefixLength
            || envelope.Length - prefixLength > MaximumPayloadBytes
            || !envelope[..Header.Length].SequenceEqual(Encoding.ASCII.GetBytes(Header))
            || envelope[prefixLength - 1] != (byte)'\n')
        {
            return false;
        }

        var stored = envelope.Slice(Header.Length, 64);
        var body = envelope[prefixLength..];
        var actual = Encoding.ASCII.GetBytes(Convert.ToHexStringLower(SHA256.HashData(body)));
        if (!CryptographicOperations.FixedTimeEquals(stored, actual))
        {
            return false;
        }

        payload = body.ToArray();
        return true;
    }

    /// <summary>Atomic replace: temp, flush, rename over the target.</summary>
    internal async Task WriteEnvelopeAsync(string name, byte[] envelope, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Location.RootPath);
        var temporary = Path.Combine(Location.RootPath, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, PathFor(name), overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static void RequireName(string? name)
    {
        if (!ValidName(name))
        {
            throw new ArgumentException("State names are short lowercase identifiers.", nameof(name));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
