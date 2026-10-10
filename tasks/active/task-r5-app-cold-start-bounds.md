# R5 — App Integration Cold-Start Bounds

Status: **active — implementation**
Authorized: 2026-10-10. Task 7 deferred finding 5 routes R5 before Task 8, under Boss's standing direction to fix root causes rather than relax tests.
Accepted remote base: `ed5f9a88685f38c84ccd07841cbf163f339863d9`
Working branch: `agent/task-r5-app-cold-start-bounds`
Builder: Claude

## Objective

Make the App process-integration tests measure exactly what they claim, so a shared CI runner's disk load can no longer turn a cold start into a false "did not exit" failure. A genuine startup hang and a genuine shutdown hang must both still fail promptly.

## Entry evidence (root cause)

`AppProcessTests.Shutdown_StopThenClose_ExitsCleanlyWithStoppedStateAndNoLeftoverProcess` failed at its 30-second bound three times:

- PR run `38031390951`, attempt 1;
- push run `38031750912`;
- PR run `38031754225`.

Other passes came close: 27.0 s on accepted-lineage R4 run `38028570862` (before Task 7 existed), and 29.8 s, 17.2 s, 17.5 s, and 16.1 s on later runs, the last on post-merge `main` `ed5f9a8`.

The TRX start times prove the cause. In every one of those runs, this test is the **first** App process the suite launches: it starts within milliseconds of the assembly's first test, and every other App scenario launches after it. Even on quiet runs it is the slowest App test (≈3.5 s, against ≈1 s for later launches). Every slow run overlapped heavy disk-sync suites, Memory and then Api.

What the first launch does:

- The policy catalog writes nothing: a missing policy file loads as valid-empty.
- So the first launch's extra cost is the cold load and JIT of .NET and WPF from disk, which competes with concurrent fsync traffic.

The shutdown step itself (`Stop`, window close, `OnExit` disposal) is not shown to be slow. The test's single 30-second `WaitForExit` charges the whole cold start against a bound whose stated purpose is to catch a process that does not exit.

## Required correction

- `AppProcess` measures two phases for every scenario:
  - **Startup:** launch until the scenario's first stdout marker line, bounded by an explicit cold-start liveness bound.
  - **Exit:** from that marker until process exit, bounded by the unchanged 30-second `ExitTimeout`.
- Every test mode prints exactly one marker immediately before it stops, closes, or shuts down, so the exit phase covers precisely the stop/close/`OnExit` work.
- A scenario that ends its output without a marker still has its exit bounded.
- A timeout in either phase kills the process tree and throws a message naming the phase and its bound.
- Standard error is drained concurrently from launch, so neither pipe can block the child.
- Each test writes both phase durations to its test output for evidence.
- The second-process test applies the same split: the held process's first marker uses the startup bound, and its exit after release uses the 30-second exit bound.

## Invariants and limits

- The 30-second exit bound is not raised. It now times only the exit work.
- A startup hang still fails at the startup bound, and a shutdown hang still fails at 30 s. No scenario may wait without bound.
- No product, App, CI workflow, or other test file changes. The fix lives entirely in the integration harness and its tests.
- No test is skipped, quarantined, retried, or reordered.

## Acceptance scenarios

1. All 13 App integration tests pass on both Windows CI event paths, with both phase durations visible in the TRX output.
2. The shutdown test's exit phase is small: well under its 30-second bound, including on a run where startup is slow.
3. Every other suite is unchanged, and the full run passes 453/453.

## Allowed change scope

- `tests/CompanionCore.App.IntegrationTests/AppProcess.cs`
- `tests/CompanionCore.App.IntegrationTests/AppProcessTests.cs`
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Pending. The gate requires:

- the acceptance scenarios;
- a strict build with 0 warnings;
- both Windows CI paths with verified artifacts and phase evidence;
- actual-diff review;
- an exact merge-ref check;
- recorded judgments.

## Personal Round Judgments

### J1 — The cold-start liveness bound is 90 seconds

The worst cold start observed under heavy concurrent disk load is about 30 s. That figure includes the three runs that hit the old bound while still in startup. Three times the worst observation leaves headroom for a busier runner, and a real startup hang still fails within about 1.5 minutes. Startup duration was never an acceptance requirement; shutdown cleanliness is, and its bound stays 30 s. Reverse by changing one constant, with evidence.

### J2 — Red evidence is the recorded TRX timeline, not a synthetic reproduction

The failure depends on runner disk contention, which cannot be reproduced deterministically in CI without changing CI. The six-run TRX start-time analysis above is the red evidence. The phase durations this packet adds will show, in future runs, which phase any slow launch spends its time in.

## Deferred findings

1. **Shutdown exit-phase profile (Stage 11):** the `shutdown` scenario's exit phase is consistently about 2.0 s (2.026 s and 2.038 s on the candidate), while `ready` and `multiwindow` exit in 0.01–0.16 s. No 2-second timeout exists on the App, TargetAuth, Capture.Client, or Runtime paths. The scenario's distinguishing step is closing a real WPF window during `OnStartup`, which suggests WPF or render teardown. It passes well inside its bound, so it is recorded for Stage 11 physical profiling rather than widened into this packet.
