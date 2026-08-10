using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CompanionCore.Memory.Tests;

public sealed class MemoryRepairTests
{
    public static TheoryData<ArchiveAttack> InvalidArchiveAttacks =>
        new()
        {
            ArchiveAttack.ManifestChecksum,
            ArchiveAttack.NonCanonicalManifest,
            ArchiveAttack.DatabaseChecksum,
            ArchiveAttack.DatabaseHealth,
            ArchiveAttack.ExtraEntry,
            ArchiveAttack.DuplicateEntry,
            ArchiveAttack.TraversalEntry,
            ArchiveAttack.UnsupportedFormat,
            ArchiveAttack.UnsupportedSchema,
        };

    [Fact]
    public async Task CorruptLiveDatabase_RestoresSnapshotPreservesDamageAndReplaysPostCutOnce()
    {
        using var directory = new MemoryTestDirectory();
        const string archivedSubject = "synthetic.repair.archived";
        const string postCutSubject = "synthetic.repair.post-cut";
        await CreateBackupAndPostCutAsync(directory, archivedSubject, postCutSubject);
        var damagedBytes = Encoding.UTF8.GetBytes("synthetic damaged database evidence");
        var damagedWalBytes = Encoding.UTF8.GetBytes("synthetic damaged wal evidence");
        var damagedShmBytes = Encoding.UTF8.GetBytes("synthetic damaged shm evidence");
        await File.WriteAllBytesAsync(directory.Location.DatabasePath, damagedBytes);
        await File.WriteAllBytesAsync(directory.Location.DatabasePath + "-wal", damagedWalBytes);
        await File.WriteAllBytesAsync(directory.Location.DatabasePath + "-shm", damagedShmBytes);

        var result = await CreateRepairService(directory).RepairAsync();

        Assert.Equal(1, result.ArchiveCutSequence);
        Assert.Equal(2, result.RecoveredThroughSequence);
        Assert.Equal(2, result.OperationCount);
        Assert.Equal(2, result.RecordCount);
        Assert.Equal(0, result.LinkCount);
        Assert.True(Directory.Exists(result.DamagedSourceDirectory));
        var preservation = await DamagedSourcePreservation.OpenAndValidateAsync(
            directory.Location,
            Path.GetFileName(result.DamagedSourceDirectory),
            default);
        Assert.Equal(result.RepairId, preservation.Manifest.RepairId);
        Assert.Equal(
            damagedBytes,
            await File.ReadAllBytesAsync(Path.Combine(
                result.DamagedSourceDirectory,
                Path.GetFileName(directory.Location.DatabasePath))));
        Assert.Equal(
            damagedWalBytes,
            await File.ReadAllBytesAsync(Path.Combine(
                result.DamagedSourceDirectory,
                Path.GetFileName(directory.Location.DatabasePath) + "-wal")));
        Assert.Equal(
            damagedShmBytes,
            await File.ReadAllBytesAsync(Path.Combine(
                result.DamagedSourceDirectory,
                Path.GetFileName(directory.Location.DatabasePath) + "-shm")));

        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Single(await recovered.RetrieveBySubjectAsync(archivedSubject));
        Assert.Single(await recovered.RetrieveBySubjectAsync(postCutSubject));
        Assert.Equal((2L, 2L, 0L), await recovered.Store.ReadCountsAsync(default));
        Assert.Equal(2, recovered.Journal.HighestAppendSequence);
        Assert.Equal(2, recovered.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task RepairWhileRepositoryOpen_IsRejectedBeforePreservationOrMutation()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, "synthetic.repair.busy");

        await Assert.ThrowsAsync<MemoryMaintenanceBusyException>(() =>
            CreateRepairService(directory).RepairAsync());

        Assert.False(Directory.Exists(directory.Location.DamagedPreservationDirectoryPath));
        Assert.False(File.Exists(directory.Location.RepairMarkerPath));
        Assert.Single(await repository.RetrieveBySubjectAsync("synthetic.repair.busy"));
    }

