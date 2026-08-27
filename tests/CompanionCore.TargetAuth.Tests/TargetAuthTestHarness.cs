using CompanionCore.Capture.Contracts;
using CompanionCore.Privacy;

namespace CompanionCore.TargetAuth.Tests;

internal sealed class TargetAuthTestHarness : IAsyncDisposable
{
    private TargetAuthTestHarness(
        string root,
        TargetPolicyCatalog catalog,
        FakeTargetDiscovery discovery,
        FakeDisplayTopology topology,
        RuntimePrivacyState privacy,
        TargetAuthorizationService authorization)
    {
        Root = root;
        Catalog = catalog;
        Discovery = discovery;
        Topology = topology;
        Privacy = privacy;
        Authorization = authorization;
    }

    internal string Root { get; }

    internal TargetPolicyCatalog Catalog { get; }

    internal FakeTargetDiscovery Discovery { get; }

    internal FakeDisplayTopology Topology { get; }

    internal RuntimePrivacyState Privacy { get; }

    internal TargetAuthorizationService Authorization { get; }

    internal static async Task<TargetAuthTestHarness> CreateAsync(
        IAuthorizationPolicyTestHook? policyHook = null,
        int sessionSeed = 0)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "CompanionCore.TargetAuth.Tests",
            Guid.NewGuid().ToString("N"));
        var catalog = await TargetPolicyCatalog.OpenTestAsync(root, policyHook);
        var discovery = new FakeTargetDiscovery();
        var topology = new FakeDisplayTopology();
        var privacy = new RuntimePrivacyState();
        var nextSession = sessionSeed;
        var authorization = new TargetAuthorizationService(
            discovery,
            topology,
            catalog,
            privacy,
            () => new Guid(Interlocked.Increment(ref nextSession), 0, 0, new byte[8]));
        return new TargetAuthTestHarness(
            root,
            catalog,
            discovery,
            topology,
            privacy,
            authorization);
    }

    internal static TargetCandidate Candidate(
        char fingerprintCharacter = 'A',
        long windowId = 101,
        int processId = 201,
        string fileName = "synthetic-game.exe",
        ApplicationCategory applicationCategory = ApplicationCategory.Other) =>
        new(
            new CaptureTargetIdentity(
                windowId,
                processId,
                fileName,
                new string(fingerprintCharacter, 64)),
            applicationCategory);

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeTargetDiscovery : ITargetDiscovery
{
    internal IReadOnlyList<TargetCandidate> Candidates { get; set; } = [];

    internal bool IsValid { get; set; } = true;

    internal int DiscoveryCalls { get; private set; }

    internal int ValidationCalls { get; private set; }

    internal Func<TargetCandidate, CancellationToken, Task<bool>>? ValidationHandler { get; set; }

    internal long SimulatedForegroundWindowId { get; set; }

    public Task<IReadOnlyList<TargetCandidate>> DiscoverAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DiscoveryCalls++;
        return Task.FromResult(Candidates);
    }

    public async Task<bool> IsStillValidAsync(TargetCandidate target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidationCalls++;
        if (ValidationHandler is not null)
        {
            return await ValidationHandler(target, cancellationToken).ConfigureAwait(false);
        }

        return IsValid;
    }
}

internal sealed class FakeDisplayTopology : IDisplayTopology
{
    internal int DisplayCount { get; set; } = 1;

    internal bool ThrowOnRead { get; set; }

    public int GetAttachedDisplayCount()
    {
        if (ThrowOnRead)
        {
            throw new InvalidOperationException("Synthetic display-topology failure.");
        }

        return DisplayCount;
    }
}

internal sealed class RecordingCaptureWorker : ICaptureWorker
{
    private long _sequence;
    private readonly Queue<CaptureFrameMetadata> _buffer = new();
    private readonly Queue<AttentionSheet> _sheets = new();
    private bool _disposed;

    public CaptureWorkerStatus Status { get; private set; } = CaptureWorkerStatus.Stopped;

    internal int StartCount { get; private set; }

    internal int StopAndClearCount { get; private set; }

    internal bool FailStopAndClear { get; set; }

    internal bool FailStart { get; set; }

    internal CaptureAuthorizationGrant? LastGrant { get; private set; }

    internal int ManualRegionSetCount { get; private set; }

    internal NormalizedRegion? ManualRegion { get; private set; }

    internal int BufferedCount => _buffer.Count;

    public event EventHandler<CaptureWorkerStatusChanged>? StatusChanged;

    public event EventHandler<CaptureFrameMetadata>? FrameProduced;

    public event EventHandler<AttentionSheetMetadata>? AttentionSheetProduced;

    internal Func<CaptureAuthorizationGrant, CancellationToken, Task>? StartHandler { get; set; }

