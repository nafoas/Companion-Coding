using CompanionCore.Curtain;

namespace CompanionCore.BunDex;

/// <summary>
/// What looking something up was like: his rummage, step by step, as he lived it, and the page
/// he found, if he found one. Nothing about how the subconscious helped is in here.
/// </summary>
public sealed class Rummage
{
    internal Rummage(IReadOnlyList<LookStep> steps, Remembered? found, bool sure)
    {
        Steps = steps;
        Found = found;
        Sure = sure;
    }

    public IReadOnlyList<LookStep> Steps { get; }

    /// <summary>The page he actually found, as remembering it; null when he did not find it.</summary>
    public Remembered? Found { get; }

    /// <summary>Whether he is sure it is the one, or only thinks so.</summary>
    public bool Sure { get; }
}

/// <summary>
/// The Bnuy lookup (M4b, Boss 2026-10-13): "okay... now how find dis...?" The subconscious knows
/// which page truly fits, but never points there. Places come to mind from his own cues: his place
/// names, the lines he wrote about them, his page names and where they happened (weaker as they
/// fade), and where he wrote lately. He looks over each place's pages himself, one place at a time
/// along his own path, and only finds a page that truly fits. Nothing here is random and nothing
/// counts mistakes: the same notebook and question always give the same rummage, and anything
/// imperfect comes only from his own filing, his cues, and fading.
/// </summary>
public sealed partial class BunDexStore
{
    internal const double SureFit = 0.8;
    internal const double ThinksFit = 0.6;
    internal const int MaximumPlaces = 5;

    /// <summary>He looks something up in his BunDex: what he is after, in plain words.</summary>
    public Rummage LookFor(string whatHeIsAfter)
    {
        ArgumentNullException.ThrowIfNull(whatHeIsAfter);
        return Look(whatHeIsAfter, null, Membrane.StartsLooking(), target: null);
    }

    /// <summary>
    /// The lookup itself. With a <paramref name="target"/> (a page that rang a bell), that page is
    /// what he is after; otherwise every page that truly fits what he is after is.
    /// </summary>
    internal Rummage Look(string cue, string? where, LookStep start, Guid? target)
    {
        lock (_gate)
        {
            var steps = new List<LookStep> { start };
            var words = Words(cue);
            var cues = new HashSet<string>(words, StringComparer.Ordinal);
            cues.UnionWith(Words(where ?? string.Empty));
            var now = Now();

            // What truly fits: the subconscious knows, and it decides only what he can find.
            // Weighed by how distinctive each word is to him: familiar words are background.
            var wanted = Weight(words);
            var fits = new Dictionary<Guid, double>();
            foreach (var page in _pages.Values)
            {
                var fit = target is { } bell
                    ? page.Id == bell ? 1.0 : 0.0
                    : wanted < MeaningfulCue ? 0.0 : Weight(words.Where(page.Words.Contains)) / wanted;
                fits[page.Id] = fit;
            }

            if (!fits.Values.Any(fit => fit >= ThinksFit))
            {
                steps.Add(target is null && wanted >= MeaningfulCue ? Membrane.NotWrittenDown() : Membrane.CantFind());
                return new Rummage(steps, null, false);
            }

            var search = new Search(this, steps, cues, fits, now);
            var top = Places(null).ToList();
            foreach (var place in search.Ordered(top.Where(place => search.Pull(place) > 0)))
            {
                if (search.Visit(place))
                {
                    break;
                }
            }

            // Nothing his cues point to: the next place to come to mind is where he wrote lately.
            while (!search.Done)
            {
                var lately = AllPlaces()
                    .Where(place => !search.Visited.Contains(place) && _pages.Values.Any(page => page.FolderId == place))
                    .OrderByDescending(search.LastWritten)
                    .ThenBy(PlaceName, StringComparer.Ordinal)
                    .Take(1)
                    .ToList();
                if (lately.Count == 0 || search.Visit(lately[0]))
                {
                    break;
                }
            }

            if (search.Found is null)
            {
                steps.Add(Membrane.CantFind());
            }

            return new Rummage(steps, search.Found, search.Sure);
        }
    }

    /// <summary>The places directly inside a place: his folders, and the front of his BunDex at the top when it has pages.</summary>
    private IEnumerable<Guid?> Places(Guid? parent)
    {
        foreach (var folder in _folders.Values.Where(folder => folder.ParentId == parent))
        {
            yield return folder.Id;
        }

        if (parent is null && _pages.Values.Any(page => page.FolderId is null))
        {
            yield return null;
        }
    }

