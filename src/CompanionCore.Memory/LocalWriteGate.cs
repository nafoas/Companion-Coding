namespace CompanionCore.Memory;

/// <summary>
/// Sole public live-runtime/automated write ingress. The allowlist recognizes only the
/// concrete append proposal; unknown proposal implementations never reach durability.
/// </summary>
public sealed class LocalWriteGate
{
    private readonly MemoryCommitCoordinator _coordinator;

    internal LocalWriteGate(MemoryCommitCoordinator coordinator)
    {
        _coordinator = coordinator;
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

        try
        {
            return await _coordinator.SubmitAsync(appendProposal, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MemoryValidationException)
        {
            return WriteGateResult.Rejected(WriteGateRejectionReason.InvalidProposal);
        }
    }
}