    [Fact]
    public async Task TornTrailingJournalFrame_IsIgnoredOnCopyWhileOriginalEvidenceIsPreserved()
    {
        using var directory = new MemoryTestDirectory();
        const string archivedSubject = "synthetic.repair.torn.archived";
        const string postCutSubject = "synthetic.repair.torn.post-cut";
        await using (var repository = await directory.OpenRepositoryAsync())
        {
            await CommitAsync(repository, archivedSubject);
            _ = await repository.CreateBackupAsync();
            await CommitAsync(repository, postCutSubject);
            var torn = MemoryProposalValidator.Prepare(
                SyntheticMemory.Proposal(SyntheticMemory.Record(
                    subjectKey: "synthetic.repair.torn.discarded")));
            await repository.Journal.AppendTornFrameForTestAsync(
                torn.CanonicalPayload,
                bytesToWrite: 24);
        }

        var fullDamagedJournal = await File.ReadAllBytesAsync(directory.Location.JournalPath);
        await File.WriteAllTextAsync(directory.Location.DatabasePath, "damaged");

        var result = await CreateRepairService(directory).RepairAsync();

        Assert.Equal(
            fullDamagedJournal,
            await File.ReadAllBytesAsync(Path.Combine(
                result.DamagedSourceDirectory,
                Path.GetFileName(directory.Location.JournalPath))));
        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Single(await recovered.RetrieveBySubjectAsync(archivedSubject));
        Assert.Single(await recovered.RetrieveBySubjectAsync(postCutSubject));
        Assert.Empty(await recovered.RetrieveBySubjectAsync(
            "synthetic.repair.torn.discarded"));
    }

