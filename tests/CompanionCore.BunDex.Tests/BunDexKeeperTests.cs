using System.Reflection;
using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>
/// M1: the subconscious's "keep this" pull. A new place appears only when one is needed, he may
/// decline, and the writer boundary carries nothing from behind the curtain.
/// </summary>
public sealed class BunDexKeeperTests
{
    [Fact]
    public async Task APlace_IsMadeOnlyWhenNoneExists_ThenComesToMindAndIsReused()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var writer = new StandInWriter(newPlace: moment => moment.WhatHappened.Contains("skeleton", StringComparison.Ordinal) ? ["Spooky Stuffs"] : ["Big Adventures!!"]);
        var keeper = new BunDexKeeper(store, writer);

        var first = await keeper.KeepAsync(BunDexRole.LongTerm, Tags(BunDexRole.LongTerm), "we beat da castle boss after three tries");
        var second = await keeper.KeepAsync(BunDexRole.LongTerm, Tags(BunDexRole.LongTerm), "we found a secret garden behind da waterfall");

        Assert.Equal((KeepOutcomeKind.Written, true), (first.Kind, first.NewPlace));
        Assert.Equal((KeepOutcomeKind.Written, false), (second.Kind, second.NewPlace));
        Assert.Null(writer.Moments[0].ComesToMind);
        Assert.Equal(["Big Adventures!!"], writer.Moments[1].ComesToMind);
        Assert.Equal(Path.GetDirectoryName(store.PagePath(first.PageId!.Value)), Path.GetDirectoryName(store.PagePath(second.PageId!.Value)));
        Assert.Single(store.View().Folders);

        // A different kind of thing has no home yet, so it gets its own new place.
        var other = await keeper.KeepAsync(BunDexRole.ScaryOrCool, Tags(BunDexRole.ScaryOrCool), "a skeleton that tap dances");
        Assert.True(other.NewPlace);
        Assert.Null(writer.Moments[2].ComesToMind);
        Assert.Equal(2, store.View().Folders.Count);

        // The registration survives a reopen: the home still comes to mind.
        var reopened = harness.Open();
        var later = new StandInWriter();
        await new BunDexKeeper(reopened, later).KeepAsync(BunDexRole.LongTerm, Tags(BunDexRole.LongTerm), "the moon level was very pretty");
        Assert.Equal(["Big Adventures!!"], later.Moments[0].ComesToMind);
    }

    [Fact]
    public async Task NotNow_IsRespected_AndTheFeelingGrowsGently_WithoutAnyWords()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var declines = 2;
        var writer = new StandInWriter(declineWhen: _ => declines-- > 0);
        var keeper = new BunDexKeeper(store, writer);

        var a = await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore), "da statues sing at night");
        var b = await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore), "da statues sing at night again");
        Assert.Equal((KeepOutcomeKind.NotNow, KeepOutcomeKind.NotNow), (a.Kind, b.Kind));
        Assert.Empty(store.View().Folders);
        Assert.Empty(store.FindPages(_ => true));

        var c = await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore), "da statues sing for the moon");
        Assert.Equal(KeepOutcomeKind.Written, c.Kind);
        Assert.Equal([KeepFeeling.Gentle, KeepFeeling.Warm, KeepFeeling.Strong], writer.Moments.Select(moment => moment.Feeling));

        // The feeling is capped, and what he was told is only ever what happened.
        await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore), "one more statue");
        Assert.Equal(KeepFeeling.Strong, writer.Moments[^1].Feeling);
        Assert.All(writer.Moments, moment => Assert.StartsWith("da statues", moment.WhatHappened.Replace("one more statue", "da statues", StringComparison.Ordinal), StringComparison.Ordinal));
    }

    [Fact]
    public async Task HeCanAddToAPageHeAlreadyWrote()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var keeper = new BunDexKeeper(store, new AddingWriter());

        var first = await keeper.KeepAsync(BunDexRole.Relationship, Tags(BunDexRole.Relationship), "Boss checks every suspicious wall.");
        var second = await keeper.KeepAsync(BunDexRole.Relationship, Tags(BunDexRole.Relationship), " Prince checks dem too now.");

        Assert.Equal(first.PageId, second.PageId);
        Assert.Equal("Boss checks every suspicious wall. Prince checks dem too now.", store.PageText(first.PageId!.Value));
    }

    [Fact]
    public async Task ABadResponse_IsRefusedSafely_AndNothingIsWritten()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var keeper = new BunDexKeeper(store, new FixedWriter(new KeepResponse(KeepChoice.Write, ["a", "b", "c", "d", "e"], "too deep", Text: "hi")));

        var outcome = await keeper.KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm), "something small");

        Assert.Equal(KeepOutcomeKind.Refused, outcome.Kind);
        Assert.Empty(store.FindPages(_ => true));
    }

    [Fact]
    public void TheWriterBoundary_CarriesNothingFromBehindTheCurtain()
    {
        var hidden = new[] { typeof(Guid), typeof(BunDexRole), typeof(MemoryLayer), typeof(PageTags), typeof(IndexedPage), typeof(BunDexStore) };
        foreach (var type in new[] { typeof(KeepMoment), typeof(BunDexView), typeof(FolderView), typeof(KeepResponse) })
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.True(!Reaches(property.PropertyType, hidden), $"{type.Name}.{property.Name} reaches behind the curtain.");
            }
        }

        static bool Reaches(Type type, Type[] hidden) =>
            hidden.Contains(Nullable.GetUnderlyingType(type) ?? type)
            || (type.IsGenericType && type.GetGenericArguments().Any(argument => Reaches(argument, hidden)))
            || (type.IsArray && Reaches(type.GetElementType()!, hidden));
    }

    private sealed class AddingWriter : IBunDexWriter
    {
        public ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new KeepResponse(KeepChoice.Write, moment.ComesToMind ?? ["About Da Boss"], "Boss", AddToExistingPage: true, Text: moment.WhatHappened));
    }

    private sealed class FixedWriter(KeepResponse response) : IBunDexWriter
    {
        public ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken) =>
            ValueTask.FromResult(response);
    }
}
