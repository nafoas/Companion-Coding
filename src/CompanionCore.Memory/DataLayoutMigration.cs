namespace CompanionCore.Memory;

public enum DataLayoutMigrationOutcome
{
    /// <summary>Nothing from the old layout remains.</summary>
    Current = 1,

    /// <summary>One or more old stores were moved into the BrainCase.</summary>
    Moved = 2,
}

public sealed record DataLayoutMigrationReport(DataLayoutMigrationOutcome Outcome, IReadOnlyList<string> Moved);

/// <summary>
/// M1b: moves the machine stores that once lived directly in the data root (<c>Memory</c>,
/// <c>Keepsakes</c>, <c>State</c>, <c>Transcripts</c>) into the BrainCase. Each store is moved
/// with one rename, never copied or deleted, so there is always exactly one copy of every
/// memory. It is idempotent and resumes after an interruption. When a store exists in both
/// places it refuses and changes nothing further; it never picks a side. It must run before
/// anything opens the data and only while the App is not running.
/// </summary>
public static class DataLayoutMigration
{
    public static DataLayoutMigrationReport Apply(MemoryStoreLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        RequireDevelopmentOrTest(location);

        var conflicts = new List<string>();
        foreach (var name in DataLayout.BraincaseStoreNames)
        {
            var legacy = Path.Combine(location.DataRootPath, name);
            var current = Path.Combine(location.BraincaseRootPath, name);
            if (Directory.Exists(legacy) && Directory.Exists(current) && Directory.EnumerateFileSystemEntries(current).Any())
            {
                conflicts.Add(name);
            }
        }

        if (conflicts.Count > 0)
        {
            throw new DataRootViolationException(
                $"Both the old and the new place hold data for: {string.Join(", ", conflicts)}. Nothing was moved; the data needs a look before Prince opens.");
        }

        var moved = new List<string>();
        foreach (var name in DataLayout.BraincaseStoreNames)
        {
            var legacy = Path.Combine(location.DataRootPath, name);
            if (!Directory.Exists(legacy))
            {
                continue;
            }

            var current = Path.Combine(location.BraincaseRootPath, name);
            Directory.CreateDirectory(location.BraincaseRootPath);
            if (Directory.Exists(current))
            {
                // Checked empty above: an empty directory holds nothing to keep.
                Directory.Delete(current);
            }

            Directory.Move(legacy, current);
            moved.Add(name);
        }

        return new DataLayoutMigrationReport(moved.Count == 0 ? DataLayoutMigrationOutcome.Current : DataLayoutMigrationOutcome.Moved, moved);
    }

    /// <summary>True when any store still sits in its old place.</summary>
    public static bool HasLegacyStores(MemoryStoreLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return DataLayout.BraincaseStoreNames.Any(name => Directory.Exists(Path.Combine(location.DataRootPath, name)));
    }

    private static void RequireDevelopmentOrTest(MemoryStoreLocation location)
    {
        var expected = location.Kind switch
        {
            DataRootKind.Development => DevelopmentDataRootPolicy.DevelopmentApplicationNamespace,
            DataRootKind.Test => TestDataRootPolicy.TestApplicationNamespace,
            _ => null,
        };

        if (expected is null || !string.Equals(location.ApplicationNamespace, expected, StringComparison.Ordinal))
        {
            throw new DataRootViolationException("The layout migration only touches development or test data.");
        }
    }
}
