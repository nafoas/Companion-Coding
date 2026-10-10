using System.Security.Cryptography;
using CompanionCore.Capture.Contracts;
using CompanionCore.Memory;
using CompanionCore.Privacy;

namespace CompanionCore.Keepsakes;

/// <summary>
/// The only durable-image path. A photograph is written only after a visible camera action
/// for the current authorized target and privacy generation, only from a frame tagged with
/// that exact session and generation inside the action window, and only after the local
/// privacy guard admits it. The file is written atomically first, then recorded through
/// the generation-bound write gate; a retry of the same action is idempotent.
/// </summary>
public sealed class KeepsakeCamera
{
    private readonly MemoryRepository _repository;
    private readonly RuntimePrivacyState _privacy;
    private readonly KeepsakeLocation _location;
    private readonly KeepsakeConfiguration _configuration;
    private readonly Guid _cameraId;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _takeGate = new(1, 1);
    private readonly Dictionary<Guid, CameraAction> _open = [];
    private readonly Dictionary<Guid, (DateTimeOffset StartedAt, PhotographResult Result)> _saved = [];
    private readonly Queue<DateTimeOffset> _recentActions = new();
    private readonly LocalPrivacyGuard _guard = new();
    private DateTimeOffset? _lastActionAt;
    private long _counter;

    public KeepsakeCamera(
        MemoryRepository repository,
        RuntimePrivacyState privacy,
        KeepsakeLocation location,
        KeepsakeConfiguration? configuration = null,
        Guid? cameraId = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _privacy = privacy ?? throw new ArgumentNullException(nameof(privacy));
        _location = location ?? throw new ArgumentNullException(nameof(location));
        _configuration = configuration ?? KeepsakeConfiguration.Default;
        _configuration.Validate();
        _cameraId = cameraId ?? Guid.NewGuid();
        if (_cameraId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty camera ID is required.", nameof(cameraId));
        }
    }

