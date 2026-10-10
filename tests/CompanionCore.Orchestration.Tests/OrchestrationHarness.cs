using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using CompanionCore.Api;
using CompanionCore.Capture.Contracts;
using CompanionCore.Memory;
using CompanionCore.Privacy;
using CompanionCore.TargetAuth;
using CompanionCore.Watchbun;

namespace CompanionCore.Orchestration.Tests;

internal sealed class ManualTime(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

internal sealed class SyntheticDiscovery : ITargetDiscovery
{
    public List<TargetCandidate> Candidates { get; } = [];

    public bool Valid { get; set; } = true;

    public Task<IReadOnlyList<TargetCandidate>> DiscoverAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TargetCandidate>>(Candidates.ToArray());

    public Task<bool> IsStillValidAsync(TargetCandidate target, CancellationToken cancellationToken) => Task.FromResult(Valid);
}

internal sealed class SingleDisplay : IDisplayTopology
{
    public int GetAttachedDisplayCount() => 1;
}

internal sealed class SyntheticPlatform : IPlatformSignals
{
    public event EventHandler<ForegroundWindow>? ForegroundChanged;

    public event EventHandler? InputObserved;

    public event EventHandler<TargetExit>? TargetExited;

    public event EventHandler<CaptureTargetIdentity>? TargetLaunched;

    public event EventHandler<SuspendReason>? Suspended;

    public event EventHandler<SuspendReason>? Resumed;

    public CaptureTargetIdentity? Watched { get; private set; }

    public CaptureTargetIdentity? AwaitingRelaunchOf { get; private set; }

    public void WatchTarget(CaptureTargetIdentity? target) => Watched = target;

    public void WatchForRelaunch(CaptureTargetIdentity? previous) => AwaitingRelaunchOf = previous;

    public void Foreground(long windowId, int processId) => ForegroundChanged?.Invoke(this, new ForegroundWindow(windowId, processId));

    public void Input() => InputObserved?.Invoke(this, EventArgs.Empty);

    public void Exit(TargetExit exit) => TargetExited?.Invoke(this, exit);

    public void Launch(CaptureTargetIdentity identity) => TargetLaunched?.Invoke(this, identity);

    public void Suspend(SuspendReason reason) => Suspended?.Invoke(this, reason);

    public void Resume(SuspendReason reason) => Resumed?.Invoke(this, reason);
}

/// <summary>A synthetic worker that emits admitted frames and real, strictly encoded PNG sheets.</summary>
internal sealed class ScriptedCaptureWorker : ICaptureWorker
{
    internal const int SheetWidth = 64;
    internal const int SheetHeight = 48;
    private readonly Queue<AttentionSheet> _sheets = new();
    private long _sequence;
    private CaptureAuthorizationGrant? _grant;

    public CaptureWorkerStatus Status { get; private set; } = CaptureWorkerStatus.Stopped;

    public List<AttentionSheet> Emitted { get; } = [];

    public List<byte[]> EmittedPayloads { get; } = [];

    public event EventHandler<CaptureWorkerStatusChanged>? StatusChanged;

    public event EventHandler<CaptureFrameMetadata>? FrameProduced;

    public event EventHandler<AttentionSheetMetadata>? AttentionSheetProduced;

    public Task StartAsync(CaptureAuthorizationGrant authorization, CancellationToken cancellationToken)
    {
        _grant = authorization;
        SetStatus(CaptureWorkerStatus.Starting);
        SetStatus(CaptureWorkerStatus.Running);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        SetStatus(CaptureWorkerStatus.Stopped);
        Clear();
        return Task.CompletedTask;
    }

    public Task<CaptureStopResult> StopAndClearAsync(CancellationToken cancellationToken)
    {
        SetStatus(CaptureWorkerStatus.Stopped);
        Clear();
        return Task.FromResult(new CaptureStopResult(0));
    }

    public async Task RestartAsync(CaptureAuthorizationGrant authorization, CancellationToken cancellationToken)
    {
        await StopAsync(cancellationToken);
        await StartAsync(authorization, cancellationToken);
    }

    public Task SetManualRegionAsync(CaptureAuthorizationGrant authorization, NormalizedRegion? region, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RequestOrientationAsync(CaptureAuthorizationGrant authorization, CancellationToken cancellationToken) => Task.CompletedTask;

    public AttentionSheet? TakeLatestAttentionSheet()
    {
        lock (_sheets)
        {
            while (_sheets.Count > 1)
            {
                _sheets.Dequeue().Dispose();
            }

            return _sheets.Count == 0 ? null : _sheets.Dequeue();
        }
    }

    public Task<CaptureWorkerMetrics> GetMetricsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new CaptureWorkerMetrics { Status = Status });

    /// <summary>Emits one admitted frame and its orientation sheet for the current grant.</summary>
    public AttentionSheet EmitSheet(DateTimeOffset timestamp, byte shade = 90, double changeScore = 0.5, CaptureAuthorizationGrant? grant = null)
    {
        var authorization = grant ?? _grant ?? throw new InvalidOperationException("No grant.");
        var sequence = Interlocked.Increment(ref _sequence);
        var png = EncodeUniform(SheetWidth, SheetHeight, shade);
        var metadata = new AttentionSheetMetadata
        {
            TargetSessionId = authorization.TargetSessionId,
            Generation = authorization.Generation,
            Target = authorization.Target,
            SourceSequenceNumber = sequence,
            SourceTimestamp = timestamp,
            SourceWidth = 640,
            SourceHeight = 480,
            SheetWidth = SheetWidth,
            SheetHeight = SheetHeight,
            EncodedByteLength = png.Length,
            Kind = AttentionSheetKind.Orientation,
            ChangeScore = changeScore,
            Regions =
            [
                new AttentionSheetRegionMetadata
                {
                    Kind = AttentionRegionKind.FullContext,
                    NormalizedSource = new NormalizedRegion(0, 0, 1, 1),
                    SourcePixels = new PixelRect(0, 0, 640, 480),
                    SheetPixels = new PixelRect(0, 0, SheetWidth, SheetHeight),
                },
            ],
        };
        var sheet = new AttentionSheet(metadata, png.ToArray());
        EmittedPayloads.Add(png);
        Emitted.Add(sheet);
        lock (_sheets)
        {
            _sheets.Enqueue(sheet);
        }

        FrameProduced?.Invoke(this, new CaptureFrameMetadata(authorization, sequence, timestamp, 640, 480));
        AttentionSheetProduced?.Invoke(this, metadata);
        return sheet;
    }

    public void Dispose() => Clear();

    private void Clear()
    {
        lock (_sheets)
        {
            while (_sheets.Count > 0)
            {
                _sheets.Dequeue().Dispose();
            }
        }
    }

    private void SetStatus(CaptureWorkerStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, new CaptureWorkerStatusChanged(status, DateTimeOffset.UtcNow));
    }

    internal static byte[] EncodeUniform(int width, int height, byte shade)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[width * 4 + 1];
            for (var x = 0; x < width; x++)
            {
                row[1 + (x * 4)] = shade;
                row[2 + (x * 4)] = (byte)(shade + x);
                row[3 + (x * 4)] = 40;
                row[4 + (x * 4)] = 255;
            }

            for (var y = 0; y < height; y++)
            {
                zlib.Write(row);
            }
        }

        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        Chunk(output, "IHDR", header);
        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = 0xFFFFFFFFu;
        foreach (var value in typeBytes.Concat(data))
        {
            crc ^= value;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFFu);
        output.Write(crcBytes);
    }
}

