namespace CompanionCore.Memory;

public sealed class MemoryValidationException : Exception
{
    public MemoryValidationException(string message)
        : base(message)
    {
    }
}
