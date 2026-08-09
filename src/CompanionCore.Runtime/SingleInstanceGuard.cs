namespace CompanionCore.Runtime;

/// <summary>
/// A named-mutex second-process guard. The composition root must call
/// <see cref="TryAcquire"/> before constructing a <see cref="CompanionRuntime"/> or
/// initializing any subsystem — a second process that fails to acquire must exit (or
/// activate the first instance) without ever reaching that construction step.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _acquired;
    private bool _disposed;

    public SingleInstanceGuard(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _mutex = new Mutex(initiallyOwned: false, name: name);
    }

    /// <summary>
    /// Returns true if this instance now holds the guard. Never blocks — a second
    /// process must decide deterministically and immediately, not wait for the first
    /// to exit.
    /// </summary>
    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _acquired = _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // The previous holder terminated without releasing; its state is gone, but
            // the mutex itself is still valid and we now own it.
            _acquired = true;
        }

        return _acquired;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_acquired)
        {
            _mutex.ReleaseMutex();
            _acquired = false;
        }

        _mutex.Dispose();
    }
}
