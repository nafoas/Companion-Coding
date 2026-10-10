using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace CompanionCore.App.IntegrationTests;

/// <summary>
/// The full orchestration composition inside the real compiled App: the host starts on an
/// isolated test root (never the development root), reports no faults, and a second launch
/// on the same root restores the same conversation lineage through the Vault state store.
/// </summary>
[Collection(AppProcessCollection.Name)]
public sealed partial class WiredCompositionTests
{
    private readonly ITestOutputHelper _output;

    public WiredCompositionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Wired_StartsTheFullHostOnATestRoot_AndASecondLaunchRestoresTheSameLineage()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "CompanionCore.App.IntegrationTests", Guid.NewGuid().ToString("N"));
        var run = Guid.NewGuid();
        var arguments = $"\"--test-data-root={root}\" --test-run={run:D}";
        try
        {
            var first = Launch(arguments);
            var second = Launch(arguments);

            Assert.Equal("Test", first.Root);
            Assert.Equal(0, first.Faults);
            Assert.Equal(0, second.Faults);
            Assert.Equal(0, second.Unconsolidated);
            Assert.NotEqual(Guid.Empty, first.Lineage);
            Assert.Equal(first.Lineage, second.Lineage);
            Assert.True(Directory.Exists(Path.Combine(root, "CompanionCore.Tests", run.ToString("N"), "Memory")));
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
    public void Wired_WithoutATestRoot_UsesAFreshIsolatedRoot()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var first = Launch(string.Empty);
        var second = Launch(string.Empty);

        Assert.Equal("Test", first.Root);
        Assert.NotEqual(first.Lineage, second.Lineage);
    }

    private (string Root, long Faults, Guid Lineage, int Unconsolidated) Launch(string arguments)
    {
        var result = AppProcess.Run("wired", arguments);
        _output.WriteLine($"wired: exit {result.ExitCode}, startup {result.StartupDuration.TotalSeconds:F3} s, exit {result.ExitDuration.TotalSeconds:F3} s: {result.StdOut.Trim()}");
        Assert.Equal(0, result.ExitCode);
        var match = WiredMarker().Match(result.StdOut);
        Assert.True(match.Success, result.StdOut + result.StdErr);
        Assert.Equal("1", match.Groups["constructions"].Value);
        return (
            match.Groups["root"].Value,
            long.Parse(match.Groups["faults"].Value, System.Globalization.CultureInfo.InvariantCulture),
            Guid.ParseExact(match.Groups["lineage"].Value, "N"),
            int.Parse(match.Groups["unconsolidated"].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"WIRED ROOT:(?<root>\w+) FAULTS:(?<faults>\d+) LINEAGE:(?<lineage>[0-9a-f]{32}) UNCONSOLIDATED:(?<unconsolidated>\d+) CONSTRUCTIONS:(?<constructions>\d+)")]
    private static partial Regex WiredMarker();
}
