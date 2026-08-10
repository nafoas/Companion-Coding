namespace CompanionCore.Memory;

/// <summary>
/// The deliberately narrow ingress shape for future automated/API output. Implementing
/// this interface does not grant a write; LocalWriteGate allowlists the concrete append
/// type and exact operation name.
/// </summary>
public interface IAutomatedWriteProposal
{
    Guid LocalOperationId { get; }

    string OperationName { get; }
}
