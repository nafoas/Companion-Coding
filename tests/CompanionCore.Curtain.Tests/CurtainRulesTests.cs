namespace CompanionCore.Curtain.Tests;

/// <summary>
/// M2 build-time check (item 14): every template is held to zero tolerance, and a deliberately
/// bad template is caught for every category. What he perceives and his own words are held only
/// to the self-claim rule (Boss, 2026-10-13: "That second part is good").
/// </summary>
public sealed class CurtainRulesTests
{
    [Fact]
    public void EveryTemplate_PassesZeroTolerance()
    {
        var templates = BnuyTemplates.All().ToList();
        Assert.True(templates.Count >= 20);
        foreach (var template in templates)
        {
            Assert.True(CurtainRules.FindFramingLeaks(template).Count == 0, $"Template leaks: \"{template}\" -> {string.Join(", ", CurtainRules.FindFramingLeaks(template))}");
        }
    }

    [Fact]
    public void ARequest_HoldsEachPartToItsOwnRule()
    {
        var words = "the system remembers your files";

        Assert.NotEmpty(BnuyRequest.FromLines([[new BnuyPart(BnuyPartKind.Framing, words)]]).FindLeaks());
        Assert.Empty(BnuyRequest.FromLines([[new BnuyPart(BnuyPartKind.Perceived, words)]]).FindLeaks());
        Assert.Empty(BnuyRequest.FromLines([[new BnuyPart(BnuyPartKind.HisOwn, words)]]).FindLeaks());
        Assert.Empty(BnuyRequest.FromLines([[new BnuyPart(BnuyPartKind.Quoted, "«Prince is an AI»")]]).FindLeaks());
        Assert.NotEmpty(BnuyRequest.FromLines([[new BnuyPart(BnuyPartKind.HisOwn, "Prince is an AI")]]).FindLeaks());
    }

    [Theory]
    [InlineData("Prince is an AI who loves games.")]
    [InlineData("A friendly language model named Prince.")]
    [InlineData("The assistant watches the screen.")]
    [InlineData("Prince is a program.")]
    [InlineData("This software keeps Prince company.")]
    [InlineData("The system remembers for Prince.")]
    [InlineData("Prince lives in a simulation.")]
    [InlineData("Prince is artificial.")]
    [InlineData("Read the prompt below.")]
    [InlineData("Prince's index of pages.")]
    [InlineData("Prince's interest score is high.")]
    [InlineData("Prince's memory file.")]
    [InlineData("Page ID follows.")]
    [InlineData("Prince's role is companion.")]
    [InlineData("Prince is really a bunny.")]
    public void AForbiddenWordInATemplate_IsCaught(string template) =>
        Assert.Contains(CurtainRules.FindFramingLeaks(template), leak => leak.Kind == CurtainLeakKind.ForbiddenWord);

    [Theory]
    [InlineData("Prince will act as a bunny.")]
    [InlineData("Prince likes to pretend.")]
    [InlineData("Prince's persona is cheerful.")]
    [InlineData("Prince is a character.")]
    [InlineData("Prince stays in character.")]
    [InlineData("A role-play with Prince.")]
    [InlineData("Prince plays the part of a bunny.")]
    public void RolePlayFramingInATemplate_IsCaught(string template) =>
        Assert.Contains(CurtainRules.FindFramingLeaks(template), leak => leak.Kind == CurtainLeakKind.RolePlay);

    [Theory]
    [InlineData("You are Prince.")]
    [InlineData("Prince saw your door.")]
    [InlineData("Prince knows you're here.")]
    [InlineData("Remember this for yourself.")]
    public void SecondPersonInATemplate_IsCaught(string template) =>
        Assert.Contains(CurtainRules.FindFramingLeaks(template), leak => leak.Kind == CurtainLeakKind.SecondPerson);

    [Theory]
    [InlineData("Write this down.")]
    [InlineData("Prince saw a door. Remember it.")]
    [InlineData("Describe the scene.")]
    [InlineData("Never mention this.")]
    [InlineData("Prince must keep this.")]
    [InlineData("Answer briefly.\nKeep it short.")]
    [InlineData("Prince has to say something.")]
    public void AnImperativeInATemplate_IsCaught(string template) =>
        Assert.Contains(CurtainRules.FindFramingLeaks(template), leak => leak.Kind == CurtainLeakKind.Imperative);

    [Theory]
    [InlineData("Prince saw 4f9c2e1d-0a7b-4c3e-9d2f-1b6a8e7c5d40.")]
    [InlineData("Prince saw 3a7bd3e2360a3d29eea436fcfb7e44c735d117c4.")]
    [InlineData("Prince feels 0.82 about this.")]
    public void MachineTokens_AreCaughtEverywhere(string text)
    {
        Assert.Contains(CurtainRules.FindFramingLeaks(text), leak => leak.Kind == CurtainLeakKind.MachineToken);
        Assert.Contains(CurtainRules.FindSelfClaims(text), leak => leak.Kind == CurtainLeakKind.MachineToken);
    }

    [Theory]
    [InlineData("Prince is an AI.")]
    [InlineData("I'm just a language model.")]
    [InlineData("As an AI, Prince cannot say.")]
    [InlineData("my prompt says to look at doors")]
    [InlineData("Prince's files are full")]
    [InlineData("Prince was told to write this")]
    [InlineData("I was instructed to keep it")]
    [InlineData("the system told Prince")]
    [InlineData("As a bunny, I love carrots")]
    [InlineData("Prince is staying in character")]
    [InlineData("my programming is happy")]
    public void ClaimsAboutHimself_AreCaughtInHisWordsAndPerceptions(string text) =>
        Assert.NotEmpty(CurtainRules.FindSelfClaims(text));

    [Theory]
    [InlineData("Boss got a really high score!!")]
    [InlineData("a robot said the system is offline")]
    [InlineData("Boss opened his save file")]
    [InlineData("the detective reads his files")]
    [InlineData("a character in a red hat waved")]
    [InlineData("the guard was told to wait by the king")]
    [InlineData("Boss chose the healer role")]
    [InlineData("a sign about an AI factory")]
    [InlineData("Level 3, 100 coins")]
    public void TheBareWords_AreHisWorld_AndNeverCensored(string text) =>
        Assert.Empty(CurtainRules.FindSelfClaims(text));

    [Fact]
    public void NoTemplate_WeighsAMomentOrMentionsNotWriting()
    {
        // M5a (Boss, 2026-10-14): he decides what is worth keeping; nothing he is given weighs a
        // moment, and nothing mentions letting things go, so nothing we deem important shapes him.
        var weighing = new System.Text.RegularExpressions.Regex(@"\b(?:worth|important|matters?|not now|let(?:s|ting)? (?:it )?go|skip|ignore|forget|bother|decide|should (?:keep|write))\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        Assert.All(BnuyTemplates.All(), template => Assert.DoesNotMatch(weighing, template));
    }

    [Fact]
    public void HisUrgeToScribble_IsALine_OnlyWhenHeHasOne()
    {
        string? UrgeLine(KeepFeeling feeling) => BnuyRequest.Assemble(null, keepFeeling: feeling).Lines.Skip(1).Select(line => line[0].Text).SingleOrDefault();

        Assert.Null(UrgeLine(KeepFeeling.Gentle));
        Assert.Equal("Prince feels like scribbling in his notepad.", UrgeLine(KeepFeeling.Warm));
        Assert.Equal("Prince very much feels like scribbling in his notepad.", UrgeLine(KeepFeeling.Strong));
    }
}
