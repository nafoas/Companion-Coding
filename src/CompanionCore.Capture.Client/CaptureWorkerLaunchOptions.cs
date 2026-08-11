namespace CompanionCore.Capture.Client;

public sealed record CaptureWorkerLaunchOptions
{
    private CaptureWorkerLaunchOptions(
        string workerExecutablePath,
        bool useSyntheticPrivateTestSource,
        TimeSpan connectTimeout,
        TimeSpan commandTimeout,
        TimeSpan exitTimeout)
    {
        WorkerExecutablePath = ValidatePath(workerExecutablePath);
        UseSyntheticPrivateTestSource = useSyntheticPrivateTestSource;
        ConnectTimeout = ValidateTimeout(connectTimeout, nameof(connectTimeout));
        CommandTimeout = ValidateTimeout(commandTimeout, nameof(commandTimeout));
        ExitTimeout = ValidateTimeout(exitTimeout, nameof(exitTimeout));
    }

    public string WorkerExecutablePath { get; }

    public TimeSpan ConnectTimeout { get; }

    public TimeSpan CommandTimeout { get; }

    public TimeSpan ExitTimeout { get; }

    internal bool UseSyntheticPrivateTestSource { get; }

    public static CaptureWorkerLaunchOptions ForSiblingWorker() =>
        new(
            Path.Combine(AppContext.BaseDirectory, "CompanionCore.Capture.Worker.exe"),
            useSyntheticPrivateTestSource: false,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3));

    /// <summary>
    /// Private-safe process fixture used only by automated/manual test modes. It still
    /// cannot start without a genuine sealed authorization grant and never captures.
    /// </summary>
    internal static CaptureWorkerLaunchOptions ForPrivateSafeSyntheticTests(
        string? workerExecutablePath = null) =>
        new(
            workerExecutablePath
                ?? Path.Combine(AppContext.BaseDirectory, "CompanionCore.Capture.Worker.exe"),
            useSyntheticPrivateTestSource: true,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3));

    internal static CaptureWorkerLaunchOptions ForTests(
        string workerExecutablePath,
        bool synthetic,
        TimeSpan connectTimeout,
        TimeSpan commandTimeout,
        TimeSpan exitTimeout) =>
        new(workerExecutablePath, synthetic, connectTimeout, commandTimeout, exitTimeout);

    private static string ValidatePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A worker executable path is required.", nameof(value));
        }

        var fullPath = Path.GetFullPath(value);
        if (!string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The capture worker must be a dedicated executable.", nameof(value));
        }

        return fullPath;
    }

    private static TimeSpan ValidateTimeout(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero || value > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }
}
