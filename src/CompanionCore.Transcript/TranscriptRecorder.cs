using CompanionCore.Conversation;

namespace CompanionCore.Transcript;

/// <summary>
/// Maps coordinator updates to typed structural transcript events, in order, and records
/// utterances, neutral urgent observations, and coordinator checkpoints. Offers, seeds,
/// and ambient chatter are not conversation and are not transcribed.
/// </summary>
public sealed class TranscriptRecorder
{
    private readonly SessionTranscript _transcript;

    public TranscriptRecorder(SessionTranscript transcript)
    {
        _transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
    }

    public IReadOnlyList<TranscriptAppendResult> Record(ConversationUpdate update, long expectedGeneration, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(update);
        var results = new List<TranscriptAppendResult>();
        foreach (var intent in update.Intents)
        {
            var record = Map(intent, at);
            if (record is not null)
            {
                results.Add(_transcript.Append(record, expectedGeneration));
            }
        }

        return results;
    }

    /// <summary>An urgent interruption: the neutral observation first, then the coordinator's structural events.</summary>
    public IReadOnlyList<TranscriptAppendResult> RecordUrgent(
        string topic,
        string observation,
        ConversationUpdate update,
        long expectedGeneration,
        DateTimeOffset at)
    {
        var results = new List<TranscriptAppendResult>
        {
            _transcript.Append(
                new TranscriptEvent { At = at, Kind = TranscriptEventKind.UrgentObservation, Topic = topic, Text = observation },
                expectedGeneration),
        };
        results.AddRange(Record(update, expectedGeneration, at));
        return results;
    }

    public TranscriptAppendResult RecordUtterance(
        Speaker speaker,
        string text,
        ConversationThread? thread,
        long expectedGeneration,
        DateTimeOffset at)
    {
        if (!Enum.IsDefined(speaker))
        {
            throw new ArgumentOutOfRangeException(nameof(speaker));
        }

        return _transcript.Append(
            new TranscriptEvent
            {
                At = at,
                Kind = TranscriptEventKind.Utterance,
                Speaker = speaker,
                Text = text,
                ThreadId = thread?.ThreadId,
                Topic = thread?.TopicKey,
                ThreadKind = thread?.Kind,
            },
            expectedGeneration);
    }

    /// <summary>Checkpoints sessions and Conversation Locks so they survive restart.</summary>
    public TranscriptAppendResult RecordCheckpoint(ConversationCheckpoint checkpoint, long expectedGeneration, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return _transcript.Append(
            new TranscriptEvent { At = at, Kind = TranscriptEventKind.CoordinatorCheckpoint, Checkpoint = checkpoint },
            expectedGeneration);
    }

    private TranscriptEvent? Map(ConversationIntent intent, DateTimeOffset at)
    {
        var threadId = intent.ThreadId;
        return intent.Kind switch
        {
            ConversationIntentKind.ThreadStarted => Structural(TranscriptEventKind.ConversationActive),
            ConversationIntentKind.HoldThought => Structural(TranscriptEventKind.BnuyModeInterrupted) with
            {
                ResumePoint = threadId is { } id ? _transcript.LastEventFor(id) : null,
            },
            ConversationIntentKind.ResumeOffered => Structural(TranscriptEventKind.ReturnOffered) with
            {
                InterruptionId = threadId is { } id ? _transcript.LastInterruptionFor(id) : null,
            },
            ConversationIntentKind.ThreadResumed => Structural(TranscriptEventKind.ConversationResumed) with
            {
                InterruptionId = threadId is { } id ? _transcript.LastInterruptionFor(id) : null,
            },
            ConversationIntentKind.ThreadSettled => Structural(TranscriptEventKind.ConversationSettled) with
            {
                SettleReason = intent.SettleReason,
            },
            _ => null,
        };

        TranscriptEvent Structural(TranscriptEventKind kind) => new()
        {
            At = at,
            Kind = kind,
            ThreadId = threadId,
            Topic = intent.TopicKey,
            ThreadKind = intent.ThreadKind,
        };
    }
}
