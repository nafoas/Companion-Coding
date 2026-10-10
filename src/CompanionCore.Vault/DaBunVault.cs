using System.Security.Cryptography;
using CompanionCore.Keepsakes;
using CompanionCore.Memory;
using CompanionCore.Privacy;

namespace CompanionCore.Vault;

public enum CompanionStatus
{
    Valid = 1,
    Missing = 2,
    Invalid = 3,
}

public sealed record VaultRestoreReport(
    Guid RepairId,
    Guid MemoryBackupId,
    long RecoveredThroughSequence,
    CompanionStatus Companion,
    Guid? CompanionBackupId,
    int PhotographsVerified,
    int PhotographsRestored,
    IReadOnlyList<Guid> MissingPhotographs,
    int StatesKept,
    int StatesRestored,
    string DamagedSourceDirectory)
{
    /// <summary>True only when the companion was valid and every live photograph verifies.</summary>
    public bool Complete => Companion == CompanionStatus.Valid && MissingPhotographs.Count == 0;
}

/// <summary>
/// Da Bun Vault: the accepted memory archive plus a companion archive of photographs and
/// state. It is the single composition point of backup and repair authority. Nothing here
/// deletes a live photograph or a BunDex record; damaged copies are preserved, not discarded.
/// </summary>
internal static class DaBunVault
{
    internal const string DamagedDirectoryName = "damaged-v1";
    private const string CompanionValidationDirectoryName = ".companion-validation-v1";
    private const int PageSize = 500;

    internal static Task<MemoryBackupResult> CreateAsync(
        MemoryRepository repository,
        KeepsakeLocation keepsakes,
        VaultStateStore state,
        IBackupTestHook? testHook = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(keepsakes);
        ArgumentNullException.ThrowIfNull(state);
        return repository.CreateBackupAsync(new Companion(repository.Location, keepsakes, state), testHook, cancellationToken);
    }

