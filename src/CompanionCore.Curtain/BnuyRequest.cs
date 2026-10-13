using System.Globalization;
using System.Text;

namespace CompanionCore.Curtain;

/// <summary>What a part of a Bnuy request is, so every part is held to its own rule.</summary>
public enum BnuyPartKind
{
    /// <summary>Our fixed words (templates): zero tolerance.</summary>
    Framing = 1,

    /// <summary>What he perceives in plain words: only self-claims and machinery are leaks.</summary>
    Perceived = 2,

    /// <summary>Text from his world, verbatim inside quotes: data, never part of his inner voice.</summary>
    Quoted = 3,

    /// <summary>His own words and names: only self-claims and machinery are leaks.</summary>
    HisOwn = 4,
}

public sealed record BnuyPart(BnuyPartKind Kind, string Text);

/// <summary>One folder or page in his notebook, by his own names, for a request.</summary>
public sealed record NotebookLine(int Depth, string Name, string? About, bool IsPlace);

/// <summary>
/// Everything one Bnuy request carries (item 8a), assembled only from membrane experiences and
/// his own words, from inside him: his nature, where he is, what he senses (each with how he
/// knows it), what comes to mind, his notebook, and how things feel. Technical requirements
/// travel separately as <see cref="AnswerShape"/>, a structured schema, never as a voice. There
/// is no public constructor: <see cref="Assemble"/> is the only way to make one.
/// </summary>
public sealed class BnuyRequest
{
    private BnuyRequest(IReadOnlyList<IReadOnlyList<BnuyPart>> lines, string? answerShape)
    {
        Lines = lines;
        Parts = [.. lines.SelectMany(line => line)];
        AnswerShape = answerShape;
        Text = string.Join('\n', lines.Select(line => string.Join(' ', line.Select(part => part.Text))));
    }

    /// <summary>The request line by line; each line is one or more parts.</summary>
    public IReadOnlyList<IReadOnlyList<BnuyPart>> Lines { get; }

    public IReadOnlyList<BnuyPart> Parts { get; }

    /// <summary>The whole request as one text, line by line.</summary>
    public string Text { get; }

    /// <summary>The shape of the answer, as a JSON schema; never part of what he perceives.</summary>
    public string? AnswerShape { get; }

    public static BnuyRequest Assemble(
        Place? where,
        IEnumerable<Sense>? senses = null,
        IEnumerable<Remembered>? comesToMind = null,
        IEnumerable<NotebookLine>? notebook = null,
        KeepFeeling? keepFeeling = null,
        bool tidying = false,
        string? answerShape = null,
        IEnumerable<Feeling>? feelings = null,
        IEnumerable<LookStep>? rummage = null)
    {
        var lines = new List<IReadOnlyList<BnuyPart>>();
        lines.Add([new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.Nature)]);
        if (where is not null)
        {
            lines.Add(Slotted(BnuyTemplates.Where, (where.Name, BnuyPartKind.Perceived)));
        }

        foreach (var sense in senses ?? [])
        {
            lines.Add(PartsOf(sense));
        }

