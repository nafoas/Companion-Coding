using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CompanionCore.Capture.Contracts;

namespace CompanionCore.Api;

public sealed class ReplayFixtureException : Exception
{
    public ReplayFixtureException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Replays sanitized recorded request/response fixtures through the same contract. A
/// fixture is keyed by request shape (operation kind, sheet kind, region kinds, packet
/// subjects) and holds an ordered reply script; the last reply repeats. Success replies
/// are validated by the shared strict parser at load time and receive the live
/// operation ID at replay time. Fixtures never contain image bytes.
/// </summary>
public sealed class ReplaySemanticProvider : ISemanticProvider
{
    public const string FixtureSearchPattern = "*.replay.json";
    internal const int MaximumFixtureFiles = 256;
    internal const int MaximumFixtureBytes = 256 * 1024;
    internal const int MaximumRepliesPerFixture = 16;

    private readonly object _gate = new();
    private readonly Dictionary<string, Fixture> _fixtures;

    private ReplaySemanticProvider(Dictionary<string, Fixture> fixtures)
    {
        _fixtures = fixtures;
    }

    public string ProviderName => "replay";

    public int FixtureCount => _fixtures.Count;

    public static ReplaySemanticProvider Load(string fixtureDirectory)
    {
        if (string.IsNullOrWhiteSpace(fixtureDirectory) || !Path.IsPathFullyQualified(fixtureDirectory))
        {
            throw new ReplayFixtureException("The replay fixture directory must be an absolute path.");
        }

        if (!Directory.Exists(fixtureDirectory))
        {
            throw new ReplayFixtureException("The replay fixture directory does not exist.");
        }

        var files = Directory.EnumerateFiles(fixtureDirectory, FixtureSearchPattern, SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Take(MaximumFixtureFiles + 1)
            .ToArray();
        if (files.Length == 0 || files.Length > MaximumFixtureFiles)
        {
            throw new ReplayFixtureException("The replay fixture directory must hold between 1 and 256 fixtures.");
        }

        var fixtures = new Dictionary<string, Fixture>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var fixture = LoadFixture(file);
            if (!fixtures.TryAdd(fixture.Key, fixture))
            {
                throw new ReplayFixtureException("Two replay fixtures share one request shape.");
            }
        }

        return new ReplaySemanticProvider(fixtures);
    }

    public Task<ProviderReply> InterpretAsync(SemanticRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var key = Key(
            request.Kind,
            request.Sheet.Kind,
            request.Sheet.RegionKinds,
            request.ResumePacket.Subjects);
        if (!_fixtures.TryGetValue(key, out var fixture))
        {
            return Task.FromResult(ProviderReply.Unavailable(ProviderUnavailableReason.NoMatchingFixture));
        }

        ReplayReplyWire reply;
        lock (_gate)
        {
            reply = fixture.Replies[Math.Min(fixture.Cursor, fixture.Replies.Count - 1)];
            fixture.Cursor++;
        }

        return Task.FromResult(reply.Kind switch
        {
            ReplayReplyKind.Success => ProviderReply.Success(WithOperationId(reply.Response!.Value, request.OperationId)),
            ReplayReplyKind.Transient => ProviderReply.Transient(),
            ReplayReplyKind.Outage => ProviderReply.Outage(),
            ReplayReplyKind.RateLimited => ProviderReply.RateLimited(
                reply.RetryAfterSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null),
            _ => ProviderReply.Unavailable(reply.UnavailableReason!.Value),
        });
    }

    private static Fixture LoadFixture(string file)
    {
        var info = new FileInfo(file);
        if (info.Length is <= 0 or > MaximumFixtureBytes)
        {
            throw new ReplayFixtureException("A replay fixture is empty or exceeds its size bound.");
        }

        ReplayFixtureWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<ReplayFixtureWire>(File.ReadAllBytes(file), SemanticSchema.StrictOptions);
        }
        catch (JsonException exception)
        {
            throw new ReplayFixtureException("A replay fixture is not valid fixture JSON.", exception);
        }

