using CompanionCore.Conversation;

namespace CompanionCore.Transcript;

public sealed record TranscriptUtterance(Guid EventId, long Sequence, Speaker Speaker, string Text, DateTimeOffset At);

public enum ReconstructedThreadStatus
{
    Active = 1,
    Interrupted = 2,
    Settled = 3,
}

public sealed record ReconstructedThread(
    Guid ThreadId,
    string? Topic,
    ThreadKind? Kind,
    ReconstructedThreadStatus Status,
    Guid? ResumePoint,
    Guid? InterruptionId,
    IReadOnlyList<TranscriptUtterance> RecentUtterances,
    IReadOnlyList<TranscriptUtterance> PreInterruptionUtterances);

/// <summary>
/// The local answer to "what were we just talking about?", rebuilt from structural events
/// alone; committed memory is never consulted.
/// </summary>
public sealed record TranscriptContext(
    Guid SessionId,
    ReconstructedThread? CurrentThread,
    ConversationCheckpoint? LatestCheckpoint,
    IReadOnlyList<TranscriptEventKind> Kinds,
    long LastSequence,
    bool SessionEnded);

public static class TranscriptReader
{
    public const int DefaultRecentUtterances = 50;

    /// <summary>
    /// Read-only reconstruction, safe while the session is live. A torn trailing line is
    /// ignored (not modified); interior corruption fails closed.
    /// </summary>
    public static TranscriptContext Reconstruct(
        TranscriptLocation location,
        Guid sessionId,
        int recentUtterances = DefaultRecentUtterances)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentOutOfRangeException.ThrowIfLessThan(recentUtterances, 1);
        var path = location.SessionPath(sessionId);
        byte[] content;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            content = new byte[stream.Length];
            stream.ReadExactly(content);
        }

        var events = new List<TranscriptEvent>();
        if (!TranscriptParser.TryParse(content, sessionId, events, out _, out _))
        {
            throw new InvalidDataException("The session transcript is corrupt.");
        }

        return Reconstruct(sessionId, events, recentUtterances);
    }

    internal static TranscriptContext Reconstruct(Guid sessionId, IReadOnlyList<TranscriptEvent> events, int recentUtterances)
    {
        var threads = new Dictionary<Guid, ThreadState>();
        Guid? current = null;
        ConversationCheckpoint? checkpoint = null;
        var ended = false;
        foreach (var record in events)
        {
            switch (record.Kind)
            {
                case TranscriptEventKind.ConversationActive:
                case TranscriptEventKind.ConversationResumed:
                    var started = Thread(threads, record);
                    started.Status = ReconstructedThreadStatus.Active;
                    current = started.ThreadId;
                    break;
                case TranscriptEventKind.BnuyModeInterrupted:
                    var interrupted = Thread(threads, record);
                    interrupted.Status = ReconstructedThreadStatus.Interrupted;
                    interrupted.ResumePoint = record.ResumePoint;
                    interrupted.InterruptionId = record.EventId;
                    // The resume point is the thread's last event before this interruption, so the
                    // utterances seen so far are exactly the pre-interruption context.
                    interrupted.PreInterruption = [.. interrupted.Utterances];
                    current = interrupted.ThreadId;
                    break;
                case TranscriptEventKind.ConversationSettled:
                    Thread(threads, record).Status = ReconstructedThreadStatus.Settled;
                    break;
                case TranscriptEventKind.Utterance when record.ThreadId is not null:
                    Thread(threads, record).Utterances.Add(new TranscriptUtterance(record.EventId, record.Seq, record.Speaker!.Value, record.Text!, record.At));
                    break;
                case TranscriptEventKind.CoordinatorCheckpoint:
                    checkpoint = record.Checkpoint;
                    break;
                case TranscriptEventKind.SessionEnded:
                    ended = true;
                    break;
            }
        }

        ReconstructedThread? reconstructed = null;
        if (current is { } id && threads.TryGetValue(id, out var state))
        {
            reconstructed = new ReconstructedThread(
                state.ThreadId,
                state.Topic,
                state.Kind,
                state.Status,
                state.ResumePoint,
                state.InterruptionId,
                state.Utterances.TakeLast(recentUtterances).ToArray(),
                state.PreInterruption.TakeLast(recentUtterances).ToArray());
        }

        return new TranscriptContext(
            sessionId,
            reconstructed,
            checkpoint,
            events.Select(record => record.Kind).ToArray(),
            events.Count == 0 ? 0 : events[^1].Seq,
            ended);
    }

    private static ThreadState Thread(Dictionary<Guid, ThreadState> threads, TranscriptEvent record)
    {
        var id = record.ThreadId ?? throw new InvalidDataException("A thread event is missing its thread identifier.");
        if (!threads.TryGetValue(id, out var state))
        {
            state = new ThreadState(id);
            threads[id] = state;
        }

        state.Topic ??= record.Topic;
        state.Kind ??= record.ThreadKind;
        return state;
    }

    private sealed class ThreadState(Guid threadId)
    {
        public Guid ThreadId { get; } = threadId;

        public string? Topic { get; set; }

        public ThreadKind? Kind { get; set; }

        public ReconstructedThreadStatus Status { get; set; } = ReconstructedThreadStatus.Active;

        public Guid? ResumePoint { get; set; }

        public Guid? InterruptionId { get; set; }

        public List<TranscriptUtterance> Utterances { get; } = [];

        public List<TranscriptUtterance> PreInterruption { get; set; } = [];
    }
}
