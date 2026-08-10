using CompanionCore.Runtime;

namespace CompanionCore.Runtime.Tests;

/// <summary>
/// <see cref="CompanionRuntime.ClaimConstructionAuthority"/> can only succeed once per
/// process, so this is deliberately a single test method exercising the whole mechanism
/// end to end, rather than several independent tests that would race each other for the
/// one claim. <see cref="LifecycleStateMachineTests"/> covers everything about lifecycle
/// behavior that doesn't depend on this one-shot restriction — this file is only about
/// proving the restriction itself is real.
/// </summary>
public sealed class CompanionRuntimeConstructionTests
{
    [Fact]
    public void ConstructionAuthority_IsSingleUse_EndToEnd()
    {
        var before = CompanionRuntime.ConstructionCount;

        var authority = CompanionRuntime.ClaimConstructionAuthority();

        // A second claim, from anywhere in the process, fails. This is the actual fix
        // for "any type in the App assembly could construct a second instance": the
        // point was never that other types can't call this — internal visibility can't
        // prevent that — it's that only the first caller in the process ever succeeds.
        Assert.Throws<InvalidOperationException>(() => CompanionRuntime.ClaimConstructionAuthority());

        var runtime = authority.Construct();

        Assert.Equal(before + 1, CompanionRuntime.ConstructionCount);
        Assert.Equal(RuntimeState.NotStarted, runtime.State);

        // The authority itself is single-use too, independent of the process-wide claim
        // above — reusing a held authority must also fail.
        Assert.Throws<InvalidOperationException>(() => authority.Construct());

        // Delegation sanity: CompanionRuntime's public surface actually drives real
        // lifecycle state through the wrapped LifecycleStateMachine, not a no-op.
        var startResult = runtime.Start();
        Assert.True(startResult.IsValid);
        Assert.Equal(RuntimeState.Running, runtime.State);

        runtime.Dispose();
        Assert.Equal(RuntimeState.Stopped, runtime.State);
        Assert.Throws<ObjectDisposedException>(() => runtime.Start());
    }
}
