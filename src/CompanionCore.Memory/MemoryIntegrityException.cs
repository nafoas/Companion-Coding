namespace CompanionCore.Memory;

public sealed class MemoryIntegrityException : Exception
{
    public MemoryIntegrityException(string message)
        : base(message)
    {
    }
}