    /// <summary>Shows the camera action. Rare by construction: a minimum interval and a daily bound apply.</summary>
    public CameraActionResult BeginCameraAction(CaptureAuthorizationGrant grant, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grant);
        lock (_gate)
        {
            if (!_privacy.IsCurrent(grant.Generation))
            {
                return new CameraActionResult(null, [], KeepsakeRefusal.PrivacyStale);
            }

            while (_recentActions.Count > 0 && _recentActions.Peek() <= now - TimeSpan.FromDays(1))
            {
                _recentActions.Dequeue();
            }

            if (_lastActionAt is { } last && now - last < _configuration.MinimumInterval)
            {
                return new CameraActionResult(null, [], KeepsakeRefusal.TooSoon);
            }

            if (_recentActions.Count >= _configuration.MaximumActionsPerDay)
            {
                return new CameraActionResult(null, [], KeepsakeRefusal.DailyLimit);
            }

            foreach (var expired in _open.Values.Where(open => open.ExpiresAt < now).ToArray())
            {
                _open.Remove(expired.ActionId);
            }

            foreach (var old in _saved.Where(pair => pair.Value.StartedAt <= now - TimeSpan.FromDays(1)).Select(pair => pair.Key).ToArray())
            {
                _saved.Remove(old);
            }

            var action = new CameraAction(NextActionId(), grant.TargetSessionId, grant.Generation, grant.Target, now, now + _configuration.ActionWindow);
            _open[action.ActionId] = action;
            _recentActions.Enqueue(now);
            _lastActionAt = now;
            return new CameraActionResult(action, [new KeepsakeIntent(KeepsakeIntentKind.CameraShown, action.ActionId)], KeepsakeRefusal.None);
        }
    }

    public async Task<PhotographResult> TakeAsync(
        CameraAction action,
        PhotographFrame frame,
        KeepsakeContext context,
        TargetContentPolicy contentPolicy,
        PrivacyAssessment assessment,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(frame.Metadata);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assessment);
        await _takeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CameraAction? open;
            lock (_gate)
            {
                if (_saved.TryGetValue(action.ActionId, out var saved) && saved.Result.Sha256 is not null)
                {
                    return saved.Result with { Intents = [], AlreadySaved = true };
                }

                _open.TryGetValue(action.ActionId, out open);
            }

            if (open is null || open != action)
            {
                return Refuse(KeepsakeRefusal.UnknownAction);
            }

            if (now > open.ExpiresAt)
            {
                lock (_gate)
                {
                    _open.Remove(open.ActionId);
                }

                return Refuse(KeepsakeRefusal.ActionExpired);
            }

            var metadata = frame.Metadata;
            if (metadata.TargetSessionId != open.TargetSessionId || metadata.Generation != open.Generation || metadata.Target != open.Target)
            {
                return Refuse(KeepsakeRefusal.WrongTarget);
            }

            if (metadata.Timestamp < open.StartedAt || metadata.Timestamp > open.ExpiresAt)
            {
                return Refuse(KeepsakeRefusal.OutsideWindow);
            }

            if (!_privacy.IsCurrent(open.Generation))
            {
                return Refuse(KeepsakeRefusal.PrivacyStale);
            }

            var decision = _guard.Evaluate(contentPolicy, assessment);
            if (decision is not (PrivacyGuardDecision.Allowed or PrivacyGuardDecision.TrustedGameBypass))
            {
                return Refuse(KeepsakeRefusal.PrivacyRejected);
            }

            if (!ValidFrame(frame))
            {
                return Refuse(KeepsakeRefusal.InvalidFrame);
            }

            var (png, width, height) = KeepsakePng.Encode(frame.Bgra32.Span, metadata.Width, metadata.Height, frame.Stride, _configuration.MaximumSavedEdge);
            if (png.Length > _configuration.MaximumEncodedBytes)
            {
                return Refuse(KeepsakeRefusal.EncodedTooLarge);
            }

            var sha = Convert.ToHexStringLower(SHA256.HashData(png));
            var keepsake = new KeepsakeMetadata(open.ActionId, sha, png.Length, width, height, metadata.Timestamp.ToUniversalTime(), Deleted: false);
            if (!await WriteAtomicallyAsync(KeepsakeRecords.FileName(open.ActionId), png, sha, cancellationToken).ConfigureAwait(false))
            {
                return Refuse(KeepsakeRefusal.StoredFileMismatch);
            }

            var proposal = KeepsakeRecords.Photograph(keepsake, context);
            var written = await _repository.WriteGate.SubmitAsync(proposal, open.Generation, cancellationToken).ConfigureAwait(false);
            if (!written.IsAccepted)
            {
                // The file stays as a reported orphan; a retry inside the window completes the record.
                return Refuse(written.Status == WriteGateStatus.Conflict ? KeepsakeRefusal.RecordConflict : KeepsakeRefusal.RecordRejected);
            }

            var photographId = proposal.Records[0].RecordId;
            var result = new PhotographResult(
                photographId,
                sha,
                [new KeepsakeIntent(KeepsakeIntentKind.PhotographSaved, open.ActionId, photographId, sha)],
                KeepsakeRefusal.None);
            lock (_gate)
            {
                _open.Remove(open.ActionId);
                _saved[open.ActionId] = (open.StartedAt, result);
            }

            return result;
        }
        finally
        {
            _takeGate.Release();
        }
    }

    private bool ValidFrame(PhotographFrame frame)
    {
        var metadata = frame.Metadata;
        if (metadata.Width > _configuration.MaximumSourceEdge || metadata.Height > _configuration.MaximumSourceEdge)
        {
            return false;
        }

        var rowBytes = (long)metadata.Width * 4;
        return frame.Stride >= rowBytes
            && frame.Bgra32.Length >= ((long)frame.Stride * (metadata.Height - 1)) + rowBytes;
    }

    /// <summary>Writes temp, flushes, then renames; an existing file must already hold the same bytes.</summary>
    private async Task<bool> WriteAtomicallyAsync(string fileName, byte[] png, string sha, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_location.RootPath);
        var path = Path.Combine(_location.RootPath, fileName);
        if (File.Exists(path))
        {
            return await MatchesAsync(path, sha, cancellationToken).ConfigureAwait(false);
        }

        var temporary = Path.Combine(_location.RootPath, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(png, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            return await MatchesAsync(path, sha, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    internal static async Task<bool> MatchesAsync(string path, string sha, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
        var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        return string.Equals(actual, sha, StringComparison.Ordinal);
    }

    private static PhotographResult Refuse(KeepsakeRefusal refusal) => new(null, null, [], refusal);

    private Guid NextActionId()
    {
        Span<byte> input = stackalloc byte[24];
        _cameraId.TryWriteBytes(input, bigEndian: true, out _);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(input[16..], ++_counter);
        var bytes = SHA256.HashData(input)[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}
