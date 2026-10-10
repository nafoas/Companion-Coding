using System.Text.Json.Serialization;

namespace CompanionCore.Api;

internal enum BraincaseJournalEntryType
{
    Started = 1,
    Completed = 2,
    Interrupted = 3,
    Nap = 4,
    Awake = 5,
    Snapshot = 6,
}

/// <summary>
/// One checksummed journal line. It carries identifiers, enums, counts, committed subject
/// keys, and usage estimates only: never credentials, images, interpretation or
/// recollection text, or provider error text.
/// </summary>
internal sealed class BraincaseJournalEntry
{
    public int V { get; set; } = 1;

    public long Seq { get; set; }

    public DateTimeOffset At { get; set; }

    public BraincaseJournalEntryType Type { get; set; }

    public Guid? Op { get; set; }

    public BridgeOutcomeKind? Outcome { get; set; }

    public int? Attempts { get; set; }

    public int? Proposed { get; set; }

    public RemoteProposalRejection? Rejection { get; set; }

    public string? Commit { get; set; }

    public List<string>? Subjects { get; set; }

    public long? InputUnits { get; set; }

    public long? OutputUnits { get; set; }

    public BraincaseNapReason? NapReason { get; set; }

    public DateTimeOffset? NapUntil { get; set; }

    public long? ProbeIntervalSeconds { get; set; }

    public BraincaseJournalSnapshot? Snapshot { get; set; }

    [JsonPropertyOrder(int.MaxValue)]
    public string? Sum { get; set; }
}

internal sealed class BraincaseJournalSnapshot
{
    public List<string> RecentSubjects { get; set; } = [];

    public BraincaseNapReason NapReason { get; set; }

    public DateTimeOffset? NapUntil { get; set; }

    public long ProbeIntervalSeconds { get; set; }

    public long Operations { get; set; }

    public long Completed { get; set; }

    public long Interrupted { get; set; }

    public long NapEpisodes { get; set; }

    public long TotalInputUnits { get; set; }

    public long TotalOutputUnits { get; set; }

    public DateOnly? UsageDay { get; set; }

    public long UsageDayInputUnits { get; set; }
}
