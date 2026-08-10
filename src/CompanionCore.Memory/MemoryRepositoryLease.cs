namespace CompanionCore.Memory;

/// <summary>
/// Cross-process ownership fence shared by ordinary repository access and offline
/// maintenance. A stale file is harmless; the open handle, not file existence, is the lease.
/// </summary>
internal sealed class MemoryRepositoryLease : IDisposable
{
    private readonly FileStream _stream;
    private bool _disposed;

    private MemoryRepositoryLease(FileStream stream)
    {
        _stream = stream;
    }

    internal static MemoryRepositoryLease Acquire(MemoryStoreLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        Directory.CreateDirectory(location.RootPath);
        try
        {
            var stream = new FileStream(
                location.MaintenanceLockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
            return new MemoryRepositoryLease(stream);
        }
        catch (IOException exception)
        {
            throw new MemoryMaintenanceBusyException(
                "The memory repository is already open or held by maintenance.",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new MemoryMaintenanceBusyException(
                "Exclusive ownership of the memory repository could not be established.",
                exception);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
    }
}
