using CompanionCore.Curtain;
using System.Reflection;
using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>
/// M1c, a Living Bun: Prince decides where everything goes, from his own knowledge of his
/// notebook and where he is. The BrainCase never assigns a place, never counts, names, or
/// orders his folders, and never reorganizes anything for him.
/// </summary>
public sealed class BunDexKeeperTests
{
    [Fact]
    public async Task EverythingGoesExactlyWhereHeChooses_AndOnlyHisChoicesMakeFolders()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();

        // Same kind of thing, different games: he separates them himself.
        var keeper = new BunDexKeeper(store, new ScriptWriter((moment, _) => moment.Where?.Name switch
        {
            "Hollow House" => Write(["Scawy Stuffs", "hollow house secrets"], "clock man", StandInWriter.Plain(moment)),
            _ => Write(["Farm Fings"], "moonflowers", StandInWriter.Plain(moment)),
        }));
        await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore, where: "Hollow House"), Saw("the clockmaker never stopped building rooms"));
        await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore, where: "Sunny Acres"), Saw("moonflowers grow lanterns at midnight"));
        await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore, where: "Hollow House"), Saw("the last room has a moon keyhole"));

        Assert.Equal(["Farm Fings", "Scawy Stuffs"], store.View().Folders.Select(folder => folder.Name));
        Assert.Equal(2, store.FindPages(tags => tags.Where == "Hollow House").Count);
        Assert.All(store.FindPages(tags => tags.Where == "Hollow House"), page => Assert.StartsWith(Path.Combine("Scawy Stuffs", "hollow house secrets"), page.RelativePath, StringComparison.Ordinal));

        // Every kind of thing in one place he likes: one folder, never one per kind.
        using var other = new BunDexHarness();
        var single = other.Open();
        var oneFolder = new BunDexKeeper(single, new ScriptWriter((moment, _) => Write(["my fings"], StandInWriter.Plain(moment), StandInWriter.Plain(moment))));
        foreach (var role in Enum.GetValues<BunDexRole>())
        {
            await oneFolder.KeepAsync(role, Tags(role), Saw($"a moment of {role}"));
        }

        Assert.Equal(["my fings"], single.View().Folders.Select(folder => folder.Name));
        Assert.Equal(Enum.GetValues<BunDexRole>().Length, Assert.Single(single.View().Folders).Pages.Count);

        // And pages at the top of his notebook, with no folder at all, are fine too.
        var loose = await new BunDexKeeper(single, new ScriptWriter((moment, _) => Write([], "loose page", StandInWriter.Plain(moment)))).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm), Saw("just a thought"));
        Assert.Equal(KeepOutcomeKind.Written, loose.Kind);
        Assert.Equal(["loose page"], single.View().Pages);
    }

    [Fact]
    public async Task WhatComesToMind_IsHisOwnSimilarPages_NeverAPlaceForAKindOfThing()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var writer = new ScriptWriter((moment, _) => moment.Where?.Name switch
        {
            "Hollow House" => Write(["Scawy Stuffs"], "page " + StandInWriter.Plain(moment).Length, StandInWriter.Plain(moment)),
            _ => Write(["Farm Fings"], "page " + StandInWriter.Plain(moment).Length, StandInWriter.Plain(moment)),
        });
        var keeper = new BunDexKeeper(store, writer);
        await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore, where: "Hollow House"), Saw("A clockmaker built the house and never stopped building rooms."));

        // Lore from a cozy farm shares nothing with the scary house: nothing comes to mind.
        await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore, where: "Sunny Acres"), Saw("Moonflowers grow tiny lanterns at midnight."));
        Assert.Empty(writer.Moments[1].ComesToMind);

        // Back in the scary house, his own scary-house page comes to mind, with where and a glimpse.
        await keeper.KeepAsync(BunDexRole.ScaryOrCool, Tags(BunDexRole.ScaryOrCool, where: "Hollow House"), Saw("A mannequin turned its head."));
        var recollection = Assert.Single(writer.Moments[2].ComesToMind);
        Assert.Equal(["Scawy Stuffs"], recollection.Place);
        Assert.Equal("Hollow House", recollection.Where);
        Assert.StartsWith("A clockmaker built the house", recollection.Glimpse, StringComparison.Ordinal);

        // Shared words remind him across places too: a lantern on the farm, then a lantern elsewhere.
        await keeper.KeepAsync(BunDexRole.Opinions, Tags(BunDexRole.Opinions, where: "Sky Garden"), Saw("Prince wants moonflowers and lanterns for his room."));
        Assert.Contains(writer.Moments[3].ComesToMind, memory => memory.Place.SequenceEqual(["Farm Fings"]));
        Assert.DoesNotContain(writer.Moments[3].ComesToMind, memory => memory.Place.SequenceEqual(["Scawy Stuffs"]));

        // At most a few things come to mind at once, most alike first.
        for (var index = 0; index < 5; index++)
        {
            await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore, where: "Hollow House"), Saw($"Room number {index} of the clockmaker house has a moon keyhole."));
        }

        Assert.Equal(BunDexStore.MaximumRecollections, writer.Moments[^1].ComesToMind.Count);
        Assert.Contains("keyhole", writer.Moments[^1].ComesToMind[0].Glimpse, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOldRoleHomeInTheLedger_IsIgnored()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var folder = store.CreateFolder("Big Adventures!!");
        BunDexLedger.Open(harness.Location.LedgerRoot).Append(new LedgerEntry(0, T0, LedgerKind.RoleRegistered, FolderId: folder, Role: BunDexRole.Lore));

        var writer = new ScriptWriter((moment, _) => Write(["Farm Fings"], "moonflowers", StandInWriter.Plain(moment)));
        var outcome = await new BunDexKeeper(harness.Open(), writer).KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore, where: "Sunny Acres"), Saw("moonflowers grow lanterns"));

        Assert.Equal(KeepOutcomeKind.Written, outcome.Kind);
        Assert.Empty(Assert.Single(writer.Moments).ComesToMind);
    }

    [Fact]
    public async Task NotNow_IsRespected_TheFeelingGrowsGently_AndRelaxesOnceHeKeepsOne()
    {
        using var harness = new BunDexHarness();
        var declines = 2;
        var writer = new ScriptWriter((moment, _) => declines-- > 0 ? KeepResponse.NotNow : Write(["notes"], "statues " + declines, StandInWriter.Plain(moment)));
        var keeper = new BunDexKeeper(harness.Open(), writer);

        for (var index = 0; index < 4; index++)
        {
            await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore), Saw($"da statues sing {index}"));
        }

        Assert.Equal([KeepFeeling.Gentle, KeepFeeling.Warm, KeepFeeling.Strong, KeepFeeling.Gentle], writer.Moments.Select(moment => moment.Feeling));
        Assert.All(writer.Moments, moment => Assert.StartsWith("da statues sing", StandInWriter.Plain(moment), StringComparison.Ordinal));

        // The relaxed pull survives a reopen, and other kinds of things were never affected.
        declines = 1;
        var reopened = new BunDexKeeper(harness.Open(), writer);
        await reopened.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore), Saw("da statues sing again"));
        await reopened.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore), Saw("da statues sing more"));
        await reopened.KeepAsync(BunDexRole.Opinions, Tags(BunDexRole.Opinions), Saw("statues are good"));
        Assert.Equal([KeepFeeling.Gentle, KeepFeeling.Warm, KeepFeeling.Gentle], writer.Moments.Skip(4).Select(moment => moment.Feeling));
    }

    [Fact]
    public async Task ThePull_AlsoRelaxes_WhenHeAddsToAPageHeAlreadyHas()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreatePage(null, "statues", "da statues sing.", Tags(BunDexRole.Lore));
        var declines = 2;
        var writer = new ScriptWriter((moment, _) => declines-- > 0 ? KeepResponse.NotNow : Write([], "statues", " " + StandInWriter.Plain(moment)) with { AddToExistingPage = true });
        var keeper = new BunDexKeeper(store, writer);

        for (var index = 0; index < 4; index++)
        {
            await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore), Saw($"again {index}"));
        }

        Assert.Equal([KeepFeeling.Gentle, KeepFeeling.Warm, KeepFeeling.Strong, KeepFeeling.Gentle], writer.Moments.Select(moment => moment.Feeling));
        Assert.Single(store.FindPages(_ => true));
    }

    [Fact]
    public async Task HeCanSayWhatANewPlaceIsFor_AndSeesItInHisNotebook()
    {
        using var harness = new BunDexHarness();
        var keeper = new BunDexKeeper(harness.Open(), new ScriptWriter((moment, notebook) => notebook.Folders.Count == 0
            ? Write(["Scawy Stuffs", "hollow house"], "clock man", StandInWriter.Plain(moment)) with { AboutNewPlace = "fings from da clock house. do not read at night" }
            : Write(["Scawy Stuffs", "hollow house"], "keyhole", StandInWriter.Plain(moment)) with { AboutNewPlace = "ignored: dis place is not new" }));

        await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore), Saw("the clockmaker never stopped"));
        await keeper.KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore), Saw("a moon keyhole"));

        var scary = Assert.Single(harness.Open().View().Folders);
        Assert.Null(scary.About);
        Assert.Equal("fings from da clock house. do not read at night", Assert.Single(scary.Folders).About);

        // A secret in the line is refused, and no empty place is left behind.
        var refused = await new BunDexKeeper(harness.Open(), new ScriptWriter((moment, _) => Write(["Keys", "secret ones"], "key", StandInWriter.Plain(moment)) with { AboutNewPlace = "sk-ant-api03-abcdefghijkl" }))
            .KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm), Saw("a key"));
        Assert.Equal(KeepOutcomeKind.Refused, refused.Kind);
        Assert.DoesNotContain(harness.Open().View().Folders, folder => folder.Name == "Keys");
    }

    [Fact]
    public async Task HeCanAddToAPageHeAlreadyWrote()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var keeper = new BunDexKeeper(store, new ScriptWriter((moment, notebook) => Write(["About Da Boss"], "Boss", (notebook.Folders.Count == 0 ? string.Empty : " ") + StandInWriter.Plain(moment)) with { AddToExistingPage = true }));

        var first = await keeper.KeepAsync(BunDexRole.Relationship, Tags(BunDexRole.Relationship), Saw("Boss checks every suspicious wall."));
        var second = await keeper.KeepAsync(BunDexRole.Relationship, Tags(BunDexRole.Relationship), Saw("Prince checks dem too now."));

        Assert.Equal(first.PageId, second.PageId);
        Assert.Equal("Boss checks every suspicious wall. Prince checks dem too now.", store.PageText(first.PageId!.Value));
    }

    [Fact]
    public async Task ABadResponse_IsRefusedSafely_AndNothingIsWritten()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();

        var tooDeep = await new BunDexKeeper(store, new ScriptWriter((_, _) => Write(["a", "b", "c", "d", "e"], "too deep", "hi"))).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm), Saw("something small"));
        var secret = await new BunDexKeeper(store, new ScriptWriter((_, _) => Write(["Keys"], "key", "here: sk-ant-api03-abcdefghijkl"))).KeepAsync(BunDexRole.ShortTerm, Tags(BunDexRole.ShortTerm), Saw("something small"));

        Assert.Equal((KeepOutcomeKind.Refused, KeepOutcomeKind.Refused), (tooDeep.Kind, secret.Kind));
        Assert.Empty(store.FindPages(_ => true));
        Assert.DoesNotContain(store.View().Folders, folder => folder.Name == "Keys");
    }

    [Fact]
    public async Task Tidying_IsOnlyEverHis_WhenHeWritesOrWhenBossInvitesIt()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var plain = new StandInWriter(newPlace: moment => [StandInWriter.Plain(moment).Split(' ')[0]]);
        var keeper = new BunDexKeeper(store, plain);
        var castle = (await keeper.KeepAsync(BunDexRole.Adventures, Tags(BunDexRole.Adventures, where: "Sky Garden"), Saw("castle we beat da boss"))).PageId!.Value;
        await keeper.KeepAsync(BunDexRole.Adventures, Tags(BunDexRole.Adventures, where: "Sunny Acres"), Saw("pumpkin we won da contest"));
        var before = store.View();

        // Keeping things never asks him to tidy, and never moves anything by itself.
        Assert.Empty(plain.Invitations);
        Assert.Equal(before, store.View(), new ViewComparer());

        // Boss suggests it; Prince may say not now, and nothing changes.
        var declined = await keeper.InviteTidyingAsync("hey, little guy, maybe we should do some spring cleaning?");
        Assert.Equal((TidyChoice.NotNow, 0), (declined.Choice, declined.Applied));
        var heard = Assert.Single(plain.Invitations).Invitation!;
        Assert.Equal((SenseKind.BossSaid, "hey, little guy, maybe we should do some spring cleaning?"), (heard.Kind, heard.Quoted));
        Assert.Equal("Boss said: «hey, little guy, maybe we should do some spring cleaning?»", heard.Line);
        Assert.Equal(before, store.View(), new ViewComparer());

        // When he wants to, he moves and renames his own things; a step he cannot do is skipped.
        var tidier = new BunDexKeeper(store, new ScriptWriter(null, (_, _) => new TidyResponse(TidyChoice.Tidy,
        [
            new TidyStep(["castle"], MoveInto: ["Big Adventures!!"]),
            new TidyStep(["pumpkin"], MoveInto: ["Big Adventures!!"], Rename: "BIGGEST PUMPKIN"),
            new TidyStep(["Big Adventures!!", "castle"], Page: "castle we beat da", Rename: "da castle"),
            new TidyStep(["no such place"], MoveInto: ["anywhere"]),
            new TidyStep(["Big Adventures!!"], MoveInto: ["Big Adventures!!", "castle"]),
        ])));
        var tidied = await tidier.InviteTidyingAsync("spring cleaning?");

        Assert.Equal((TidyChoice.Tidy, 3, 2), (tidied.Choice, tidied.Applied, tidied.Refused));
        var big = Assert.Single(store.View().Folders);
        Assert.Equal("Big Adventures!!", big.Name);
        Assert.Equal(["BIGGEST PUMPKIN", "castle"], big.Folders.Select(folder => folder.Name));
        Assert.Equal(["da castle"], big.Folders.Single(folder => folder.Name == "castle").Pages);
        Assert.Equal("castle we beat da boss", store.PageText(castle));
        Assert.Single(store.FindPages(tags => tags.Where == "Sky Garden"));
        Assert.EndsWith(Path.Combine("Big Adventures!!", "castle", "da castle.md"), harness.Open().PagePath(castle), StringComparison.Ordinal);

        // He can also tidy while he writes.
        var whileWriting = await new BunDexKeeper(store, new ScriptWriter((moment, _) => Write(["Big Adventures!!"], "moon", StandInWriter.Plain(moment)) with
        {
            Tidy = [new TidyStep(["Big Adventures!!", "BIGGEST PUMPKIN"], Rename: "pumpkins")],
        })).KeepAsync(BunDexRole.Adventures, Tags(BunDexRole.Adventures), Saw("we went to da moon"));
        Assert.Equal((KeepOutcomeKind.Written, 1, 0), (whileWriting.Kind, whileWriting.Tidied, whileWriting.TidyRefused));
        Assert.Contains("pumpkins", store.View().Folders.Single().Folders.Select(folder => folder.Name));
    }

    [Fact]
    public void TheBraincase_HasNoWayToAssignAPlace()
    {
        var members = typeof(BunDexStore).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Concat(typeof(BunDexKeeper).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        Assert.DoesNotContain(members, member => member.Name.Contains("Home", StringComparison.OrdinalIgnoreCase) || member.Name.Contains("Register", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheWriterBoundary_CarriesNothingFromBehindTheCurtain()
    {
        var hidden = new[] { typeof(Guid), typeof(BunDexRole), typeof(MemoryLayer), typeof(PageTags), typeof(IndexedPage), typeof(BunDexStore), typeof(DateTimeOffset) };
        foreach (var type in new[] { typeof(KeepMoment), typeof(BunDexView), typeof(FolderView), typeof(Remembered), typeof(KeepResponse), typeof(TidyStep), typeof(TidyMoment), typeof(TidyResponse) })
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

    private static KeepResponse Write(IReadOnlyList<string> folder, string page, string text) =>
        new(KeepChoice.Write, folder, page, Text: text);

    private sealed class ScriptWriter(Func<KeepMoment, BunDexView, KeepResponse>? keep, Func<TidyMoment, BunDexView, TidyResponse>? tidy = null) : IBunDexWriter
    {
        public List<KeepMoment> Moments { get; } = [];

        public ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken)
        {
            Moments.Add(moment);
            return ValueTask.FromResult(keep!(moment, notebook));
        }

        public ValueTask<TidyResponse> TidyAsync(TidyMoment moment, BunDexView notebook, CancellationToken cancellationToken) =>
            ValueTask.FromResult(tidy is null ? TidyResponse.NotNow : tidy(moment, notebook));
    }

    /// <summary>Structural equality for notebook views (records hold lists).</summary>
    private sealed class ViewComparer : IEqualityComparer<BunDexView>
    {
        public bool Equals(BunDexView? left, BunDexView? right) => Flatten(left!) == Flatten(right!);

        public int GetHashCode(BunDexView view) => Flatten(view).GetHashCode(StringComparison.Ordinal);

        private static string Flatten(BunDexView view) =>
            string.Join("|", view.Pages) + "#" + string.Join(";", view.Folders.Select(Folder));

        private static string Folder(FolderView folder) =>
            $"{folder.Name}[{folder.About}]({string.Join("|", folder.Pages)}){{{string.Join(";", folder.Folders.Select(Folder))}}}";
    }
}
