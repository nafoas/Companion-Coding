using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CompanionCore.Api;

namespace CompanionCore.Sightings;

public sealed record SightingOpenReport(long Sightings, bool TornTailDropped, int UnreadableLines);

/// <summary>
/// The BrainCase's complete, factual record of everything Prince's eyes saw (plan items 1 and
/// 7a). Append-only JSON lines in numbered segments; a line is committed only once it is complete
/// and flushed, and a torn final line is dropped on open. From the record it keeps two things for
/// the subconscious: impressions ("saw dis a few times") and familiarity (getting used to things).
/// Nothing here is ever shown to Prince directly; what he feels of it crosses the membrane.
/// </summary>
public sealed class SightingStore
{
    internal const long MaximumSegmentBytes = 4L * 1024 * 1024;
    internal const string SegmentPrefix = "sightings-v1-";
    internal const string SegmentExtension = ".jsonl";
    internal const int MaximumWords = 400;

    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SightingLocation _location;
    private readonly object _gate = new();
    private readonly Familiarity _familiarity = new();
    private readonly Bravery _bravery = new();
    private readonly Dictionary<(string Game, string Thing), List<Impression>> _impressions = [];
    private int _segment;
    private long _segmentBytes;

    private SightingStore(SightingLocation location)
    {
        _location = location;
    }

    public SightingLocation Location => _location;

    public long Count { get; private set; }

    public static SightingStore Open(SightingLocation location, out SightingOpenReport report)
    {
        ArgumentNullException.ThrowIfNull(location);
        var store = new SightingStore(location);
        report = store.Load();
        return store;
    }

    /// <summary>Something the eyes noticed: recorded, then how new it felt (getting used to things).</summary>
    public double See(Sighting sighting)
    {
        ArgumentNullException.ThrowIfNull(sighting);
        lock (_gate)
        {
            var bounded = Bound(sighting);
            Append(bounded);
            return Apply(bounded);
        }
    }

    /// <summary>Records a sighting that only adds to impressions (a memory proposal).</summary>
    public void Record(Sighting sighting)
    {
        ArgumentNullException.ThrowIfNull(sighting);
        lock (_gate)
        {
            var bounded = Bound(sighting);
            Append(bounded);
            Apply(bounded);
        }
    }

    /// <summary>How new something would feel right now, without recording it.</summary>
    public double NoveltyOf(Sighting sighting)
    {
        ArgumentNullException.ThrowIfNull(sighting);
        lock (_gate)
        {
            return _bravery.Calm(Bound(sighting), _familiarity.NoveltyOf(Bound(sighting)));
        }
    }

    /// <summary>A threat being fought and how it went (M3b). It can only make Prince braver and surer of Boss.</summary>
    public void Encounter(string game, string? situation, string threat, EncounterOutcome outcome, DateTimeOffset at)
    {
        lock (_gate)
        {
            Record(new Sighting(at, game, situation, threat, ObservationKind.Creature, Danger: false, threat, SightingSource.Encounter, Outcome: outcome));
        }
    }

    /// <summary>How Prince feels about a threat in this game, as feelings only (M3b).</summary>
    public IReadOnlyList<Curtain.Feeling> FeelingsAbout(string game, string threat)
    {
        lock (_gate)
        {
            var normalized = Normalize(game);
            return _bravery.IsThreat(normalized, threat)
                ? Curtain.Membrane.FeelingsAbout(_bravery.Fear(normalized, threat), _bravery.Trust(normalized, threat) >= Bravery.BossHasGotItAt)
                : [];
        }
    }

    internal double Trust(string game, string threat)
    {
        lock (_gate)
        {
            return _bravery.Trust(Normalize(game), threat);
        }
    }

    internal double Fear(string game, string threat)
    {
        lock (_gate)
        {
            return _bravery.Fear(Normalize(game), threat);
        }
    }

    /// <summary>The growing impression of a thing in a game; with <paramref name="words"/>, the one those words belong to.</summary>
    public Impression? ImpressionOf(string game, string thing, string? words = null)
    {
        lock (_gate)
        {
            if (!_impressions.TryGetValue(Key(game, thing), out var list))
            {
                return null;
            }

            return words is null ? list.MaxBy(impression => impression.Count) : list.LastOrDefault(impression => Likeness.Same(impression.LatestWords, words));
        }
    }

    public IReadOnlyList<Impression> Impressions(string game)
    {
        lock (_gate)
        {
            return [.. _impressions.Where(pair => string.Equals(pair.Key.Game, Normalize(game), StringComparison.Ordinal)).SelectMany(pair => pair.Value)];
        }
    }

