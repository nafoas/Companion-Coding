using System.Security.Cryptography;

namespace CompanionCore.Memory;

internal sealed class MemoryRepairService
{
    private readonly MemoryStoreLocation _location;
    private readonly MemoryRepairAuthority _authority;

    internal MemoryRepairService(
        MemoryStoreLocation location,
        MemoryRepairAuthority authority)
    {
        _location = location ?? throw new ArgumentNullException(nameof(location));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
    }

    internal async Task<MemoryRepairResult> RepairAsync(
        IRepairTestHook? testHook = null,
        CancellationToken cancellationToken = default)
    {
        _ = _authority;
        using var lease = MemoryRepositoryLease.Acquire(_location);
        if (File.Exists(_location.RepairMarkerPath))
        {
            // A prior attempt has already crossed the mutation boundary. Its marker
            // keeps ordinary startup closed, and caller cancellation must not leave
            // another partially restored mix. Finish the byte-exact rollback first,
            // then honor cancellation before beginning a fresh repair attempt.
            await RollBackInterruptedRepairAsync(CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        var repairId = Guid.NewGuid();
        var files = RepairFileSet.Create(_location, repairId);
        Directory.CreateDirectory(_location.RepairStagingDirectoryPath);
        Directory.CreateDirectory(files.ValidationDirectory);
        DamagedSourcePreservation? preservation = null;
        var mutationStarted = false;

        try
        {
            await using var backup = await MemoryBackupArchiveValidator.ValidateAsync(
                    _location,
                    _location.BackupArchivePath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        RepairTestPoint.ArchiveValidated,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var postCutFrames = await ReadAndValidatePostCutJournalAsync(
                    backup.Manifest,
                    files.ValidationJournalPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        RepairTestPoint.JournalValidated,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await CopyFileDurablyAsync(
                    backup.ExtractedDatabasePath,
                    files.DatabaseCandidatePath,
                    cancellationToken)
                .ConfigureAwait(false);
            await SessionJournal.BuildRecoveryJournalAsync(
                    files.JournalCandidatePath,
                    backup.Manifest.BackupId,
                    backup.Manifest.CutSequence,
                    postCutFrames,
                    cancellationToken)
                .ConfigureAwait(false);
            await ValidateRecoveryJournalAsync(
                    files.JournalCandidatePath,
                    backup.Manifest,
                    postCutFrames.Count,
                    cancellationToken)
                .ConfigureAwait(false);

            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        RepairTestPoint.BeforeMutation,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Beginning damaged-source preservation starts the guarded maintenance
            // transaction. An incomplete bundle is removed by CreateAsync itself;
            // once a complete validated bundle exists, it is immutable evidence and
            // caller cancellation is ignored through completion or rollback.
            preservation = await DamagedSourcePreservation.CreateAsync(
                    _location,
                    repairId,
                    backup.Manifest.BackupId,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        RepairTestPoint.PreservationCompleted,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            var marker = new RepairStateMarker(
                1,
                repairId,
                backup.Manifest.BackupId,
                repairId.ToString("N"),
                DateTimeOffset.UtcNow);
            await RepairStateMarkerCodec.WriteAsync(
                    _location,
                    marker,
                    CancellationToken.None)
                .ConfigureAwait(false);
            mutationStarted = true;

            MoveIfPresent(_location.DatabasePath + "-wal", files.WalRollbackPath);
            MoveIfPresent(_location.DatabasePath + "-shm", files.ShmRollbackPath);
            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        RepairTestPoint.AfterCompanionsMoved,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            File.Replace(
                files.DatabaseCandidatePath,
                _location.DatabasePath,
                files.DatabaseRollbackPath,
                ignoreMetadataErrors: true);
            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        RepairTestPoint.AfterDatabaseReplacement,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            File.Replace(
                files.JournalCandidatePath,
                _location.JournalPath,
                files.JournalRollbackPath,
                ignoreMetadataErrors: true);
            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        RepairTestPoint.AfterJournalReplacement,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                await testHook.OnPointAsync(
                        RepairTestPoint.BeforeRecoveryOpen,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            MemoryHealthReport health;
            long recoveredThrough;
            await using (var recovered = await MemoryRepository.OpenForMaintenanceValidationAsync(
                             _location,
                             lease,
                             CancellationToken.None)
                         .ConfigureAwait(false))
            {
                recoveredThrough = recovered.Journal.HighestAppendSequence;
                if (recovered.Journal.ConfirmedThrough != recoveredThrough)
                {
                    throw new MemoryIntegrityException(
                        "Ordinary recovery did not checkpoint every valid post-cut append.");
                }

                health = await recovered.Store.ValidateFullHealthAsync(
                        recoveredThrough,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            if (testHook is not null)
            {
                await testHook.OnPointAsync(
                        RepairTestPoint.AfterRecoveryValidation,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            RepairStateMarkerCodec.Delete(_location);
            mutationStarted = false;
            files.DeleteNonAuthoritativeFiles();

            return new MemoryRepairResult(
                repairId,
                backup.Manifest.BackupId,
                backup.Manifest.CutSequence,
                recoveredThrough,
                health.OperationCount,
                health.RecordCount,
                health.LinkCount,
                preservation?.DirectoryPath
                    ?? throw new MemoryIntegrityException(
                        "Repair completed without its required damaged-source preservation."));
        }
        catch (Exception repairException)
        {
            mutationStarted |= File.Exists(_location.RepairMarkerPath);
            if (mutationStarted)
            {
                try
                {
                    var marker = await RepairStateMarkerCodec.ReadAsync(
                            _location,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    var preserved = await DamagedSourcePreservation.OpenAndValidateAsync(
                            _location,
                            marker.PreservationDirectoryName,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    await RestorePreservedSourceAsync(
                            marker,
                            preserved,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception rollbackException)
                {
                    throw new MemoryIntegrityException(
                        "Repair failed and its byte-exact rollback could not be completed.",
                        new AggregateException(repairException, rollbackException));
                }
            }
            throw;
        }
        finally
        {
            files.DeleteCandidatesOnly();
            MemoryPathGuard.TryDeleteTaskOwnedDirectory(
                _location.RepairStagingDirectoryPath,
                files.ValidationDirectory);
        }
    }

    private async Task RollBackInterruptedRepairAsync(CancellationToken cancellationToken)
    {
        var marker = await RepairStateMarkerCodec.ReadAsync(_location, cancellationToken)
            .ConfigureAwait(false);
        var preservation = await DamagedSourcePreservation.OpenAndValidateAsync(
                _location,
                marker.PreservationDirectoryName,
                cancellationToken)
            .ConfigureAwait(false);
        if (preservation.Manifest.BackupId != marker.BackupId)
        {
            throw new MemoryIntegrityException(
                "The interrupted repair marker and preservation bundle disagree.");
        }

        await RestorePreservedSourceAsync(marker, preservation, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<JournalAppendFrame>> ReadAndValidatePostCutJournalAsync(
        MemoryBackupManifest manifest,
        string validationJournalPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_location.JournalPath))
        {
            throw new MemoryIntegrityException("The live journal required for repair is missing.");
        }

        await CopyFileDurablyAsync(
                _location.JournalPath,
                validationJournalPath,
                cancellationToken)
            .ConfigureAwait(false);
        await using var journal = await SessionJournal.OpenAsync(
                validationJournalPath,
                cancellationToken)
            .ConfigureAwait(false);
        if ((journal.RotationBase?.CutSequence ?? 0) > manifest.CutSequence
            || journal.HighestAppendSequence < manifest.CutSequence)
        {
            throw new JournalCorruptionException(
                "The live journal cannot prove continuity from the promoted backup cut.");
        }

        var postCut = journal.AllAppendFrames
            .Where(frame => frame.Sequence > manifest.CutSequence)
            .OrderBy(frame => frame.Sequence)
            .Select(frame => new JournalAppendFrame(
                frame.Sequence,
                frame.CanonicalOperationPayload.ToArray()))
            .ToArray();
        var expectedSequence = manifest.CutSequence + 1;
        var operationIds = new HashSet<Guid>();
        foreach (var frame in postCut)
        {
            if (frame.Sequence != expectedSequence)
            {
                throw new JournalCorruptionException(
                    "The post-backup journal tail is not globally contiguous.");
            }

            expectedSequence = checked(expectedSequence + 1);
            var proposal = MemoryPayloadParser.ParseOperation(frame.CanonicalOperationPayload);
            var prepared = MemoryProposalValidator.Prepare(proposal);
            if (!frame.CanonicalOperationPayload.AsSpan().SequenceEqual(prepared.CanonicalPayload)
                || !operationIds.Add(proposal.LocalOperationId))
            {
                throw new JournalCorruptionException(
                    "A post-cut journal operation is non-canonical or reuses an operation ID.");
            }
        }

        return postCut;
    }

    private static async Task ValidateRecoveryJournalAsync(
        string path,
        MemoryBackupManifest manifest,
        int expectedTailCount,
        CancellationToken cancellationToken)
    {
        await using var journal = await SessionJournal.OpenAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (journal.RotationBase?.BackupId != manifest.BackupId
            || journal.RotationBase?.CutSequence != manifest.CutSequence
            || journal.ConfirmedThrough != manifest.CutSequence
            || journal.RecoveryTail.Count != expectedTailCount)
        {
            throw new JournalCorruptionException(
                "The staged recovery journal failed independent validation.");
        }
    }

    private async Task RestorePreservedSourceAsync(
        RepairStateMarker marker,
        DamagedSourcePreservation preservation,
        CancellationToken cancellationToken)
    {
        if (preservation.Manifest.RepairId != marker.RepairId
            || preservation.Manifest.BackupId != marker.BackupId)
        {
            throw new MemoryIntegrityException(
                "The repair marker cannot authorize a different preservation bundle.");
        }

        var expectedLivePaths = new[]
        {
            _location.DatabasePath,
            _location.DatabasePath + "-wal",
            _location.DatabasePath + "-shm",
            _location.JournalPath,
        };
        foreach (var livePath in expectedLivePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(livePath);
            var preserved = preservation.Manifest.Files.SingleOrDefault(file =>
                string.Equals(file.Name, name, StringComparison.Ordinal));
            if (preserved is null)
            {
                if (File.Exists(livePath))
                {
                    File.Delete(livePath);
                }

                continue;
            }

            var source = Path.Combine(preservation.DirectoryPath, preserved.Name);
            var temporary = Path.Combine(
                _location.RootPath,
                $".{name}.{marker.RepairId:N}.restore.tmp");
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            await CopyFileDurablyAsync(source, temporary, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporary, livePath, overwrite: true);
        }

        foreach (var preserved in preservation.Manifest.Files)
        {
            var livePath = Path.Combine(_location.RootPath, preserved.Name);
            await using var stream = new FileStream(
                livePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var digest = Convert.ToHexString(
                    await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false))
                .ToLowerInvariant();
            if (stream.Length != preserved.ByteLength
                || !CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(digest),
                    Convert.FromHexString(preserved.Sha256)))
            {
                throw new MemoryIntegrityException(
                    "Rollback did not reproduce the preserved damaged source exactly.");
            }
        }

        RepairFileSet.Create(_location, marker.RepairId).DeleteNonAuthoritativeFiles();
        RepairStateMarkerCodec.Delete(_location);
    }

    private static async Task CopyFileDurablyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await source.CopyToAsync(destination, 64 * 1024, cancellationToken)
            .ConfigureAwait(false);
        await destination.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        destination.Flush(flushToDisk: true);
    }

    private static void MoveIfPresent(string sourcePath, string destinationPath)
    {
        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath);
        }
    }

    private sealed record RepairFileSet(
        string ValidationDirectory,
        string ValidationJournalPath,
        string DatabaseCandidatePath,
        string JournalCandidatePath,
        string DatabaseRollbackPath,
        string JournalRollbackPath,
        string WalRollbackPath,
        string ShmRollbackPath)
    {
        internal static RepairFileSet Create(MemoryStoreLocation location, Guid repairId)
        {
            var token = repairId.ToString("N");
            var databaseName = Path.GetFileName(location.DatabasePath);
            var journalName = Path.GetFileName(location.JournalPath);
            var validationDirectory = Path.Combine(
                location.RepairStagingDirectoryPath,
                token);
            return new RepairFileSet(
                validationDirectory,
                Path.Combine(validationDirectory, "live-journal-validation-v1.bin"),
                Path.Combine(location.RootPath, $".{databaseName}.{token}.repair.new"),
                Path.Combine(location.RootPath, $".{journalName}.{token}.repair.new"),
                Path.Combine(location.RootPath, $".{databaseName}.{token}.repair.old"),
                Path.Combine(location.RootPath, $".{journalName}.{token}.repair.old"),
                Path.Combine(location.RootPath, $".{databaseName}-wal.{token}.repair.old"),
                Path.Combine(location.RootPath, $".{databaseName}-shm.{token}.repair.old"));
        }

        internal void DeleteCandidatesOnly()
        {
            DeleteIfPresent(DatabaseCandidatePath);
            DeleteIfPresent(JournalCandidatePath);
        }

        internal void DeleteNonAuthoritativeFiles()
        {
            DeleteCandidatesOnly();
            DeleteIfPresent(DatabaseRollbackPath);
            DeleteIfPresent(JournalRollbackPath);
            DeleteIfPresent(WalRollbackPath);
            DeleteIfPresent(ShmRollbackPath);
        }

        private static void DeleteIfPresent(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // A task-owned non-authoritative candidate/rollback may be cleaned by
                // a later bounded maintenance attempt. It can never become live by name.
            }
            catch (UnauthorizedAccessException)
            {
                // Same safe orphan outcome as the IOException case above.
            }
        }
    }
}
