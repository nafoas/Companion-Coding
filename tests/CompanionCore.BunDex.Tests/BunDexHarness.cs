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

    internal static PageTags Tags(BunDexRole role, string? game = "synthetic.exploration", string? where = null, params string[] subjects) =>
        new(role, MemoryLayer.Unspecified, game, Save: null, subjects, T0, where);

    internal static byte[] Bytes(string path) => File.ReadAllBytes(path);

    /// <summary>Something that happened, as it crosses the membrane: "Prince saw...".</summary>
    internal static CompanionCore.Curtain.Sense Saw(string description) => CompanionCore.Curtain.Membrane.Saw(description)!;

    /// <summary>Synthetic, varied fond things he expressed about a memory: each brings something new (M5b).</summary>
    internal static readonly string[] FondThings =
    [
        "da dirt is so soft under my peets",
        "carrots smell sweet and earthy",
        "crunchy crunch, so juicy",
        "leaves tickle my whiskers",
        "bees hum in da sunny patch",
        "warm sunshine on my fluffy back",
        "Boss laughed when i hopped happily",
        "sparkly dew drops on every leaf",
        "cozy nap beside da fence",
        "dandelions waving hello",
        "puddle splashes giggle wiggle",
        "pumpkin muffins crumbly yummy",
        "twinkly fireflies blinking softly",
        "fuzzy caterpillar wobbling slowly",
        "rainbow ribbons fluttering breezy",
    ];

    /// <summary>He grows to cherish a page the only way there is: many varied, fond experiences of it, in his own words.</summary>
    internal static void Cherish(BunDexStore store, Guid page)
    {
        foreach (var fond in FondThings)
        {
            store.Experience(page, fond, new ExpressedFeeling(FeelingKind.Fond, 1));
        }

        Assert.True(store.Cherishes(page));
    }

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
