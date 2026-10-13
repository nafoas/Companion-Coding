using System.Reflection;
using System.Text;
using CompanionCore.Memory;
using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>M1: the BunDex is Prince's append-only notebook, built from the BrainCase ledger.</summary>
public sealed class BunDexStoreTests
{
    [Fact]
    public void TheTwoHomes_AreSeparateDirectories_UnderTheValidatedRoot_AndNeverProduction()
    {
        using var harness = new BunDexHarness();
        var dataRoot = harness.MemoryLocation.DataRootPath;

        // M1b: the BunDex sits in the data root; the ledger and every machine store sit in the BrainCase.
        Assert.Equal(Path.Combine(dataRoot, "BunDex"), harness.Location.BunDexRoot);
        Assert.Equal(Path.Combine(dataRoot, "Braincase", "bundex-ledger-v1"), harness.Location.LedgerRoot);
        Assert.Equal(Path.Combine(dataRoot, "Braincase", "Memory"), harness.MemoryLocation.RootPath);
        Assert.False(harness.Location.LedgerRoot.StartsWith(harness.Location.BunDexRoot, StringComparison.Ordinal));
        Assert.Equal(DataRootKind.Test, harness.Location.Kind);

        // The development namespace resolves the same layout, and never the production namespace.
        var development = BunDexLocation.For(new DevelopmentDataRootPolicy(harness.BasePath).Resolve());
        Assert.Contains(Path.DirectorySeparatorChar + DevelopmentDataRootPolicy.DevelopmentApplicationNamespace + Path.DirectorySeparatorChar, development.BunDexRoot, StringComparison.Ordinal);
        var production = Path.Combine(harness.BasePath, DevelopmentDataRootPolicy.ProductionApplicationNamespace) + Path.DirectorySeparatorChar;
        Assert.False(development.BunDexRoot.StartsWith(production, StringComparison.Ordinal));
        Assert.False(development.LedgerRoot.StartsWith(production, StringComparison.Ordinal));
    }

    [Fact]
    public void Pages_AreAppendOnly_AndNoMethodRewritesOrDeletes()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var folder = store.CreateFolder("Prince's Scawy Stuffs!!");
        var page = store.CreatePage(folder, "da big oogly", "Saw a big oogly today. ", Tags(BunDexRole.ScaryOrCool));
        var before = Bytes(store.PagePath(page));

        store.AddToPage(page, "Boss beat it!! bnuy!!");

        var after = Bytes(store.PagePath(page));
        Assert.True(after.AsSpan(0, before.Length).SequenceEqual(before));
        Assert.Equal("Saw a big oogly today. Boss beat it!! bnuy!!", Encoding.UTF8.GetString(after));
        Assert.Equal("Saw a big oogly today. Boss beat it!! bnuy!!", store.PageText(page));

        // The mess stays exactly as written.
        Assert.EndsWith(Path.Combine("Prince's Scawy Stuffs!!", "da big oogly.md"), store.PagePath(page), StringComparison.Ordinal);

