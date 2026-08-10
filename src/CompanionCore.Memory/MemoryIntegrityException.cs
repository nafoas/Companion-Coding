namespace CompanionCore.Memory;

public sealed class MemoryIntegrityException : InvalidDataException
{
    public MemoryIntegrityException(string message)
        : base(message)
    {
    }
}