    [Fact]
    public async Task ChecksumInvalidTrailingJournalFrameIsIgnoredOnlyOnCopyAndPreservedInFull()
    {
        using var directory = new MemoryTestDirectory();
        const string archivedSubject = "synthetic.repair.trailing-checksum.archived";
        const string postCutSubject = "synthetic.repair.trailing-checksum.post-cut";
        await CreateBackupAndPostCutAsync(directory, archivedSubject, postCutSubject);
        var journal = await File.ReadAllBytesAsync(directory.Location.JournalPath);
        journal[^1] ^= 0xff;
        await File.WriteAllBytesAsync(directory.Location.JournalPath, journal);
        await File.WriteAllTextAsync(directory.Location.DatabasePath, "damaged");

        var result = await CreateRepairService(directory).RepairAsync();

        Assert.Equal(
            journal,
            await File.ReadAllBytesAsync(Path.Combine(
                result.DamagedSourceDirectory,
                Path.GetFileName(directory.Location.JournalPath))));
        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Single(await recovered.RetrieveBySubjectAsync(archivedSubject));
        Assert.Single(await recovered.RetrieveBySubjectAsync(postCutSubject));
        Assert.Equal(2, recovered.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task NonTrailingJournalCorruptionFailsBeforePreservationAndLiveMutation()
    {
        using var directory = new MemoryTestDirectory();
        await using (var repository = await directory.OpenRepositoryAsync())
        {
            await CommitAsync(repository, "synthetic.repair.non-trailing.archived");
            _ = await repository.CreateBackupAsync();
            await CommitAsync(repository, "synthetic.repair.non-trailing.one");
            await CommitAsync(repository, "synthetic.repair.non-trailing.two");
        }

        await CorruptFirstFrameAfterRotationBaseAsync(directory.Location.JournalPath);
        var databaseBefore = await File.ReadAllBytesAsync(directory.Location.DatabasePath);
        var journalBefore = await File.ReadAllBytesAsync(directory.Location.JournalPath);

        await Assert.ThrowsAsync<JournalCorruptionException>(() =>
            CreateRepairService(directory).RepairAsync());

        Assert.Equal(databaseBefore, await File.ReadAllBytesAsync(directory.Location.DatabasePath));
        Assert.Equal(journalBefore, await File.ReadAllBytesAsync(directory.Location.JournalPath));
        AssertNoPreservation(directory.Location);
        Assert.False(File.Exists(directory.Location.RepairMarkerPath));
    }

    [Theory]
    [MemberData(nameof(InvalidArchiveAttacks))]
    public async Task TamperedOrStructurallyInvalidArchiveFailsClosed(ArchiveAttack attack)
    {
        using var directory = new MemoryTestDirectory();
        await CreateBackupAndPostCutAsync(
            directory,
            "synthetic.repair.invalid-archive.archived",
            "synthetic.repair.invalid-archive.post-cut");
        await MutateArchiveAsync(directory.Location.BackupArchivePath, attack);
        var databaseBefore = await File.ReadAllBytesAsync(directory.Location.DatabasePath);
        var journalBefore = await File.ReadAllBytesAsync(directory.Location.JournalPath);

        await Assert.ThrowsAnyAsync<Exception>(() => CreateRepairService(directory).RepairAsync());

        Assert.Equal(databaseBefore, await File.ReadAllBytesAsync(directory.Location.DatabasePath));
        Assert.Equal(journalBefore, await File.ReadAllBytesAsync(directory.Location.JournalPath));
        AssertNoPreservation(directory.Location);
        Assert.False(File.Exists(directory.Location.RepairMarkerPath));
    }

    [Fact]
    public async Task CancellationBeforeMutationLeavesLiveFilesExactAndNoPreservationBundle()
    {
        using var directory = new MemoryTestDirectory();
        await CreateBackupAndPostCutAsync(
            directory,
            "synthetic.repair.cancel.archived",
            "synthetic.repair.cancel.post-cut");
        await File.WriteAllTextAsync(directory.Location.DatabasePath, "damaged before cancellation");
        var databaseBefore = await File.ReadAllBytesAsync(directory.Location.DatabasePath);
        var journalBefore = await File.ReadAllBytesAsync(directory.Location.JournalPath);
        using var cancellation = new CancellationTokenSource();
        var hook = DelegateRepairHook.At(
            RepairTestPoint.BeforeMutation,
            _ =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateRepairService(directory).RepairAsync(hook, cancellation.Token));

        Assert.Equal(databaseBefore, await File.ReadAllBytesAsync(directory.Location.DatabasePath));
        Assert.Equal(journalBefore, await File.ReadAllBytesAsync(directory.Location.JournalPath));
        AssertNoPreservation(directory.Location);
        Assert.False(File.Exists(directory.Location.RepairMarkerPath));
    }

    [Fact]
    public async Task FaultAfterPreservationCompletesRetainsEvidenceAndLeavesLiveFilesExact()
    {
        using var directory = new MemoryTestDirectory();
        await CreateBackupAndPostCutAsync(
            directory,
            "synthetic.repair.preservation-fault.archived",
            "synthetic.repair.preservation-fault.post-cut");
        await File.WriteAllTextAsync(
            directory.Location.DatabasePath,
            "damaged evidence retained after preservation fault");
        var databaseBefore = await File.ReadAllBytesAsync(directory.Location.DatabasePath);
        var journalBefore = await File.ReadAllBytesAsync(directory.Location.JournalPath);
        var hook = DelegateRepairHook.At(
            RepairTestPoint.PreservationCompleted,
            token =>
            {
                Assert.False(token.CanBeCanceled);
                throw new InjectedRepairFaultException();
            });

        await Assert.ThrowsAsync<InjectedRepairFaultException>(() =>
            CreateRepairService(directory).RepairAsync(hook));

        Assert.Equal(databaseBefore, await File.ReadAllBytesAsync(directory.Location.DatabasePath));
        Assert.Equal(journalBefore, await File.ReadAllBytesAsync(directory.Location.JournalPath));
        Assert.False(File.Exists(directory.Location.RepairMarkerPath));
        var evidenceDirectory = Assert.Single(
            Directory.EnumerateDirectories(directory.Location.DamagedPreservationDirectoryPath));
        var evidence = await DamagedSourcePreservation.OpenAndValidateAsync(
            directory.Location,
            Path.GetFileName(evidenceDirectory),
            default);
        Assert.Equal(
            databaseBefore,
            await File.ReadAllBytesAsync(Path.Combine(
                evidence.DirectoryPath,
                Path.GetFileName(directory.Location.DatabasePath))));
    }

    [Fact]
    public async Task FaultAfterMutationBeginsRollsBackByteExactAndRetryPreservesDistinctEvidence()
    {
        using var directory = new MemoryTestDirectory();
        const string archivedSubject = "synthetic.repair.rollback.archived";
        const string postCutSubject = "synthetic.repair.rollback.post-cut";
        await CreateBackupAndPostCutAsync(directory, archivedSubject, postCutSubject);
        await File.WriteAllTextAsync(directory.Location.DatabasePath, "damaged rollback evidence");
        await File.WriteAllTextAsync(
            directory.Location.DatabasePath + "-wal",
            "damaged rollback wal evidence");
        await File.WriteAllTextAsync(
            directory.Location.DatabasePath + "-shm",
            "damaged rollback shm evidence");
        var databaseBefore = await File.ReadAllBytesAsync(directory.Location.DatabasePath);
        var journalBefore = await File.ReadAllBytesAsync(directory.Location.JournalPath);
        var walBefore = await File.ReadAllBytesAsync(directory.Location.DatabasePath + "-wal");
        var shmBefore = await File.ReadAllBytesAsync(directory.Location.DatabasePath + "-shm");
        var hook = DelegateRepairHook.At(
            RepairTestPoint.AfterDatabaseReplacement,
            _ => throw new InjectedRepairFaultException());

        await Assert.ThrowsAsync<InjectedRepairFaultException>(() =>
            CreateRepairService(directory).RepairAsync(hook));

        Assert.Equal(databaseBefore, await File.ReadAllBytesAsync(directory.Location.DatabasePath));
        Assert.Equal(journalBefore, await File.ReadAllBytesAsync(directory.Location.JournalPath));
        Assert.Equal(walBefore, await File.ReadAllBytesAsync(directory.Location.DatabasePath + "-wal"));
        Assert.Equal(shmBefore, await File.ReadAllBytesAsync(directory.Location.DatabasePath + "-shm"));
        Assert.False(File.Exists(directory.Location.RepairMarkerPath));
        var firstEvidence = Assert.Single(
            Directory.EnumerateDirectories(directory.Location.DamagedPreservationDirectoryPath));

        var retry = await CreateRepairService(directory).RepairAsync();

        Assert.NotEqual(firstEvidence, retry.DamagedSourceDirectory);
        Assert.Equal(
            2,
            Directory.EnumerateDirectories(directory.Location.DamagedPreservationDirectoryPath).Count());
        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Single(await recovered.RetrieveBySubjectAsync(archivedSubject));
        Assert.Single(await recovered.RetrieveBySubjectAsync(postCutSubject));
        Assert.Equal((2L, 2L, 0L), await recovered.Store.ReadCountsAsync(default));
    }

    [Fact]
    public async Task CancellationAfterMutationStartsIsIgnoredUntilValidRepairCompletes()
    {
        using var directory = new MemoryTestDirectory();
        await CreateBackupAndPostCutAsync(
            directory,
            "synthetic.repair.late-cancel.archived",
            "synthetic.repair.late-cancel.post-cut");
        await File.WriteAllTextAsync(directory.Location.DatabasePath, "damaged late cancellation");
        using var cancellation = new CancellationTokenSource();
        var hook = DelegateRepairHook.At(
            RepairTestPoint.AfterDatabaseReplacement,
            token =>
            {
                Assert.False(token.CanBeCanceled);
                cancellation.Cancel();
                return Task.CompletedTask;
            });

        var result = await CreateRepairService(directory).RepairAsync(hook, cancellation.Token);

        Assert.Equal(2, result.RecoveredThroughSequence);
        Assert.False(File.Exists(directory.Location.RepairMarkerPath));
        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Equal((2L, 2L, 0L), await recovered.Store.ReadCountsAsync(default));
    }

    [Fact]
    public async Task InterruptedRepairMarkerBlocksStartupAndRollsBackBeforeCancellationOrRetry()
    {
        using var directory = new MemoryTestDirectory();
        const string archivedSubject = "synthetic.repair.interrupted.archived";
        const string postCutSubject = "synthetic.repair.interrupted.post-cut";
        await CreateBackupAndPostCutAsync(directory, archivedSubject, postCutSubject);
        await File.WriteAllTextAsync(
            directory.Location.DatabasePath,
            "damaged source before interrupted repair");
        var damagedDatabase = await File.ReadAllBytesAsync(directory.Location.DatabasePath);
        var damagedJournal = await File.ReadAllBytesAsync(directory.Location.JournalPath);

        Guid backupId;
        await using (var backup = await MemoryBackupArchiveValidator.ValidateAsync(
                         directory.Location,
                         directory.Location.BackupArchivePath,
                         default))
        {
            backupId = backup.Manifest.BackupId;
        }

        var interruptedRepairId = Guid.NewGuid();
        var interruptedPreservation = await DamagedSourcePreservation.CreateAsync(
            directory.Location,
            interruptedRepairId,
            backupId,
            default);
        await RepairStateMarkerCodec.WriteAsync(
            directory.Location,
            new RepairStateMarker(
                1,
                interruptedRepairId,
                backupId,
                interruptedRepairId.ToString("N"),
                DateTimeOffset.UtcNow),
            default);

        await File.WriteAllTextAsync(directory.Location.DatabasePath, "partial replacement database");
        await File.WriteAllTextAsync(directory.Location.JournalPath, "partial replacement journal");

        await Assert.ThrowsAsync<MemoryIntegrityException>(() => directory.OpenRepositoryAsync());

        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateRepairService(directory).RepairAsync(cancellationToken: cancellation.Token));
        }

        Assert.Equal(damagedDatabase, await File.ReadAllBytesAsync(directory.Location.DatabasePath));
        Assert.Equal(damagedJournal, await File.ReadAllBytesAsync(directory.Location.JournalPath));
        Assert.False(File.Exists(directory.Location.RepairMarkerPath));
        Assert.True(Directory.Exists(interruptedPreservation.DirectoryPath));

        var result = await CreateRepairService(directory).RepairAsync();

        Assert.NotEqual(interruptedPreservation.DirectoryPath, result.DamagedSourceDirectory);
        Assert.Equal(
            2,
            Directory.EnumerateDirectories(directory.Location.DamagedPreservationDirectoryPath).Count());
        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Single(await recovered.RetrieveBySubjectAsync(archivedSubject));
        Assert.Single(await recovered.RetrieveBySubjectAsync(postCutSubject));
        Assert.Equal((2L, 2L, 0L), await recovered.Store.ReadCountsAsync(default));
    }

