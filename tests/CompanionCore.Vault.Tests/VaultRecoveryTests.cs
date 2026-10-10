using System.IO.Compression;
using System.Text;
using CompanionCore.Keepsakes;
using CompanionCore.Memory;
using static CompanionCore.Vault.Tests.VaultHarness;

namespace CompanionCore.Vault.Tests;

public sealed class VaultRecoveryTests
{
    [Fact]
    public async Task Scenario1_CompleteRecovery_RestoresBunDexPhotographsSettingsAndCheckpoint()
    {
        await using var harness = await CreateAsync();
        var archived = await harness.RememberAsync("synthetic.archived");
        var (photoOne, actionOne) = await harness.PhotographAsync(3);
        var (photoTwo, actionTwo) = await harness.PhotographAsync(5);
        var settings = Bytes("{\"volume\":3}");
        var checkpoint = Bytes("{\"phase\":\"watching\",\"active\":true}");
        await harness.State.PutAsync("settings", settings);
        await harness.State.PutAsync("watchbun-checkpoint", checkpoint);
        var photoTwoBytes = await File.ReadAllBytesAsync(harness.PhotographPath(actionTwo));

        var backup = await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        Assert.True(backup.CompanionIncluded);
        Assert.True(File.Exists(harness.Location.CompanionArchivePath));

        // After the cut: a new memory and a new photograph that only live on disk and in the journal.
        var postCut = await harness.RememberAsync("synthetic.post-cut");
        var (photoThree, _) = await harness.PhotographAsync(7);
        await harness.CloseAsync();

        // Damage everything.
        await File.WriteAllBytesAsync(harness.Location.DatabasePath, Bytes("synthetic damaged database"));
        File.Delete(harness.PhotographPath(actionOne));
        var tampered = photoTwoBytes.ToArray();
        tampered[^20] ^= 0xFF;
        await File.WriteAllBytesAsync(harness.PhotographPath(actionTwo), tampered);
        await File.WriteAllBytesAsync(harness.State.PathFor("settings"), Bytes("garbage"));
        File.Delete(harness.State.PathFor("watchbun-checkpoint"));

        var report = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);

        Assert.Equal(CompanionStatus.Valid, report.Companion);
        Assert.Equal(backup.BackupId, report.CompanionBackupId);
        Assert.Equal(backup.BackupId, report.MemoryBackupId);
        Assert.Equal(2, report.PhotographsRestored);
        Assert.Equal(1, report.PhotographsVerified);
        Assert.Empty(report.MissingPhotographs);
        Assert.Equal(2, report.StatesRestored);
        Assert.Equal(0, report.StatesKept);
        Assert.True(report.Complete);

        await harness.OpenAsync();
        Assert.Single(await harness.Repository!.RetrieveAsync(new MemoryQuery { RecordIds = [archived] }));
        Assert.Single(await harness.Repository!.RetrieveAsync(new MemoryQuery { RecordIds = [postCut] }));
        foreach (var photo in new[] { photoOne, photoTwo, photoThree })
        {
            Assert.Equal(InspectionStatus.Verified, (await harness.Store.InspectAsync(photo)).Status);
        }

        Assert.Equal(photoTwoBytes, await File.ReadAllBytesAsync(harness.PhotographPath(actionTwo)));
        Assert.Equal(settings, (await harness.State.GetAsync("settings")).Payload.ToArray());
        Assert.Equal(checkpoint, (await harness.State.GetAsync("watchbun-checkpoint")).Payload.ToArray());