        // No public method can rewrite, truncate, or delete anything.
        var forbidden = new[] { "Delete", "Remove", "Truncate", "Overwrite", "Rewrite", "Replace", "Set", "Clear", "Edit", "Update" };
        var methods = typeof(BunDexStore).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(methods, method => forbidden.Any(word => method.Name.Contains(word, StringComparison.Ordinal)));
    }

    [Fact]
    public void ACrashAfterTheLedgerCommit_IsRepairedOnReopen_WithNoPartialPage()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.AfterCommit = kind => throw new IOException($"crash after {kind}");
        Assert.Throws<IOException>(() => store.CreatePage(null, "first page", "Prince's first page!!", Tags(BunDexRole.Adventures)));
        Assert.Empty(Directory.EnumerateFiles(harness.Location.BunDexRoot, "*.md", SearchOption.AllDirectories));

        var reopened = harness.Open(out var report);

        Assert.Equal(1, report.PagesRestored);
        var page = Assert.Single(reopened.FindPages(_ => true));
        Assert.Equal("Prince's first page!!", File.ReadAllText(reopened.PagePath(page.PageId)));
        Assert.Empty(Directory.EnumerateFiles(harness.Location.BunDexRoot, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void ACrashMidAddition_IsCompletedOnReopen()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "nanners", "Nanners is yellow. ", Tags(BunDexRole.Lore));
        store.AfterCommit = kind => throw new IOException($"crash after {kind}");
        Assert.Throws<IOException>(() => store.AddToPage(page, "And curvy!!"));

        // Half of the addition reached the disk before the crash.
        File.AppendAllText(store.PagePath(page), "And c");

        var reopened = harness.Open(out var report);

        Assert.Equal(1, report.PagesCompleted);
        Assert.Equal("Nanners is yellow. And curvy!!", File.ReadAllText(reopened.PagePath(page)));
    }

    [Fact]
    public void AnAlteredPage_IsReported_LeftUntouched_AndTakesNoMoreText()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "da tower", "A very tall tower!!", Tags(BunDexRole.Adventures));
        File.WriteAllText(store.PagePath(page), "someone else wrote this");

        var reopened = harness.Open(out var report);

        Assert.Equal(["da tower.md"], report.AlteredPages);
        Assert.Equal("someone else wrote this", File.ReadAllText(reopened.PagePath(page)));
        Assert.Throws<BunDexException>(() => reopened.AddToPage(page, "more"));
        Assert.Equal("someone else wrote this", File.ReadAllText(reopened.PagePath(page)));
    }

    [Fact]
    public void AnAlteredPage_OfTheSameLength_StillTakesNoMoreText()
    {
        using var harness = new BunDexHarness();
        var page = harness.Open().CreatePage(null, "da tower", "A very tall tower!!", Tags(BunDexRole.Adventures));
        File.WriteAllText(harness.Open().PagePath(page), "A very TALL tower!!");

        var reopened = harness.Open(out var report);

        Assert.Equal(["da tower.md"], report.AlteredPages);
        Assert.Throws<BunDexException>(() => reopened.AddToPage(page, "more"));
        Assert.Equal("A very TALL tower!!", File.ReadAllText(reopened.PagePath(page)));
    }

    [Fact]
    public void AForgedLine_ThatRehashesItself_StillBreaksTheChain()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "one", "first", Tags(BunDexRole.ShortTerm));
        store.CreatePage(null, "two", "second", Tags(BunDexRole.ShortTerm));
        var segment = harness.LastSegment();
        var lines = File.ReadAllLines(segment);
        var forged = System.Text.Json.JsonSerializer.Deserialize<LedgerEntry>(lines[1], BunDexLedger.SerializerOptions)! with { Prev = new string('f', 64), Text = "forged" };
        forged = forged with { Hash = BunDexLedger.ComputeHash(forged) };
        lines[1] = System.Text.Json.JsonSerializer.Serialize(forged, BunDexLedger.SerializerOptions);
        File.WriteAllLines(segment, lines);

        harness.Open(out var report);

        Assert.Equal(BunDexHealth.Damaged, report.Health);
    }

    [Fact]
    public void ALostBunDex_IsRebuiltCompletely_FromTheLedger()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var folder = store.CreateFolder("Lore Bits");
        var inner = store.CreateFolder("Old Ruins", folder);
        var page = store.CreatePage(inner, "who built dem", "Prince thinks very old people.", Tags(BunDexRole.Lore));
        store.AddToPage(page, " Maybe Buns??");
        Directory.Delete(harness.Location.BunDexRoot, recursive: true);

        var reopened = harness.Open(out var report);

        Assert.Equal(2, report.FoldersRestored);
        Assert.Equal(1, report.PagesRestored);
        Assert.Equal("Prince thinks very old people. Maybe Buns??", File.ReadAllText(reopened.PagePath(page)));
        Assert.EndsWith(Path.Combine("Lore Bits", "Old Ruins", "who built dem.md"), reopened.PagePath(page), StringComparison.Ordinal);
    }

    [Fact]
    public void Reorganizing_IsRecorded_PagesStayFoundByTheirTags_AndACrashMidMoveCompletes()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var scary = store.CreateFolder("scary");
        var stuffs = store.CreateFolder("Stuffs");
        var page = store.CreatePage(scary, "zombies", "Zombies is oogly.", Tags(BunDexRole.ScaryOrCool, "synthetic.horror", "zombie"));

        store.MoveFolder(scary, stuffs, "Scary or Cool Stuffs");
        var found = Assert.Single(store.FindPages(tags => tags.Subjects.Contains("zombie")));
        Assert.Equal(page, found.PageId);
        Assert.Equal(Path.Combine("Stuffs", "Scary or Cool Stuffs", "zombies.md"), found.RelativePath);
        Assert.Equal("Zombies is oogly.", File.ReadAllText(store.PagePath(page)));
        Assert.False(Directory.Exists(Path.Combine(harness.Location.BunDexRoot, "scary")));

        // A crash between the committed move and the move on disk completes on reopen.
        store.AfterCommit = kind => throw new IOException($"crash after {kind}");
        Assert.Throws<IOException>(() => store.MovePage(page, null, "da zombies"));
        Assert.True(File.Exists(Path.Combine(harness.Location.BunDexRoot, "Stuffs", "Scary or Cool Stuffs", "zombies.md")));

        var reopened = harness.Open(out var report);

        Assert.Equal(1, report.MovesCompleted);
        Assert.Equal(Path.Combine(harness.Location.BunDexRoot, "da zombies.md"), reopened.PagePath(page));
        Assert.Equal("Zombies is oogly.", File.ReadAllText(reopened.PagePath(page)));
        Assert.Single(reopened.FindPages(tags => tags.Role == BunDexRole.ScaryOrCool));
    }

    [Fact]
    public void ATornLedgerTail_IsDropped_AndTheLedgerStaysWritable()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "one", "one", Tags(BunDexRole.ShortTerm));
        File.AppendAllText(harness.LastSegment(), "{\"seq\":2,\"at\":\"2026-");

        var reopened = harness.Open(out var report);

        Assert.True(report.TornTailDropped);
        Assert.Equal(BunDexHealth.Healthy, report.Health);
        reopened.CreatePage(null, "two", "two", Tags(BunDexRole.ShortTerm));
        var third = harness.Open(out var again);
        Assert.Equal(BunDexHealth.Healthy, again.Health);
        Assert.Equal(2, third.FindPages(_ => true).Count);
    }

    [Fact]
    public void ABrokenLedger_FailsClosed_ReadOnly_AndNothingIsTouched()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "one", "one", Tags(BunDexRole.ShortTerm));
        store.CreatePage(null, "two", "two", Tags(BunDexRole.ShortTerm));
        var lines = File.ReadAllLines(harness.LastSegment());
        lines[0] = lines[0].Replace("\"one\"", "\"0ne\"", StringComparison.Ordinal);
        File.WriteAllLines(harness.LastSegment(), lines);
        var pageBytes = Bytes(store.PagePath(page));

        var damaged = harness.Open(out var report);

        Assert.Equal(BunDexHealth.Damaged, report.Health);
        Assert.Equal(BunDexHealth.Damaged, damaged.Health);
        Assert.Throws<BunDexException>(() => damaged.CreateFolder("anything"));
        Assert.Equal(pageBytes, Bytes(Path.Combine(harness.Location.BunDexRoot, "one.md")));
    }

    [Fact]
    public void TheLedger_RollsIntoNewSegments_AndStaysOneChain()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var text = new string('b', 60 * 1024);
        var pages = (int)(BunDexLedger.MaximumSegmentBytes / text.Length) + 3;
        for (var index = 0; index < pages; index++)
        {
            store.CreatePage(null, $"page {index}", text, Tags(BunDexRole.ShortTerm));
        }

        Assert.True(BunDexLedger.Segments(harness.Location.LedgerRoot).Count >= 2);
        var reopened = harness.Open(out var report);
        Assert.Equal(BunDexHealth.Healthy, report.Health);
        Assert.Equal(pages, reopened.FindPages(_ => true).Count);
    }

    [Theory]
    [InlineData("wat is dis?", "wat is dis")]
    [InlineData("../../escape", "escape")]
    [InlineData("a/b\\c:d", "a b c d")]
    [InlineData("  lots   of   spaces  ", "lots of spaces")]
    [InlineData("CON", "~CON")]
    [InlineData("nul.txt", "~nul.txt")]
    [InlineData("Prince's Scawy Stuffs!!", "Prince's Scawy Stuffs!!")]
    [InlineData("tabs\tand\nnewlines", "tabs and newlines")]
    public void Names_AreMadeSafe_NeverTidied(string given, string expected) =>
        Assert.Equal(expected, BunDexNames.MakeSafe(given));

    [Theory]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("sk-ant-api03-abcdefghij")]
    [InlineData("my api_key: 12345")]
    [InlineData("token Ab3dEfGh1jKlMn0pQrStUv4xYz5aBcDeFgHiJkLm9")]
    public void UnusableOrSecretNames_AreRefused(string given) => Assert.Null(BunDexNames.MakeSafe(given));

    [Fact]
    public void NamesAreBounded_CollisionsSuffixed_DepthLimited_AndSecretsNeverWritten()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        Assert.Equal(BunDexNames.MaximumNameLength, BunDexNames.MakeSafe(new string('x', 200))!.Length);

        var first = store.CreateFolder("Stuffs");
        var second = store.CreateFolder("stuffs");
        Assert.EndsWith("stuffs (2)", store.FolderPath(second), StringComparison.Ordinal);
        var page = store.CreatePage(first, "notes", "a", Tags(BunDexRole.ShortTerm));
        var again = store.CreatePage(first, "Notes", "b", Tags(BunDexRole.ShortTerm));
        Assert.EndsWith("Notes (2).md", store.PagePath(again), StringComparison.Ordinal);
        Assert.NotEqual(store.PagePath(page), store.PagePath(again));

        Guid? parent = null;
        for (var depth = 0; depth < BunDexNames.MaximumDepth; depth++)
        {
            parent = store.CreateFolder($"level {depth}", parent);
        }

        Assert.Throws<BunDexException>(() => store.CreateFolder("too deep", parent));
        Assert.Throws<BunDexException>(() => store.CreatePage(null, "key", "here: sk-ant-api03-abcdefghijkl", Tags(BunDexRole.ShortTerm)));
        Assert.Throws<BunDexException>(() => store.CreatePage(null, "token", "Ab3dEfGh1jKlMn0pQrStUv4xYz5aBcDeFgHiJkLm9", Tags(BunDexRole.ShortTerm)));
        Assert.NotEqual(Guid.Empty, store.CreatePage(null, "long word", new string('o', 80) + "h nooo", Tags(BunDexRole.ShortTerm)));
        Assert.Throws<BunDexException>(() => store.CreatePage(null, "empty", string.Empty, Tags(BunDexRole.ShortTerm)));
        Assert.DoesNotContain(Directory.EnumerateFiles(harness.Location.BunDexRoot, "*", SearchOption.AllDirectories), path => path.EndsWith("key.md", StringComparison.Ordinal));
    }

    [Fact]
    public void FilesBoss_PutInTheBunDexHimself_AreNeverTouched()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        Directory.CreateDirectory(harness.Location.BunDexRoot);
        var bosses = Path.Combine(harness.Location.BunDexRoot, "Notes.md");
        File.WriteAllText(bosses, "Boss's own file");

        var page = store.CreatePage(null, "Notes", "Prince's notes", Tags(BunDexRole.ShortTerm));

        Assert.Equal("Boss's own file", File.ReadAllText(bosses));
        Assert.EndsWith("Notes (2).md", store.PagePath(page), StringComparison.Ordinal);
        harness.Open(out var report);
        Assert.Empty(report.AlteredPages);
        Assert.Equal("Boss's own file", File.ReadAllText(bosses));
    }
}
