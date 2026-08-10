namespace CompanionCore.Memory;

public sealed class MemoryValidationException : InvalidDataException
{
    public MemoryValidationException(string message)
        : base(message)
    {
    }
}