    internal static IReadOnlyList<(int Number, string Path)> Segments(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var segments = new List<(int, string)>();
        foreach (var path in Directory.EnumerateFiles(root))
        {
            var name = Path.GetFileName(path);
            if (name.StartsWith(SegmentPrefix, StringComparison.Ordinal) && name.EndsWith(SegmentExtension, StringComparison.Ordinal)
                && int.TryParse(name.AsSpan(SegmentPrefix.Length, name.Length - SegmentPrefix.Length - SegmentExtension.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number > 0)
            {
                segments.Add((number, path));
            }
        }

        segments.Sort((left, right) => left.Item1.CompareTo(right.Item1));
        return segments;
    }

    internal static string SegmentName(int number) =>
        SegmentPrefix + number.ToString("D6", CultureInfo.InvariantCulture) + SegmentExtension;

    private SightingOpenReport Load()
    {
        var segments = Segments(_location.RootPath);
        var torn = false;
        var unreadable = 0;
        for (var index = 0; index < segments.Count; index++)
        {
            var (number, path) = segments[index];
            var bytes = File.ReadAllBytes(path);
            var start = 0;
            while (start < bytes.Length)
            {
                var newline = Array.IndexOf(bytes, (byte)'\n', start);
                if (newline < 0)
                {
                    if (index == segments.Count - 1)
                    {
                        // A torn final line was never committed: drop it so the next append starts clean.
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
                        stream.SetLength(start);
                        stream.Flush(flushToDisk: true);
                        torn = true;
                    }
                    else
                    {
                        unreadable++;
                    }

                    break;
                }

                Sighting? sighting = null;
                try
                {
                    sighting = JsonSerializer.Deserialize<Sighting>(bytes.AsSpan(start, newline - start), SerializerOptions);
                }
                catch (JsonException)
                {
                }

                if (sighting is null || string.IsNullOrEmpty(sighting.Game) || string.IsNullOrEmpty(sighting.Thing))
                {
                    unreadable++;
                }
                else
                {
                    Apply(sighting);
                }

                start = newline + 1;
            }

            _segment = number;
            _segmentBytes = new FileInfo(path).Length;
        }

        return new SightingOpenReport(Count, torn, unreadable);
    }

    private double Apply(Sighting sighting)
    {
        Count++;
        var novelty = sighting.Source == SightingSource.Observation ? _bravery.Calm(sighting, _familiarity.Observe(sighting)) : 0;
        _bravery.Observe(sighting);
        if (sighting.Source == SightingSource.Encounter)
        {
            // A fight is part of the record and of his courage, not another "saw this again".
            return novelty;
        }

        var key = Key(sighting.Game, sighting.Thing);
        if (!_impressions.TryGetValue(key, out var list))
        {
            _impressions[key] = list = [];
        }

        var index = list.FindLastIndex(impression => sighting.Source == SightingSource.Observation || Likeness.Same(impression.LatestWords, sighting.Words));
        if (index < 0)
        {
            list.Add(new Impression(Normalize(sighting.Game), sighting.Thing, 1, sighting.At, sighting.At, sighting.Words));
        }
        else
        {
            var impression = list[index];
            list[index] = impression with
            {
                Count = impression.Count + 1,
                First = sighting.At < impression.First ? sighting.At : impression.First,
                Last = sighting.At > impression.Last ? sighting.At : impression.Last,
                LatestWords = sighting.Words,
            };
        }

        return novelty;
    }

    private void Append(Sighting sighting)
    {
        var line = JsonSerializer.SerializeToUtf8Bytes(sighting, SerializerOptions);
        var bytes = new byte[line.Length + 1];
        line.CopyTo(bytes, 0);
        bytes[^1] = (byte)'\n';
        Directory.CreateDirectory(_location.RootPath);
        if (_segment == 0 || _segmentBytes + bytes.Length > MaximumSegmentBytes)
        {
            _segment++;
            _segmentBytes = 0;
        }

        using (var stream = new FileStream(Path.Combine(_location.RootPath, SegmentName(_segment)), FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        _segmentBytes += bytes.Length;
    }

    private static Sighting Bound(Sighting sighting) => sighting with
    {
        Game = Normalize(sighting.Game),
        Words = sighting.Words.Length <= MaximumWords ? sighting.Words : sighting.Words[..MaximumWords],
    };

    private static (string, string) Key(string game, string thing) => (Normalize(game), thing.Trim().ToLowerInvariant());

    private static string Normalize(string game) => string.IsNullOrWhiteSpace(game) ? "unknown" : game.Trim();
}
