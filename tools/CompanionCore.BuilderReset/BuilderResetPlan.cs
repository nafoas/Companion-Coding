using CompanionCore.Memory;

namespace CompanionCore.BuilderReset;

/// <summary>
/// The one-time Builder Prince reset Boss directed on 2026-10-12 ("They should be discarded,
/// that's what Builder Prince is for"). Builder Prince is the resettable construction identity:
/// this removes his development memories and everything he lived through, and nothing else.
/// It only ever resolves the development namespace, so it can never reach Companion Prince.
/// </summary>
internal sealed class BuilderResetPlan
{
    /// <summary>Kept: his Braincase settings (live calls, Bun Budget, models).</summary>
    internal const string SettingsFileName = "braincase-settings.json";

    /// <summary>Kept in the data root: which apps Boss allowed or denied, and diagnostics.</summary>
    internal static readonly string[] KeptSiblings = ["TargetAuthorization", "Calibration"];

    private BuilderResetPlan(string memoryRoot, IReadOnlyList<string> remove, IReadOnlyList<string> keep)
    {
        MemoryRoot = memoryRoot;
        Remove = remove;
        Keep = keep;
    }

    /// <summary>The exact path Boss must confirm.</summary>
    internal string MemoryRoot { get; }

    internal IReadOnlyList<string> Remove { get; }

    internal IReadOnlyList<string> Keep { get; }

    internal static BuilderResetPlan For(string localApplicationData)
    {
        var location = new DevelopmentDataRootPolicy(localApplicationData).Resolve();
        if (location.Kind != DataRootKind.Development
            || !string.Equals(location.ApplicationNamespace, DevelopmentDataRootPolicy.DevelopmentApplicationNamespace, StringComparison.Ordinal))
        {
            throw new DataRootViolationException("The Builder reset only ever touches the development namespace.");
        }

        if (DataLayoutMigration.HasLegacyStores(location))
        {
            throw new InvalidOperationException("Builder Prince's data is still in the old layout; it moves into the BrainCase first.");
        }

        // Everything in the BrainCase except his Braincase settings, and his BunDex.
        var memoryRoot = location.RootPath;
        var dataRoot = location.DataRootPath;
        var remove = new List<string>();
        var keep = new List<string>();
        if (Directory.Exists(location.BraincaseRootPath))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(location.BraincaseRootPath).Order(StringComparer.Ordinal))
            {
                if (!PathsEqual(entry, memoryRoot))
                {
                    remove.Add(entry);
                }
            }
        }

        if (Directory.Exists(memoryRoot))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(memoryRoot).Order(StringComparer.Ordinal))
            {
                (Path.GetFileName(entry) == SettingsFileName ? keep : remove).Add(entry);
            }
        }

        var bunDex = Path.Combine(dataRoot, DataLayout.BunDexDirectoryName);
        if (Directory.Exists(bunDex))
        {
            remove.Add(bunDex);
        }

        foreach (var name in KeptSiblings)
        {
            var path = Path.Combine(dataRoot, name);
            if (Directory.Exists(path))
            {
                keep.Add(path);
            }
        }

        var production = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(localApplicationData, DevelopmentDataRootPolicy.ProductionApplicationNamespace))) + Path.DirectorySeparatorChar;
        var development = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)) + Path.DirectorySeparatorChar;
        if (remove.Any(path => !Path.GetFullPath(path).StartsWith(development, StringComparison.Ordinal)
                               || Path.GetFullPath(path).StartsWith(production, StringComparison.Ordinal)))
        {
            throw new DataRootViolationException("The Builder reset stays inside the development namespace.");
        }

        return new BuilderResetPlan(memoryRoot, remove, keep);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>True only for the exact memory path, typed by Boss.</summary>
    internal bool Confirms(string? confirmation)
    {
        if (string.IsNullOrWhiteSpace(confirmation))
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(confirmation.Trim().Trim('"')),
            Path.TrimEndingDirectorySeparator(MemoryRoot),
            comparison);
    }

    /// <summary>Removes exactly the planned paths. Running it again finds nothing more to remove.</summary>
    internal int Execute(string? confirmation)
    {
        if (!Confirms(confirmation))
        {
            throw new InvalidOperationException("The reset needs the exact development memory path as confirmation.");
        }

        foreach (var path in Remove)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        return Remove.Count;
    }
}
