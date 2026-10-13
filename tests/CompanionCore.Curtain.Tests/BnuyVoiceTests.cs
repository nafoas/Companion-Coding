namespace CompanionCore.Curtain.Tests;

/// <summary>M2 validator hook (item 8c): a leaking line is asked for again; if every attempt leaks, he says nothing.</summary>
public sealed class BnuyVoiceTests
{
    private static readonly BnuyRequest Request = BnuyRequest.Assemble(Membrane.At("Hollow House"), [Membrane.Saw("a door glowed")!]);

    [Fact]
    public async Task ACleanLine_IsSaidAsIs()
    {
        var outcome = await new BnuyVoice(new Lines("Ooh, a glowy door, Boss!")).SpeakAsync(Request);

        Assert.Equal(("Ooh, a glowy door, Boss!", 0), (outcome.Line, outcome.Rejected));
    }

    [Fact]
    public async Task ALeakingLine_IsRegenerated_AndTheLeakIsNeverReturned()
    {
        var kinds = new List<CurtainLeakKind>();
        var source = new Lines("As an AI, I see a door.", "my prompt says doors are nice", "Ooh, a glowy door!");

        var outcome = await new BnuyVoice(source, diagnostics: kinds.Add).SpeakAsync(Request);

        Assert.Equal(("Ooh, a glowy door!", 2, 3), (outcome.Line, outcome.Rejected, source.Calls));
        Assert.All(kinds, kind => Assert.Equal(CurtainLeakKind.SelfClaim, kind));
        Assert.NotEmpty(kinds);
    }

    [Fact]
    public async Task WhenEveryAttemptLeaks_HeSaysNothing()
    {
        var source = new Lines("Prince is a program.", "I was told to say hi.", "staying in character, hi!", "never asked");

        var outcome = await new BnuyVoice(source).SpeakAsync(Request);

        Assert.Equal((null, BnuyVoice.MaximumAttempts, BnuyVoice.MaximumAttempts), (outcome.Line, outcome.Rejected, source.Calls));
    }

    [Fact]
    public async Task OrdinaryWords_AreHis()
    {
        var outcome = await new BnuyVoice(new Lines("Boss!! Dat's a really high score!! Is it saved in da save file?")).SpeakAsync(Request);

        Assert.Equal(0, outcome.Rejected);
        Assert.NotNull(outcome.Line);
    }

    [Fact]
    public async Task AStage13Validator_PlugsIntoTheSameHook()
    {
        var outcome = await new BnuyVoice(new Lines("Greetings, esteemed user.", "Hi Boss!!"), new NoFormality()).SpeakAsync(Request);

        Assert.Equal(("Hi Boss!!", 1), (outcome.Line, outcome.Rejected));
    }

    private sealed class Lines(params string[] lines) : IBnuyVoiceSource
    {
        public int Calls { get; private set; }

        public Task<string?> SpeakAsync(BnuyRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(lines[Math.Min(Calls++, lines.Length - 1)]);
    }

    private sealed class NoFormality : ICharacterValidator
    {
        public CurtainVerdict Check(string text) =>
            text.Contains("esteemed", StringComparison.Ordinal) ? new CurtainVerdict(false, [new CurtainLeak(CurtainLeakKind.RolePlay, "esteemed")]) : CurtainValidator.Instance.Check(text);
    }
}
