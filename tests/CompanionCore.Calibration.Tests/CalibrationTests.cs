using System.Text.Json;

namespace CompanionCore.Calibration.Tests;

public sealed class CalibrationTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private const long Mib = 1024 * 1024;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "CompanionCore.Calibration.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static CalibrationSample Sample(double minutes, long appPrivate = 200 * Mib, string? attention = null, string? watchbun = null, long? workerPrivate = null) => new()
    {
        Utc = T0.AddMinutes(minutes),
        UptimeSeconds = minutes * 60,
        AppPrivateBytes = appPrivate,
        AppWorkingSetBytes = appPrivate + (10 * Mib),
        AppHandleCount = 400,
        AppThreadCount = 30,
        AppCpuPercent = 1.5,
        AttentionState = attention,
        WatchbunPhase = watchbun,
        WorkerPrivateBytes = workerPrivate,
        WorkerHandleCount = workerPrivate is null ? null : 250,
    };

    // ---- recorder --------------------------------------------------------------------

    [Fact]
    public void TheRecorder_WritesOneJsonLinePerSample_WithOnlyNumbersAndStateNames()
    {
        using var recorder = new CalibrationRecorder(_root);
        recorder.Append(Sample(0, attention: "Engaged", watchbun: "Watching", workerPrivate: 90 * Mib));
        recorder.Append(Sample(1));

        var lines = File.ReadAllLines(recorder.CurrentFile!);
        Assert.Equal(2, lines.Length);
        Assert.Equal(2, recorder.Written);
        using var document = JsonDocument.Parse(lines[0]);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            Assert.True(
                property.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                    || property.Name is "utc" or "workerStatus" or "attentionState" or "watchbunPhase" or "braincaseNapReason",
                $"Unexpected free-text field {property.Name}");
        }

        Assert.DoesNotContain("stateLabel", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void TheRecorder_RotatesAtTheFileBound_AndKeepsOnlyTheNewestFiles()
    {
        var time = new ManualTime(T0);
        Directory.CreateDirectory(_root);
        var foreign = Path.Combine(_root, "a-foreign-notes.txt");
        File.WriteAllText(foreign, "not a calibration file");
        using var recorder = new CalibrationRecorder(_root, time);
        var line = JsonSerializer.Serialize(Sample(0), CalibrationRecorder.Json).Length + 1;
        var perFile = (int)(CalibrationRecorder.MaximumFileBytes / line);
        var total = (perFile * (CalibrationRecorder.MaximumFiles + 2)) + 5;

        for (var index = 0; index < total; index++)
        {
            recorder.Append(Sample(index));
            time.Advance(TimeSpan.FromMilliseconds(1));
        }

        var files = CalibrationRecorder.OwnedFiles(_root).ToArray();
        Assert.Equal(CalibrationRecorder.MaximumFiles, files.Length);
        Assert.All(files, file => Assert.True(new FileInfo(file).Length <= CalibrationRecorder.MaximumFileBytes));
        Assert.Equal(total, recorder.Written);
        Assert.Equal(0, recorder.Failures);
        Assert.True(File.Exists(foreign));
    }

    [Fact]
    public void TheRecorder_NeverTouchesFilesItDoesNotOwn_AndRefusesRelativePaths()
    {
        Directory.CreateDirectory(_root);
        var foreign = Path.Combine(_root, "notes.txt");
        File.WriteAllText(foreign, "keep me");
        using (var recorder = new CalibrationRecorder(_root))
        {
            recorder.Append(Sample(0));
        }

        Assert.True(File.Exists(foreign));
        Assert.Throws<ArgumentException>(() => new CalibrationRecorder("relative/path"));
    }

    [Fact]
    public void AfterDispose_AppendIsANoOp()
    {
        var recorder = new CalibrationRecorder(_root);
        recorder.Dispose();
        recorder.Append(Sample(0));
        Assert.Equal(0, recorder.Written);
        Assert.Empty(CalibrationRecorder.OwnedFiles(_root));
    }

    // ---- analyzer --------------------------------------------------------------------

    [Fact]
    public void StateLabels_FollowTheDesignsMeasurementList()
    {
        Assert.Equal("Resting", Sample(0).StateLabel);
        Assert.Equal("Noticing", Sample(0, attention: "Noticing").StateLabel);
        Assert.Equal("BnuyMode", Sample(0, attention: "HighAttention").StateLabel);
        Assert.Equal("Engaged", Sample(0, attention: "Engaged", watchbun: "Watching").StateLabel);
        Assert.Equal("Watchbun:Dozing", Sample(0, attention: "Noticing", watchbun: "Dozing").StateLabel);
    }

    [Fact]
    public void AStableSoak_IsStable_AndThresholdsSitAboveItsPeaks()
    {
        var samples = Enumerable.Range(0, 8 * 60)
            .Select(minute => Sample(minute, appPrivate: (200 + (minute % 7)) * Mib, attention: minute % 3 == 0 ? "Engaged" : "Noticing", workerPrivate: (90 + (minute % 5)) * Mib))
            .ToArray();

        var report = CalibrationAnalyzer.Analyze(samples);

        Assert.All(report.Growth, growth => Assert.Equal(GrowthVerdict.Stable, growth.Verdict));
        var app = report.Thresholds.Single(threshold => threshold.Resource == "App private bytes");
        Assert.InRange(app.SustainedPeak, 205 * Mib, 206 * Mib);
        Assert.Equal(app.SustainedPeak * 1.5, app.SoftConcern, 3);
        Assert.Equal(app.SustainedPeak * 2.0, app.WorkerRestart, 3);
        Assert.Equal(app.SustainedPeak * 3.0, app.EmergencyNaptime, 3);
        Assert.Equal(["Engaged", "Noticing"], report.States.Select(state => state.State));
        Assert.Equal(8 * 60, report.States.Sum(state => state.Samples));
        Assert.Contains("Recommended thresholds", report.ToMarkdown(), StringComparison.Ordinal);
    }

    [Fact]
    public void SustainedGrowthAfterWarmup_IsReportedAsALeakConcern()
    {
        var samples = Enumerable.Range(0, 4 * 60)
            .Select(minute => Sample(minute, appPrivate: (200 * Mib) + (minute * Mib)))
            .ToArray();

        var report = CalibrationAnalyzer.Analyze(samples);

        var app = report.Growth.Single(growth => growth.Resource == "App private bytes");
        Assert.Equal(GrowthVerdict.SustainedGrowth, app.Verdict);
        Assert.InRange(app.SlopePerHour, 59 * Mib, 61 * Mib);
        Assert.Equal(GrowthVerdict.Stable, report.Growth.Single(growth => growth.Resource == "App handles").Verdict);
    }

    [Fact]
    public void WarmupGrowth_IsIgnored_AndShortRunsAreInsufficient()
    {
        var warmupThenFlat = Enumerable.Range(0, 3 * 60)
            .Select(minute => Sample(minute, appPrivate: minute < 10 ? minute * 50 * Mib : 500 * Mib))
            .ToArray();
        Assert.Equal(GrowthVerdict.Stable, CalibrationAnalyzer.Analyze(warmupThenFlat).Growth[0].Verdict);

        var shortRun = Enumerable.Range(0, 40).Select(minute => Sample(minute)).ToArray();
        Assert.All(CalibrationAnalyzer.Analyze(shortRun).Growth, growth => Assert.Equal(GrowthVerdict.InsufficientData, growth.Verdict));
    }

    [Fact]
    public void Thresholds_AreCappedFarBelowThePhysicalMemory()
    {
        var huge = Enumerable.Range(0, 120).Select(minute => Sample(minute + 10, appPrivate: 6L * 1024 * Mib)).ToArray();

        var report = CalibrationAnalyzer.Analyze(huge, new CalibrationReportOptions { PhysicalMemoryBytes = 32L * 1024 * Mib });

        var app = report.Thresholds.Single(threshold => threshold.Resource == "App private bytes");
        Assert.Equal(8L * 1024 * Mib, app.EmergencyNaptime);
        Assert.True(app.SoftConcern <= app.WorkerRestart && app.WorkerRestart <= app.EmergencyNaptime);
    }

    [Fact]
    public void ReadingTheDirectory_SkipsMalformedLines_AndOrdersByTime()
    {
        using (var recorder = new CalibrationRecorder(_root))
        {
            recorder.Append(Sample(5));
            recorder.Append(Sample(1));
        }

        File.AppendAllText(CalibrationRecorder.OwnedFiles(_root).Single(), "{not json\n\n");

        var (samples, malformed) = CalibrationAnalyzer.ReadDirectory(_root);

        Assert.Equal(1, malformed);
        Assert.Equal([T0.AddMinutes(1), T0.AddMinutes(5)], samples.Select(sample => sample.Utc));
        Assert.Equal(2, CalibrationAnalyzer.Analyze(samples, malformed: malformed).Samples);
        Assert.Empty(CalibrationAnalyzer.ReadDirectory(Path.Combine(_root, "missing")).Samples);
    }

    [Fact]
    public void AnEmptyReport_IsWellFormed()
    {
        var report = CalibrationAnalyzer.Analyze([]);
        Assert.Equal(0, report.Samples);
        Assert.Equal(TimeSpan.Zero, report.Duration);
        Assert.Contains("Samples: 0", report.ToMarkdown(), StringComparison.Ordinal);
    }
}