        var remembered = (comesToMind ?? []).ToList();
        if (remembered.Count > 0)
        {
            lines.Add([new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.ComesToMind)]);
            foreach (var page in remembered)
            {
                lines.Add(PageLine(page));
            }
        }

        if (notebook is not null)
        {
            var entries = notebook.ToList();
            lines.Add([new BnuyPart(BnuyPartKind.Framing, entries.Count == 0 ? BnuyTemplates.EmptyNotebook : BnuyTemplates.Notebook)]);
            foreach (var line in entries)
            {
                var indent = string.Concat(Enumerable.Repeat(BnuyTemplates.Indent, line.Depth + 1));
                var about = line.About is null ? string.Empty : string.Format(CultureInfo.InvariantCulture, BnuyTemplates.NotebookAbout, line.About);
                lines.Add([new BnuyPart(BnuyPartKind.HisOwn, line.IsPlace
                    ? string.Format(CultureInfo.InvariantCulture, BnuyTemplates.NotebookPlace, indent, line.Name, about)
                    : string.Format(CultureInfo.InvariantCulture, BnuyTemplates.NotebookPage, indent, line.Name))]);
            }
        }

        if (keepFeeling is { } feeling)
        {
            lines.Add([new BnuyPart(BnuyPartKind.Framing, feeling switch
            {
                KeepFeeling.Strong => BnuyTemplates.KeepStrong,
                KeepFeeling.Warm => BnuyTemplates.KeepWarm,
                _ => BnuyTemplates.KeepGentle,
            })]);
        }

        foreach (var felt in feelings ?? [])
        {
            lines.Add([new BnuyPart(BnuyPartKind.Framing, felt.Line)]);
        }

        foreach (var step in rummage ?? [])
        {
            lines.AddRange(LinesOf(step));
        }

        if (tidying)
        {
            lines.Add([new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.TidyFeeling)]);
        }

        return new BnuyRequest(lines, answerShape);
    }

    /// <summary>Test seam: a request from given lines, to prove each part is held to its own rule.</summary>
    internal static BnuyRequest FromLines(IReadOnlyList<IReadOnlyList<BnuyPart>> lines) => new(lines, null);

    /// <summary>Every leak in the request, each part held to its own rule.</summary>
    public IReadOnlyList<CurtainLeak> FindLeaks()
    {
        var leaks = new List<CurtainLeak>();
        foreach (var part in Parts)
        {
            leaks.AddRange(part.Kind switch
            {
                BnuyPartKind.Framing => CurtainRules.FindFramingLeaks(part.Text),
                BnuyPartKind.Quoted => [],
                _ => CurtainRules.FindSelfClaims(part.Text),
            });
        }

        return leaks;
    }

    /// <summary>One sense as one line: our fixed words as framing, his perception or quoted world text in its slot.</summary>
    internal static IReadOnlyList<BnuyPart> PartsOf(Sense sense) => sense.Kind switch
    {
        SenseKind.Read when sense.Words.Length > 0 =>
            [.. Slotted(BnuyTemplates.ReadOn, (sense.Words, BnuyPartKind.Perceived)), Quoted(sense)],
        SenseKind.Read => [new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.Read), Quoted(sense)],
        SenseKind.BossSaid => [new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.BossSaid), Quoted(sense)],
        SenseKind.Saw => Slotted(BnuyTemplates.Saw, (sense.Words, BnuyPartKind.Perceived)),
        SenseKind.ThinksHeSaw => Slotted(BnuyTemplates.ThinksHeSaw, (sense.Words, BnuyPartKind.Perceived)),
        SenseKind.GuessesHeSaw => Slotted(BnuyTemplates.GuessesHeSaw, (sense.Words, BnuyPartKind.Perceived)),
        SenseKind.SawAgain => Slotted(BnuyTemplates.SawAgain, (sense.Words, BnuyPartKind.Perceived)),
        SenseKind.SawAFewTimes => Slotted(BnuyTemplates.SawAFewTimes, (sense.Words, BnuyPartKind.Perceived)),
        SenseKind.SawLotsOfTimes => Slotted(BnuyTemplates.SawLotsOfTimes, (sense.Words, BnuyPartKind.Perceived)),
        _ => Slotted(BnuyTemplates.Remembers, (sense.Words, BnuyPartKind.Perceived)),
    };

    /// <summary>One rummage step as its lines: our fixed words as framing, his own names in their slots, a found page as his own words.</summary>
    internal static IReadOnlyList<IReadOnlyList<BnuyPart>> LinesOf(LookStep step) => step.Kind switch
    {
        LookStepKind.StartsLooking => [[new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.StartsLooking)]],
        LookStepKind.WantsToRememberMore => [[new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.WantsToRememberMore)]],
        LookStepKind.Hunch when step.Place is null => [[new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.HunchFront)]],
        LookStepKind.Hunch => [Slotted(BnuyTemplates.Hunch, (step.Place, BnuyPartKind.HisOwn))],
        LookStepKind.NotThere when step.Place is null => [[new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.NotAtFront)]],
        LookStepKind.NotThere => [Slotted(BnuyTemplates.NotThere, (step.Place, BnuyPartKind.HisOwn))],
        LookStepKind.ADifferentOne => [Slotted(BnuyTemplates.ADifferentOne, (step.OtherPage!, BnuyPartKind.HisOwn))],
        LookStepKind.Found => [[new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.FoundIt)], PageLine(step.Page!)],
        LookStepKind.ThinksItWas => [[new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.ThinksItWas)], PageLine(step.Page!)],
        LookStepKind.CantFind => [[new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.CantFind)]],
        _ => [[new BnuyPart(BnuyPartKind.Framing, BnuyTemplates.NotWrittenDown)]],
    };

    private static IReadOnlyList<BnuyPart> PageLine(Remembered page)
    {
        var place = page.Place.Count == 0 ? BnuyTemplates.TopOfNotebook : string.Join(BnuyTemplates.PlaceSeparator, page.Place);
        var from = page.Where is null ? string.Empty : string.Format(CultureInfo.InvariantCulture, BnuyTemplates.PageFrom, page.Where);
        return [new BnuyPart(BnuyPartKind.HisOwn, string.Format(CultureInfo.InvariantCulture, BnuyTemplates.Page, page.Page, place, from, page.Glimpse))];
    }

    private static BnuyPart Quoted(Sense sense) => new(BnuyPartKind.Quoted, Membrane.InQuotes(sense.Quoted!));

    /// <summary>
    /// A template with one slot: the words before the slot are framing, the slot keeps its own
    /// kind, and the words after it (usually punctuation) are framing again.
    /// </summary>
    private static IReadOnlyList<BnuyPart> Slotted(string template, (string Text, BnuyPartKind Kind) slot)
    {
        var halves = template.Split("{0}");
        var parts = new List<BnuyPart>();
        if (halves[0].Trim().Length > 0)
        {
            parts.Add(new BnuyPart(BnuyPartKind.Framing, halves[0].TrimEnd()));
        }

        // Trailing punctuation stays attached to the slot so the line reads naturally; it is not words.
        var tail = halves.Length > 1 ? halves[1] : string.Empty;
        parts.Add(new BnuyPart(slot.Kind, slot.Text + tail));
        return parts;
    }
}
