namespace CompanionCore.Sightings;

public enum SightingRestore
{
    /// <summary>No sighting record in the Vault and none live.</summary>
    None = 1,

    /// <summary>A live record exists: it is never overwritten.</summary>
    Kept = 2,

    /// <summary>The live record was missing and was put back from the Vault.</summary>
    Restored = 3,
}

/// <summary>
/// Da Bun Vault's part of the sighting record: complete lines are backed up, and the record is
/// put back only when the live one is missing. A live record is never overwritten or merged.
/// </summary>
internal static class SightingVault
{
    internal static bool IsSegmentName(string name) =>
        name.StartsWith(SightingStore.SegmentPrefix, StringComparison.Ordinal)
        && name.EndsWith(SightingStore.SegmentExtension, StringComparison.Ordinal)
        && name.Length == SightingStore.SegmentPrefix.Length + 6 + SightingStore.SegmentExtension.Length
        && name.AsSpan(SightingStore.SegmentPrefix.Length, 6).ToString().All(char.IsAsciiDigit);

    /// <summary>The record as backed up: complete lines only, in order.</summary>
    internal static IReadOnlyList<(string SegmentName, byte[] Bytes)> SnapshotSegments(SightingLocation location)
    {
        var snapshot = new List<(string, byte[])>();
        foreach (var (number, path) in SightingStore.Segments(location.RootPath))
        {
            var bytes = File.ReadAllBytes(path);
            var end = Array.LastIndexOf(bytes, (byte)'\n') + 1;
            snapshot.Add((SightingStore.SegmentName(number), bytes.AsSpan(0, end).ToArray()));
        }

        return snapshot;
    }

    internal static SightingRestore RestoreIfMissing(SightingLocation location, IReadOnlyList<(string SegmentName, byte[] Bytes)> vaultSegments)
    {
        if (SightingStore.Segments(location.RootPath).Count > 0)
        {
            return SightingRestore.Kept;
        }

        var valid = vaultSegments.Where(segment => IsSegmentName(segment.SegmentName)).ToList();
        if (valid.Count == 0)
        {
            return SightingRestore.None;
        }

        Directory.CreateDirectory(location.RootPath);
        foreach (var (name, bytes) in valid)
        {
            var temporary = Path.Combine(location.RootPath, $".~{Guid.NewGuid():N}.tmp");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, Path.Combine(location.RootPath, name), overwrite: false);
        }

        return SightingRestore.Restored;
    }
}
