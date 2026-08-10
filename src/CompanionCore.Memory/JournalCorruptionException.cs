namespace CompanionCore.Memory;

public sealed class JournalCorruptionException : InvalidDataException
{
    public JournalCorruptionException(string message)
        : base(message)
    {
    }
}
