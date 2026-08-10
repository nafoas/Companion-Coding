using CompanionCore.Runtime.Diagnostics;

namespace CompanionCore.Runtime;

/// <summary>
/// The single authoritative companion identity for one process. Structurally, not just
/// conventionally, at most one of these can ever be constructed per process:
/// </summary>
/// <remarks>
/// <para>
/// The constructor is <c>private</c> — no code anywhere, including other types in
/// <c>CompanionCore.App</c> (which needs <see cref="InternalsVisibleToAttribute"/>
/// access for <see cref="ClaimConstructionAuthority"/>, see below), can call
/// <c>new CompanionRuntime(...)</c> directly. A prior revision granted
/// <c>internal</c> access to the constructor itself, which — correctly flagged in
/// review — let any type in an <c>InternalsVisibleTo</c>'d assembly construct
/// additional instances; internal visibility is granted per-assembly, so it could not
/// be narrowed further than "the whole App assembly." This revision fixes that by
/// gating construction through a one-shot authority instead of exposing the
/// constructor at any visibility broader than private.
/// </para>
/// <para>
/// <see cref="ClaimConstructionAuthority"/> is the only way to obtain a
/// <see cref="RuntimeAuthority"/>, and it succeeds at most once per process — a second
/// call from anywhere, including another type in the same assembly, throws
/// <see cref="InvalidOperationException"/> rather than silently minting a second
/// authority. <see cref="RuntimeAuthority"/>'s own constructor is <c>private</c> too,
/// so nothing can construct one except <see cref="ClaimConstructionAuthority"/> itself
/// (a nested type's private constructor is only reachable from the enclosing type's own
/// members, regardless of what other code in the same or a referencing assembly can
/// see). And <see cref="RuntimeAuthority.Construct"/> is single-use per authority
/// instance. The result: at most one <see cref="CompanionRuntime"/> can ever exist in a
/// process, and any attempt to construct a second one fails loudly and immediately
/// rather than succeeding.
/// </para>
/// </remarks>
public sealed class CompanionRuntime : IDisposable
{
    private static int _constructionCount;

    /// <summary>
    /// Total instances constructed in this process — structurally 0 or 1, never more,
    /// because of the one-shot authority above. Exists so the "one runtime across
    /// windows" acceptance test has something concrete to assert against.
    /// </summary>
    public static int ConstructionCount => _constructionCount;

    private readonly LifecycleStateMachine _stateMachine;

    private CompanionRuntime(IDiagnosticsSink? diagnostics)
    {
        _stateMachine = new LifecycleStateMachine(diagnostics);
        Interlocked.Increment(ref _constructionCount);
    }

    /// <summary>
    /// Claims the single-use authority to construct this process's one
    /// <see cref="CompanionRuntime"/>. Only <c>CompanionCore.App</c>'s composition root
    /// should call this, exactly once. A second call anywhere in the process — whether
    /// from the composition root by mistake, or from any other App type attempting to
    /// build a second identity — throws instead of succeeding.
    /// </summary>
    internal static RuntimeAuthority ClaimConstructionAuthority() => RuntimeAuthority.Claim();

    public RuntimeState State => _stateMachine.State;

    /// <summary>
    /// Cancelled when the runtime stops or is disposed. Owned background work (capture
    /// workers, pending requests) should observe this token so shutdown can cancel
    /// everything the runtime owns without leaving orphaned work behind.
    /// </summary>
    public CancellationToken LifetimeToken => _stateMachine.LifetimeToken;

    public LifecycleTransitionResult Start(bool checkpointRecovered = false) => _stateMachine.Start(checkpointRecovered);

    public LifecycleTransitionResult Nap() => _stateMachine.Nap();

    public LifecycleTransitionResult Wake() => _stateMachine.Wake();

    public LifecycleTransitionResult Stop() => _stateMachine.Stop();

    /// <summary>
    /// Idempotent. Cancels owned work and moves to <see cref="RuntimeState.Stopped"/> if
    /// it wasn't already; calling this more than once, or after <see cref="Stop"/>, is safe.
    /// </summary>
    public void Dispose() => _stateMachine.Dispose();

    /// <summary>
    /// A single-use capability: whoever holds one may construct exactly one
    /// <see cref="CompanionRuntime"/>, once. Its constructor is private, so the only way
    /// to obtain an instance is <see cref="ClaimConstructionAuthority"/>.
    /// </summary>
    internal sealed class RuntimeAuthority
    {
        private static int _claimed;

        private int _consumed;

        // Only Claim() below can reach this — a nested type's own static members may
        // always call its own private constructor, C# accessibility rules don't require
        // exposing it any further than that.
        private RuntimeAuthority()
        {
        }

        /// <summary>
        /// The single-use claim itself. Throws on any call after the first in the
        /// process. Private construction plus this being the only place that constructs
        /// a <see cref="RuntimeAuthority"/> means nothing can obtain one except through
        /// this gate.
        /// </summary>
        internal static RuntimeAuthority Claim()
        {
            if (Interlocked.CompareExchange(ref _claimed, 1, 0) != 0)
            {
                throw new InvalidOperationException(
                    "CompanionRuntime construction authority was already claimed in this process. " +
                    "Only the application composition root may claim it, and only once.");
            }

            return new RuntimeAuthority();
        }

        /// <summary>
        /// Constructs the one <see cref="CompanionRuntime"/> this authority permits.
        /// Calling this more than once on the same authority throws — the authority is
        /// single-use, matching "exactly one runtime," not merely "at most one at a time."
        /// A nested type has access to its enclosing type's private members (the reverse
        /// is not true in C#, which is why <see cref="Claim"/> constructs the authority
        /// itself rather than <see cref="CompanionRuntime"/> doing it), so this can call
        /// <c>CompanionRuntime</c>'s private constructor directly.
        /// </summary>
        internal CompanionRuntime Construct(IDiagnosticsSink? diagnostics = null)
        {
            if (Interlocked.CompareExchange(ref _consumed, 1, 0) != 0)
            {
                throw new InvalidOperationException("This construction authority has already been used.");
            }

            return new CompanionRuntime(diagnostics);
        }
    }
}
