using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using CompanionCore.Capture.Contracts;
using CompanionCore.Memory;
using CompanionCore.Privacy;
using CompanionCore.Runtime.Diagnostics;

namespace CompanionCore.Api.Tests;

/// <summary>
/// One isolated synthetic test root: a test memory repository, its sibling bridge state,
/// a privacy state, a RAM-only credential store, a settable clock, and recorded logs.
/// Nothing here can resolve a development or production root.
/// </summary>
internal sealed class ApiTestHarness : IAsyncDisposable
{
    internal static readonly DateTimeOffset BaselineUtc = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly List<ApiBridge> _bridges = [];

    private ApiTestHarness(string basePath, MemoryStoreLocation location)
    {
        BasePath = basePath;
        Location = location;
        StateLocation = BraincaseStateLocation.For(location);
    }

    internal string BasePath { get; }

    internal MemoryStoreLocation Location { get; }

    internal BraincaseStateLocation StateLocation { get; }

    internal RuntimePrivacyState Privacy { get; } = new();

    internal InMemoryCredentialStore Credentials { get; } = new();

    internal ManualClock Clock { get; } = new(BaselineUtc);

    internal RecordingDiagnosticsSink Diagnostics { get; } = new();

    internal MemoryRepository Repository { get; private set; } = null!;

    internal static readonly BridgeOptions FastOptions = new()
    {
        InitialBackoff = TimeSpan.FromMilliseconds(1),
        MaximumBackoff = TimeSpan.FromMilliseconds(4),
    };

    internal static async Task<ApiTestHarness> CreateAsync()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "CompanionCore.Api.Tests", Guid.NewGuid().ToString("N"));
        var harness = new ApiTestHarness(basePath, TestDataRootPolicy.Create(basePath, Guid.NewGuid()));
        harness.Repository = await MemoryRepository.OpenAsync(harness.Location, harness.Privacy);
        return harness;
    }

    internal ApiBridge OpenBridge(ISemanticProvider provider, BridgeOptions? options = null)
    {
        var bridge = ApiBridge.Open(
            provider,
            Repository.WriteGate,
            new MemoryRepositoryReader(Repository),
            Privacy,
            StateLocation,
            Credentials,
            options ?? FastOptions,
            Clock,
            Diagnostics);
        _bridges.Add(bridge);
        return bridge;
    }

    internal async Task CloseBridgesAsync()
    {
        foreach (var bridge in _bridges)
        {
            await bridge.DisposeAsync();
        }

        _bridges.Clear();
    }

    internal async Task ReopenRepositoryAsync()
    {
        await Repository.DisposeAsync();
        Repository = await MemoryRepository.OpenAsync(Location, Privacy);
    }

    internal CaptureAuthorizationGrant Grant(long? generation = null, Guid? sessionId = null) =>
        CaptureAuthorizationGrant.Issue(
            sessionId ?? Guid.NewGuid(),
            generation ?? Privacy.Snapshot.Generation,
            new CaptureTargetIdentity(0x1234, 4321, "synthetic-game.exe", new string('A', 64)));

    internal static AttentionSheet Sheet(
        CaptureAuthorizationGrant grant,
        AttentionSheetKind kind = AttentionSheetKind.Orientation,
        byte fill = 0x5A)
    {
        var bytes = new byte[64];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        bytes.AsSpan(8).Fill(fill);
        var full = new AttentionSheetRegionMetadata
        {
            Kind = AttentionRegionKind.FullContext,
            NormalizedSource = new NormalizedRegion(0, 0, 1, 1),
            SourcePixels = new PixelRect(0, 0, 64, 64),

            // A photograph's one region covers its whole sheet.
            SheetPixels = kind == AttentionSheetKind.Photograph ? new PixelRect(0, 0, 64, 32) : new PixelRect(0, 0, 32, 32),
        };
        var center = new AttentionSheetRegionMetadata
        {
            Kind = AttentionRegionKind.CenterEnvironment,
            NormalizedSource = new NormalizedRegion(0.25, 0.25, 0.5, 0.5),
            SourcePixels = new PixelRect(16, 16, 32, 32),
            SheetPixels = new PixelRect(32, 0, 32, 32),
        };
        var metadata = new AttentionSheetMetadata
        {
            TargetSessionId = grant.TargetSessionId,
            Generation = grant.Generation,
            Target = grant.Target,
            SourceSequenceNumber = 1,
            SourceTimestamp = BaselineUtc,
            SourceWidth = 64,
            SourceHeight = 64,
            SheetWidth = 64,
            SheetHeight = 32,
            EncodedByteLength = bytes.Length,
            Kind = kind,
            ChangeScore = 0.5,
            Regions = kind == AttentionSheetKind.Regional ? [full, center] : [full],
        };
        return new AttentionSheet(metadata, bytes);
    }

    internal Task<IReadOnlyList<RetrievedMemory>> RetrieveAsync(string subject) =>
        Repository.RetrieveBySubjectAsync(subject);

    internal async Task<Guid> SeedMemoryAsync(string subject, string recollection)
    {
        var recordId = Guid.NewGuid();
        var result = await Repository.WriteGate.SubmitAsync(new AppendMemoryProposal(
            Guid.NewGuid(),
            [
                new MemoryRecordDraft
                {
                    RecordId = recordId,
                    CreatedAtUtc = BaselineUtc,
                    Scope = MemoryScope.General,
                    SourceKind = MemorySourceKind.Observed,
                    Confidence = 0.8,
                    SubjectKey = subject,
                    VisibleRecollection = recollection,
                },
            ]));
        Assert.Equal(WriteGateStatus.Committed, result.Status);
        return recordId;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseBridgesAsync();
        await Repository.DisposeAsync();
        Credentials.Dispose();
        if (Directory.Exists(BasePath))
        {
            Directory.Delete(BasePath, recursive: true);
        }
    }
}

internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private long _ticks = start.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    internal void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}

internal sealed class RecordingDiagnosticsSink : IDiagnosticsSink
{
    private readonly ConcurrentQueue<string> _lines = new();

    internal IReadOnlyList<string> Lines => [.. _lines];

    public void Log(string category, string message) => _lines.Enqueue($"[{category}] {message}");
}

/// <summary>Synthetic schema-v1 responses built as JSON, exactly as a remote would send them.</summary>
internal static class SyntheticResponses
{
    internal static string Interpretation(
        Guid operationId,
        string summary = "Neutral synthetic summary.",
        IEnumerable<JsonObject>? proposals = null,
        string region = "fullContext",
        JsonObject? usage = null)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["operationId"] = operationId.ToString("D"),
            ["interpretation"] = new JsonObject
            {
                ["summary"] = summary,
                ["observations"] = new JsonArray(new JsonObject
                {
                    ["region"] = region,
                    ["label"] = "synthetic observation",
                    ["confidence"] = 0.6,
                }),
            },
        };
        if (proposals is not null)
        {
            root["memoryProposals"] = new JsonArray([.. proposals]);
        }

        if (usage is not null)
        {
            root["usage"] = usage;
        }

        return root.ToJsonString();
    }

    internal static JsonObject Append(
        string subject,
        string recollection,
        string sourceKind = "observed",
        string scope = "session",
        double confidence = 0.7,
        JsonArray? links = null)
    {
        var proposal = new JsonObject
        {
            ["operation"] = SemanticSchema.AppendOperationName,
            ["scope"] = scope,
            ["sourceKind"] = sourceKind,
            ["confidence"] = confidence,
            ["subjectKey"] = subject,
            ["entityReferences"] = new JsonArray("synthetic.entity"),
            ["recollection"] = recollection,
        };
        if (links is not null)
        {
            proposal["links"] = links;
        }

        return proposal;
    }

    internal static JsonObject Link(Guid target, string kind) =>
        new() { ["targetRecordId"] = target.ToString("D"), ["kind"] = kind };

    internal static MockSemanticProvider.MockStep Gated(
        TaskCompletionSource entered,
        Task release,
        Func<SemanticRequest, string> response,
        bool honorCancellation = true) =>
        async (request, cancellationToken) =>
        {
            entered.TrySetResult();
            if (honorCancellation)
            {
                await release.WaitAsync(cancellationToken);
            }
            else
            {
                await release;
            }

            return ProviderReply.Success(response(request));
        };
}
