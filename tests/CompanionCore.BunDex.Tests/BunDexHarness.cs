using CompanionCore.Memory;

namespace CompanionCore.BunDex.Tests;

/// <summary>One isolated synthetic test root. Nothing here can resolve a development or production root.</summary>
internal sealed class BunDexHarness : IDisposable
{
    internal static readonly DateTimeOffset T0 = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    internal BunDexHarness()
    {
        BasePath = Path.Combine(Path.GetTempPath(), "CompanionCore.BunDex.Tests", Guid.NewGuid().ToString("N"));
        MemoryLocation = TestDataRootPolicy.Create(BasePath, Guid.NewGuid());
        Location = BunDexLocation.For(MemoryLocation);
    }

    internal string BasePath { get; }

    internal MemoryStoreLocation MemoryLocation { get; }

    internal BunDexLocation Location { get; }

    internal BunDexStore Open() => BunDexStore.Open(Location, out _);

    internal BunDexStore Open(out BunDexOpenReport report) => BunDexStore.Open(Location, out report);

    internal static PageTags Tags(BunDexRole role, string? game = "synthetic.exploration", params string[] subjects) =>
        new(role, MemoryLayer.Unspecified, game, Save: null, subjects, T0);

    internal static byte[] Bytes(string path) => File.ReadAllBytes(path);

    internal string LastSegment() => BunDexLedger.Segments(Location.LedgerRoot)[^1].Path;

    public void Dispose()
    {
        try
        {
            Directory.Delete(BasePath, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
