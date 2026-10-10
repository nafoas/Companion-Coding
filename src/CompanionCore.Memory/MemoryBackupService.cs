using System.Security.Cryptography;

namespace CompanionCore.Memory;

internal sealed class MemoryBackupService
{
    private readonly MemoryRepository _repository;
    private readonly IVaultCompanion? _companion;

    internal MemoryBackupService(MemoryRepository repository, IVaultCompanion? companion = null)
    {
        _repository = repository;
        _companion = companion;
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
        var companionCandidatePath = Path.Combine(
            location.BackupDirectoryPath,
            $".companion-vault-v1.{attemptId:N}.tmp");
        var companionPromoted = false;
        _ = MemoryPathGuard.RequireImmediateChild(
            location.BackupDirectoryPath,
            companionCandidatePath);
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
            }

            if (_companion is not null)
            {
                // The companion builds and validates its own candidate before anything is
                // promoted; any failure leaves the previous Vault untouched.
                await _companion.BuildCandidateAsync(
                        backupId,
                        createdAtUtc,
                        companionCandidatePath,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!File.Exists(companionCandidatePath))
                {
                    throw new BackupValidationException("The Vault companion produced no candidate archive.");
                }

                if (testHook is not null)
                {
                    await testHook.OnPointAsync(
                            BackupTestPoint.CompanionBuilt,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        BackupTestPoint.BeforeArchivePromotion,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_companion is not null)
            {
                // Companion first: a crash before the memory promotion leaves a newer
                // companion beside the previous memory archive, which restoration tolerates
                // because every file is verified against the restored records.
                PromoteArchive(companionCandidatePath, location.CompanionArchivePath);
                companionPromoted = true;
                if (testHook is not null)
                {
                    await testHook.OnPointAsync(
                            BackupTestPoint.AfterCompanionPromotion,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

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
                archiveDigest,
                _companion is not null);
        }
        finally
        {
            if (!candidatePromoted)
            {
                MemoryPathGuard.TryDeleteTaskOwnedFile(
                    location.BackupDirectoryPath,
                    candidateArchivePath);
            }

            if (!companionPromoted)
            {
                MemoryPathGuard.TryDeleteTaskOwnedFile(
                    location.BackupDirectoryPath,
                    companionCandidatePath);
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
            $".{Path.GetFileNameWithoutExtension(promotedPath)}.{Guid.NewGuid():N}.previous");
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
