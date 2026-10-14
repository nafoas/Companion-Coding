using System.Reflection;
using CompanionCore.Curtain;
using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>
/// M5a: every page is linked to where it came from, in the BrainCase's hidden index only, and he
/// decides what to write: nothing he is given weighs a moment or mentions not writing.
/// </summary>
public sealed class SourceLinkTests
{
    private static readonly string FirstMemory = PageSources.Memory(Guid.Parse("11111111-2222-3333-4444-555555555555"));
    private static readonly string SecondMemory = PageSources.Memory(Guid.Parse("66666666-7777-8888-9999-000000000000"));

    [Fact]
    public async Task APageKeptFromAMoment_IsLinkedToItsSource_BothWays()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var writer = new StandInWriter();

        var outcome = await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm), Saw("a glowing door"), sources: [FirstMemory]);

        var page = outcome.PageId!.Value;
        Assert.Equal([FirstMemory], store.SourcesOf(page));
        Assert.Equal([page], store.PagesFrom(FirstMemory));
        Assert.Empty(store.PagesFrom(SecondMemory));
        Assert.Empty(store.SourcesOf(Guid.NewGuid()));
    }

    [Fact]
    public void Links_FollowAddingMovingRenamingAndAReopen()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "door", "A glowing door.", Tags(BunDexRole.ShortTerm) with { Sources = [FirstMemory] });
        store.AddToPage(page, " It opened!", [SecondMemory, FirstMemory]);
        var folder = store.CreateFolder("Doors");
        store.MovePage(page, folder, "glowy door");

        foreach (var opened in new[] { store, harness.Open() })
        {
            Assert.Equal([FirstMemory, SecondMemory], opened.SourcesOf(page));
            Assert.Equal([page], opened.PagesFrom(FirstMemory));
            Assert.Equal([page], opened.PagesFrom(SecondMemory));
            Assert.Equal(page, opened.FindPage(folder, "glowy door"));
        }
    }

    [Fact]
    public void APageWithoutSources_HasNoLinks_AndOldLedgerLinesAreUnchanged()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "door", "A glowing door.", Tags(BunDexRole.ShortTerm));
        store.AddToPage(page, " It opened!");

        Assert.Empty(store.SourcesOf(page));
        Assert.DoesNotContain("sources", File.ReadAllText(harness.LastSegment()), StringComparison.Ordinal);
        Assert.Equal(BunDexHealth.Healthy, harness.Open(out _).Health);
    }

    [Fact]
    public async Task TheWall_NoLinkIdTagKindOrTimeReachesAnythingHeIsGiven()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "door", "A glowing door by da pond.", Tags(BunDexRole.ShortTerm, where: "Sky Garden") with { Sources = [FirstMemory] });
        var writer = new StandInWriter();

        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Sky Garden"), Saw("a glowing door"), sources: [SecondMemory]);

        var request = writer.Moments[0].ToRequest(store.View());
        Assert.NotEmpty(writer.Moments[0].ComesToMind);
        Assert.DoesNotContain("memory:", request.Text, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"[0-9a-f]{8}-?[0-9a-f]{4}", request.Text);
        Assert.DoesNotContain(nameof(BunDexRole.ShortTerm), request.Text, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\d", request.Text);
        Assert.Empty(request.FindLeaks());

        // Nothing he is given, and nothing he can say back, carries a link or a tag.
        foreach (var type in new[] { typeof(KeepMoment), typeof(KeepResponse), typeof(TidyStep), typeof(BunDexView), typeof(FolderView) })
        {
            Assert.DoesNotContain(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
                property.Name.Contains("Source", StringComparison.Ordinal)
                || property.Name.Contains("Tag", StringComparison.Ordinal)
                || property.PropertyType == typeof(PageTags)
                || property.PropertyType == typeof(Guid));
        }
    }

    [Fact]
    public async Task AddingToAPageHeAlreadyHas_LinksTheNewSourceToo()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var page = store.CreatePage(null, "door", "A glowing door.", Tags(BunDexRole.ShortTerm) with { Sources = [FirstMemory] });
        var writer = new StandInWriter();
        var adds = new AddingWriter();

        await new BunDexKeeper(store, adds).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm), Saw("the door opened"), sources: [SecondMemory]);

        Assert.Equal([FirstMemory, SecondMemory], store.SourcesOf(page));
        Assert.Equal([page], store.PagesFrom(SecondMemory));
    }

    private sealed class AddingWriter : IBunDexWriter
    {
        public ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new KeepResponse(KeepChoice.Write, [], "door", AddToExistingPage: true, Text: " It opened!"));

        public ValueTask<TidyResponse> TidyAsync(TidyMoment moment, BunDexView notebook, CancellationToken cancellationToken) =>
            ValueTask.FromResult(TidyResponse.NotNow);
    }
}
