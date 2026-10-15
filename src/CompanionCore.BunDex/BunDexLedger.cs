using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanionCore.BunDex;

internal enum LedgerKind
{
    FolderCreated = 1,
    PageCreated = 2,
    PageAdded = 3,
    FolderMoved = 4,
    PageMoved = 5,
    /// <summary>Retired in M1c (no folder is ever a role's home); old entries are read and ignored.</summary>
    RoleRegistered = 6,
    KeepDeclined = 7,

    /// <summary>A play session began (M4a): the session clock for remembering.</summary>
    SessionBegan = 8,

    /// <summary>A page came to mind (M4a): remembering restarts its clock.</summary>
    PageRecalled = 9,

    /// <summary>He expressed something about a page (M5b): an experience of it, in his own words.</summary>
    Experienced = 10,
}

internal sealed record PageTagsWire(BunDexRole Role, MemoryLayer Layer, string? Game, string? Save, string[] Subjects, DateTimeOffset CreatedAtUtc, string? Where = null, string[]? Sources = null)
{
    internal static PageTagsWire From(PageTags tags) =>
        new(tags.Role, tags.Layer, tags.Game, tags.Save, [.. tags.Subjects], tags.CreatedAtUtc, tags.Where, tags.Sources is { Count: > 0 } sources ? [.. sources] : null);

    internal PageTags ToTags() => new(Role, Layer, Game, Save, Subjects, CreatedAtUtc, Where, Sources ?? []);
}

/// <summary>One committed ledger line. <see cref="Hash"/> chains to the previous line.</summary>
internal sealed record LedgerEntry(
    long Seq,
    DateTimeOffset At,
    LedgerKind Kind,
    Guid? FolderId = null,
    Guid? ParentId = null,
    string? Name = null,
    Guid? PageId = null,
    string? Text = null,
    PageTagsWire? Tags = null,
    BunDexRole? Role = null,
    string Prev = "",
    string? Hash = null,
    string[]? Sources = null,
    string? Title = null,
    double? Gain = null,
    FeelingKind? Felt = null,
    double? FeltStrength = null);

/// <summary>
/// The BrainCase's append-only BunDex ledger: JSON lines in numbered segments, each line
/// hash-chained to the one before. A line is committed only once it is complete and flushed.
/// A torn final line (never committed) is dropped on open; any other break fails closed.
/// </summary>
internal sealed class BunDexLedger
{
    internal const long MaximumSegmentBytes = 4L * 1024 * 1024;
    internal const string SegmentPrefix = "ledger-v1-";
    internal const string SegmentExtension = ".jsonl";
    internal static readonly string GenesisHash = new('0', 64);

    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _root;
    private int _segment;
    private long _segmentBytes;

    private BunDexLedger(string root)
    {
        _root = root;
    }

    internal IReadOnlyList<LedgerEntry> Entries => _entries;

    internal bool Damaged { get; private set; }

    internal bool TornTailDropped { get; private set; }

    internal string LastHash { get; private set; } = GenesisHash;

    private readonly List<LedgerEntry> _entries = [];

    internal static string SegmentName(int number) =>
        SegmentPrefix + number.ToString("D6", CultureInfo.InvariantCulture) + SegmentExtension;

    internal static bool TryParseSegmentName(string fileName, out int number)
    {
        number = 0;
        return fileName.StartsWith(SegmentPrefix, StringComparison.Ordinal)
            && fileName.EndsWith(SegmentExtension, StringComparison.Ordinal)
            && fileName.Length == SegmentPrefix.Length + 6 + SegmentExtension.Length
            && int.TryParse(fileName.AsSpan(SegmentPrefix.Length, 6), NumberStyles.None, CultureInfo.InvariantCulture, out number)
            && number > 0;
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
            if (TryParseSegmentName(System.IO.Path.GetFileName(path), out var number))
            {
                segments.Add((number, path));
            }
        }

        segments.Sort((left, right) => left.Item1.CompareTo(right.Item1));
        return segments;
    }

    internal static BunDexLedger Open(string root)
    {
        var ledger = new BunDexLedger(root);
        ledger.Load();
        return ledger;
    }

    private void Load()
    {
        var segments = Segments(_root);
        var previous = GenesisHash;
        long expectedSeq = 1;
        for (var index = 0; index < segments.Count; index++)
        {
            var (number, path) = segments[index];
            if (number != index + 1)
            {
                Damaged = true;
                return;
            }

            var bytes = File.ReadAllBytes(path);
            var last = index == segments.Count - 1;
            var start = 0;
            while (start < bytes.Length)
            {
                var newline = Array.IndexOf(bytes, (byte)'\n', start);
                if (newline < 0)
                {
                    if (!last)
                    {
                        Damaged = true;
                        return;
                    }

                    // A torn final line was never committed: drop it so the next append starts clean.
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        stream.SetLength(start);
                        stream.Flush(flushToDisk: true);
                    }

                    TornTailDropped = true;
                    break;
                }

                var entry = Parse(bytes.AsSpan(start, newline - start));
                if (entry is null || entry.Seq != expectedSeq || entry.Prev != previous || entry.Hash != ComputeHash(entry))
                {
                    Damaged = true;
                    return;
                }

                _entries.Add(entry);
                previous = entry.Hash!;
                expectedSeq++;
                start = newline + 1;
            }

            _segment = number;
            _segmentBytes = new FileInfo(path).Length;
        }

        LastHash = previous;
    }

    private static LedgerEntry? Parse(ReadOnlySpan<byte> line)
    {
        try
        {
            return JsonSerializer.Deserialize<LedgerEntry>(line, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string ComputeHash(LedgerEntry entry)
    {
        var canonical = JsonSerializer.SerializeToUtf8Bytes(entry with { Hash = null }, SerializerOptions);
        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    /// <summary>Commits one entry: complete line, appended and flushed to disk.</summary>
    internal LedgerEntry Append(LedgerEntry draft)
    {
        if (Damaged)
        {
            throw new BunDexException("The BunDex ledger failed verification; it is read-only until Bnuy Repairs.");
        }

        var entry = draft with { Seq = _entries.Count + 1, Prev = LastHash, Hash = null };
        entry = entry with { Hash = ComputeHash(entry) };
        var line = JsonSerializer.SerializeToUtf8Bytes(entry, SerializerOptions);
        var bytes = new byte[line.Length + 1];
        line.CopyTo(bytes, 0);
        bytes[^1] = (byte)'\n';

        Directory.CreateDirectory(_root);
        if (_segment == 0 || _segmentBytes + bytes.Length > MaximumSegmentBytes)
        {
            _segment++;
            _segmentBytes = 0;
        }

        using (var stream = new FileStream(System.IO.Path.Combine(_root, SegmentName(_segment)), FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        _segmentBytes += bytes.Length;
        _entries.Add(entry);
        LastHash = entry.Hash!;
        return entry;
    }
}
