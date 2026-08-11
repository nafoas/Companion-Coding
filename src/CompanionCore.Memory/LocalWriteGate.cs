namespace CompanionCore.Memory;

using CompanionCore.Privacy;

/// <summary>
/// Sole public live-runtime/automated write ingress. The allowlist recognizes only the
/// concrete append proposal; unknown proposal implementations never reach durability.
/// </summary>
public sealed class LocalWriteGate
{
    private readonly MemoryCommitCoordinator _coordinator;
    private readonly RuntimePrivacyState _privacyState;
    private readonly ILiveWriteAdmissionTestHook? _testHook;

    internal LocalWriteGate(
        MemoryCommitCoordinator coordinator,
        RuntimePrivacyState privacyState,
        ILiveWriteAdmissionTestHook? testHook = null)
    {
        _coordinator = coordinator;
        _privacyState = privacyState;
        _testHook = testHook;
    }

    public async Task<WriteGateResult> SubmitAsync(
        IAutomatedWriteProposal proposal,
        CancellationToken cancellationToken = default)
    {
        if (proposal is not AppendMemoryProposal appendProposal
            || !string.Equals(
                proposal.OperationName,
                AppendMemoryProposal.AllowlistedOperationName,
                StringComparison.Ordinal))
        {
            return WriteGateResult.Rejected(WriteGateRejectionReason.OperationNotAllowlisted);
        }

        if (!_privacyState.TryAcquireAdmissionLease(out var privacyLease))
        {
            return WriteGateResult.Rejected(WriteGateRejectionReason.PrivacyPausedOrStale);
        }

        using (privacyLease)
        try
        {
            if (_testHook is not null)
            {
                await _testHook.OnAdmittedAsync(cancellationToken).ConfigureAwait(false);
            }

            return await _coordinator.SubmitAsync(appendProposal, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MemoryValidationException)
        {
            return WriteGateResult.Rejected(WriteGateRejectionReason.InvalidProposal);
        }
    }

    /// <summary>
    /// Generation-bound ingress for future asynchronous/remote work. Task 7 must use
    /// this overload so a result created before privacy stop cannot be admitted after
    /// explicit resume under a newer generation.
    /// </summary>
    public async Task<WriteGateResult> SubmitAsync(
        IAutomatedWriteProposal proposal,
        long expectedPrivacyGeneration,
        CancellationToken cancellationToken = default)
    {
        if (proposal is not AppendMemoryProposal appendProposal
            || !string.Equals(
                proposal.OperationName,
                AppendMemoryProposal.AllowlistedOperationName,
                StringComparison.Ordinal))
        {
            return WriteGateResult.Rejected(WriteGateRejectionReason.OperationNotAllowlisted);
        }

        if (!_privacyState.TryAcquireAdmissionLease(expectedPrivacyGeneration, out var privacyLease))
        {
            return WriteGateResult.Rejected(WriteGateRejectionReason.PrivacyPausedOrStale);
        }

        using (privacyLease)
        try
        {
            if (_testHook is not null)
            {
                await _testHook.OnAdmittedAsync(cancellationToken).ConfigureAwait(false);
            }

            return await _coordinator.SubmitAsync(appendProposal, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MemoryValidationException)
        {
            return WriteGateResult.Rejected(WriteGateRejectionReason.InvalidProposal);
        }
    }
}

internal interface ILiveWriteAdmissionTestHook
{
    Task OnAdmittedAsync(CancellationToken cancellationToken);
}
