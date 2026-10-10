using System.Text;
using CompanionCore.Memory;

namespace CompanionCore.Api.Tests;

public sealed class BraincaseJournalTests
{
    [Fact]
    public async Task StateLocation_IsASiblingOfAValidatedTestRoot()
    {
        await using var harness = await ApiTestHarness.CreateAsync();

        Assert.Equal(DataRootKind.Test, harness.StateLocation.Kind);
        Assert.Equal(
            Path.Combine(Path.GetDirectoryName(harness.Location.RootPath)!, "Braincase"),
            harness.StateLocation.RootPath);
        Assert.StartsWith(harness.BasePath, harness.StateLocation.RootPath, StringComparison.Ordinal);
    }

    [Fact]
    public void StateLocation_RejectsAnUnrecognizedOrMismatchedRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "CompanionCore.Api.Tests", Guid.NewGuid().ToString("N"), "Memory");

        Assert.Throws<DataRootViolationException>(() => BraincaseStateLocation.For(
            new MemoryStoreLocation((DataRootKind)99, DevelopmentDataRootPolicy.ProductionApplicationNamespace, root, "memory-v1.db")));
        Assert.Throws<DataRootViolationException>(() => BraincaseStateLocation.For(
            new MemoryStoreLocation(DataRootKind.Test, DevelopmentDataRootPolicy.ProductionApplicationNamespace, root, "memory-v1.db")));
        Assert.Throws<DataRootViolationException>(() => BraincaseStateLocation.For(
            new MemoryStoreLocation(DataRootKind.Development, TestDataRootPolicy.TestApplicationNamespace, root, "memory-v1.db")));
    }

    [Fact]
    public async Task Reopen_RecoversExactSubjectsNapAndCounters()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        using (var journal = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc))
        {
            Complete(journal, ["a", "b"]);
            Complete(journal, ["c", "a"]);
            journal.Append(Nap(BraincaseNapReason.RateLimited, TimeSpan.FromMinutes(7)));
        }

        using var reopened = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc);

        Assert.Equal(["c", "a", "b"], reopened.State.RecentSubjects);
        Assert.Equal(BraincaseNapReason.RateLimited, reopened.State.NapReason);
        Assert.Equal(ApiTestHarness.BaselineUtc + TimeSpan.FromMinutes(7), reopened.State.NapUntil);
        Assert.Equal(2, reopened.State.Completed);
        Assert.Equal(2, reopened.State.Operations);
        Assert.Equal(1, reopened.State.NapEpisodes);
        Assert.Equal(new BraincaseJournalRecovery(false, false, 0), reopened.Recovery);
    }

    [Fact]
    public async Task RecentSubjects_AreBoundedMostRecentFirst()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        using var journal = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc);

        for (var index = 0; index < 12; index++)
        {
            Complete(journal, [$"subject.{index}"]);
        }

        Assert.Equal(
            Enumerable.Range(4, 8).Reverse().Select(index => $"subject.{index}"),
            journal.State.RecentSubjects);
    }

    [Theory]
    [InlineData("{\"v\":1,\"seq\":")]
    [InlineData("garbage-without-newline")]
    [InlineData("{}\n")]
    public async Task TornTrailingLine_IsTruncatedAndLaterAppendsRemainValid(string torn)
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        using (var journal = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc))
        {
            Complete(journal, ["kept"]);
        }

        var validLength = new FileInfo(harness.StateLocation.JournalPath).Length;
        await File.AppendAllTextAsync(harness.StateLocation.JournalPath, torn);

        using (var recovered = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc))
        {
            Assert.True(recovered.Recovery.TornTailTruncated);
            Assert.Equal(validLength, recovered.LengthBytes);
            Assert.Equal(["kept"], recovered.State.RecentSubjects);
            Complete(recovered, ["after"]);
        }

        using var clean = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc);
        Assert.False(clean.Recovery.TornTailTruncated);
        Assert.Equal(["after", "kept"], clean.State.RecentSubjects);
    }

    [Fact]
    public async Task InteriorCorruption_IsPreservedAsideAndTheBridgeStartsFresh()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        using (var journal = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc))
        {
            Complete(journal, ["first"]);
            Complete(journal, ["second"]);
        }

        var original = await File.ReadAllBytesAsync(harness.StateLocation.JournalPath);
        var tampered = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original).Replace("\"first\"", "\"forged\"", StringComparison.Ordinal));
        await File.WriteAllBytesAsync(harness.StateLocation.JournalPath, tampered);

        using var recovered = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc);

        Assert.True(recovered.Recovery.CorruptJournalPreserved);
        Assert.Empty(recovered.State.RecentSubjects);
        Assert.Equal(0, recovered.LengthBytes);
        var preserved = Assert.Single(Directory.GetFiles(harness.StateLocation.CorruptDirectoryPath));
        Assert.Equal(tampered, await File.ReadAllBytesAsync(preserved));
    }

    [Fact]
    public async Task SequenceGap_IsTreatedAsCorruption()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        using (var journal = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc))
        {
            Complete(journal, ["first"]);
            Complete(journal, ["second"]);
            Complete(journal, ["third"]);
        }

        var lines = (await File.ReadAllLinesAsync(harness.StateLocation.JournalPath)).ToList();
        lines.RemoveRange(2, 2);
        await File.WriteAllTextAsync(harness.StateLocation.JournalPath, string.Join('\n', lines) + "\n");

        using var recovered = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc);

        Assert.True(recovered.Recovery.CorruptJournalPreserved);
    }

    [Fact]
    public async Task Compaction_IsAtomicBoundedAndPreservesExactState()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        BraincaseJournalSnapshot before;
        using (var journal = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc, compactionThresholdBytes: 2048))
        {
            journal.Append(Nap(BraincaseNapReason.Outage, TimeSpan.FromMinutes(2), probeSeconds: 120));
            var compacted = false;
            for (var index = 0; index < 40 && !compacted; index++)
            {
                Complete(journal, [$"subject.{index}"]);
                before = journal.State.ToSnapshot();
                compacted = journal.CompactIfNeeded(ApiTestHarness.BaselineUtc);
            }

            Assert.True(compacted);
            Assert.True(journal.LengthBytes < 2048);
            before = journal.State.ToSnapshot();
            Complete(journal, ["after.compaction"]);
            before = journal.State.ToSnapshot();
        }

        Assert.False(File.Exists(harness.StateLocation.CompactionTemporaryPath));
        using var reopened = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc, compactionThresholdBytes: 2048);
        var after = reopened.State.ToSnapshot();

        Assert.Equal(before.RecentSubjects, after.RecentSubjects);
        Assert.Equal(before.NapReason, after.NapReason);
        Assert.Equal(before.NapUntil, after.NapUntil);
        Assert.Equal(before.ProbeIntervalSeconds, after.ProbeIntervalSeconds);
        Assert.Equal(before.Operations, after.Operations);
        Assert.Equal(before.Completed, after.Completed);
        Assert.Equal(before.NapEpisodes, after.NapEpisodes);
        Assert.Equal(before.TotalInputUnits, after.TotalInputUnits);
        Assert.Equal("after.compaction", after.RecentSubjects[0]);
    }

    [Fact]
    public async Task LeftoverCompactionTemporaryFile_IsDiscardedOnOpen()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        using (var journal = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc))
        {
            Complete(journal, ["kept"]);
        }

        await File.WriteAllTextAsync(harness.StateLocation.CompactionTemporaryPath, "partial snapshot");

        using var reopened = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc);

        Assert.False(File.Exists(harness.StateLocation.CompactionTemporaryPath));
        Assert.Equal(["kept"], reopened.State.RecentSubjects);
    }

    [Fact]
    public async Task SecondWriter_IsFencedWhileTheFirstIsOpen()
    {
        await using var harness = await ApiTestHarness.CreateAsync();
        using var first = BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc);

        Assert.Throws<BraincaseStateBusyException>(() =>
            BraincaseJournal.Open(harness.StateLocation, ApiTestHarness.BaselineUtc));
    }

    private static void Complete(BraincaseJournal journal, IReadOnlyList<string> subjects)
    {
        var operation = Guid.NewGuid();
        journal.Append(new BraincaseJournalEntry
        {
            At = ApiTestHarness.BaselineUtc,
            Type = BraincaseJournalEntryType.Started,
            Op = operation,
        });
        journal.Append(new BraincaseJournalEntry
        {
            At = ApiTestHarness.BaselineUtc,
            Type = BraincaseJournalEntryType.Completed,
            Op = operation,
            Outcome = BridgeOutcomeKind.Interpreted,
            Attempts = 1,
            Subjects = [.. subjects],
            InputUnits = 10,
            OutputUnits = 4,
        });
    }

    private static BraincaseJournalEntry Nap(BraincaseNapReason reason, TimeSpan duration, long probeSeconds = 0) => new()
    {
        At = ApiTestHarness.BaselineUtc,
        Type = BraincaseJournalEntryType.Nap,
        NapReason = reason,
        NapUntil = ApiTestHarness.BaselineUtc + duration,
        ProbeIntervalSeconds = probeSeconds,
    };
}