        // Damaged copies are preserved as evidence, never discarded.
        Assert.Equal(tampered, await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(Path.Combine(harness.Keepsakes.RootPath, "damaged-v1")))));
        Assert.Equal(Bytes("garbage"), await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(Path.Combine(harness.State.Location.RootPath, "damaged-v1")))));
        Assert.True(Directory.Exists(report.DamagedSourceDirectory));
    }

    [Fact]
    public async Task Scenario2_DeletedPhotographsAreNotResurrected_AndValidNewerStateIsKept()
    {
        await using var harness = await CreateAsync();
        var (deletedBefore, actionBefore) = await harness.PhotographAsync(3);
        var (deletedAfter, actionAfter) = await harness.PhotographAsync(5);
        await harness.Store.DeleteAsync(deletedBefore, KeepsakeDeletionAuthority.ForExplicitLocalUserIntent(), T0.AddHours(1));
        await harness.State.PutAsync("settings", Bytes("old"));
        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);

        await harness.Store.DeleteAsync(deletedAfter, KeepsakeDeletionAuthority.ForExplicitLocalUserIntent(), T0.AddHours(2));
        await harness.State.PutAsync("settings", Bytes("newer"));
        await harness.CloseAsync();
        await File.WriteAllBytesAsync(harness.Location.DatabasePath, Bytes("synthetic damaged database"));

        var report = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);

        Assert.Equal(0, report.PhotographsRestored + report.PhotographsVerified);
        Assert.Empty(report.MissingPhotographs);
        Assert.True(report.Complete);
        Assert.False(File.Exists(harness.PhotographPath(actionBefore)));
        Assert.False(File.Exists(harness.PhotographPath(actionAfter)));
        Assert.Equal(1, report.StatesKept);
        Assert.Equal(Bytes("newer"), (await harness.State.GetAsync("settings")).Payload.ToArray());

        await harness.OpenAsync();
        Assert.Equal(InspectionStatus.Deleted, (await harness.Store.InspectAsync(deletedAfter)).Status);
        Assert.Equal(2, (await harness.Repository!.RetrieveBySubjectAsync($"photo:{actionAfter:N}")).Count);
    }

    [Fact]
    public async Task Scenario3_IncompleteRecovery_IsReportedHonestly()
    {
        await using var harness = await CreateAsync();
        await harness.PhotographAsync(3);
        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        var (lost, lostAction) = await harness.PhotographAsync(5);
        await harness.CloseAsync();
        File.Delete(harness.PhotographPath(lostAction));

        var report = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);

        Assert.Equal([lost], report.MissingPhotographs);
        Assert.Equal(1, report.PhotographsVerified);
        Assert.False(report.Complete);
        Assert.False(File.Exists(harness.PhotographPath(lostAction)));
        await harness.OpenAsync();
        Assert.Equal(InspectionStatus.Missing, (await harness.Store.InspectAsync(lost)).Status);
    }

    [Fact]
    public async Task Scenario3_AVaultCopyThatDoesNotMatchItsRecord_IsNeverRestored()
    {
        await using var harness = await CreateAsync();
        var (photo, action) = await harness.PhotographAsync(3);
        var tampered = await File.ReadAllBytesAsync(harness.PhotographPath(action));
        tampered[^20] ^= 0x01;
        await File.WriteAllBytesAsync(harness.PhotographPath(action), tampered);
        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        await harness.CloseAsync();

        var report = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);

        Assert.Equal([photo], report.MissingPhotographs);
        Assert.Equal(0, report.PhotographsRestored);
        Assert.False(report.Complete);
        Assert.Equal(tampered, await File.ReadAllBytesAsync(harness.PhotographPath(action)));
    }

    [Fact]
    public async Task Scenario4_ACompanionThatProducesNothing_FailsTheBackup()
    {
        await using var harness = await CreateAsync();
        await harness.PhotographAsync(3);
        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        var memoryHash = Hash(harness.Location.BackupArchivePath);
        var companionHash = Hash(harness.Location.CompanionArchivePath);

        await Assert.ThrowsAsync<BackupValidationException>(() => harness.Repository!.CreateBackupAsync(new SilentCompanion()));

        Assert.Equal(memoryHash, Hash(harness.Location.BackupArchivePath));
        Assert.Equal(companionHash, Hash(harness.Location.CompanionArchivePath));
    }

    [Fact]
    public async Task Scenario4_ADamagedStateFailsTheBackup_AndThePreviousVaultIsUntouched()
    {
        await using var harness = await CreateAsync();
        await harness.PhotographAsync(3);
        await harness.State.PutAsync("settings", Bytes("good"));
        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        var memoryHash = Hash(harness.Location.BackupArchivePath);
        var companionHash = Hash(harness.Location.CompanionArchivePath);
        await harness.RememberAsync("synthetic.after-good-backup");
        var journalHash = Hash(harness.Location.JournalPath);
        await File.WriteAllBytesAsync(harness.State.PathFor("settings"), Bytes("damaged"));

        await Assert.ThrowsAsync<BackupValidationException>(() => DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State));

        Assert.Equal(memoryHash, Hash(harness.Location.BackupArchivePath));
        Assert.Equal(companionHash, Hash(harness.Location.CompanionArchivePath));
        Assert.Equal(journalHash, Hash(harness.Location.JournalPath));
        Assert.Equal(["companion-vault-v1.zip", "memory-vault-v1.zip"], Directory.GetFiles(Path.GetDirectoryName(harness.Location.BackupArchivePath)!).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(nameof(BackupTestPoint.CompanionBuilt))]
    [InlineData(nameof(BackupTestPoint.BeforeArchivePromotion))]
    public async Task Scenario4_InterruptionBeforePromotion_KeepsThePreviousVault(string pointName)
    {
        var point = Enum.Parse<BackupTestPoint>(pointName);
        await using var harness = await CreateAsync();
        await harness.PhotographAsync(3);
        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        var memoryHash = Hash(harness.Location.BackupArchivePath);
        var companionHash = Hash(harness.Location.CompanionArchivePath);
        await harness.PhotographAsync(5);

        await Assert.ThrowsAsync<InvalidOperationException>(() => DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State, new FailAt(point)));

        Assert.Equal(memoryHash, Hash(harness.Location.BackupArchivePath));
        Assert.Equal(companionHash, Hash(harness.Location.CompanionArchivePath));
        Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(harness.Location.BackupArchivePath)!).Length);
    }

    [Fact]
    public async Task Scenario4_CrashBetweenPromotions_StillRecoversCompletely()
    {
        await using var harness = await CreateAsync();
        var (first, _) = await harness.PhotographAsync(3);
        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        var memoryHash = Hash(harness.Location.BackupArchivePath);
        var (second, secondAction) = await harness.PhotographAsync(5);

        await Assert.ThrowsAsync<InvalidOperationException>(() => DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State, new FailAt(BackupTestPoint.AfterCompanionPromotion)));
        Assert.Equal(memoryHash, Hash(harness.Location.BackupArchivePath));
        await harness.CloseAsync();
        await File.WriteAllBytesAsync(harness.Location.DatabasePath, Bytes("synthetic damaged database"));
        File.Delete(harness.PhotographPath(secondAction));

        // The newer companion pairs with the older memory archive; every file is verified against restored records.
        var report = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);

        Assert.True(report.Complete);
        Assert.NotEqual(report.MemoryBackupId, report.CompanionBackupId);
        Assert.Equal(1, report.PhotographsRestored);
        await harness.OpenAsync();
        Assert.Equal(InspectionStatus.Verified, (await harness.Store.InspectAsync(first)).Status);
        Assert.Equal(InspectionStatus.Verified, (await harness.Store.InspectAsync(second)).Status);
    }

    [Fact]
    public async Task Scenario6_BackupAndRestoreNeverDeleteLivePhotographsOrRecords()
    {
        await using var harness = await CreateAsync();
        var photos = new List<(Guid Photo, Guid Action)> { await harness.PhotographAsync(3), await harness.PhotographAsync(5) };
        await harness.RememberAsync("synthetic.memory");
        var before = photos.ToDictionary(item => item.Action, item => Hash(harness.PhotographPath(item.Action)));

        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        Assert.All(before, pair => Assert.Equal(pair.Value, Hash(harness.PhotographPath(pair.Key))));
        var recordsBefore = await harness.Repository!.RetrieveSubjectPrefixPageAsync("photo:", null, 1000);
        await harness.CloseAsync();

        var report = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);

        Assert.Equal(2, report.PhotographsVerified);
        Assert.Equal(0, report.PhotographsRestored);
        Assert.All(before, pair => Assert.Equal(pair.Value, Hash(harness.PhotographPath(pair.Key))));
        Assert.False(Directory.Exists(Path.Combine(harness.Keepsakes.RootPath, "damaged-v1")));
        await harness.OpenAsync();
        Assert.Equal(
            recordsBefore.Select(memory => memory.RecordChecksum),
            (await harness.Repository!.RetrieveSubjectPrefixPageAsync("photo:", null, 1000)).Select(memory => memory.RecordChecksum));
    }

    [Fact]
    public async Task Scenario9_MemoryOnlyOrInvalidCompanion_StillRestoresTheBunDex_AndSaysSo()
    {
        await using var harness = await CreateAsync();
        var remembered = await harness.RememberAsync("synthetic.memory-only");
        await harness.PhotographAsync(3);
        await harness.Repository!.CreateBackupAsync();
        await harness.CloseAsync();
        Assert.False(File.Exists(harness.Location.CompanionArchivePath));
        await File.WriteAllBytesAsync(harness.Location.DatabasePath, Bytes("synthetic damaged database"));

        var missing = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);

        Assert.Equal(CompanionStatus.Missing, missing.Companion);
        Assert.Null(missing.CompanionBackupId);
        Assert.Equal(1, missing.PhotographsVerified);
        Assert.False(missing.Complete);
        await harness.OpenAsync();
        Assert.Single(await harness.Repository!.RetrieveAsync(new MemoryQuery { RecordIds = [remembered] }));

        await DaBunVault.CreateAsync(harness.Repository!, harness.Keepsakes, harness.State);
        await harness.CloseAsync();
        var companionBytes = await File.ReadAllBytesAsync(harness.Location.CompanionArchivePath);
        companionBytes[companionBytes.Length / 2] ^= 0xFF;
        await File.WriteAllBytesAsync(harness.Location.CompanionArchivePath, companionBytes);

        var invalid = await DaBunVault.RestoreAsync(harness.Location, harness.Privacy, harness.Keepsakes, harness.State);
        Assert.Equal(CompanionStatus.Invalid, invalid.Companion);
        Assert.False(invalid.Complete);
        Assert.Equal(1, invalid.PhotographsVerified);
    }

    [Fact]
    public async Task LivePhotographs_ArePagedBeyondASinglePage()
    {
        await using var harness = await CreateAsync();
        var actions = new List<Guid>();
        for (var batch = 0; batch < 9; batch++)
        {
            var drafts = Enumerable.Range(0, 128).Select(index =>
            {
                var actionId = Guid.NewGuid();
                actions.Add(actionId);
                return new MemoryRecordDraft
                {
                    RecordId = Guid.NewGuid(),
                    CreatedAtUtc = T0,
                    Scope = MemoryScope.General,
                    SourceKind = MemorySourceKind.Observed,
                    Confidence = 1,
                    SubjectKey = $"photo:{actionId:N}",
                    VisibleRecollection = "[neutral photograph caption]",
                    RetrievalMetadataJson = new KeepsakeMetadata(actionId, new string('a', 64), 1, 1, 1, T0, false).ToJson(),
                };
            }).ToArray();
            Assert.True((await harness.Repository!.WriteGate.SubmitAsync(new AppendMemoryProposal(Guid.NewGuid(), drafts))).IsAccepted);
        }

        var live = await DaBunVault.LivePhotographsAsync(harness.Repository!, default);

        Assert.Equal(1152, live.Count);
        Assert.Equal(actions.Order(), live.Select(item => item.Metadata.ActionId).Order());
    }

    private sealed class SilentCompanion : IVaultCompanion
    {
        public Task BuildCandidateAsync(Guid backupId, DateTimeOffset createdAtUtc, string candidatePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailAt(BackupTestPoint point) : IBackupTestHook
    {
        public Task OnPointAsync(BackupTestPoint current, CancellationToken cancellationToken) =>
            current == point ? throw new InvalidOperationException($"synthetic interruption at {current}") : Task.CompletedTask;
    }
}
