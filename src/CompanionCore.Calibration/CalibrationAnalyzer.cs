using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CompanionCore.Calibration;

public sealed record CalibrationReportOptions
{
    /// <summary>Samples before this much uptime are warm-up and excluded from growth and thresholds.</summary>
    public TimeSpan Warmup { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Boss's system: 32 GB.</summary>
    public long PhysicalMemoryBytes { get; init; } = 32L * 1024 * 1024 * 1024;

    /// <summary>No threshold may exceed this share of physical memory ("far below anything that threatens" the system).</summary>
    public double MaximumShareOfPhysicalMemory { get; init; } = 0.25;

    /// <summary>Growth needs at least this much settled time before it can be judged.</summary>
    public TimeSpan MinimumGrowthWindow { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Sustained growth above this rate (or 2% of the median per hour, whichever is larger) is a leak concern.</summary>
    public long GrowthToleranceBytesPerHour { get; init; } = 16L * 1024 * 1024;
}

public sealed record Distribution(int Count, double Median, double P95, double P99, double Maximum)
{
    internal static Distribution Of(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return new Distribution(0, 0, 0, 0, 0);
        }

        var sorted = values.Order().ToArray();
        return new Distribution(sorted.Length, Percentile(sorted, 0.5), Percentile(sorted, 0.95), Percentile(sorted, 0.99), sorted[^1]);
    }

    private static double Percentile(double[] sorted, double fraction)
    {
        var rank = fraction * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * (rank - lower));
    }
}

public sealed record StateStatistics(
    string State,
    int Samples,
    Distribution AppPrivateBytes,
    Distribution AppCpuPercent,
    Distribution AppHandles,
    Distribution WorkerPrivateBytes,
    Distribution WorkerHandles);

public enum GrowthVerdict
{
    InsufficientData = 1,
    Stable = 2,
    SustainedGrowth = 3,
}

public sealed record GrowthAssessment(string Resource, double SlopePerHour, double ToleratedPerHour, GrowthVerdict Verdict);

public sealed record ThresholdRecommendation(string Resource, double SustainedPeak, double SoftConcern, double WorkerRestart, double EmergencyNaptime);

