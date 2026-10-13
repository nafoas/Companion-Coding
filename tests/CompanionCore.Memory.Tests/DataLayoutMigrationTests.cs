namespace CompanionCore.Memory.Tests;

/// <summary>M1b: the old layout moves into the BrainCase: one copy of everything, never deleted, never a side picked.</summary>
public sealed class DataLayoutMigrationTests
{
    [Fact]
    public void TheTestLayout_HoldsTheBunDexAndTheBraincase_SideBySide()
    {
        using var directory = new MemoryTestDirectory();
        var location = directory.Location;

        Assert.Equal("Memory", Path.GetFileName(location.RootPath));
        Assert.Equal(Path.Combine(location.DataRootPath, "Braincase"), location.BraincaseRootPath);
        Assert.StartsWith(directory.BasePath, location.DataRootPath, StringComparison.Ordinal);
        Assert.Equal(DataLayoutMigrationOutcome.Current, DataLayoutMigration.Apply(location).Outcome);
        Assert.False(Directory.Exists(location.DataRootPath));
    }

    [Fact]
    public void OldStores_AreMovedWholeAndByteIdentical_AndEverythingElseStays()
    {
        using var directory = new MemoryTestDirectory();
        var location = directory.Location;
        var files = new Dictionary<string, byte[]>();
        foreach (var name in DataLayout.BraincaseStoreNames)
        {
            files[Path.Combine(name, "deep", "data.bin")] = Write(Path.Combine(location.DataRootPath, name, "deep", "data.bin"), name);
        }

        Write(Path.Combine(location.BraincaseRootPath, "braincase-journal-v1.jsonl"), "journal");
        var kept = new[] { "BunDex", "TargetAuthorization", "Calibration" };
        foreach (var name in kept)
        {
            Write(Path.Combine(location.DataRootPath, name, "keep.txt"), name);
        }

        Assert.True(DataLayoutMigration.HasLegacyStores(location));
        var report = DataLayoutMigration.Apply(location);

        Assert.Equal(DataLayoutMigrationOutcome.Moved, report.Outcome);
        Assert.Equal(DataLayout.BraincaseStoreNames, report.Moved);
        Assert.False(DataLayoutMigration.HasLegacyStores(location));
        foreach (var (relative, bytes) in files)
        {
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(location.BraincaseRootPath, relative)));
            Assert.False(File.Exists(Path.Combine(location.DataRootPath, relative)));
        }

        Assert.Equal("journal", File.ReadAllText(Path.Combine(location.BraincaseRootPath, "braincase-journal-v1.jsonl")));
        Assert.All(kept, name => Assert.Equal(name, File.ReadAllText(Path.Combine(location.DataRootPath, name, "keep.txt"))));

        // Idempotent.
        Assert.Equal(DataLayoutMigrationOutcome.Current, DataLayoutMigration.Apply(location).Outcome);
    }

    [Fact]
    public void AnInterruptedMigration_Resumes()
    {
        using var directory = new MemoryTestDirectory();
        var location = directory.Location;
        Write(Path.Combine(location.BraincaseRootPath, "Memory", "db"), "already moved");
        Write(Path.Combine(location.DataRootPath, "State", "s"), "state");

        var report = DataLayoutMigration.Apply(location);

        Assert.Equal(["State"], report.Moved);
        Assert.Equal("already moved", File.ReadAllText(Path.Combine(location.BraincaseRootPath, "Memory", "db")));
        Assert.Equal("state", File.ReadAllText(Path.Combine(location.BraincaseRootPath, "State", "s")));
    }

    [Fact]
    public void AnEmptyNewPlace_IsNoConflict()
    {
        using var directory = new MemoryTestDirectory();
        var location = directory.Location;
        Directory.CreateDirectory(Path.Combine(location.BraincaseRootPath, "Keepsakes"));
        Write(Path.Combine(location.DataRootPath, "Keepsakes", "photo.png"), "photo");

        Assert.Equal(["Keepsakes"], DataLayoutMigration.Apply(location).Moved);
        Assert.Equal("photo", File.ReadAllText(Path.Combine(location.BraincaseRootPath, "Keepsakes", "photo.png")));
    }

    [Fact]
    public void DataInBothPlaces_IsRefused_AndNothingMoves()
    {
        using var directory = new MemoryTestDirectory();
        var location = directory.Location;
        Write(Path.Combine(location.DataRootPath, "Memory", "old.db"), "old");
        Write(Path.Combine(location.BraincaseRootPath, "Memory", "new.db"), "new");
        Write(Path.Combine(location.DataRootPath, "Transcripts", "t"), "transcript");

        var refused = Assert.Throws<DataRootViolationException>(() => DataLayoutMigration.Apply(location));

        Assert.Contains("Memory", refused.Message, StringComparison.Ordinal);
        Assert.Equal("old", File.ReadAllText(Path.Combine(location.DataRootPath, "Memory", "old.db")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(location.BraincaseRootPath, "Memory", "new.db")));
        Assert.Equal("transcript", File.ReadAllText(Path.Combine(location.DataRootPath, "Transcripts", "t")));
        Assert.False(Directory.Exists(Path.Combine(location.BraincaseRootPath, "Transcripts")));
    }

    [Fact]
    public void TheDevelopmentLayout_NeverReachesProduction()
    {
        using var directory = new MemoryTestDirectory();
        var policy = new DevelopmentDataRootPolicy(directory.BasePath);
        var production = Path.Combine(directory.BasePath, DevelopmentDataRootPolicy.ProductionApplicationNamespace, "Memory", "keep.txt");
        Write(production, "Companion Prince");
        var development = policy.Resolve();
        Write(Path.Combine(development.DataRootPath, "Memory", "db"), "builder");

        DataLayoutMigration.Apply(development);

        Assert.Equal("Companion Prince", File.ReadAllText(production));
        Assert.Equal("builder", File.ReadAllText(Path.Combine(development.RootPath, "db")));
    }

    private static byte[] Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(path, bytes);
        return bytes;
    }
}
