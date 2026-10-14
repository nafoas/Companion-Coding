using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>
/// M5a: his page names are exactly his. The BrainCase never renumbers or cleans what he called a
/// page; keeping file names unique on disk is its own business, underneath.
/// </summary>
public sealed class PageTitleTests
{
    [Fact]
    public void TwoPagesWithTheSameName_BothKeepHisName_WhileTheirFilesStayApart()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();

        var first = store.CreatePage(null, "trees", "Saw trees.", Tags(BunDexRole.ShortTerm));
        var second = store.CreatePage(null, "trees", "Saw MORE trees.", Tags(BunDexRole.ShortTerm));

        Assert.Equal(["trees", "trees"], store.View().Pages);
        Assert.NotEqual(store.PagePath(first), store.PagePath(second));
        Assert.Equal("Saw MORE trees.", File.ReadAllText(store.PagePath(second)));
        Assert.Equal(["trees", "trees"], harness.Open().View().Pages);
        Assert.Equal(first, store.FindPage(null, "trees"));
    }

    [Fact]
    public void ANameWithCharactersAFileCannotHold_IsStillHisName()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();

        var page = store.CreatePage(null, "moon/sun? da big one", "Both at once!!", Tags(BunDexRole.ShortTerm));

        Assert.Equal(["moon/sun? da big one"], store.View().Pages);
        Assert.Equal("moon sun da big one.md", Path.GetFileName(store.PagePath(page)));
        Assert.Equal(page, store.FindPage(null, "moon/sun? da big one"));
        Assert.Equal(["moon/sun? da big one"], harness.Open().View().Pages);
    }

    [Fact]
    public void RenamingAndMoving_KeepHisNewName_AndMovingAloneKeepsHisOld()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var folder = store.CreateFolder("Trees");
        store.CreatePage(folder, "trees", "Some other trees.", Tags(BunDexRole.ShortTerm));
        var page = store.CreatePage(null, "trees", "Saw trees.", Tags(BunDexRole.ShortTerm));

        store.MovePage(page, folder);
        Assert.Equal(["trees", "trees"], store.View().Folders[0].Pages);

        store.MovePage(page, null, "big trees?");
        Assert.Equal(["big trees?"], harness.Open().View().Pages);
    }

    [Fact]
    public void APageWhoseNameNeedsNoChange_WritesNoExtraLedgerField()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "trees", "Saw trees.", Tags(BunDexRole.ShortTerm));

        Assert.DoesNotContain("\"title\"", File.ReadAllText(harness.LastSegment()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhatComesToMind_AndWhatHeFinds_CarryHisOwnNames()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "moon/sun? lantern", "Da moon lantern and da sun lantern.", Tags(BunDexRole.LongTerm, where: "Sky Garden"));

        var writer = new StandInWriter(declineWhen: _ => true);
        await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm, where: "Sky Garden"), Saw("a moon lantern"));
        var found = store.LookFor("the moon lantern").Found;

        Assert.Equal("moon/sun? lantern", Assert.Single(writer.Moments[0].ComesToMind).Page);
        Assert.Equal("moon/sun? lantern", found!.Page);
    }

    [Fact]
    public void FindingAPage_GoesByHisName_EvenWhenAnotherFileHasTheCleanedVersion()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var plain = store.CreatePage(null, "moon sun", "One.", Tags(BunDexRole.ShortTerm));
        var his = store.CreatePage(null, "moon/sun?", "Two.", Tags(BunDexRole.ShortTerm));

        Assert.Equal(his, store.FindPage(null, "moon/sun?"));
        Assert.Equal(plain, store.FindPage(null, "moon sun"));
    }

    [Fact]
    public void AVeryLongName_IsBounded()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();

        store.CreatePage(null, new string('a', BunDexNames.MaximumNameLength + 40), "Long.", Tags(BunDexRole.ShortTerm));

        Assert.Equal(BunDexNames.MaximumNameLength, Assert.Single(store.View().Pages).Length);
    }
}
