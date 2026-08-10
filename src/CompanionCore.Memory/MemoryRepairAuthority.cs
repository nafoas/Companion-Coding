namespace CompanionCore.Memory;

internal sealed class MemoryRepairAuthority
{
    private MemoryRepairAuthority()
    {
    }

    internal static MemoryRepairAuthority ForExplicitLocalUserIntent() => new();
}
