using System.Text;

namespace CompanionCore.BunDex;

public enum BunDexLedgerRestore
{
    /// <summary>No BunDex ledger in the Vault and none live: nothing to do.</summary>
    None = 1,

    /// <summary>The live ledger verified and already holds everything the Vault holds.</summary>
    Kept = 2,

    /// <summary>The live ledger was preserved and rebuilt from the Vault plus every valid newer entry.</summary>
    Restored = 3,

    /// <summary>The Vault's ledger copy failed verification; the live ledger was left as it is.</summary>
    VaultUnusable = 4,
}

public sealed record BunDexLedgerRestoreReport(BunDexLedgerRestore Outcome, long VaultEntries, long EntriesAfterRestore, string? PreservedDirectory);

/// <summary>
/// Bnuy Repairs for the BunDex ledger. The live ledger is kept whenever it verifies and contains
/// the Vault's copy. Otherwise it is moved aside (never deleted) and rebuilt from the Vault,
/// extended by every valid live entry that continues the Vault's chain, so nothing newer than
/// the backup is lost when it can be kept. The BunDex pages are then rebuilt on open.
/// </summary>
internal static class BunDexLedgerRepair
{
    /// <summary>The ledger segments as backed up: complete lines only, in order.</summary>
    internal static IReadOnlyList<(string SegmentName, byte[] Bytes)> SnapshotSegments(BunDexLocation location)
    {
        var snapshot = new List<(string, byte[])>();
        foreach (var (number, path) in BunDexLedger.Segments(location.LedgerRoot))
        {
            var bytes = File.ReadAllBytes(path);
            var end = Array.LastIndexOf(bytes, (byte)'\n') + 1;
            snapshot.Add((BunDexLedger.SegmentName(number), bytes.AsSpan(0, end).ToArray()));
        }

        return snapshot;
    }

    internal static BunDexLedgerRestoreReport Restore(BunDexLocation location, IReadOnlyList<(string SegmentName, byte[] Bytes)> vaultSegments)
    {
        var vault = Parse(vaultSegments.OrderBy(segment => segment.SegmentName, StringComparer.Ordinal).Select(segment => segment.Bytes), out var vaultValid);
        if (!vaultValid)
        {
            return new BunDexLedgerRestoreReport(BunDexLedgerRestore.VaultUnusable, 0, 0, null);
        }

        var liveSegments = BunDexLedger.Segments(location.LedgerRoot);
        if (vault.Count == 0 && liveSegments.Count == 0)
        {
            return new BunDexLedgerRestoreReport(BunDexLedgerRestore.None, 0, 0, null);
        }

        var live = BunDexLedger.Open(location.LedgerRoot);
        if (!live.Damaged
            && live.Entries.Count >= vault.Count
            && (vault.Count == 0 || live.Entries[vault.Count - 1].Hash == vault[^1].Hash))
        {
            return new BunDexLedgerRestoreReport(BunDexLedgerRestore.Kept, vault.Count, live.Entries.Count, null);
        }

        // The longest valid prefix of the live ledger, read independently of the Vault.
        var livePrefix = Parse(liveSegments.Select(segment => File.ReadAllBytes(segment.Path)), out _);
        var merged = new List<LedgerEntry>(vault);
        if (livePrefix.Count > vault.Count && (vault.Count == 0 || livePrefix[vault.Count - 1].Hash == vault[^1].Hash))
        {
            merged.AddRange(livePrefix.Skip(vault.Count));
        }

        string? preserved = null;
        if (Directory.Exists(location.LedgerRoot))
        {
            Directory.CreateDirectory(location.DamagedLedgerPath);
            preserved = Path.Combine(location.DamagedLedgerPath, $"{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}");
            Directory.Move(location.LedgerRoot, preserved);
        }

        var rebuilt = BunDexLedger.Open(location.LedgerRoot);
        foreach (var entry in merged)
        {
            rebuilt.Append(entry);
        }

        return new BunDexLedgerRestoreReport(BunDexLedgerRestore.Restored, vault.Count, rebuilt.Entries.Count, preserved);
    }

    /// <summary>Entries from consecutive segments up to the first break; <paramref name="valid"/> is false on any break.</summary>
    private static List<LedgerEntry> Parse(IEnumerable<byte[]> segments, out bool valid)
    {
        var entries = new List<LedgerEntry>();
        var previous = BunDexLedger.GenesisHash;
        valid = true;
        foreach (var bytes in segments)
        {
            foreach (var line in Encoding.UTF8.GetString(bytes).Split('\n'))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                LedgerEntry? entry;
                try
                {
                    entry = System.Text.Json.JsonSerializer.Deserialize<LedgerEntry>(line, BunDexLedger.SerializerOptions);
                }
                catch (System.Text.Json.JsonException)
                {
                    entry = null;
                }

                if (entry is null || entry.Seq != entries.Count + 1 || entry.Prev != previous || entry.Hash != BunDexLedger.ComputeHash(entry))
                {
                    valid = false;
                    return entries;
                }

                entries.Add(entry);
                previous = entry.Hash!;
            }
        }

        return entries;
    }
}
