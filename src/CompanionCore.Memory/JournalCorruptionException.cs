namespace CompanionCore.Memory;

public sealed class JournalCorruptionException : Exception
{
    public JournalCorruptionException(string message)
        : base(message)
    {
    }
}