    [Fact]
    public async Task RepeatedRepairIsIdempotentAndNeverOverwritesPriorEvidence()
    {
        using var directory = new MemoryTestDirectory();
        await CreateBackupAndPostCutAsync(
            directory,
            "synthetic.repair.repeat.archived",
            "synthetic.repair.repeat.post-cut");
        await File.WriteAllTextAsync(directory.Location.DatabasePath, "damaged repeat source");
        var first = await CreateRepairService(directory).RepairAsync();
        var firstManifestBytes = await File.ReadAllBytesAsync(Path.Combine(
            first.DamagedSourceDirectory,
            "preservation-manifest-v1.json"));

        var second = await CreateRepairService(directory).RepairAsync();

        Assert.NotEqual(first.RepairId, second.RepairId);
        Assert.NotEqual(first.DamagedSourceDirectory, second.DamagedSourceDirectory);
        Assert.Equal(
            firstManifestBytes,
            await File.ReadAllBytesAsync(Path.Combine(
                first.DamagedSourceDirectory,
                "preservation-manifest-v1.json")));
        await using var recovered = await directory.OpenRepositoryAsync();
        Assert.Equal((2L, 2L, 0L), await recovered.Store.ReadCountsAsync(default));
    }

    [Fact]
    public void PublicSurfaceExposesNoBackupRepairMaintenanceRawPathOrProductionCapability()
    {
        var exported = typeof(MemoryRepository).Assembly.GetExportedTypes();

        Assert.DoesNotContain(exported, type =>
            type.Name.Contains("Backup", StringComparison.OrdinalIgnoreCase)
            || type.Name.Contains("Repair", StringComparison.OrdinalIgnoreCase)
            || type.Name.Contains("Maintenance", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(MemoryRepository).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Backup", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("Repair", StringComparison.OrdinalIgnoreCase)
                || method.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(string)
                    && (parameter.Name?.Contains("path", StringComparison.OrdinalIgnoreCase) == true
                        || parameter.Name?.Contains("root", StringComparison.OrdinalIgnoreCase) == true)));
        Assert.DoesNotContain(
            typeof(MemoryStoreLocation).Assembly.GetExportedTypes(),
            type => type.Name.Contains("Production", StringComparison.OrdinalIgnoreCase));
    }