        if (wire is null
            || wire.FixtureVersion != 1
            || string.IsNullOrWhiteSpace(wire.Name)
            || wire.Match is null
            || wire.Match.RegionKinds is not { Count: > 0 }
            || wire.Match.Subjects is null
            || wire.Replies is not { Count: > 0 and <= MaximumRepliesPerFixture })
        {
            throw new ReplayFixtureException("A replay fixture is missing required content.");
        }

        foreach (var reply in wire.Replies)
        {
            ValidateReply(reply, wire.Match.RegionKinds);
        }

        return new Fixture(
            Key(wire.Match.OperationKind, wire.Match.SheetKind, wire.Match.RegionKinds, wire.Match.Subjects),
            wire.Replies.AsReadOnly());
    }

    private static void ValidateReply(ReplayReplyWire reply, IReadOnlyCollection<AttentionRegionKind> regions)
    {
        var valid = reply is not null && reply.Kind switch
        {
            ReplayReplyKind.Success =>
                reply.Response is { ValueKind: JsonValueKind.Object } response
                && reply.RetryAfterSeconds is null
                && reply.UnavailableReason is null
                && SemanticResponseParser.TryParse(response.GetRawText(), Guid.Empty, regions, out _, out _),
            ReplayReplyKind.RateLimited =>
                reply.Response is null
                && reply.UnavailableReason is null
                && reply.RetryAfterSeconds is null or (>= 1 and <= 86_400),
            ReplayReplyKind.Unavailable =>
                reply.Response is null
                && reply.RetryAfterSeconds is null
                && reply.UnavailableReason is { } reason
                && reason != ProviderUnavailableReason.None
                && Enum.IsDefined(reason),
            ReplayReplyKind.Transient or ReplayReplyKind.Outage =>
                reply.Response is null && reply.RetryAfterSeconds is null && reply.UnavailableReason is null,
            _ => false,
        };

        if (!valid)
        {
            throw new ReplayFixtureException("A replay fixture reply is invalid for its kind.");
        }
    }

    private static string WithOperationId(JsonElement response, Guid operationId)
    {
        var node = JsonNode.Parse(response.GetRawText())!.AsObject();
        node["operationId"] = operationId.ToString("D");
        return node.ToJsonString();
    }

    private static string Key(
        SemanticOperationKind operationKind,
        AttentionSheetKind sheetKind,
        IEnumerable<AttentionRegionKind> regions,
        IEnumerable<string> subjects) =>
        string.Join(
            '\u001e',
            operationKind.ToString(),
            sheetKind.ToString(),
            string.Join(',', regions),
            string.Join('\u001f', subjects.Order(StringComparer.Ordinal)));

    private sealed class Fixture(string key, IReadOnlyList<ReplayReplyWire> replies)
    {
        public string Key { get; } = key;

        public IReadOnlyList<ReplayReplyWire> Replies { get; } = replies;

        public int Cursor { get; set; }
    }
}

internal enum ReplayReplyKind
{
    Success = 1,
    Transient = 2,
    Outage = 3,
    RateLimited = 4,
    Unavailable = 5,
}

internal sealed class ReplayFixtureWire
{
    [JsonRequired]
    public int FixtureVersion { get; set; }

    [JsonRequired]
    public string Name { get; set; } = null!;

    [JsonRequired]
    public ReplayMatchWire Match { get; set; } = null!;

    [JsonRequired]
    public List<ReplayReplyWire> Replies { get; set; } = null!;
}

internal sealed class ReplayMatchWire
{
    [JsonRequired]
    public SemanticOperationKind OperationKind { get; set; }

    [JsonRequired]
    public AttentionSheetKind SheetKind { get; set; }

    [JsonRequired]
    public List<AttentionRegionKind> RegionKinds { get; set; } = null!;

    [JsonRequired]
    public List<string> Subjects { get; set; } = null!;
}

internal sealed class ReplayReplyWire
{
    [JsonRequired]
    public ReplayReplyKind Kind { get; set; }

    public JsonElement? Response { get; set; }

    public int? RetryAfterSeconds { get; set; }

    public ProviderUnavailableReason? UnavailableReason { get; set; }
}
