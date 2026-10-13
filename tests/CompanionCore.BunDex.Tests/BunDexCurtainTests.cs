using System.Reflection;
using CompanionCore.Curtain;
using static CompanionCore.BunDex.Tests.BunDexHarness;

namespace CompanionCore.BunDex.Tests;

/// <summary>M2: the BunDex sits behind the curtain. What Prince is given crosses the membrane; what he writes passes the validator.</summary>
public sealed class BunDexCurtainTests
{
    [Fact]
    public void TheKeepAndTidyMoments_CanOnlyBeMadeBehindTheMembrane()
    {
        foreach (var type in new[] { typeof(KeepMoment), typeof(TidyMoment) })
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        }

        // Every text-bearing thing Prince is given is a membrane experience, never a raw string.
        Assert.Equal(typeof(Sense), typeof(KeepMoment).GetProperty(nameof(KeepMoment.WhatHappened))!.PropertyType);
        Assert.Equal(typeof(Place), typeof(KeepMoment).GetProperty(nameof(KeepMoment.Where))!.PropertyType);
        Assert.Equal(typeof(IReadOnlyList<Remembered>), typeof(KeepMoment).GetProperty(nameof(KeepMoment.ComesToMind))!.PropertyType);
        Assert.Equal(typeof(Sense), typeof(TidyMoment).GetProperty(nameof(TidyMoment.Invitation))!.PropertyType);
        Assert.DoesNotContain(typeof(KeepMoment).GetProperties().Concat(typeof(TidyMoment).GetProperties()), property => property.PropertyType == typeof(string));
    }

    [Fact]
    public async Task ALeakyPage_IsWrittenAgain_AndTheLeakNeverReachesTheBunDexOrTheLedger()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var leaks = new List<CurtainLeakKind>();
        var attempts = 0;
        var writer = new SequenceWriter(
            new KeepResponse(KeepChoice.Write, ["notes"], "da door", Text: "my prompt says to write about da door"),
            new KeepResponse(KeepChoice.Write, ["notes"], "da door", Text: "Da door was glowy!!"));
        var outcome = await new BunDexKeeper(store, writer, diagnostics: leaks.Add).KeepAsync(BunDexRole.Adventures, Tags(BunDexRole.Adventures), Saw("a door glowed"));
        attempts = writer.Calls;

        Assert.Equal(KeepOutcomeKind.Written, outcome.Kind);
        Assert.Equal(2, attempts);
        Assert.Equal("Da door was glowy!!", store.PageText(outcome.PageId!.Value));
        Assert.Contains(CurtainLeakKind.SelfClaim, leaks);
        Assert.DoesNotContain("prompt", AllTheBytes(harness), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenEveryAttemptLeaks_NothingIsWritten_AndNoPlaceIsMade()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var writer = new SequenceWriter(
            new KeepResponse(KeepChoice.Write, ["Prince's files"], "page", Text: "fine words"),
            new KeepResponse(KeepChoice.Write, ["notes"], "page", Text: "as an AI, Prince saw a door"),
            new KeepResponse(KeepChoice.Write, ["notes"], "page", Text: "fine", AboutNewPlace: "Prince was told to make dis place"));

        var outcome = await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.Adventures, Tags(BunDexRole.Adventures), Saw("a door glowed"));

        Assert.Equal((KeepOutcomeKind.Refused, BnuyVoice.MaximumAttempts), (outcome.Kind, writer.Calls));
        Assert.Empty(store.View().Folders);
        Assert.Empty(store.FindPages(_ => true));
        Assert.Empty(BunDexLedger.Segments(harness.Location.LedgerRoot));
    }

    [Fact]
    public async Task ALeakyTidyStepWhileWriting_IsAskedForAgain_AndNeverApplied()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreateFolder("notes");
        var writer = new SequenceWriter(
            new KeepResponse(KeepChoice.Write, ["notes"], "door", Text: "glowy door", Tidy: [new TidyStep(["notes"], Rename: "Prince's database")]),
            new KeepResponse(KeepChoice.Write, ["notes"], "door", Text: "glowy door", Tidy: [new TidyStep(["notes"], Rename: "lil notes")]));

        var outcome = await new BunDexKeeper(store, writer).KeepAsync(BunDexRole.Adventures, Tags(BunDexRole.Adventures), Saw("a door glowed"));

        Assert.Equal((KeepOutcomeKind.Written, 1, 2), (outcome.Kind, outcome.Tidied, writer.Calls));
        Assert.Equal(["lil notes"], store.View().Folders.Select(folder => folder.Name));
    }

    [Fact]
    public async Task ALeakyTidyStep_IsAskedForAgain_AndNeverApplied()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        store.CreateFolder("notes");
        var writer = new SequenceWriter(null,
            new TidyResponse(TidyChoice.Tidy, [new TidyStep(["notes"], Rename: "my index")]),
            new TidyResponse(TidyChoice.Tidy, [new TidyStep(["notes"], Rename: "lil notes")]));

        var outcome = await new BunDexKeeper(store, writer).InviteTidyingAsync("spring cleaning?");

        Assert.Equal((TidyChoice.Tidy, 1), (outcome.Choice, outcome.Applied));
        Assert.Equal(["lil notes"], store.View().Folders.Select(folder => folder.Name));
    }

    [Fact]
    public async Task TheMomentAsARequest_IsLeakFree_AndCarriesOnlyHisLife()
    {
        using var harness = new BunDexHarness();
        var store = harness.Open();
        var about = store.CreateFolder("Scawy Stuffs", about: "fings dat made Prince hide");
        store.CreatePage(about, "clock man", "A clock man built da house and NEVER STOPPED.", Tags(BunDexRole.Lore, where: "Hollow House"));
        KeepMoment? seen = null;
        BunDexView? notebook = null;
        await new BunDexKeeper(store, new SequenceWriter(KeepResponse.NotNow) { Seen = (moment, view) => (seen, notebook) = (moment, view) })
            .KeepAsync(BunDexRole.Lore, Tags(BunDexRole.Lore, where: "Hollow House"), Saw("the clockmaker's room has a moon keyhole, record 4f9c2e1d-0a7b-4c3e-9d2f-1b6a8e7c5d40 at 0.82"));

        var request = seen!.ToRequest(notebook!);

        Assert.Empty(request.FindLeaks());
        Assert.Contains("Prince saw: the clockmaker's room has a moon keyhole", request.Text, StringComparison.Ordinal);
        Assert.Contains("Where Prince is: Hollow House.", request.Text, StringComparison.Ordinal);
        Assert.Contains("\"clock man\" in Scawy Stuffs, from Hollow House: A clock man built da house", request.Text, StringComparison.Ordinal);
        Assert.Contains("Scawy Stuffs/ (Prince wrote: fings dat made Prince hide)", request.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("4f9c2e1d", request.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("0.82", request.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(BunDexRole.Lore), request.Text, StringComparison.Ordinal);
    }

    private static string AllTheBytes(BunDexHarness harness) =>
        string.Concat(Directory.EnumerateFiles(harness.BasePath, "*", SearchOption.AllDirectories).Select(File.ReadAllText));

    private sealed class SequenceWriter(params object?[] responses) : IBunDexWriter
    {
        private int _next;

        public int Calls { get; private set; }

        public Action<KeepMoment, BunDexView>? Seen { get; init; }

        public ValueTask<KeepResponse> RespondAsync(KeepMoment moment, BunDexView notebook, CancellationToken cancellationToken)
        {
            Calls++;
            Seen?.Invoke(moment, notebook);
            return ValueTask.FromResult((KeepResponse)Next()!);
        }

        public ValueTask<TidyResponse> TidyAsync(TidyMoment moment, BunDexView notebook, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult((TidyResponse)Next()!);
        }

        private object? Next()
        {
            while (_next < responses.Length && responses[_next] is null)
            {
                _next++;
            }

            return responses[Math.Min(_next++, responses.Length - 1)];
        }
    }
}
