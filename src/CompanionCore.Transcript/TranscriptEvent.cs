using System.Text.Json;
using System.Text.Json.Serialization;
using CompanionCore.Conversation;

namespace CompanionCore.Transcript;

public enum TranscriptEventKind
{
    SessionStarted = 1,
    ConversationActive = 2,
    Utterance = 3,
    BnuyModeInterrupted = 4,
    UrgentObservation = 5,
    ReturnOffered = 6,
    ConversationResumed = 7,
    ConversationSettled = 8,
    CoordinatorCheckpoint = 9,
    SessionEnded = 10,
}

public enum Speaker
{
    Boss = 1,
    Prince = 2,
}

/// <summary>
/// One checksummed transcript record. Text only: there is no image or byte field, so raw
/// frames cannot enter. Structural events carry stable thread/event identifiers and the
/// pre-interruption resume point, so reconstruction never interprets free text.
/// </summary>
public sealed record TranscriptEvent
{
    public int V { get; set; } = 1;

    public long Seq { get; set; }

    public Guid EventId { get; set; }

    public Guid SessionId { get; set; }

    public DateTimeOffset At { get; set; }

    public TranscriptEventKind Kind { get; set; }

    public Guid? ThreadId { get; set; }

    public string? Topic { get; set; }

    public ThreadKind? ThreadKind { get; set; }

    public Speaker? Speaker { get; set; }

    public string? Text { get; set; }

    public Guid? ResumePoint { get; set; }

    public Guid? InterruptionId { get; set; }

    public SettleReason? SettleReason { get; set; }

    public bool? Recovered { get; set; }

    public ConversationCheckpoint? Checkpoint { get; set; }

    [JsonPropertyOrder(int.MaxValue)]
    public string? Sum { get; set; }
}

internal static class TranscriptJson
{
    internal static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowDuplicateProperties = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 32,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
