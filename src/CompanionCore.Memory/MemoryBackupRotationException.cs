namespace CompanionCore.Memory;

internal sealed class MemoryBackupRotationException : Exception
{
    internal MemoryBackupRotationException(
        string archivePath,
        long cutSequence,
        Exception innerException)
        : base(
            "The validated backup was promoted, but journal rotation did not complete.",
            innerException)
    {
        ArchivePath = archivePath;
        CutSequence = cutSequence;
    }

    internal string ArchivePath { get; }

    internal long CutSequence { get; }
}