    private IEnumerable<Guid?> AllPlaces() =>
        _folders.Keys.Select(id => (Guid?)id).Append(null);

    private string PlaceName(Guid? place) => place is { } id ? _folders[id].Name : string.Empty;

    private sealed class Search(BunDexStore store, List<LookStep> steps, HashSet<string> cues, Dictionary<Guid, double> fits, DateTimeOffset now)
    {
        private readonly Dictionary<Guid, double> _pulls = [];
        private int _checked;

        public HashSet<Guid?> Visited { get; } = [];

        public Remembered? Found { get; private set; }

        public bool Sure { get; private set; }

        public bool Done => Found is not null || _checked == MaximumPlaces;

        /// <summary>How strongly a place comes to mind: its own name and his line about it, plus the strongest pull inside it.</summary>
        public double Pull(Guid? place)
        {
            if (_pulls.TryGetValue(place ?? Guid.Empty, out var known))
            {
                return known;
            }

            var own = 0.0;
            if (place is { } id)
            {
                var folder = store._folders[id];
                own = (2 * Shared(folder.Name)) + Shared(folder.About ?? string.Empty);
            }

            var inside = store.Places(place).Where(child => child is not null).Select(Pull)
                .Concat(store._pages.Values.Where(page => page.FolderId == place).Select(PageCue))
                .DefaultIfEmpty(0)
                .Max();
            return _pulls[place ?? Guid.Empty] = own + inside;
        }

        public DateTimeOffset LastWritten(Guid? place)
        {
            var latest = store._pages.Values.Where(page => page.FolderId == place).Select(page => page.LastWrittenAt).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
            foreach (var child in store.Places(place).Where(child => child is not null))
            {
                var inner = LastWritten(child);
                latest = inner > latest ? inner : latest;
            }

            return latest;
        }

        public IEnumerable<Guid?> Ordered(IEnumerable<Guid?> places) => places
            .OrderByDescending(Pull)
            .ThenByDescending(LastWritten)
            .ThenBy(store.PlaceName, StringComparer.Ordinal);

        /// <summary>He looks in one place: over its pages, then deeper along his own path. True when the search is over.</summary>
        public bool Visit(Guid? place)
        {
            if (Done)
            {
                return true;
            }

            _checked++;
            Visited.Add(place);
            var name = place is { } id ? store._folders[id].Name : null;
            Add(Membrane.Hunch(name));

            var here = store._pages.Values.Where(page => page.FolderId == place)
                .OrderByDescending(page => fits[page.Id])
                .ThenByDescending(page => page.LastWrittenAt)
                .ThenBy(page => page.Title, StringComparer.Ordinal)
                .ToList();
            if (here.FirstOrDefault(page => fits[page.Id] >= ThinksFit) is { } match
                && Membrane.Remember(store.FolderNames(match.FolderId), match.Title, match.Tags.Where, match.Text.ToString()) is { } remembered)
            {
                Sure = fits[match.Id] >= SureFit;
                Found = remembered;
                steps.Add(Membrane.Found(remembered, Sure));
                store.Remembering(match.Id, now);
                return true;
            }

            if (here.FirstOrDefault(page => fits[page.Id] > 0) is { } similar)
            {
                Add(Membrane.ADifferentOne(similar.Title));
            }

            var deeper = Ordered(store.Places(place).Where(child => child is not null && Pull(child) > 0)).ToList();
            foreach (var child in deeper)
            {
                if (Visit(child))
                {
                    return true;
                }
            }

            if (deeper.Count == 0)
            {
                Add(Membrane.NotThere(name));
            }

            return Done;
        }

        private double PageCue(PageState page)
        {
            var clarity = store.ClarityOf(page, now, null) switch
            {
                Clarity.Clear => 1.0,
                Clarity.RingsABell => 0.5,
                _ => 0.25,
            };
            return (Shared(page.Title) + Shared(page.Tags.Where ?? string.Empty)) * clarity;
        }

        private double Shared(string text) => store.Weight(Words(text).Where(cues.Contains));

        private void Add(LookStep? step)
        {
            if (step is not null)
            {
                steps.Add(step);
            }
        }
    }
}
