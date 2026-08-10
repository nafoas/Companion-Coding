using CompanionCore.Runtime;

namespace CompanionCore.Runtime.Tests;

/// <summary>
/// These exercise the guard's acquire/release logic within one process using two
/// independent guard objects bound to the same mutex name — a proxy for, not a full
/// replacement of, an actual second-process launch (that's what
/// <c>CompanionCore.App.IntegrationTests</c> covers). True second-process behavior for
/// the real application executable can only be verified by actually launching it twice
/// on Windows.
/// </summary>
public sealed class SingleInstanceGuardTests
{
    private static string UniqueName() => $"CompanionCore.Tests.{Guid.NewGuid():N}";

    [Fact]
    public void TryAcquire_WhenUnheld_Succeeds()
    {
        using var guard = new SingleInstanceGuard(UniqueName());

        Assert.True(guard.TryAcquire());
    }

    [Fact]
    public void TryAcquire_WhenAlreadyHeldByAnotherGuardWithSameName_Fails()
    {
        // Windows-only assertion: a named OS mutex's cross-instance exclusion is
        // well-established Win32 behavior, which is what this guard is actually built
        // for (the shipping target is Windows). On Linux, .NET's named Mutex does not
        // reliably provide the same cross-instance guarantee in this environment/SDK —
        // confirmed empirically while writing this test — so it runs for real on
        // Windows (including CI) and is a documented no-op elsewhere.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var name = UniqueName();
        using var first = new SingleInstanceGuard(name);
        Assert.True(first.TryAcquire());

        // Deliberately Thread, not Task/async: a named mutex is owned per-thread, and
        // ReleaseMutex must run on the exact thread that acquired it. An async
        // continuation (await Task.Run(...)) can resume on a different thread-pool
        // thread than the one that started the method, which would make `first`'s
        // eventual `using`-triggered Dispose() run on the wrong thread and throw
        // SynchronizationLockException — this is exactly what the real Windows CI run
        // caught against a previous version of this test. A plain Thread + Join keeps
        // the entire method, including `first`'s acquire and its end-of-method Dispose,
        // on one thread throughout.
        var secondAcquired = false;
        var thread = new Thread(() =>
        {
            using var second = new SingleInstanceGuard(name);
            secondAcquired = second.TryAcquire();
        });
        thread.Start();
        thread.Join();

        Assert.False(secondAcquired);
    }

    [Fact]
    public void TryAcquire_AfterFirstHolderDisposes_Succeeds()
    {
        // Windows-only assertion; see the comment in the previous test for why.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var name = UniqueName();
        var first = new SingleInstanceGuard(name);
        Assert.True(first.TryAcquire());
        first.Dispose(); // Same thread that acquired it — no await between the two.

        var secondAcquired = false;
        var thread = new Thread(() =>
        {
            using var second = new SingleInstanceGuard(name);
            secondAcquired = second.TryAcquire();
        });
        thread.Start();
        thread.Join();

        Assert.True(secondAcquired);
    }

    [Fact]
    public void TryAcquire_TwiceOnSameGuard_DoesNotLeakRecursiveOwnership()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var name = UniqueName();
        var first = new SingleInstanceGuard(name);
        Assert.True(first.TryAcquire());
        Assert.True(first.TryAcquire());
        first.Dispose();

        var secondAcquired = false;
        var thread = new Thread(() =>
        {
            using var second = new SingleInstanceGuard(name);
            secondAcquired = second.TryAcquire();
        });
        thread.Start();
        thread.Join();

        Assert.True(secondAcquired);
    }

    [Fact]
    public void TryAcquire_AfterDispose_Throws()
    {
        var guard = new SingleInstanceGuard(UniqueName());
        guard.Dispose();

        Assert.Throws<ObjectDisposedException>(() => guard.TryAcquire());
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var guard = new SingleInstanceGuard(UniqueName());
        guard.TryAcquire();

        guard.Dispose();
        var exception = Record.Exception(guard.Dispose);

        Assert.Null(exception);
    }
}
