using System.Security.Cryptography;

namespace CompanionCore.Memory;

internal sealed class MemoryBackupService
{
    private readonly MemoryRepository _repository;

    internal MemoryBackupService(MemoryRepository repository)
    {
        _repository = repository;
    }

    internal async Task<MemoryBackupResult> CreateAsync(
        IBackupTestHook? testHook,
        CancellationToken cancellationToken)
    {
        _repository.ThrowIfDisposed();
        var location = _repository.Location;
        Directory.CreateDirectory(location.BackupDirectoryPath);
        Directory.CreateDirectory(location.BackupStagingDirectoryPath);

        var attemptId = Guid.NewGuid();
        var backupId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;
        var stagingDirectory = Path.Combine(
            location.BackupStagingDirectoryPath,
            attemptId.ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        var snapshotPath = Path.Combine(
            stagingDirectory,
            MemoryBackupFormat.DatabaseEntryName);
        var candidateArchivePath = Path.Combine(
            location.BackupDirectoryPath,
            $".memory-vault-v1.{attemptId:N}.tmp");
        var candidatePromoted = false;
        _ = MemoryPathGuard.RequireImmediateChild(
            location.BackupDirectoryPath,
            candidateArchivePath);

        try
        {
            long cutSequence;
            await using (var pinnedSnapshot = await _repository.Coordinator
                             .EstablishBackupCutAsync(cancellationToken)
                             .ConfigureAwait(false))
            {
                cutSequence = pinnedSnapshot.CutSequence;
                if (testHook is not null)
                {
                    await testHook.OnPointAsync(
                            BackupTestPoint.CutEstablished,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await pinnedSnapshot.CopyToAsync(snapshotPath, cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        BackupTestPoint.SnapshotCopied,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var snapshotLocation = new MemoryStoreLocation(
                location.Kind,
                location.ApplicationNamespace,
                stagingDirectory,
                MemoryBackupFormat.DatabaseEntryName);
            await using (var snapshotStore = await MemoryStore.OpenExistingAsync(
                             snapshotLocation,
                             cancellationToken)
                         .ConfigureAwait(false))
            {
                _ = await snapshotStore.ValidateFullHealthAsync(
                        cutSequence,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            _ = await _repository.Coordinator.ValidateSourceHealthAsync(cancellationToken)
                .ConfigureAwait(false);
            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        BackupTestPoint.SourceValidated,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var manifest = await MemoryBackupArchiveWriter.BuildAsync(
                    snapshotPath,
                    candidateArchivePath,
                    backupId,
                    createdAtUtc,
                    cutSequence,
                    cancellationToken)
                .ConfigureAwait(false);
            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        BackupTestPoint.CandidateBuilt,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await using (var validated = await MemoryBackupArchiveValidator.ValidateAsync(
                             location,
                             candidateArchivePath,
                             cancellationToken)
                         .ConfigureAwait(false))
            {
                if (validated.Manifest != manifest)
                {
                    throw new BackupValidationException(
                        "Independent archive validation did not reproduce the staged manifest.");
                }
            }

            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        BackupTestPoint.CandidateValidated,
                        cancellationToken)
                    .ConfigureAwait(false);
                await testHook.OnPointAsync(
                        BackupTestPoint.BeforeArchivePromotion,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            PromoteArchive(candidateArchivePath, location.BackupArchivePath);
            candidatePromoted = true;

            try
            {
                if (testHook is not null)
                {
                    await testHook.OnPointAsync(
                            BackupTestPoint.AfterArchivePromotion,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }

                await _repository.Coordinator.RotateJournalThroughAsync(
                        cutSequence,
                        backupId,
                        testHook,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new MemoryBackupRotationException(
                    location.BackupArchivePath,
                    cutSequence,
                    exception);
            }

            var archiveLength = new FileInfo(location.BackupArchivePath).Length;
            string archiveDigest;
            await using (var archive = new FileStream(
                             location.BackupArchivePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             bufferSize: 64 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                archiveDigest = Convert.ToHexString(
                        await SHA256.HashDataAsync(archive, CancellationToken.None).ConfigureAwait(false))
                    .ToLowerInvariant();
            }

            return new MemoryBackupResult(
                backupId,
                cutSequence,
                location.BackupArchivePath,
                archiveLength,
                archiveDigest);
        }
        finally
        {
            if (!candidatePromoted)
            {
                MemoryPathGuard.TryDeleteTaskOwnedFile(
                    location.BackupDirectoryPath,
                    candidateArchivePath);
            }

            MemoryPathGuard.TryDeleteTaskOwnedDirectory(
                location.BackupStagingDirectoryPath,
                stagingDirectory);
        }
    }

    private static void PromoteArchive(string candidatePath, string promotedPath)
    {
        var directory = Path.GetDirectoryName(promotedPath)
            ?? throw new MemoryIntegrityException("The promoted archive has no parent directory.");
        _ = MemoryPathGuard.RequireImmediateChild(directory, candidatePath);
        _ = MemoryPathGuard.RequireImmediateChild(directory, promotedPath);

        if (!File.Exists(promotedPath))
        {
            File.Move(candidatePath, promotedPath);
            return;
        }

        var rollbackPath = Path.Combine(
            directory,
            $".memory-vault-v1.{Guid.NewGuid():N}.previous");
        try
        {
            File.Replace(candidatePath, promotedPath, rollbackPath, ignoreMetadataErrors: true);
            try
            {
                File.Delete(rollbackPath);
            }
            catch (IOException)
            {
                // The fixed promoted archive is already valid and authoritative. A
                // uniquely named, non-authoritative prior copy may be cleaned on a
                // later attempt; cleanup failure must not undo or obscure promotion.
            }
            catch (UnauthorizedAccessException)
            {
                // Same outcome as the IOException case above.
            }
        }
        catch
        {
            if (File.Exists(rollbackPath) && !File.Exists(promotedPath))
            {
                File.Move(rollbackPath, promotedPath);
            }

            throw;
        }
    }
}
