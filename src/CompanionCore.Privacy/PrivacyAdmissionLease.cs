namespace CompanionCore.Privacy;

/// <summary>
/// Admission evidence for work entering a privacy-sensitive downstream boundary. The
/// lease is acquired only while its generation is current and active. Privacy stop
/// revokes new admission synchronously, then waits for existing leases to drain rather
/// than interrupting a durable write or half-delivering an already-admitted event.
/// </summary>
public sealed class PrivacyAdmissionLease : IDisposable
{
    private RuntimePrivacyState? _owner;

    internal PrivacyAdmissionLease(RuntimePrivacyState owner, long generation)
    {
        _owner = owner;
        Generation = generation;
    }

    public long Generation { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref _owner, null)?.ReleaseAdmissionLease();
    }
}
