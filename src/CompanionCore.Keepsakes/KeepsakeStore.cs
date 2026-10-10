using System.Security.Cryptography;
using CompanionCore.Memory;

namespace CompanionCore.Keepsakes;

/// <summary>Only an explicit, local Boss request can construct this capability.</summary>
internal sealed class KeepsakeDeletionAuthority
{
    private KeepsakeDeletionAuthority()
    {
    }

    internal static KeepsakeDeletionAuthority ForExplicitLocalUserIntent() => new();
}

/// <summary>
/// Local inspection of committed photographs. Committed records are never removed: an
/// explicit deletion appends a superseding note and removes only that photograph's file.
/// Nothing here cleans up automatically; orphans and disk growth are only reported.
/// </summary>
public sealed class KeepsakeStore
{
    private readonly MemoryRepository _repository;
    private readonly KeepsakeLocation _location;

    public KeepsakeStore(MemoryRepository repository, KeepsakeLocation location)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _location = location ?? throw new ArgumentNullException(nameof(location));
    }

    /// <summary>Lists up to <see cref="MemoryQuery.MaximumLimit"/> photograph records, newest first.</summary>
    public async Task<IReadOnlyList<PhotographEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _repository.RetrieveAsync(
                new MemoryQuery { SubjectPrefix = KeepsakeRecords.SubjectPrefix, Limit = MemoryQuery.MaximumLimit },
                cancellationToken)
            .ConfigureAwait(false);
        var parsed = records
            .Select(memory => (Memory: memory, Metadata: KeepsakeMetadata.Parse(memory.Record.RetrievalMetadataJson)))
            .Where(item => item.Metadata is not null && item.Memory.Record.SubjectKey == KeepsakeRecords.Subject(item.Metadata.ActionId))
            .ToArray();
        var deleted = parsed.Where(item => item.Metadata!.Deleted).Select(item => item.Memory.Record.SubjectKey).ToHashSet(StringComparer.Ordinal);
        return parsed
            .Where(item => !item.Metadata!.Deleted)
            .Select(item => Entry(item.Memory, item.Metadata!, deleted.Contains(item.Memory.Record.SubjectKey)))
            .OrderByDescending(entry => entry.TakenAt)
            .ThenBy(entry => entry.PhotographId)
            .ToArray();
    }

    /// <summary>Returns the photograph's bytes only when they match the committed digest.</summary>
    public async Task<KeepsakeInspection> InspectAsync(Guid photographId, CancellationToken cancellationToken = default)
    {
        var found = await FindAsync(photographId, cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            return new KeepsakeInspection(InspectionStatus.Unknown, ReadOnlyMemory<byte>.Empty);
        }

        if (found.Value.Deleted)
        {
            return new KeepsakeInspection(InspectionStatus.Deleted, ReadOnlyMemory<byte>.Empty);
        }

        var path = Path.Combine(_location.RootPath, KeepsakeRecords.FileName(found.Value.Metadata.ActionId));
        if (!File.Exists(path))
        {
            return new KeepsakeInspection(InspectionStatus.Missing, ReadOnlyMemory<byte>.Empty);
        }

        var length = new FileInfo(path).Length;
        if (length != found.Value.Metadata.ByteLength)
        {
            return new KeepsakeInspection(InspectionStatus.Tampered, ReadOnlyMemory<byte>.Empty);
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return string.Equals(digest, found.Value.Metadata.Sha256, StringComparison.Ordinal)
            ? new KeepsakeInspection(InspectionStatus.Verified, bytes)
            : new KeepsakeInspection(InspectionStatus.Tampered, ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>
    /// Boss's explicit deletion: the superseding note is committed first, then only this
    /// photograph's file is removed. Repeating it is idempotent.
    /// </summary>
    internal async Task<DeletionResult> DeleteAsync(
        Guid photographId,
        KeepsakeDeletionAuthority authority,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authority);
        var found = await FindAsync(photographId, cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            return new DeletionResult(DeletionStatus.Unknown, []);
        }

        var (photograph, metadata, alreadyDeleted) = found.Value;
        if (!alreadyDeleted)
        {
            var written = await _repository.WriteGate
                .SubmitAsync(KeepsakeRecords.Deletion(photograph, metadata, now), cancellationToken)
                .ConfigureAwait(false);
            if (!written.IsAccepted)
            {
                return new DeletionResult(DeletionStatus.RecordRejected, []);
            }
        }

        var path = Path.Combine(_location.RootPath, KeepsakeRecords.FileName(metadata.ActionId));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return new DeletionResult(
            alreadyDeleted ? DeletionStatus.AlreadyDeleted : DeletionStatus.Deleted,
            alreadyDeleted ? [] : [new KeepsakeIntent(KeepsakeIntentKind.PhotographDeleted, metadata.ActionId, photograph.Record.RecordId, metadata.Sha256)]);
    }

    /// <summary>Files with no live photograph record (including interrupted temporaries). Reported only.</summary>
    public async Task<IReadOnlyList<string>> FindOrphansAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_location.RootPath))
        {
            return [];
        }

        var orphans = new List<string>();
        foreach (var path in Directory.EnumerateFiles(_location.RootPath).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);
            if (!name.EndsWith(".png", StringComparison.Ordinal)
                || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out var actionId)
                || !string.Equals(name, KeepsakeRecords.FileName(actionId), StringComparison.Ordinal))
            {
                orphans.Add(name);
                continue;
            }

            var subject = await _repository.RetrieveBySubjectAsync(KeepsakeRecords.Subject(actionId), cancellationToken).ConfigureAwait(false);
            var metadata = subject.Select(memory => KeepsakeMetadata.Parse(memory.Record.RetrievalMetadataJson)).Where(item => item is not null).ToArray();
            if (!metadata.Any(item => !item!.Deleted) || metadata.Any(item => item!.Deleted))
            {
                orphans.Add(name);
            }
        }

        return orphans;
    }

    /// <summary>Read-only disk-growth figures. There is deliberately no cleanup counterpart.</summary>
    public DiskGrowthReport MeasureDiskGrowth()
    {
        var (keepsakeFiles, keepsakeBytes) = Measure(_location.RootPath);
        var (memoryFiles, memoryBytes) = Measure(_location.MemoryLocation.RootPath);
        return new DiskGrowthReport(keepsakeFiles, keepsakeBytes, memoryFiles, memoryBytes);
    }

    private static (long Files, long Bytes) Measure(string root)
    {
        if (!Directory.Exists(root))
        {
            return (0, 0);
        }

        long files = 0, bytes = 0;
        foreach (var file in new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            files++;
            bytes += file.Length;
        }

        return (files, bytes);
    }

    private async Task<(RetrievedMemory Photograph, KeepsakeMetadata Metadata, bool Deleted)?> FindAsync(Guid photographId, CancellationToken cancellationToken)
    {
        if (photographId == Guid.Empty)
        {
            return null;
        }

        var record = (await _repository.RetrieveAsync(new MemoryQuery { RecordIds = [photographId] }, cancellationToken).ConfigureAwait(false)).SingleOrDefault();
        var metadata = record is null ? null : KeepsakeMetadata.Parse(record.Record.RetrievalMetadataJson);
        if (record is null || metadata is null || metadata.Deleted || record.Record.SubjectKey != KeepsakeRecords.Subject(metadata.ActionId))
        {
            return null;
        }

        var subject = await _repository.RetrieveBySubjectAsync(record.Record.SubjectKey, cancellationToken).ConfigureAwait(false);
        var deleted = subject.Any(memory => KeepsakeMetadata.Parse(memory.Record.RetrievalMetadataJson) is { Deleted: true });
        return (record, metadata, deleted);
    }

    private static PhotographEntry Entry(RetrievedMemory memory, KeepsakeMetadata metadata, bool deleted) => new(
        memory.Record.RecordId,
        metadata.ActionId,
        metadata.Sha256,
        metadata.ByteLength,
        metadata.Width,
        metadata.Height,
        metadata.TakenAt,
        memory.Record.VisibleRecollection,
        memory.Record.GameReference,
        memory.Record.SaveReference,
        memory.Record.SessionReference,
        deleted);
}
