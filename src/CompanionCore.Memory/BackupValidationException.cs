namespace CompanionCore.Memory;

internal sealed class BackupValidationException : Exception
{
    internal BackupValidationException(string message)
        : base(message)
    {
    }

    internal BackupValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
