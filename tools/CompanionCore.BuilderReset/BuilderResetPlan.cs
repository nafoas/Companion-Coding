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

    /// <summary>Kept beside the memory directory: which apps Boss allowed or denied, and diagnostics.</summary>
    internal static readonly string[] KeptSiblings = ["TargetAuthorization", "Calibration"];

    /// <summary>Removed beside the memory directory: everything Builder Prince lived through.</summary>
    internal static readonly string[] RemovedSiblings = ["Keepsakes", "State", "Transcripts", "Braincase", "BunDex"];

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

        var memoryRoot = location.RootPath;
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(memoryRoot))!;
        var remove = new List<string>();
        var keep = new List<string>();
        if (Directory.Exists(memoryRoot))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(memoryRoot).Order(StringComparer.Ordinal))
            {
                (Path.GetFileName(entry) == SettingsFileName ? keep : remove).Add(entry);
            }
        }

        foreach (var name in RemovedSiblings)
        {
            var path = Path.Combine(parent, name);
            if (Directory.Exists(path))
            {
                remove.Add(path);
            }
        }

        foreach (var name in KeptSiblings)
        {
            var path = Path.Combine(parent, name);
            if (Directory.Exists(path))
            {
                keep.Add(path);
            }
        }

        var production = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(localApplicationData, DevelopmentDataRootPolicy.ProductionApplicationNamespace))) + Path.DirectorySeparatorChar;
        var development = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar;
        if (remove.Any(path => !Path.GetFullPath(path).StartsWith(development, StringComparison.Ordinal)
                               || Path.GetFullPath(path).StartsWith(production, StringComparison.Ordinal)))
        {
            throw new DataRootViolationException("The Builder reset stays inside the development namespace.");
        }

        return new BuilderResetPlan(memoryRoot, remove, keep);
    }

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