/// <summary>A full, real composition over one synthetic test root.</summary>
internal sealed class OrchestrationHarness : IAsyncDisposable
{
    internal static readonly DateTimeOffset T0 = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private OrchestrationHarness(string basePath, MemoryStoreLocation location, TargetPolicyCatalog catalog)
    {
        BasePath = basePath;
        Location = location;
        Authorization = new TargetAuthorizationService(Discovery, new SingleDisplay(), catalog, Privacy);
        Controller = new TargetSessionController(Authorization, Worker, Privacy, new LocalPrivacyGuard());
    }

    internal string BasePath { get; }

    internal MemoryStoreLocation Location { get; }

    internal RuntimePrivacyState Privacy { get; } = new();

    internal SyntheticDiscovery Discovery { get; } = new();

    internal ScriptedCaptureWorker Worker { get; } = new();

    internal SyntheticPlatform Platform { get; } = new();

    internal ManualTime Time { get; } = new(T0);

    internal MockSemanticProvider Provider { get; } = new();

    internal TargetAuthorizationService Authorization { get; }

    internal TargetSessionController Controller { get; }

    internal CompanionHost Host { get; private set; } = null!;

    internal CompanionOrchestrator Orchestrator => Host.Orchestrator;

    internal ConcurrentQueue<CompanionNotice> Notices { get; } = new();