    public async Task StartAsync(
        CaptureAuthorizationGrant authorization,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();
        if (Status != CaptureWorkerStatus.Stopped)
        {
            throw new InvalidOperationException("Worker is already active.");
        }

        if (FailStart)
        {
            throw new NotSupportedException("Synthetic worker-start failure.");
        }

        LastGrant = authorization;
        StartCount++;
        SetStatus(CaptureWorkerStatus.Starting);
        SetStatus(CaptureWorkerStatus.Running);
        if (StartHandler is not null)
        {
            await StartHandler(authorization, cancellationToken).ConfigureAwait(false);
        }

        Emit(authorization);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        SetStatus(CaptureWorkerStatus.Stopped);
        ClearSheets();
        return Task.CompletedTask;
    }

    public Task<CaptureStopResult> StopAndClearAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        StopAndClearCount++;
        if (FailStopAndClear)
        {
            throw new IOException("Synthetic stop/clear failure.");
        }

        SetStatus(CaptureWorkerStatus.Stopped);
        var count = _buffer.Count;
        _buffer.Clear();
        ClearSheets();
        return Task.FromResult(new CaptureStopResult(count));
    }

    public async Task RestartAsync(
        CaptureAuthorizationGrant authorization,
        CancellationToken cancellationToken)
    {
        await StopAsync(cancellationToken);
        await StartAsync(authorization, cancellationToken);
    }

    public Task SetManualRegionAsync(
        CaptureAuthorizationGrant authorization,
        NormalizedRegion? region,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();
        region?.Validate(nameof(region));
        if (Status != CaptureWorkerStatus.Running || !ReferenceEquals(authorization, LastGrant))
        {
            throw new InvalidOperationException("Grant is not active.");
        }

        ManualRegionSetCount++;
        ManualRegion = region;
        ClearSheets();
        return Task.CompletedTask;
    }

    public AttentionSheet? TakeLatestAttentionSheet()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (_sheets.Count > 1)
        {
            _sheets.Dequeue().Dispose();
        }

        return _sheets.Count == 0 ? null : _sheets.Dequeue();
    }

    public Task<CaptureWorkerMetrics> GetMetricsAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CaptureWorkerMetrics
        {
            Status = Status,
            RingFrameCount = _buffer.Count,
            CurrentSourceFrames = _buffer.Count,
            MaximumObservedSourceFrames = _buffer.Count,
            CurrentAttentionSheets = _sheets.Count,
            MaximumObservedAttentionSheets = _sheets.Count,
            CurrentAttentionSheetBytes = _sheets.Sum(sheet => sheet.Length),
            MaximumObservedAttentionSheetBytes = _sheets.Sum(sheet => sheet.Length),
        });
    }

    internal CaptureFrameMetadata Emit(CaptureAuthorizationGrant authorization)
    {
        var frame = new CaptureFrameMetadata(
            authorization,
            Interlocked.Increment(ref _sequence),
            new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
            1,
            1);
        _buffer.Enqueue(frame);
        FrameProduced?.Invoke(this, frame);
        return frame;
    }

    internal AttentionSheetMetadata EmitSheet(
        CaptureAuthorizationGrant authorization,
        long sourceSequenceNumber)
    {
        var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        var metadata = new AttentionSheetMetadata
        {
            TargetSessionId = authorization.TargetSessionId,
            Generation = authorization.Generation,
            Target = authorization.Target,
            SourceSequenceNumber = sourceSequenceNumber,
            SourceTimestamp = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
            SourceWidth = 1,
            SourceHeight = 1,
            SheetWidth = 1,
            SheetHeight = 1,
            EncodedByteLength = bytes.Length,
            Kind = AttentionSheetKind.Orientation,
            ChangeScore = 1,
            Regions =
            [
                new AttentionSheetRegionMetadata
                {
                    Kind = AttentionRegionKind.FullContext,
                    NormalizedSource = new NormalizedRegion(0, 0, 1, 1),
                    SourcePixels = new PixelRect(0, 0, 1, 1),
                    SheetPixels = new PixelRect(0, 0, 1, 1),
                },
            ],
        };
        while (_sheets.Count >= AttentionSheet.MaximumRetainedSheets)
        {
            _sheets.Dequeue().Dispose();
        }

        _sheets.Enqueue(new AttentionSheet(metadata, bytes));
        AttentionSheetProduced?.Invoke(this, metadata);
        return metadata;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _buffer.Clear();
        ClearSheets();
        AttentionSheetProduced = null;
        Status = CaptureWorkerStatus.Stopped;
    }

    private void ClearSheets()
    {
        while (_sheets.Count > 0)
        {
            _sheets.Dequeue().Dispose();
        }
    }

    private void SetStatus(CaptureWorkerStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(
            this,
            new CaptureWorkerStatusChanged(
                status,
                new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero)));
    }
}

internal sealed class ThrowingPromotionHook : IAuthorizationPolicyTestHook
{
    internal bool ThrowBeforePromotion { get; set; }

    public Task BeforePromotionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowBeforePromotion)
        {
            throw new IOException("Synthetic policy promotion failure.");
        }

        return Task.CompletedTask;
    }
}
