using System.IO.Compression;
using System.Security.Cryptography;

namespace CompanionCore.Memory.Tests;

public sealed class MemoryBackupTests
{
    [Fact]
    public async Task HealthyStore_CreatesExactValidatedArchiveAndRotatesAtCut()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, "synthetic.backup.first");
        await CommitAsync(repository, "synthetic.backup.second");

        var result = await repository.CreateBackupAsync();

        Assert.Equal(2, result.CutSequence);
        Assert.NotEqual(Guid.Empty, result.BackupId);
        Assert.Equal(directory.Location.BackupArchivePath, result.ArchivePath);
        Assert.True(result.ArchiveByteLength > 0);
        Assert.Equal(64, result.ArchiveSha256.Length);
        Assert.True(File.Exists(result.ArchivePath));
        Assert.Equal(result.BackupId, repository.Journal.RotationBase?.BackupId);
        Assert.Equal(2L, repository.Journal.RotationBase?.CutSequence);
        Assert.Empty(repository.Journal.AllAppendFrames);
        Assert.Equal(2, repository.Journal.ConfirmedThrough);
        Assert.Equal(2, repository.Journal.HighestAppendSequence);

        using (var stream = File.OpenRead(result.ArchivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            Assert.Equal(
                new[]
                {
                    MemoryBackupFormat.ManifestEntryName,
                    MemoryBackupFormat.ManifestChecksumEntryName,
                    MemoryBackupFormat.DatabaseEntryName,
                },
                archive.Entries.Select(entry => entry.FullName)
                    .OrderBy(name => name, StringComparer.Ordinal));
        }

        await using var validated = await MemoryBackupArchiveValidator.ValidateAsync(
            directory.Location,
            directory.Location.BackupArchivePath,
            default);
        Assert.Equal(result.BackupId, validated.Manifest.BackupId);
        Assert.Equal(2, validated.Manifest.CutSequence);

        await CommitAsync(repository, "synthetic.backup.after-rotation");
        Assert.Equal(3, repository.Journal.HighestAppendSequence);
        Assert.Equal(3, repository.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task WriteAfterPinnedCut_IsExcludedFromSnapshotRetainedByRotationAndRecoverable()
    {
        using var directory = new MemoryTestDirectory();
        const string beforeSubject = "synthetic.before-cut";
        const string afterSubject = "synthetic.after-cut";

        await using (var repository = await directory.OpenRepositoryAsync())
        {
            await CommitAsync(repository, beforeSubject);
            var hook = DelegateBackupHook.At(
                BackupTestPoint.CutEstablished,
                async _ => await CommitAsync(repository, afterSubject));

            var result = await repository.CreateBackupAsync(hook);

            Assert.Equal(1, result.CutSequence);
            var retained = Assert.Single(repository.Journal.AllAppendFrames);
            Assert.Equal(2, retained.Sequence);
            Assert.Equal(1L, repository.Journal.RotationBase?.CutSequence);

            await using var validated = await MemoryBackupArchiveValidator.ValidateAsync(
                directory.Location,
                result.ArchivePath,
                default);
            var snapshotLocation = new MemoryStoreLocation(
                directory.Location.Kind,
                directory.Location.ApplicationNamespace,
                Path.GetDirectoryName(validated.ExtractedDatabasePath)!,
                Path.GetFileName(validated.ExtractedDatabasePath));
            await using var snapshot = await MemoryStore.OpenExistingAsync(snapshotLocation, default);
            Assert.Single(await snapshot.RetrieveBySubjectAsync(beforeSubject, default));
            Assert.Empty(await snapshot.RetrieveBySubjectAsync(afterSubject, default));
        }

        await using var reopened = await directory.OpenRepositoryAsync();
        Assert.Single(await reopened.RetrieveBySubjectAsync(beforeSubject));
        Assert.Single(await reopened.RetrieveBySubjectAsync(afterSubject));
        Assert.Equal(2, reopened.Journal.HighestAppendSequence);
        Assert.Equal(2, reopened.Journal.ConfirmedThrough);
    }

    [Fact]
    public async Task InvalidSourceCannotReplacePreviousValidArchive()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, "synthetic.valid-backup");
        var first = await repository.CreateBackupAsync();
        var originalBytes = await File.ReadAllBytesAsync(first.ArchivePath);

        await SyntheticMemory.ExecuteSqlAsync(
            directory.Location.DatabasePath,
            """
            DROP TRIGGER immutable_memory_records_update;
            UPDATE memory_records SET visible_recollection = 'tampered after backup';
            CREATE TRIGGER immutable_memory_records_update
            BEFORE UPDATE ON memory_records
            BEGIN SELECT RAISE(ABORT, 'append-only committed memory'); END;
            """);

        await Assert.ThrowsAsync<MemoryIntegrityException>(() => repository.CreateBackupAsync());

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(first.ArchivePath));
        await using var stillValid = await MemoryBackupArchiveValidator.ValidateAsync(
            directory.Location,
            first.ArchivePath,
            default);
        Assert.Equal(first.BackupId, stillValid.Manifest.BackupId);
    }

    [Fact]
    public async Task UnexpectedSourceSchemaObjectCannotReplacePreviousValidArchive()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, "synthetic.schema-health.baseline");
        var first = await repository.CreateBackupAsync();
        var originalBytes = await File.ReadAllBytesAsync(first.ArchivePath);

        await SyntheticMemory.ExecuteSqlAsync(
            directory.Location.DatabasePath,
            "CREATE TABLE unexpected_backup_object (value TEXT NOT NULL);");

        await Assert.ThrowsAsync<MemoryIntegrityException>(() => repository.CreateBackupAsync());

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(first.ArchivePath));
        await using var stillValid = await MemoryBackupArchiveValidator.ValidateAsync(
            directory.Location,
            first.ArchivePath,
            default);
        Assert.Equal(first.BackupId, stillValid.Manifest.BackupId);
    }

    [Fact]
    public async Task CancellationBeforePromotionLeavesPreviousArchiveByteExact()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, "synthetic.cancellation-baseline");
        var first = await repository.CreateBackupAsync();
        var originalBytes = await File.ReadAllBytesAsync(first.ArchivePath);
        await CommitAsync(repository, "synthetic.cancellation-next");

        using var cancellation = new CancellationTokenSource();
        var hook = DelegateBackupHook.At(
            BackupTestPoint.CandidateValidated,
            _ =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.CreateBackupAsync(hook, cancellation.Token));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(first.ArchivePath));
        Assert.Equal(1L, repository.Journal.RotationBase?.CutSequence);
        Assert.Equal(2, repository.Journal.HighestAppendSequence);
    }

    [Fact]
    public async Task RotationFaultAfterPromotionKeepsNewArchiveAuthoritativeAndJournalValid()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, "synthetic.rotation-baseline");
        var first = await repository.CreateBackupAsync();
        await CommitAsync(repository, "synthetic.rotation-fault");
        var hook = DelegateBackupHook.At(
            BackupTestPoint.BeforeJournalReplacement,
            _ => throw new InjectedBackupFaultException());

        var exception = await Assert.ThrowsAsync<MemoryBackupRotationException>(() =>
            repository.CreateBackupAsync(hook));

        Assert.Equal(2, exception.CutSequence);
        await using (var promoted = await MemoryBackupArchiveValidator.ValidateAsync(
                         directory.Location,
                         first.ArchivePath,
                         default))
        {
            Assert.Equal(2, promoted.Manifest.CutSequence);
            Assert.NotEqual(first.BackupId, promoted.Manifest.BackupId);
        }

        Assert.Equal(1L, repository.Journal.RotationBase?.CutSequence);
        Assert.Equal(2, repository.Journal.HighestAppendSequence);
        Assert.Equal(2, repository.Journal.ConfirmedThrough);

        var retry = await repository.CreateBackupAsync();
        Assert.Equal(2, retry.CutSequence);
        Assert.Equal(2L, repository.Journal.RotationBase?.CutSequence);
        Assert.Empty(repository.Journal.AllAppendFrames);
    }

    [Fact]
    public async Task InvalidCandidateCannotReplacePreviousArchiveOrRotateJournal()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, "synthetic.invalid-candidate.baseline");
        var first = await repository.CreateBackupAsync();
        var archiveBefore = await File.ReadAllBytesAsync(first.ArchivePath);
        await CommitAsync(repository, "synthetic.invalid-candidate.retained");
        var journalBefore = await ReadLiveJournalBytesAsync(directory.Location.JournalPath);
        var hook = DelegateBackupHook.At(
            BackupTestPoint.CandidateBuilt,
            _ =>
            {
                var candidate = Assert.Single(Directory.EnumerateFiles(
                    directory.Location.BackupDirectoryPath,
                    ".memory-vault-v1.*.tmp"));
                using var archive = ZipFile.Open(candidate, ZipArchiveMode.Update);
                ReplaceEntry(
                    archive,
                    MemoryBackupFormat.ManifestChecksumEntryName,
                    System.Text.Encoding.ASCII.GetBytes(new string('0', 64)));
                return Task.CompletedTask;
            });

        await Assert.ThrowsAsync<BackupValidationException>(() =>
            repository.CreateBackupAsync(hook));

        Assert.Equal(archiveBefore, await File.ReadAllBytesAsync(first.ArchivePath));
        Assert.Equal(journalBefore, await ReadLiveJournalBytesAsync(directory.Location.JournalPath));
        Assert.Equal(1L, repository.Journal.RotationBase?.CutSequence);
        Assert.Equal(2, repository.Journal.HighestAppendSequence);
        Assert.Empty(Directory.EnumerateFiles(
            directory.Location.BackupDirectoryPath,
            ".memory-vault-v1.*.tmp"));
    }

    [Fact]
    public async Task FaultAfterJournalReplacementLeavesCompleteNewJournalAndArchive()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, "synthetic.after-replacement.baseline");
        var first = await repository.CreateBackupAsync();
        await CommitAsync(repository, "synthetic.after-replacement.next");
        var hook = DelegateBackupHook.At(
            BackupTestPoint.AfterJournalReplacement,
            _ => throw new InjectedBackupFaultException());

        var exception = await Assert.ThrowsAsync<MemoryBackupRotationException>(() =>
            repository.CreateBackupAsync(hook));

        Assert.Equal(2, exception.CutSequence);
        await using (var promoted = await MemoryBackupArchiveValidator.ValidateAsync(
                         directory.Location,
                         first.ArchivePath,
                         default))
        {
            Assert.Equal(2, promoted.Manifest.CutSequence);
            Assert.Equal(promoted.Manifest.BackupId, repository.Journal.RotationBase?.BackupId);
        }

        Assert.Equal(2L, repository.Journal.RotationBase?.CutSequence);
        Assert.Equal(2, repository.Journal.HighestAppendSequence);
        Assert.Equal(2, repository.Journal.ConfirmedThrough);
        Assert.Empty(repository.Journal.AllAppendFrames);
    }

    [Fact]
    public async Task CancellationAfterArchivePromotionIsIgnoredThroughJournalRotation()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, "synthetic.late-backup-cancellation.first");
        _ = await repository.CreateBackupAsync();
        await CommitAsync(repository, "synthetic.late-backup-cancellation.second");
        using var cancellation = new CancellationTokenSource();
        var hook = DelegateBackupHook.At(
            BackupTestPoint.AfterArchivePromotion,
            token =>
            {
                Assert.False(token.CanBeCanceled);
                cancellation.Cancel();
                return Task.CompletedTask;
            });

        var result = await repository.CreateBackupAsync(hook, cancellation.Token);

        Assert.Equal(2, result.CutSequence);
        Assert.Equal(result.BackupId, repository.Journal.RotationBase?.BackupId);
        Assert.Equal(2L, repository.Journal.RotationBase?.CutSequence);
        Assert.Equal(2, repository.Journal.ConfirmedThrough);
        Assert.Empty(repository.Journal.AllAppendFrames);
    }

    [Fact]
    public async Task ConcurrentBackupsAreSerializedSoAnOlderCutCannotRegressTheArchive()
    {
        using var directory = new MemoryTestDirectory();
        await using var repository = await directory.OpenRepositoryAsync();
        await CommitAsync(repository, "synthetic.concurrent-backup.first");
        var firstCutReached = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCutReached = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstHook = DelegateBackupHook.At(
            BackupTestPoint.CutEstablished,
            async _ =>
            {
                firstCutReached.TrySetResult();
                await releaseFirst.Task;
            });
        var secondHook = DelegateBackupHook.At(
            BackupTestPoint.CutEstablished,
            _ =>
            {
                secondCutReached.TrySetResult();
                return Task.CompletedTask;
            });

        var firstTask = repository.CreateBackupAsync(firstHook);
        await firstCutReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await CommitAsync(repository, "synthetic.concurrent-backup.second");
        var secondTask = repository.CreateBackupAsync(secondHook);

        var prematureSecondCut = await Task.WhenAny(
            secondCutReached.Task,
            Task.Delay(TimeSpan.FromMilliseconds(250)));
        try
        {
            Assert.NotSame(secondCutReached.Task, prematureSecondCut);
        }
        finally
        {
            releaseFirst.TrySetResult();
        }

        var first = await firstTask;
        var second = await secondTask;
        Assert.Equal(1, first.CutSequence);
        Assert.Equal(2, second.CutSequence);
        Assert.Equal(second.BackupId, repository.Journal.RotationBase?.BackupId);
        Assert.Equal(2L, repository.Journal.RotationBase?.CutSequence);
        await using var promoted = await MemoryBackupArchiveValidator.ValidateAsync(
            directory.Location,
            directory.Location.BackupArchivePath,
            default);
        Assert.Equal(second.BackupId, promoted.Manifest.BackupId);
        Assert.Equal(2, promoted.Manifest.CutSequence);
    }

    private static async Task CommitAsync(MemoryRepository repository, string subject)
    {
        var result = await repository.WriteGate.SubmitAsync(
            SyntheticMemory.Proposal(SyntheticMemory.Record(subjectKey: subject)));
        Assert.Equal(WriteGateStatus.Committed, result.Status);
    }

    private static void ReplaceEntry(ZipArchive archive, string name, byte[] bytes)
    {
        archive.GetEntry(name)?.Delete();
        var replacement = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = replacement.Open();
        stream.Write(bytes);
    }

    private static async Task<byte[]> ReadLiveJournalBytesAsync(string path)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        return bytes.ToArray();
    }

    private sealed class DelegateBackupHook : IBackupTestHook
    {
        private readonly BackupTestPoint _point;
        private readonly Func<CancellationToken, Task> _callback;

        private DelegateBackupHook(
            BackupTestPoint point,
            Func<CancellationToken, Task> callback)
        {
            _point = point;
            _callback = callback;
        }

        internal static DelegateBackupHook At(
            BackupTestPoint point,
            Func<CancellationToken, Task> callback) =>
            new(point, callback);

        public Task OnPointAsync(BackupTestPoint point, CancellationToken cancellationToken) =>
            point == _point ? _callback(cancellationToken) : Task.CompletedTask;
    }

    private sealed class InjectedBackupFaultException : Exception
    {
    }
}