    private static MemoryRepairService CreateRepairService(MemoryTestDirectory directory) =>
        new(
            directory.Location,
            MemoryRepairAuthority.ForExplicitLocalUserIntent());

    private static async Task CreateBackupAndPostCutAsync(
        MemoryTestDirectory directory,
        string archivedSubject,
        string postCutSubject)
    {
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, archivedSubject);
        _ = await repository.CreateBackupAsync();
        await CommitAsync(repository, postCutSubject);
    }

    private static async Task CommitAsync(MemoryRepository repository, string subject)
    {
        var result = await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(SyntheticMemory.Record(subjectKey: subject)));
        Assert.Equal(WriteGateStatus.Committed, result.Status);
    }

    private static void AssertNoPreservation(MemoryStoreLocation location)
    {
        if (Directory.Exists(location.DamagedPreservationDirectoryPath))
        {
            Assert.Empty(Directory.EnumerateDirectories(location.DamagedPreservationDirectoryPath));
        }
    }

    private static async Task CorruptFirstFrameAfterRotationBaseAsync(string journalPath)
    {
        var bytes = await File.ReadAllBytesAsync(journalPath);
        var offset = 8;
        var baseBodyLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));
        offset += sizeof(int) + baseBodyLength;
        var firstPostCutBodyLength = BinaryPrimitives.ReadInt32LittleEndian(
            bytes.AsSpan(offset, sizeof(int)));
        Assert.True(offset + sizeof(int) + firstPostCutBodyLength < bytes.Length);
        bytes[offset + sizeof(int) + firstPostCutBodyLength - 1] ^= 0xff;
        await File.WriteAllBytesAsync(journalPath, bytes);
    }

    private static Task MutateArchiveAsync(string archivePath, ArchiveAttack attack)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        switch (attack)
        {
            case ArchiveAttack.ManifestChecksum:
                ReplaceEntry(
                    archive,
                    MemoryBackupFormat.ManifestChecksumEntryName,
                    Encoding.ASCII.GetBytes(new string('0', 64)));
                break;

            case ArchiveAttack.NonCanonicalManifest:
            {
                var manifestBytes = ReadEntry(archive, MemoryBackupFormat.ManifestEntryName);
                var text = Encoding.UTF8.GetString(manifestBytes).Insert(1, " ");
                ReplaceManifestAndChecksum(archive, Encoding.UTF8.GetBytes(text));
                break;
            }

            case ArchiveAttack.DatabaseChecksum:
            {
                var databaseBytes = ReadEntry(
                    archive,
                    MemoryBackupFormat.DatabaseEntryName);
                databaseBytes[0] ^= 0xff;
                ReplaceEntry(archive, MemoryBackupFormat.DatabaseEntryName, databaseBytes);
                break;
            }

            case ArchiveAttack.DatabaseHealth:
            {
                var databaseBytes = ReadEntry(
                    archive,
                    MemoryBackupFormat.DatabaseEntryName);
                databaseBytes[0] ^= 0xff;
                ReplaceEntry(archive, MemoryBackupFormat.DatabaseEntryName, databaseBytes);
                var manifestBytes = ReadEntry(archive, MemoryBackupFormat.ManifestEntryName);
                var text = Encoding.UTF8.GetString(manifestBytes);
                const string digestProperty = "\"databaseSha256\":\"";
                var digestStart = text.IndexOf(digestProperty, StringComparison.Ordinal)
                    + digestProperty.Length;
                Assert.True(digestStart >= digestProperty.Length);
                text = text.Remove(digestStart, 64).Insert(
                    digestStart,
                    Convert.ToHexString(SHA256.HashData(databaseBytes)).ToLowerInvariant());
                ReplaceManifestAndChecksum(archive, Encoding.UTF8.GetBytes(text));
                break;
            }

            case ArchiveAttack.ExtraEntry:
                archive.CreateEntry("extra.bin");
                break;

            case ArchiveAttack.DuplicateEntry:
                archive.GetEntry(MemoryBackupFormat.DatabaseEntryName)!.Delete();
                archive.CreateEntry(MemoryBackupFormat.ManifestEntryName);
                break;

            case ArchiveAttack.TraversalEntry:
                archive.GetEntry(MemoryBackupFormat.DatabaseEntryName)!.Delete();
                archive.CreateEntry("../escape.bin");
                break;

            case ArchiveAttack.UnsupportedFormat:
                RewriteManifestVersion(archive, "formatVersion", 99);
                break;

            case ArchiveAttack.UnsupportedSchema:
                RewriteManifestVersion(archive, "memorySchemaVersion", 99);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(attack));
        }

        return Task.CompletedTask;
    }

    private static void RewriteManifestVersion(
        ZipArchive archive,
        string propertyName,
        int value)
    {
        var manifestEntry = archive.GetEntry(MemoryBackupFormat.ManifestEntryName)!;
        byte[] bytes;
        using (var stream = manifestEntry.Open())
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            bytes = memory.ToArray();
        }

        var text = Encoding.UTF8.GetString(bytes);
        text = text.Replace($"\"{propertyName}\":1", $"\"{propertyName}\":{value}", StringComparison.Ordinal);
        bytes = Encoding.UTF8.GetBytes(text);
        ReplaceManifestAndChecksum(archive, bytes);
    }

    private static void ReplaceManifestAndChecksum(ZipArchive archive, byte[] bytes)
    {
        ReplaceEntry(archive, MemoryBackupFormat.ManifestEntryName, bytes);
        ReplaceEntry(
            archive,
            MemoryBackupFormat.ManifestChecksumEntryName,
            Encoding.ASCII.GetBytes(
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
    }

    private static byte[] ReadEntry(ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void ReplaceEntry(ZipArchive archive, string name, byte[] bytes)
    {
        archive.GetEntry(name)?.Delete();
        var replacement = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = replacement.Open();
        stream.Write(bytes);
    }

    public enum ArchiveAttack
    {
        ManifestChecksum,
        NonCanonicalManifest,
        DatabaseChecksum,
        DatabaseHealth,
        ExtraEntry,
        DuplicateEntry,
        TraversalEntry,
        UnsupportedFormat,
        UnsupportedSchema,
    }

    private sealed class DelegateRepairHook : IRepairTestHook
    {
        private readonly RepairTestPoint _point;
        private readonly Func<CancellationToken, Task> _callback;

        private DelegateRepairHook(
            RepairTestPoint point,
            Func<CancellationToken, Task> callback)
        {
            _point = point;
            _callback = callback;
        }

        internal static DelegateRepairHook At(
            RepairTestPoint point,
            Func<CancellationToken, Task> callback) =>
            new(point, callback);

        public Task OnPointAsync(RepairTestPoint point, CancellationToken cancellationToken) =>
            point == _point ? _callback(cancellationToken) : Task.CompletedTask;
    }

    private sealed class InjectedRepairFaultException : Exception
    {
    }
}
