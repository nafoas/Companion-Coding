namespace CompanionCore.Memory;

internal sealed class MemoryMaintenanceBusyException : Exception
{
    internal MemoryMaintenanceBusyException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
