namespace CompanionCore.Api;

/// <summary>State reconstructed exactly from the journal; mutated only by applying entries.</summary>
internal sealed class BraincaseJournalState
{
    internal const int MaximumRecentSubjects = 8;

    private readonly List<string> _recentSubjects = [];
    private readonly HashSet<Guid> _openOperations = [];

    internal IReadOnlyList<string> RecentSubjects => _recentSubjects;

    internal IReadOnlyCollection<Guid> OpenOperations => _openOperations;

    internal BraincaseNapReason NapReason { get; private set; }

    internal DateTimeOffset? NapUntil { get; private set; }

    internal TimeSpan ProbeInterval { get; private set; }

    internal long Operations { get; private set; }

    internal long Completed { get; private set; }

    internal long Interrupted { get; private set; }

    internal long NapEpisodes { get; private set; }

    internal long TotalInputUnits { get; private set; }

    internal long TotalOutputUnits { get; private set; }

    internal DateOnly? UsageDay { get; private set; }

    internal long UsageDayInputUnits { get; private set; }

    internal long UsageDayOutputUnits { get; private set; }

    internal long Glances { get; private set; }

    internal long UsageDayGlanceInputUnits { get; private set; }

    internal long UsageDayGlanceOutputUnits { get; private set; }

    internal long GlanceInputUnitsOn(DateOnly day) => UsageDay == day ? UsageDayGlanceInputUnits : 0;

    internal long GlanceOutputUnitsOn(DateOnly day) => UsageDay == day ? UsageDayGlanceOutputUnits : 0;

    internal long UsageInputUnitsOn(DateOnly day) => UsageDay == day ? UsageDayInputUnits : 0;

    internal long UsageOutputUnitsOn(DateOnly day) => UsageDay == day ? UsageDayOutputUnits : 0;

    internal void Apply(BraincaseJournalEntry entry)
    {
        switch (entry.Type)
        {
            case BraincaseJournalEntryType.Started:
                Operations++;
                _openOperations.Add(RequireOperation(entry));
                break;
            case BraincaseJournalEntryType.Completed:
                Completed++;
                _openOperations.Remove(RequireOperation(entry));
                foreach (var subject in Enumerable.Reverse(entry.Subjects ?? []))
                {
                    _recentSubjects.Remove(subject);
                    _recentSubjects.Insert(0, subject);
                }

                if (_recentSubjects.Count > MaximumRecentSubjects)
                {
                    _recentSubjects.RemoveRange(MaximumRecentSubjects, _recentSubjects.Count - MaximumRecentSubjects);
                }

                AddUsage(DateOnly.FromDateTime(entry.At.UtcDateTime), entry.InputUnits ?? 0, entry.OutputUnits ?? 0);
                break;
            case BraincaseJournalEntryType.Glanced:
                RequireOperation(entry);
                Glances++;
                AddUsage(DateOnly.FromDateTime(entry.At.UtcDateTime), entry.InputUnits ?? 0, entry.OutputUnits ?? 0, glance: true);
                break;
            case BraincaseJournalEntryType.Interrupted:
                Interrupted++;
                _openOperations.Remove(RequireOperation(entry));
                break;
            case BraincaseJournalEntryType.Nap:
                if (entry.NapReason is not { } reason || reason == BraincaseNapReason.None || entry.NapUntil is null)
                {
                    throw new FormatException("A nap entry requires a reason and an end.");
                }

                if (NapReason == BraincaseNapReason.None)
                {
                    NapEpisodes++;
                }

                NapReason = reason;
                NapUntil = entry.NapUntil;
                ProbeInterval = TimeSpan.FromSeconds(entry.ProbeIntervalSeconds ?? 0);
                break;
            case BraincaseJournalEntryType.Awake:
                NapReason = BraincaseNapReason.None;
                NapUntil = null;
                ProbeInterval = TimeSpan.Zero;
                break;
            case BraincaseJournalEntryType.Snapshot:
                var snapshot = entry.Snapshot ?? throw new FormatException("A snapshot entry requires a snapshot.");
                if (snapshot.RecentSubjects is null || snapshot.RecentSubjects.Count > MaximumRecentSubjects)
                {
                    throw new FormatException("The snapshot subject list is invalid.");
                }

                _recentSubjects.Clear();
                _recentSubjects.AddRange(snapshot.RecentSubjects);
                _openOperations.Clear();
                NapReason = snapshot.NapReason;
                NapUntil = snapshot.NapUntil;
                ProbeInterval = TimeSpan.FromSeconds(snapshot.ProbeIntervalSeconds);
                Operations = snapshot.Operations;
                Completed = snapshot.Completed;
                Interrupted = snapshot.Interrupted;
                NapEpisodes = snapshot.NapEpisodes;
                TotalInputUnits = snapshot.TotalInputUnits;
                TotalOutputUnits = snapshot.TotalOutputUnits;
                UsageDay = snapshot.UsageDay;
                UsageDayInputUnits = snapshot.UsageDayInputUnits;
                UsageDayOutputUnits = snapshot.UsageDayOutputUnits;
                Glances = snapshot.Glances ?? 0;
                UsageDayGlanceInputUnits = snapshot.UsageDayGlanceInputUnits ?? 0;
                UsageDayGlanceOutputUnits = snapshot.UsageDayGlanceOutputUnits ?? 0;
                break;
            default:
                throw new FormatException("Unknown journal entry type.");
        }
    }

    internal BraincaseJournalSnapshot ToSnapshot() => new()
    {
        RecentSubjects = [.. _recentSubjects],
        NapReason = NapReason,
        NapUntil = NapUntil,
        ProbeIntervalSeconds = (long)ProbeInterval.TotalSeconds,
        Operations = Operations,
        Completed = Completed,
        Interrupted = Interrupted,
        NapEpisodes = NapEpisodes,
        TotalInputUnits = TotalInputUnits,
        TotalOutputUnits = TotalOutputUnits,
        UsageDay = UsageDay,
        UsageDayInputUnits = UsageDayInputUnits,
        UsageDayOutputUnits = UsageDayOutputUnits,
        Glances = Glances == 0 ? null : Glances,
        UsageDayGlanceInputUnits = UsageDayGlanceInputUnits == 0 ? null : UsageDayGlanceInputUnits,
        UsageDayGlanceOutputUnits = UsageDayGlanceOutputUnits == 0 ? null : UsageDayGlanceOutputUnits,
    };

    private void AddUsage(DateOnly day, long input, long output, bool glance = false)
    {
        TotalInputUnits += input;
        TotalOutputUnits += output;
        if (UsageDay != day)
        {
            UsageDay = day;
            UsageDayInputUnits = 0;
            UsageDayOutputUnits = 0;
            UsageDayGlanceInputUnits = 0;
            UsageDayGlanceOutputUnits = 0;
        }

        if (glance)
        {
            UsageDayGlanceInputUnits += input;
            UsageDayGlanceOutputUnits += output;
        }
        else
        {
            UsageDayInputUnits += input;
            UsageDayOutputUnits += output;
        }
    }

    private static Guid RequireOperation(BraincaseJournalEntry entry) =>
        entry.Op is { } operation && operation != Guid.Empty
            ? operation
            : throw new FormatException("An operation entry requires an operation ID.");
}
