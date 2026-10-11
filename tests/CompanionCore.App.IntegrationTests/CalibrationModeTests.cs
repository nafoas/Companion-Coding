using System.Text.Json;
using System.Text.RegularExpressions;
using CompanionCore.Calibration;
using Xunit.Abstractions;

namespace CompanionCore.App.IntegrationTests;

/// <summary>
/// Stage 11 calibration sampling in the real compiled App: bounded samples land beside the
/// isolated test root and carry only resource numbers and typed state names.
/// </summary>
[Collection(AppProcessCollection.Name)]
public sealed partial class CalibrationModeTests
{
    private readonly ITestOutputHelper _output;

    public CalibrationModeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Calibrate_RecordsPrivacySafeSamplesBesideTheTestRoot()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "CompanionCore.App.IntegrationTests", Guid.NewGuid().ToString("N"));
        var run = Guid.NewGuid();
        try
        {
            var result = AppProcess.Run(
                "calibrate",
                $"\"--test-data-root={root}\" --test-run={run:D} --calibration-log --calibration-interval-ms=200");
            _output.WriteLine(result.StdOut.Trim());
            Assert.Equal(0, result.ExitCode);
            var match = Marker().Match(result.StdOut);
            Assert.True(match.Success, result.StdOut + result.StdErr);
            Assert.True(int.Parse(match.Groups["samples"].Value, System.Globalization.CultureInfo.InvariantCulture) >= 3);
            Assert.Equal("0", match.Groups["failures"].Value);

            var directory = Path.Combine(root, "CompanionCore.Tests", run.ToString("N"), "Calibration");
            var (samples, malformed) = CalibrationAnalyzer.ReadDirectory(directory);
            Assert.Equal(0, malformed);
            Assert.True(samples.Count >= 3);
            Assert.All(samples, sample => Assert.True(sample.AppPrivateBytes > 0 && sample.AppHandleCount > 0));
            Assert.All(samples, sample => Assert.Equal("Resting", sample.StateLabel));

            foreach (var line in File.ReadLines(Directory.EnumerateFiles(directory, "calibration-*.jsonl").Single()))
            {
                Assert.DoesNotContain(root, line, StringComparison.OrdinalIgnoreCase);
                using var document = JsonDocument.Parse(line);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    Assert.True(
                        property.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                            || property.Name is "utc" or "workerStatus" or "attentionState" or "watchbunPhase" or "braincaseNapReason",
                        $"Unexpected free-text field {property.Name}");
                }
            }

        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void WithoutTheFlag_NothingIsRecorded()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "CompanionCore.App.IntegrationTests", Guid.NewGuid().ToString("N"));
        var run = Guid.NewGuid();
        try
        {
            var result = AppProcess.Run("calibrate", $"\"--test-data-root={root}\" --test-run={run:D}");
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("CALIBRATION SAMPLES:0", result.StdOut, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(root, "CompanionCore.Tests", run.ToString("N"), "Calibration")));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [GeneratedRegex(@"CALIBRATION SAMPLES:(?<samples>\d+) FAILURES:(?<failures>\d+) FILE:(?<file>\S+) CONSTRUCTIONS:1")]
    private static partial Regex Marker();
}
