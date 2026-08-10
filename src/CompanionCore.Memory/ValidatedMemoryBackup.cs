namespace CompanionCore.Memory;

internal sealed class ValidatedMemoryBackup : IAsyncDisposable
{
    private readonly string _validationParent;
    private readonly string _validationDirectory;
    private bool _disposed;

    internal ValidatedMemoryBackup(
        MemoryBackupManifest manifest,
        string extractedDatabasePath,
        string validationParent,
        string validationDirectory)
    {
        Manifest = manifest;
        ExtractedDatabasePath = extractedDatabasePath;
        _validationParent = validationParent;
        _validationDirectory = validationDirectory;
    }

    internal MemoryBackupManifest Manifest { get; }

    internal string ExtractedDatabasePath { get; }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        MemoryPathGuard.TryDeleteTaskOwnedDirectory(_validationParent, _validationDirectory);
        return ValueTask.CompletedTask;
    }
}
