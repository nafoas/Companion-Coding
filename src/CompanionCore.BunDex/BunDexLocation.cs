using CompanionCore.Memory;

namespace CompanionCore.BunDex;

/// <summary>
/// Prince's two homes for a validated development or test memory location: the
/// <c>BunDex</c> directory (his own notebook) in the data root, and the BunDex ledger inside the
/// <c>Braincase</c> directory (his subconscious). There is no raw-path constructor, so
/// neither can ever resolve the production root.
/// </summary>
public sealed class BunDexLocation
{
    internal const string LedgerDirectoryName = "bundex-ledger-v1";

    private BunDexLocation(DataRootKind kind, string bunDexRoot, string ledgerRoot)
    {
        Kind = kind;
        BunDexRoot = bunDexRoot;
        LedgerRoot = ledgerRoot;
    }

    public DataRootKind Kind { get; }

    /// <summary>Prince's notebook: folders and pages he makes himself.</summary>
    public string BunDexRoot { get; }

    /// <summary>The subconscious ledger the notebook is built from. Prince never sees it.</summary>
    public string LedgerRoot { get; }

    internal string DamagedLedgerPath => Path.Combine(Path.GetDirectoryName(LedgerRoot)!, "bundex-ledger-damaged-v1");

    public static BunDexLocation For(MemoryStoreLocation memoryLocation)
    {
        ArgumentNullException.ThrowIfNull(memoryLocation);
        var expectedNamespace = memoryLocation.Kind switch
        {
            DataRootKind.Development => DevelopmentDataRootPolicy.DevelopmentApplicationNamespace,
            DataRootKind.Test => TestDataRootPolicy.TestApplicationNamespace,
            _ => null,
        };

        if (expectedNamespace is null
            || !string.Equals(memoryLocation.ApplicationNamespace, expectedNamespace, StringComparison.Ordinal))
        {
            throw new DataRootViolationException("The BunDex requires a validated development or test memory location.");
        }

        return new BunDexLocation(
            memoryLocation.Kind,
            Path.Combine(memoryLocation.DataRootPath, DataLayout.BunDexDirectoryName),
            Path.Combine(memoryLocation.BraincaseRootPath, LedgerDirectoryName));
    }
}
