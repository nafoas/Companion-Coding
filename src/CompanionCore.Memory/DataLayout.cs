namespace CompanionCore.Memory;

/// <summary>
/// The layout of a data root: Prince's two homes. The <c>BunDex</c> (his autobiography) and
/// the <c>Braincase</c> (his subconscious, holding every machine store) sit side by side, beside
/// Boss's own <c>TargetAuthorization</c> and <c>Calibration</c> folders.
/// </summary>
public static class DataLayout
{
    public const string BraincaseDirectoryName = "Braincase";
    public const string BunDexDirectoryName = "BunDex";
    public const string MemoryDirectoryName = "Memory";

    /// <summary>The machine stores that lived directly in the data root before M1b and now live in the BrainCase.</summary>
    public static readonly IReadOnlyList<string> BraincaseStoreNames = [MemoryDirectoryName, "Keepsakes", "State", "Transcripts"];

    internal static string MemoryRootFor(string dataRoot) =>
        Path.Combine(dataRoot, BraincaseDirectoryName, MemoryDirectoryName);
}
