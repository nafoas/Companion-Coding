using CompanionCore.Memory;

namespace CompanionCore.Recall;

/// <summary>Where an adventure is in its life (plan item 7h).</summary>
public enum AdventureStage
{
    /// <summary>Being played, or paused to come back to.</summary>
    Active = 1,

    /// <summary>Just finished: every detail is still near.</summary>
    FreshlyFinished = 2,

    /// <summary>Remembered as how it went; session detail rests.</summary>
    Remembered = 3,

    /// <summary>Kept as its record and its Treasured Episodes.</summary>
    AdventureRecord = 4,
}

/// <summary>
/// The adventure lifecycle (M4c): Active → Freshly Finished → Remembered → Adventure Record, with
/// Treasured Episodes kept forever beside the record. The stage is derived from the adventure's
/// current status record and two clocks, play sessions and days, each window lasting whichever
/// is longer (M4a's medium and long layers), so time away never moves an adventure along. It
/// decides only what is offered; nothing is ever edited or deleted.
/// </summary>
public static class AdventureLifecycle
{
    public static readonly (int Sessions, TimeSpan Time) FreshWindow = (5, TimeSpan.FromDays(21));
    public static readonly (int Sessions, TimeSpan Time) RememberedWindow = (10, TimeSpan.FromDays(60));

    /// <param name="sinceStatus">Time since the current status was recorded.</param>
    /// <param name="sessionsSince">Play sessions begun since the current status was recorded.</param>
    public static AdventureStage StageOf(AdventureStatus status, TimeSpan sinceStatus, int sessionsSince)
    {
        if (status != AdventureStatus.Finished)
        {
            return AdventureStage.Active;
        }

        return Within(FreshWindow, sinceStatus, sessionsSince) ? AdventureStage.FreshlyFinished
            : Within(RememberedWindow, sinceStatus, sessionsSince) ? AdventureStage.Remembered
            : AdventureStage.AdventureRecord;
    }

    /// <summary>The stage from the adventure's current status record; null when it has none.</summary>
    public static AdventureStage? StageOf(RetrievedMemory? currentStatus, DateTimeOffset now, Func<DateTimeOffset, int> sessionsSince)
    {
        ArgumentNullException.ThrowIfNull(sessionsSince);
        if (currentStatus is null || RecallMetadata.Parse(currentStatus.Record.RetrievalMetadataJson).Adventure is not { } status)
        {
            return null;
        }

        var at = currentStatus.Record.CreatedAtUtc;
        return StageOf(status, now - at, sessionsSince(at));
    }

    /// <summary>The adventure's current status record (append-only: the one not superseded).</summary>
    public static async Task<RetrievedMemory?> CurrentStatusAsync(MemoryRepository repository, string game, string save, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return (await repository.RetrieveBySubjectAsync(RecallSubjects.Adventure(game, save), cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(memory => memory.IsCurrent);
    }

    /// <summary>Whichever clock is longer: still within either window.</summary>
    private static bool Within((int Sessions, TimeSpan Time) window, TimeSpan since, int sessions) =>
        sessions <= window.Sessions || since <= window.Time;
}
