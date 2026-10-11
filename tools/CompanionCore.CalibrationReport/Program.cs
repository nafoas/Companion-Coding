using System.Globalization;
using CompanionCore.Calibration;

// Turns a Stage 11 calibration folder into the design's baseline-calibration report.
// Usage: CompanionCore.CalibrationReport <calibration folder> [--physical-memory-gb 32]
if (args.Length is 0 or > 3 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: CompanionCore.CalibrationReport <calibration folder> [--physical-memory-gb 32]");
    return args.Length == 0 ? 64 : 0;
}

var directory = Path.GetFullPath(args[0]);
var options = new CalibrationReportOptions();
if (args.Length == 3)
{
    if (args[1] != "--physical-memory-gb"
        || !double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var gigabytes)
        || gigabytes is <= 0 or > 4096)
    {
        Console.Error.WriteLine("The physical memory must be a positive number of gigabytes.");
        return 64;
    }

    options = options with { PhysicalMemoryBytes = (long)(gigabytes * 1024 * 1024 * 1024) };
}
else if (args.Length == 2)
{
    Console.Error.WriteLine("Usage: CompanionCore.CalibrationReport <calibration folder> [--physical-memory-gb 32]");
    return 64;
}

var (samples, malformed) = CalibrationAnalyzer.ReadDirectory(directory);
if (samples.Count == 0)
{
    Console.Error.WriteLine($"No calibration samples were found in {directory}.");
    return 66;
}

var markdown = CalibrationAnalyzer.Analyze(samples, options, malformed).ToMarkdown();
var reportPath = Path.Combine(directory, "calibration-report.md");
File.WriteAllText(reportPath, markdown);
Console.WriteLine(markdown);
Console.WriteLine($"Report written to {reportPath}");
return 0;
