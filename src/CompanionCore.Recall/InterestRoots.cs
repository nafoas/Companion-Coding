using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CompanionCore.Memory;

namespace CompanionCore.Recall;

public sealed record InterestRoot(string RootId, string Description);

/// <summary>
/// Immutable, versioned, checksummed local interest roots. There is no mutation API, and a
/// tampered definition fails its fingerprint. Generated seeds never change a root.
/// </summary>
public sealed partial class InterestRootSet
{
    public const int Version = 1;
    public const int MaximumRoots = 256;

    private readonly Dictionary<string, InterestRoot> _byId;

    private InterestRootSet(IReadOnlyList<InterestRoot> roots)
    {
        Roots = roots;
        _byId = roots.ToDictionary(root => root.RootId, StringComparer.Ordinal);
        Fingerprint = ComputeFingerprint(roots);
    }

    public IReadOnlyList<InterestRoot> Roots { get; }

    public string Fingerprint { get; }

    public bool Contains(string rootId) => rootId is not null && _byId.ContainsKey(rootId);

    public static InterestRootSet Create(IEnumerable<InterestRoot> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var list = roots.Select(root => root ?? throw new ArgumentException("A root is null.")).ToArray();
        if (list.Length is < 1 or > MaximumRoots
            || list.Any(root => !RootIdPattern().IsMatch(root.RootId ?? string.Empty) || !ValidText(root.Description, 512))
            || list.Select(root => root.RootId).Distinct(StringComparer.Ordinal).Count() != list.Length)
        {
            throw new ArgumentException("Interest roots must be a bounded set of unique, valid roots.");
        }

        return new InterestRootSet(Array.AsReadOnly(list.OrderBy(root => root.RootId, StringComparer.Ordinal).ToArray()));
    }

    /// <summary>Loads a local definition; its fingerprint is required and verified.</summary>
    public static InterestRootSet Load(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        Definition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<Definition>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
            });
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The interest-root definition is invalid JSON.", exception);
        }

        if (definition is null || definition.Version != Version || definition.Roots is null)
        {
            throw new ArgumentException("The interest-root definition has an unsupported version.");
        }

        var set = Create(definition.Roots);
        if (definition.Fingerprint is null || !string.Equals(definition.Fingerprint, set.Fingerprint, StringComparison.Ordinal))
        {
            throw new ArgumentException("The interest-root definition failed its fingerprint.");
        }

        return set;
    }

    public string ToJson() =>
        JsonSerializer.Serialize(
            new Definition { Version = Version, Roots = [.. Roots], Fingerprint = Fingerprint },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    internal static bool ValidText(string? text, int maximum) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= maximum && !text.Any(character => char.IsControl(character) && character is not '\n' and not '\t');

    private static string ComputeFingerprint(IReadOnlyList<InterestRoot> roots)
    {
        // JSON framing keeps the canonical form unambiguous even when descriptions hold tabs or newlines.
        var canonical = JsonSerializer.Serialize(new object[] { Version, roots.Select(root => new[] { root.RootId, root.Description }).ToArray() });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex RootIdPattern();

    private sealed class Definition
    {
        public int Version { get; set; }

        public List<InterestRoot>? Roots { get; set; }

        public string? Fingerprint { get; set; }
    }
}

/// <summary>Validated storage for generated initiated-conversation seeds; roots stay untouched.</summary>
public static class GeneratedSeedPlanner
{
    public const int MaximumSeedCharacters = 1000;

    public static AppendMemoryProposal Plan(
        InterestRootSet roots,
        string rootId,
        string seedText,
        Guid operationId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (!roots.Contains(rootId))
        {
            throw new ArgumentException("A generated seed must belong to an existing local root.", nameof(rootId));
        }

        if (!InterestRootSet.ValidText(seedText, MaximumSeedCharacters))
        {
            throw new ArgumentException("The generated seed text is invalid.", nameof(seedText));
        }

        return new AppendMemoryProposal(operationId,
        [
            new MemoryRecordDraft
            {
                RecordId = ConsolidationPlanner.DeriveRecordId(operationId, 0),
                CreatedAtUtc = now.ToUniversalTime(),
                Scope = MemoryScope.General,
                SourceKind = MemorySourceKind.Inferred,
                Confidence = 0.6,
                SubjectKey = RecallSubjects.Seed(rootId),
                VisibleRecollection = seedText,
                RetrievalMetadataJson = new RecallMetadata { Kind = RecallRecordKind.Seed, RootId = rootId, RootsFingerprint = roots.Fingerprint }.ToJson(),
            },
        ]);
    }
}
