namespace CompanionCore.Memory;

public sealed class DataRootViolationException : InvalidOperationException
{
    public DataRootViolationException(string message)
        : base(message)
    {
    }
}