    internal static async Task<OrchestrationHarness> CreateAsync(OrchestratorOptions? options = null)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "CompanionCore.Orchestration.Tests", Guid.NewGuid().ToString("N"));
        var catalog = await TargetPolicyCatalog.OpenTestAsync(basePath);
        var harness = new OrchestrationHarness(basePath, TestDataRootPolicy.Create(basePath, Guid.NewGuid()), catalog);
        await harness.OpenHostAsync(options);
        return harness;
    }

    internal async Task OpenHostAsync(OrchestratorOptions? options = null)
    {
        Host = await CompanionHost.OpenAsync(new CompanionHostOptions(Location, Privacy, Controller, Provider, new InMemoryCredentialStore())
        {
            Platform = Platform,
            Time = Time,
            Orchestrator = options,
            Bridge = new BridgeOptions { InitialBackoff = TimeSpan.Zero, MaximumBackoff = TimeSpan.Zero },
            Notice = (_, notice) => Notices.Enqueue(notice),
        });
    }

    internal async Task ReopenHostAsync(OrchestratorOptions? options = null)
    {
        await Host.DisposeAsync();
        await OpenHostAsync(options);
    }

    internal static TargetCandidate Candidate(int processId = 4321, long windowId = 0x1234, char fingerprint = 'A') =>
        new(new CaptureTargetIdentity(windowId, processId, "synthetic-game.exe", new string(fingerprint, 64)), ApplicationCategory.Game);

    internal async Task<CaptureAuthorizationGrant> AuthorizeAsync(TargetCandidate? candidate = null)
    {
        candidate ??= Candidate();
        Discovery.Candidates.Clear();
        Discovery.Candidates.Add(candidate);
        await Controller.SetExplicitPolicyAsync(candidate, new TargetPolicy(AuthorizationCategory.StandingAuthorized, TargetContentPolicy.TrustedGame));
        var result = await Controller.AuthorizeAsync(candidate, explicitConsent: true);
        Assert.True(result.Succeeded, $"Authorization failed: {result.EventKind}");
        await WaitForAsync(() => Notices.Any(notice => notice.Kind == CompanionNoticeKind.SessionStarted
            && notice.Key == $"target-session:{Controller.CurrentSession.TargetSessionId:N}"));
        return Controller.CurrentSession.Grant!;
    }

    /// <summary>Emits a sheet and waits until the orchestrator has fully applied it.</summary>
    internal async Task<AttentionSheet> SheetAsync(byte shade = 90, double changeScore = 0.5)
    {
        var calls = Provider.CallCount;
        var sheet = Worker.EmitSheet(Time.GetUtcNow(), shade, changeScore);
        await SettleAsync();
        return sheet;
    }

    /// <summary>Flushes the mailbox and any in-flight bridge work.</summary>
    internal async Task SettleAsync()
    {
        for (var attempt = 0; attempt < 400; attempt++)
        {
            var snapshot = await Orchestrator.GetSnapshotAsync();
            if (!snapshot.BridgeInFlight)
            {
                await Orchestrator.GetSnapshotAsync();
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("The orchestrator did not settle.");
    }

    /// <summary>Commits one neutral original for a session, as an earlier capture path would have.</summary>
    internal async Task CommitSessionOriginalAsync(string sessionReference)
    {
        var result = await Host.Repository.WriteGate.SubmitAsync(new AppendMemoryProposal(Guid.NewGuid(), [
            new MemoryRecordDraft
            {
                RecordId = Guid.NewGuid(),
                CreatedAtUtc = Time.GetUtcNow(),
                Scope = MemoryScope.Session,
                SourceKind = MemorySourceKind.Observed,
                Confidence = 0.8,
                SubjectKey = "synthetic.orphan",
                EntityReferences = ["synthetic.entity"],
                SessionReference = sessionReference,
                VisibleRecollection = "[neutral memory] an orphaned moment.",
                RetrievalMetadataJson = new CompanionCore.Recall.RecallMetadata().ToJson(),
            },
        ]));
        Assert.True(result.IsAccepted);
    }

    internal async Task WaitForAsync(Func<bool> condition, CompanionOrchestrator? orchestrator = null, int timeoutMs = 10_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not met. Notices: " + string.Join(", ", Notices.Select(n => n.Kind + ":" + (n.Watchbun?.Kind.ToString() ?? n.Key))));
            }

            await (orchestrator ?? Orchestrator).GetSnapshotAsync();
            await Task.Delay(10);
        }
    }

    internal async Task AdvanceAndTickAsync(TimeSpan by)
    {
        Time.Advance(by);
        await Orchestrator.TickAsync();
        await SettleAsync();
    }

    internal bool Has(CompanionNoticeKind kind) => Notices.Any(notice => notice.Kind == kind);

    internal bool HasWatchbun(WatchbunIntentKind kind) => Notices.Any(notice => notice.Watchbun?.Kind == kind);

    internal static string ResponseWithMemory(Guid operationId, string label, string subject, string recollection) =>
        new JsonObject
        {
            ["schemaVersion"] = SemanticSchema.Version,
            ["operationId"] = operationId.ToString("D"),
            ["interpretation"] = new JsonObject
            {
                ["summary"] = "Synthetic interpretation.",
                ["observations"] = new JsonArray(new JsonObject
                {
                    ["region"] = "fullContext",
                    ["label"] = label,
                    ["confidence"] = 0.9,
                }),
            },
            ["memoryProposals"] = new JsonArray(new JsonObject
            {
                ["operation"] = SemanticSchema.AppendOperationName,
                ["scope"] = "session",
                ["sourceKind"] = "observed",
                ["confidence"] = 0.8,
                ["subjectKey"] = subject,
                ["entityReferences"] = new JsonArray("synthetic.entity"),
                ["recollection"] = recollection,
            }),
        }.ToJsonString();

    /// <summary>Proof that no file under the test root contains any emitted sheet payload.</summary>
    internal void AssertNoSheetBytesOnDisk()
    {
        foreach (var file in Directory.EnumerateFiles(BasePath, "*", SearchOption.AllDirectories))
        {
            byte[] bytes;
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                bytes = buffer.ToArray();
            }
            catch (IOException) when (file.EndsWith(".lock", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var payload in Worker.EmittedPayloads)
            {
                Assert.True(bytes.AsSpan().IndexOf(payload.AsSpan(8, Math.Min(64, payload.Length - 8))) < 0, $"Sheet bytes found in {file}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Host.DisposeAsync();
            await Controller.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(BasePath))
            {
                Directory.Delete(BasePath, recursive: true);
            }
        }
    }
}
