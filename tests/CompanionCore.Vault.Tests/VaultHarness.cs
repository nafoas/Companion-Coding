using System.Security.Cryptography;
using System.Text;
using CompanionCore.Capture.Contracts;
using CompanionCore.Keepsakes;
using CompanionCore.Memory;
using CompanionCore.Privacy;

namespace CompanionCore.Vault.Tests;

/// <summary>One isolated synthetic test root holding a BunDex, keepsakes, and state.</summary>
internal sealed class VaultHarness : IAsyncDisposable
{
    internal static readonly DateTimeOffset T0 = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private long _sequence;
    private int _photographs;

    private VaultHarness(string basePath, MemoryStoreLocation location)
    {
        BasePath = basePath;
        Location = location;
        Keepsakes = KeepsakeLocation.For(location);
        State = new VaultStateStore(VaultStateLocation.For(location));
    }

    internal string BasePath { get; }

    internal MemoryStoreLocation Location { get; }

    internal KeepsakeLocation Keepsakes { get; }

    internal VaultStateStore State { get; }

    internal RuntimePrivacyState Privacy { get; } = new();

    internal MemoryRepository? Repository { get; private set; }

    internal KeepsakeCamera Camera { get; private set; } = null!;

    internal CaptureAuthorizationGrant Grant { get; private set; } = null!;

    internal static async Task<VaultHarness> CreateAsync()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "CompanionCore.Vault.Tests", Guid.NewGuid().ToString("N"));
        var harness = new VaultHarness(basePath, TestDataRootPolicy.Create(basePath, Guid.NewGuid()));
        await harness.OpenAsync();
        harness.Grant = CaptureAuthorizationGrant.Issue(Guid.NewGuid(), harness.Privacy.Snapshot.Generation, new CaptureTargetIdentity(0x1234, 4321, "synthetic-game.exe", new string('A', 64)));
        return harness;
    }

    internal async Task OpenAsync()
    {
        Repository = await MemoryRepository.OpenAsync(Location, Privacy);
        Camera = new KeepsakeCamera(Repository, Privacy, Keepsakes);
    }

    internal async Task CloseAsync()
    {
        if (Repository is not null)
        {
            await Repository.DisposeAsync();
            Repository = null;
        }
    }

    internal KeepsakeStore Store => new(Repository!, Keepsakes);

    internal async Task<Guid> RememberAsync(string subject)
    {
        var record = new MemoryRecordDraft
        {
            RecordId = Guid.NewGuid(),
            CreatedAtUtc = T0,
            Scope = MemoryScope.General,
            SourceKind = MemorySourceKind.Observed,
            Confidence = 0.8,
            SubjectKey = subject,
            VisibleRecollection = $"[neutral memory] {subject}",
            RetrievalMetadataJson = "{}",
        };
        var result = await Repository!.WriteGate.SubmitAsync(new AppendMemoryProposal(Guid.NewGuid(), [record]));
        Assert.Equal(WriteGateStatus.Committed, result.Status);
        return record.RecordId;
    }

    /// <summary>Takes one real photograph through the camera; each call is spaced past the rarity interval.</summary>
    internal async Task<(Guid PhotographId, Guid ActionId)> PhotographAsync(byte seed)
    {
        var at = T0.AddMinutes(10 * _photographs++);
        var begun = Camera.BeginCameraAction(Grant, at);
        Assert.Equal(KeepsakeRefusal.None, begun.Refusal);
        const int width = 32, height = 24;
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)(index * seed);
        }

        var frame = new PhotographFrame(new CaptureFrameMetadata(Grant, ++_sequence, at.AddSeconds(1), width, height), pixels, width * 4);
        var saved = await Camera.TakeAsync(begun.Action!, frame, new KeepsakeContext("game.alpha"), TargetContentPolicy.TrustedGame, PrivacyAssessment.Clear, at.AddSeconds(1));
        Assert.Equal(KeepsakeRefusal.None, saved.Refusal);
        return (saved.PhotographId!.Value, begun.Action!.ActionId);
    }

    internal string PhotographPath(Guid actionId) => Path.Combine(Keepsakes.RootPath, $"{actionId:N}.png");

    internal static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    internal static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        if (Directory.Exists(BasePath))
        {
            Directory.Delete(BasePath, recursive: true);
        }
    }
}
