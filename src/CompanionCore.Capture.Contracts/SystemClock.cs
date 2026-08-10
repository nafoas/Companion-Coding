namespace CompanionCore.Capture.Contracts;

/// <summary>
/// The real clock. Production code uses this; tests substitute a fixed/manual
/// <see cref="ISystemClock"/> instead.
/// </summary>
public sealed class SystemClock : ISystemClock
{
    public static readonly SystemClock Instance = new();

    private SystemClock()
    {
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
