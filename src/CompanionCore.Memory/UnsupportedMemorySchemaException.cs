namespace CompanionCore.Memory;

public sealed class UnsupportedMemorySchemaException : InvalidDataException
{
    public UnsupportedMemorySchemaException(int actualVersion)
        : base($"Memory schema version {actualVersion} is not supported by this build.")
    {
        ActualVersion = actualVersion;
    }

    public int ActualVersion { get; }
}