    /// <summary>
    /// Bnuy Repairs for the whole Vault: validate the companion, run the accepted memory repair,
    /// then restore photographs and state against what was restored, and report honestly.
    /// </summary>
    internal static async Task<VaultRestoreReport> RestoreAsync(
        MemoryStoreLocation location,
        RuntimePrivacyState privacy,
        KeepsakeLocation keepsakes,
        VaultStateStore state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(privacy);
        ArgumentNullException.ThrowIfNull(keepsakes);
        ArgumentNullException.ThrowIfNull(state);

        ValidatedCompanion? companion = null;
        var companionStatus = CompanionStatus.Missing;
        if (File.Exists(location.CompanionArchivePath))
        {
            try
            {
                companion = await CompanionArchive.ValidateAsync(
                        location.CompanionArchivePath,
                        Path.Combine(location.RootPath, CompanionValidationDirectoryName),
                        expectedBackupId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                companionStatus = CompanionStatus.Valid;
            }
            catch (BackupValidationException)
            {
                companionStatus = CompanionStatus.Invalid;
            }
        }

        using (companion)
        {
            var repair = await new MemoryRepairService(location, MemoryRepairAuthority.ForExplicitLocalUserIntent())
                .RepairAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            int verified = 0, restored = 0;
            var missing = new List<Guid>();
            await using (var repository = await MemoryRepository.OpenAsync(location, privacy, cancellationToken).ConfigureAwait(false))
            {
                foreach (var (photograph, metadata) in await LivePhotographsAsync(repository, cancellationToken).ConfigureAwait(false))
                {
                    var path = Path.Combine(keepsakes.RootPath, KeepsakeRecords.FileName(metadata.ActionId));
                    if (await MatchesAsync(path, metadata.Sha256, cancellationToken).ConfigureAwait(false))
                    {
                        verified++;
                        continue;
                    }

                    var entryName = CompanionArchive.KeepsakeEntryName(metadata.ActionId);
                    if (companion?.Entry(entryName) is { } entry && entry.Sha256 == metadata.Sha256)
                    {
                        PreserveIfPresent(keepsakes.RootPath, path);
                        await ReplaceAtomicallyAsync(keepsakes.RootPath, companion.PathFor(entryName)!, path, cancellationToken).ConfigureAwait(false);
                        restored++;
                    }
                    else
                    {
                        missing.Add(photograph.Record.RecordId);
                    }
                }
            }

            int kept = 0, statesRestored = 0;
            foreach (var entry in companion?.Manifest.Entries.Where(entry => entry.Name.StartsWith("state-", StringComparison.Ordinal)) ?? [])
            {
                var name = entry.Name["state-".Length..^VaultStateStore.Extension.Length];
                if ((await state.GetAsync(name, cancellationToken).ConfigureAwait(false)).Status == StateStatus.Verified)
                {
                    kept++;
                    continue;
                }

                PreserveIfPresent(state.Location.RootPath, state.PathFor(name));
                var envelope = await File.ReadAllBytesAsync(companion!.PathFor(entry.Name)!, cancellationToken).ConfigureAwait(false);
                await state.WriteEnvelopeAsync(name, envelope, cancellationToken).ConfigureAwait(false);
                statesRestored++;
            }

            return new VaultRestoreReport(
                repair.RepairId,
                repair.BackupId,
                repair.RecoveredThroughSequence,
                companionStatus,
                companion?.Manifest.BackupId,
                verified,
                restored,
                missing,
                kept,
                statesRestored,
                repair.DamagedSourceDirectory);
        }
    }

    /// <summary>Every photograph record not superseded by an explicit deletion, through keyset paging.</summary>
    internal static async Task<IReadOnlyList<(RetrievedMemory Photograph, KeepsakeMetadata Metadata)>> LivePhotographsAsync(
        MemoryRepository repository,
        CancellationToken cancellationToken)
    {
        var photographs = new Dictionary<string, (RetrievedMemory, KeepsakeMetadata)>(StringComparer.Ordinal);
        var deleted = new HashSet<string>(StringComparer.Ordinal);
        Guid? after = null;
        while (true)
        {
            var page = await repository.RetrieveSubjectPrefixPageAsync(KeepsakeRecords.SubjectPrefix, after, PageSize, cancellationToken).ConfigureAwait(false);
            foreach (var memory in page)
            {
                var metadata = KeepsakeMetadata.Parse(memory.Record.RetrievalMetadataJson);
                if (metadata is null || memory.Record.SubjectKey != KeepsakeRecords.Subject(metadata.ActionId))
                {
                    continue;
                }

                if (metadata.Deleted)
                {
                    deleted.Add(memory.Record.SubjectKey);
                }
                else
                {
                    photographs[memory.Record.SubjectKey] = (memory, metadata);
                }
            }

            if (page.Count < PageSize)
            {
                break;
            }

            after = page[^1].Record.RecordId;
        }

        return photographs
            .Where(pair => !deleted.Contains(pair.Key))
            .Select(pair => pair.Value)
            .OrderBy(item => item.Item1.Record.RecordId)
            .ToArray();
    }

    private static async Task<bool> MatchesAsync(string path, string sha256, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
        var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        return string.Equals(actual, sha256, StringComparison.Ordinal);
    }

    /// <summary>A damaged copy is evidence: it is moved aside, never deleted.</summary>
    private static void PreserveIfPresent(string root, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var damaged = Path.Combine(root, DamagedDirectoryName);
        Directory.CreateDirectory(damaged);
        File.Move(path, Path.Combine(damaged, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.damaged"));
    }

    private static async Task ReplaceAtomicallyAsync(string root, string source, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        var temporary = Path.Combine(root, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }

            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private sealed class Companion(MemoryStoreLocation location, KeepsakeLocation keepsakes, VaultStateStore state) : IVaultCompanion
    {
        public async Task BuildCandidateAsync(Guid backupId, DateTimeOffset createdAtUtc, string candidatePath, CancellationToken cancellationToken)
        {
            var built = await CompanionArchive.BuildAsync(backupId, createdAtUtc, keepsakes, state, candidatePath, cancellationToken).ConfigureAwait(false);
            using var validated = await CompanionArchive.ValidateAsync(
                    candidatePath,
                    Path.Combine(location.RootPath, CompanionValidationDirectoryName),
                    backupId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!validated.Manifest.SameAs(built))
            {
                throw new BackupValidationException("Independent companion validation did not reproduce the built manifest.");
            }
        }
    }
}
