using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace CompanionCore.TargetAuth;

/// <summary>
/// Versioned, checksummed, canonical and atomically replaced development/test policy
/// storage. Invalid data returns an empty fail-closed result; it never grants standing
/// authorization by partial recovery.
/// </summary>
internal sealed class AuthorizationPolicyStore
{
    private const int FormatVersion = 1;
    private const int MaximumPolicyBytes = 1024 * 1024;
    private const int MaximumEntries = 4096;
    private readonly AuthorizationPolicyLocation _location;
    private readonly IAuthorizationPolicyTestHook? _testHook;

    internal AuthorizationPolicyStore(
        AuthorizationPolicyLocation location,
        IAuthorizationPolicyTestHook? testHook = null)
    {
        _location = location ?? throw new ArgumentNullException(nameof(location));
        _testHook = testHook;
    }

    internal async Task<AuthorizationPolicyLoadResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_location.PolicyPath))
        {
            return ValidEmpty();
        }

        try
        {
            var info = new FileInfo(_location.PolicyPath);
            if (info.Length is <= 0 or > MaximumPolicyBytes)
            {
                return Invalid();
            }

            var bytes = await File.ReadAllBytesAsync(_location.PolicyPath, cancellationToken)
                .ConfigureAwait(false);
            return TryParseCanonical(bytes, out var entries)
                ? new AuthorizationPolicyLoadResult(true, entries)
                : Invalid();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Invalid();
        }
    }

    internal async Task SaveAsync(
        IReadOnlyCollection<AuthorizationPolicyEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var canonical = SerializeCanonical(entries);
        if (canonical.Length > MaximumPolicyBytes)
        {
            throw new InvalidOperationException("The authorization policy exceeds its fixed size bound.");
        }

        Directory.CreateDirectory(_location.RootPath);
        var temporaryPath = Path.Combine(
            _location.RootPath,
            $"target-authorization-v1.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 16 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(canonical, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var candidate = await File.ReadAllBytesAsync(temporaryPath, cancellationToken)
                .ConfigureAwait(false);
            if (!TryParseCanonical(candidate, out _))
            {
                throw new InvalidOperationException("The staged authorization policy failed validation.");
            }

            if (_testHook is not null)
            {
                await _testHook.BeforePromotionAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Promotion is one same-directory replacement. Caller cancellation is no
            // longer observed once this move begins, so the outcome cannot be ambiguous.
            File.Move(temporaryPath, _location.PolicyPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception)
            {
                // A uniquely named unpromoted orphan is non-authoritative. Cleanup must
                // never turn a successful promotion into failure or touch the live file.
            }
        }
    }

    private static bool TryParseCanonical(
        ReadOnlySpan<byte> bytes,
        out IReadOnlyDictionary<string, AuthorizationPolicyEntry> entries)
    {
        entries = new Dictionary<string, AuthorizationPolicyEntry>();
        if (bytes.Length is <= 0 or > MaximumPolicyBytes)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(
                bytes.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 8,
                });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 3
                || !root.TryGetProperty("formatVersion", out var versionElement)
                || versionElement.ValueKind != JsonValueKind.Number
                || !versionElement.TryGetInt32(out var version)
                || version != FormatVersion
                || !root.TryGetProperty("entries", out var entriesElement)
                || entriesElement.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("checksum", out var checksumElement)
                || checksumElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var parsed = new Dictionary<string, AuthorizationPolicyEntry>(StringComparer.Ordinal);
            foreach (var element in entriesElement.EnumerateArray())
            {
                if (parsed.Count >= MaximumEntries
                    || !TryParseEntry(element, out var entry)
                    || !parsed.TryAdd(entry.ExecutablePathFingerprint, entry))
                {
                    return false;
                }
            }

            var checksum = checksumElement.GetString();
            if (checksum is null || checksum.Length != 64)
            {
                return false;
            }

            var canonical = SerializeCanonical(parsed.Values);
            if (!bytes.SequenceEqual(canonical))
            {
                return false;
            }

            entries = parsed;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    private static bool TryParseEntry(JsonElement element, out AuthorizationPolicyEntry entry)
    {
        entry = null!;
        if (element.ValueKind != JsonValueKind.Object
            || element.EnumerateObject().Count() != 4
            || !element.TryGetProperty("executablePathFingerprint", out var fingerprintElement)
            || !element.TryGetProperty("executableFileName", out var fileNameElement)
            || !element.TryGetProperty("authorizationCategory", out var categoryElement)
            || !element.TryGetProperty("contentPolicy", out var contentPolicyElement)
            || fingerprintElement.ValueKind != JsonValueKind.String
            || fileNameElement.ValueKind != JsonValueKind.String
            || categoryElement.ValueKind != JsonValueKind.String
            || contentPolicyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var fingerprint = fingerprintElement.GetString();
        var fileName = fileNameElement.GetString();
        var categoryName = categoryElement.GetString();
        var contentPolicyName = contentPolicyElement.GetString();
        if (fingerprint is not { Length: 64 }
            || fingerprint.Any(character => !Uri.IsHexDigit(character))
            || fingerprint != fingerprint.ToUpperInvariant()
            || string.IsNullOrWhiteSpace(fileName)
            || fileName.Length > 260
            || fileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
            || !Enum.TryParse(categoryName, ignoreCase: false, out AuthorizationCategory category)
            || !Enum.IsDefined(category)
            || !Enum.TryParse(contentPolicyName, ignoreCase: false, out CompanionCore.Privacy.TargetContentPolicy policy)
            || !Enum.IsDefined(policy))
        {
            return false;
        }

        entry = new AuthorizationPolicyEntry(fingerprint, fileName, category, policy);
        return true;
    }

    private static byte[] SerializeCanonical(IEnumerable<AuthorizationPolicyEntry> entries)
    {
        var sorted = entries
            .OrderBy(entry => entry.ExecutablePathFingerprint, StringComparer.Ordinal)
            .ToArray();
        if (sorted.Length > MaximumEntries
            || sorted.Select(entry => entry.ExecutablePathFingerprint).Distinct(StringComparer.Ordinal).Count() != sorted.Length)
        {
            throw new InvalidOperationException("The authorization policy contains too many or duplicate entries.");
        }

        var payloadBuffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(payloadBuffer))
        {
            WritePayload(writer, sorted);
        }

        var checksum = Convert.ToHexString(SHA256.HashData(payloadBuffer.WrittenSpan));
        var fullBuffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(fullBuffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", FormatVersion);
            WriteEntries(writer, sorted);
            writer.WriteString("checksum", checksum);
            writer.WriteEndObject();
        }

        return fullBuffer.WrittenSpan.ToArray();
    }

    private static void WritePayload(Utf8JsonWriter writer, AuthorizationPolicyEntry[] entries)
    {
        writer.WriteStartObject();
        writer.WriteNumber("formatVersion", FormatVersion);
        WriteEntries(writer, entries);
        writer.WriteEndObject();
    }

    private static void WriteEntries(Utf8JsonWriter writer, AuthorizationPolicyEntry[] entries)
    {
        writer.WriteStartArray("entries");
        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("executablePathFingerprint", entry.ExecutablePathFingerprint);
            writer.WriteString("executableFileName", entry.ExecutableFileName);
            writer.WriteString("authorizationCategory", entry.AuthorizationCategory.ToString());
            writer.WriteString("contentPolicy", entry.ContentPolicy.ToString());
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static AuthorizationPolicyLoadResult ValidEmpty() =>
        new(true, new Dictionary<string, AuthorizationPolicyEntry>(StringComparer.Ordinal));

    private static AuthorizationPolicyLoadResult Invalid() =>
        new(false, new Dictionary<string, AuthorizationPolicyEntry>(StringComparer.Ordinal));
}