public sealed record CalibrationReport(
    int Samples,
    int MalformedLines,
    TimeSpan Duration,
    IReadOnlyList<StateStatistics> States,
    IReadOnlyList<GrowthAssessment> Growth,
    IReadOnlyList<ThresholdRecommendation> Thresholds,
    long BraincaseOperations,
    long BraincaseInputUnitsToday,
    long BraincaseNapEpisodes)
{
    public string ToMarkdown()
    {
        var invariant = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        text.AppendLine("# Prince calibration report");
        text.AppendLine();
        text.AppendLine(invariant, $"- Samples: {Samples} ({MalformedLines} malformed lines skipped)");
        text.AppendLine(invariant, $"- Duration: {Duration.TotalHours:F2} h");
        text.AppendLine(invariant, $"- Braincase operations: {BraincaseOperations}; nap episodes: {BraincaseNapEpisodes}; estimated input units today: {BraincaseInputUnitsToday}");
        text.AppendLine();
        text.AppendLine("## Per state");
        text.AppendLine();
        text.AppendLine("| State | Samples | App private median / p95 / max (MiB) | App CPU median / p95 (%) | App handles p95 | Worker private median / p95 / max (MiB) | Worker handles p95 |");
        text.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var state in States)
        {
            text.AppendLine(invariant,
                $"| {state.State} | {state.Samples} | {Mib(state.AppPrivateBytes.Median)} / {Mib(state.AppPrivateBytes.P95)} / {Mib(state.AppPrivateBytes.Maximum)} | {state.AppCpuPercent.Median:F1} / {state.AppCpuPercent.P95:F1} | {state.AppHandles.P95:F0} | {Mib(state.WorkerPrivateBytes.Median)} / {Mib(state.WorkerPrivateBytes.P95)} / {Mib(state.WorkerPrivateBytes.Maximum)} | {state.WorkerHandles.P95:F0} |");
        }

        text.AppendLine();
        text.AppendLine("## Growth after warm-up");
        text.AppendLine();
        foreach (var growth in Growth)
        {
            var bytes = growth.Resource.EndsWith("bytes", StringComparison.Ordinal);
            var unit = bytes ? " MiB" : string.Empty;
            var scale = bytes ? 1024.0 * 1024 : 1;
            text.AppendLine(invariant, $"- {growth.Resource}: {growth.SlopePerHour / scale:F1}{unit} per hour (tolerated {growth.ToleratedPerHour / scale:F1}{unit}) — **{growth.Verdict}**");
        }

        text.AppendLine();
        text.AppendLine("## Recommended thresholds");
        text.AppendLine();
        text.AppendLine("| Resource | Sustained peak (p99) | Soft concern (×1.5) | Worker restart (×2) | Emergency Naptime (×3) |");
        text.AppendLine("|---|---|---|---|---|");
        foreach (var threshold in Thresholds)
        {
            var bytes = threshold.Resource.EndsWith("bytes", StringComparison.Ordinal);
            string Format(double value) => bytes ? $"{Mib(value)} MiB" : value.ToString("F0", invariant);
            text.AppendLine(invariant, $"| {threshold.Resource} | {Format(threshold.SustainedPeak)} | {Format(threshold.SoftConcern)} | {Format(threshold.WorkerRestart)} | {Format(threshold.EmergencyNaptime)} |");
        }

        return text.ToString();

        static string Mib(double bytes) => (bytes / (1024 * 1024)).ToString("F0", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Turns calibration samples into the design's "Baseline calibration" outputs: normal
/// baseline and peaks per state, whether use keeps rising after work settles (a leak is
/// sustained growth, not merely high use), and soft / restart / emergency thresholds
/// comfortably above legitimate peaks while far below anything that threatens the system.
/// </summary>
public static class CalibrationAnalyzer
{
    public const double SoftMultiplier = 1.5;
    public const double RestartMultiplier = 2.0;
    public const double EmergencyMultiplier = 3.0;

    public static (IReadOnlyList<CalibrationSample> Samples, int Malformed) ReadDirectory(string directory)
    {
        var samples = new List<CalibrationSample>();
        var malformed = 0;
        foreach (var file in CalibrationRecorder.OwnedFiles(directory))
        {
            using var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                try
                {
                    if (JsonSerializer.Deserialize<CalibrationSample>(line, CalibrationRecorder.Json) is { } sample)
                    {
                        samples.Add(sample);
                    }
                    else
                    {
                        malformed++;
                    }
                }
                catch (JsonException)
                {
                    malformed++;
                }
            }
        }

        return (samples.OrderBy(sample => sample.Utc).ToArray(), malformed);
    }

    public static CalibrationReport Analyze(IReadOnlyList<CalibrationSample> samples, CalibrationReportOptions? options = null, int malformed = 0)
    {
        ArgumentNullException.ThrowIfNull(samples);
        options ??= new CalibrationReportOptions();
        var ordered = samples.OrderBy(sample => sample.Utc).ToArray();
        var duration = ordered.Length < 2 ? TimeSpan.Zero : ordered[^1].Utc - ordered[0].Utc;

        var states = ordered
            .GroupBy(sample => sample.StateLabel, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new StateStatistics(
                group.Key,
                group.Count(),
                Distribution.Of([.. group.Select(sample => (double)sample.AppPrivateBytes)]),
                Distribution.Of([.. group.Select(sample => sample.AppCpuPercent)]),
                Distribution.Of([.. group.Select(sample => (double)sample.AppHandleCount)]),
                Distribution.Of([.. group.Where(sample => sample.WorkerPrivateBytes is not null).Select(sample => (double)sample.WorkerPrivateBytes!.Value)]),
                Distribution.Of([.. group.Where(sample => sample.WorkerHandleCount is not null).Select(sample => (double)sample.WorkerHandleCount!.Value)])))
            .ToArray();

        var settled = ordered.Where(sample => sample.UptimeSeconds >= options.Warmup.TotalSeconds).ToArray();
        var growth = new[]
        {
            Growth("App private bytes", settled, sample => sample.AppPrivateBytes, options, isBytes: true),
            Growth("App handles", settled, sample => sample.AppHandleCount, options, isBytes: false),
            Growth("Worker private bytes", settled, sample => sample.WorkerPrivateBytes, options, isBytes: true),
            Growth("Worker handles", settled, sample => sample.WorkerHandleCount, options, isBytes: false),
        };

        var cap = options.PhysicalMemoryBytes * options.MaximumShareOfPhysicalMemory;
        var thresholds = new[]
        {
            Threshold("App private bytes", settled.Select(sample => (double)sample.AppPrivateBytes), cap),
            Threshold("App handles", settled.Select(sample => (double)sample.AppHandleCount), double.MaxValue),
            Threshold("Worker private bytes", settled.Where(sample => sample.WorkerPrivateBytes is not null).Select(sample => (double)sample.WorkerPrivateBytes!.Value), cap),
            Threshold("Worker handles", settled.Where(sample => sample.WorkerHandleCount is not null).Select(sample => (double)sample.WorkerHandleCount!.Value), double.MaxValue),
        };

        var last = ordered.LastOrDefault();
        return new CalibrationReport(
            ordered.Length,
            malformed,
            duration,
            states,
            growth,
            thresholds,
            last?.BraincaseOperations ?? 0,
            last?.BraincaseInputUnitsToday ?? 0,
            last?.BraincaseNapEpisodes ?? 0);
    }

    internal static ThresholdRecommendation Threshold(string resource, IEnumerable<double> values, double cap)
    {
        var peak = Distribution.Of([.. values]).P99;
        return new ThresholdRecommendation(
            resource,
            peak,
            Math.Min(peak * SoftMultiplier, cap),
            Math.Min(peak * RestartMultiplier, cap),
            Math.Min(peak * EmergencyMultiplier, cap));
    }

    internal static GrowthAssessment Growth(
        string resource,
        IReadOnlyList<CalibrationSample> settled,
        Func<CalibrationSample, double?> select,
        CalibrationReportOptions options,
        bool isBytes)
    {
        var points = settled
            .Select(sample => (Hours: sample.UptimeSeconds / 3600.0, Value: select(sample)))
            .Where(point => point.Value is not null)
            .Select(point => (point.Hours, Value: point.Value!.Value))
            .ToArray();
        if (points.Length < 3 || points[^1].Hours - points[0].Hours < options.MinimumGrowthWindow.TotalHours)
        {
            return new GrowthAssessment(resource, 0, 0, GrowthVerdict.InsufficientData);
        }

        // Least-squares slope per hour.
        var meanX = points.Average(point => point.Hours);
        var meanY = points.Average(point => point.Value);
        var numerator = points.Sum(point => (point.Hours - meanX) * (point.Value - meanY));
        var denominator = points.Sum(point => (point.Hours - meanX) * (point.Hours - meanX));
        var slope = denominator == 0 ? 0 : numerator / denominator;
        var median = Distribution.Of([.. points.Select(point => point.Value)]).Median;
        var tolerated = Math.Max(isBytes ? options.GrowthToleranceBytesPerHour : 2, median * 0.02);
        return new GrowthAssessment(resource, slope, tolerated, slope > tolerated ? GrowthVerdict.SustainedGrowth : GrowthVerdict.Stable);
    }
}
