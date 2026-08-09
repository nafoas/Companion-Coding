# Task Handoff

## Task

Task 1 — Reproducible Neutral Skeleton, per `tasks/active/task-01-skeleton.md`. This handoff covers revision 2, addressing the foreman's PR #3 review of submission 1 (commit `bdb024e`), which requested six changes: a real Windows CI failure to fix, four missing executable acceptance tests, a structural single-runtime fix, worker determinism/cancellation, a dependency lock strategy, and a handoff/branch-protocol correction.

## Completed

1. **Fixed the real CI failure** (`bdb024e` failed `TryAcquire_WhenAlreadyHeldByAnotherGuardWithSameName_Fails` on `windows-latest`: [run 31336833432](https://github.com/nafoas/Companion-Coding/actions/runs/31336833432)). Root cause: a named mutex is owned per-thread, and the test used `await Task.Run(...)`, whose continuation can resume on a different thread-pool thread than the one that called `first.TryAcquire()` — so `first`'s end-of-method `Dispose()` (→ `ReleaseMutex()`) could run on the wrong thread and throw `SynchronizationLockException`. Fixed by rewriting both affected tests to use a plain `Thread` + `Join()` instead of `Task`/`await`, keeping `first`'s entire lifetime — acquire and dispose — on one thread with no continuation hop possible.
2. **Added `tests/CompanionCore.App.IntegrationTests`**, a Windows-only project that launches the real compiled `CompanionCore.App.exe` via `System.Diagnostics.Process` and asserts on its actual behavior, using a new `--test-mode=<scenario>` argument the app understands (`ready`, `multiwindow`, `shutdown`, `hold`) — a bounded test harness rather than UI automation tooling. Four tests cover exactly the four missing items: launch with no key/network/capture reaching a `READY` marker; three real windows sharing `CONSTRUCTIONS:1`; a genuine second OS process rejected with `CONSTRUCTIONS:0` in its own process while the first (held via `hold` mode) is unaffected; and stop-then-close exiting cleanly with `SHUTDOWN:Stopped`.
3. **Made single-runtime construction structural**, per the review's correct point that a prior `internal` constructor was reachable from every type in `CompanionCore.App` via `InternalsVisibleTo`. Split `CompanionRuntime` into two types: `LifecycleStateMachine` (internal, freely constructible, carries all the actual FSM logic — this is what most unit tests now exercise directly, since it has no singleton restriction to fight) and `CompanionRuntime` (public, `private` constructor, obtainable only via `ClaimConstructionAuthority()` — a static claim that succeeds at most once per process; a second call anywhere, including another type in the same assembly, throws `InvalidOperationException` rather than silently succeeding). The authority itself is single-use per instance too.
4. **Made `FakeCaptureWorker` deterministic and properly cancellable.** Added `ISystemClock`/`SystemClock` to `CompanionCore.Capture.Contracts`; `FakeCaptureWorker` now takes an injectable clock instead of calling `DateTimeOffset.UtcNow` directly, so tests assert exact `CaptureFrameMetadata`/`CaptureWorkerStatusChanged` values, and two independent workers given the same fixed clock produce byte-identical output. `StopAsync` (and therefore `RestartAsync`, which calls it) now honors an already-cancelled token by throwing before mutating status, rather than ignoring cancellation. Added tests for cancelled stop/restart leaving status unchanged, and disposal leaving no further-callable surface.
5. **Added a dependency lock strategy.** `Directory.Build.props` sets `RestorePackagesWithLockFile=true` for every project; `packages.lock.json` is committed for all nine projects (generated via `dotnet restore --force-evaluate`). CI, `scripts/build.ps1`, and `scripts/test.ps1` all now restore with `--locked-mode`, which fails if the lock file doesn't match a fresh restore.
6. **This handoff** records the failed run and the fix, exact current test counts, what the new integration tests execute, and (below) an honest restatement of the branch-protocol gap rather than a claim of compliance.

## Changed

- `src/CompanionCore.Runtime/CompanionRuntime.cs` — rewritten: thin public wrapper, private constructor, `ClaimConstructionAuthority()`/`RuntimeAuthority`.
- `src/CompanionCore.Runtime/LifecycleStateMachine.cs` (new) — the actual state-machine logic, moved out of `CompanionRuntime`.
- `src/CompanionCore.Runtime/AssemblyInfo.cs` — comment updated to describe what `InternalsVisibleTo` actually grants now.
- `src/CompanionCore.App/App.xaml.cs` — uses `ClaimConstructionAuthority().Construct(...)`; adds `--test-mode=<scenario>` handling (`ready`/`multiwindow`/`shutdown`/`hold`).
- `src/CompanionCore.Capture.Contracts/ISystemClock.cs`, `SystemClock.cs` (new).
- `src/CompanionCore.Capture.Fake/FakeCaptureWorker.cs` — injectable clock; `StopAsync` honors cancellation before mutating state.
- `tests/CompanionCore.Runtime.Tests/CompanionRuntimeTests.cs` → replaced by `LifecycleStateMachineTests.cs` (same coverage, against the new type) and `CompanionRuntimeConstructionTests.cs` (new: proves the one-shot authority mechanism end to end in a single test, since it can only succeed once per test process).
- `tests/CompanionCore.Runtime.Tests/SingleInstanceGuardTests.cs` — `Thread`/`Join` fix for the CI failure.
- `tests/CompanionCore.Capture.Tests/FakeCaptureWorkerTests.cs` — exact-value assertions with a manual clock; new cancellation tests.
- `tests/CompanionCore.App.IntegrationTests/**` (new) — `CompanionCore.App.IntegrationTests.csproj`, `AppProcess.cs` (process-launch helper), `AppProcessTests.cs` (4 tests).
- `Directory.Build.props` (new); `packages.lock.json` in all 9 projects (new); `.github/workflows/ci.yml`, `scripts/build.ps1`, `scripts/test.ps1` — restore in locked mode.
- `CompanionCore.slnx` — adds the integration test project.

All within the task's allowed change scope. Nothing from the forbidden list was added.

## Verification

Same environment as submission 1: Linux container, no Windows machine, local .NET 10 SDK 10.0.302 installed to actually run things.

```
dotnet restore CompanionCore.slnx -p:EnableWindowsTargeting=true --locked-mode
```
→ succeeds — proves the committed lock files are internally consistent with a fresh restore (this is the actual property `--locked-mode` checks).

```
dotnet build CompanionCore.slnx -p:EnableWindowsTargeting=true --no-restore --configuration Release
```
→ `Build succeeded. 0 Warning(s). 0 Error(s).` — all 9 projects, including the new `CompanionCore.App.IntegrationTests`.

```
dotnet test tests/CompanionCore.Runtime.Tests/... tests/CompanionCore.Presentation.Tests/... tests/CompanionCore.Capture.Tests/... --no-build --configuration Release
```
→
```
Passed! - Failed: 0, Passed: 24, Skipped: 0, Total: 24 - CompanionCore.Runtime.Tests.dll
Passed! - Failed: 0, Passed: 19, Skipped: 0, Total: 19 - CompanionCore.Presentation.Tests.dll
Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11 - CompanionCore.Capture.Tests.dll
```
54 tests, 54 passed, 0 failed, 0 skipped (up from 50: +4 net in Capture from new cancellation/exact-value tests; Runtime's 24 is now split 23 `LifecycleStateMachineTests` + 1 `CompanionRuntimeConstructionTests`, same total).

**`CompanionCore.App.IntegrationTests` could not be run here** — attempting `dotnet test` against it fails immediately with "You must install or update .NET to run this application," because its apphost is a Windows executable and this is Linux; there is no way to execute it in this environment even with the cross-compile flag (that flag only enables *compiling* against Windows reference assemblies, not running Windows binaries). What I *did* verify locally:
- The project **compiles clean** (`dotnet build ... -p:EnableWindowsTargeting=true`), including all four test methods and the process-launch helper — no type errors, and one real compile error (`Task<string?>` has no `.AsTask()` — that's only on `ValueTask`) was caught and fixed this way.
- The core risk in the design — whether `ProjectReference` to an executable project actually copies that executable's build output into the referencing test project's own output directory, which `AppProcess.Locate()` depends on — **is confirmed working**: `CompanionCore.App.dll`, its `.deps.json`/`.runtimeconfig.json`, and an apphost stub all appear in `tests/CompanionCore.App.IntegrationTests/bin/.../`. The apphost stub is named `CompanionCore.App` with no extension here only because this is a Linux build host (Unix apphosts have no extension); on the real Windows CI runner the equivalent file will be `CompanionCore.App.exe`, which is what `Locate()`'s primary path checks for. This is genuinely unverified until the first Windows CI run — **this is the single highest-risk, most-unverified piece of this revision**, flagged plainly rather than glossed over.

**Mapped against the four previously-missing acceptance items** — now each has a real test, unverified-by-me but there to run on Windows CI for the first time:
1. Real launch, no key/network/capture → `Ready_LaunchesWithNoKeyNetworkOrCapture_AndReachesReadyState`, asserts exit code 0 and a `READY`/`CONSTRUCTIONS:1` marker.
2. Multiple real windows, one runtime → `MultiWindow_ThreeRealWindows_ShareExactlyOneRuntimeConstruction`, asserts `WINDOWS:3 CONSTRUCTIONS:1`.
3. Genuine second process never builds a runtime → `SecondProcess_NeverConstructsARuntime_AndFirstProcessStaysUnaffected`, launches a `hold`-mode process, waits for its `HOLDING` marker, launches a second `ready`-mode process expecting exit code 2 and `CONSTRUCTIONS:0` in *that* process, then releases the first via stdin and asserts it exits 0.
4. Clean shutdown, no process left behind → `Shutdown_StopThenClose_ExitsCleanlyWithStoppedStateAndNoLeftoverProcess`, asserts `SHUTDOWN:Stopped` and exit code 0; `AppProcess.Run` itself throws `TimeoutException` (failing the test) if the process ever hangs rather than exiting.

## Remaining

- The integration tests are unverified by actual execution — first real signal comes from Windows CI on this push.
- No Deferred Findings.

## Risks and assumptions

- **Same environment ceiling as submission 1, now narrower**: everything except real Windows process/window/mutex behavior is verified by actually running it. That remaining gap is now covered by real (but unexecuted-by-me) tests instead of being an acknowledged absence.
- **`RuntimeAuthority`'s construction had to move**: I initially tried making `RuntimeAuthority`'s constructor `private` and calling it from `CompanionRuntime.ClaimConstructionAuthority()` (a *different* member of the enclosing type) — this does not compile in C#; a nested type's private members are not automatically accessible from the enclosing type (I had this backwards). Fixed by moving the claim logic onto `RuntimeAuthority` itself as a static method (which can call its own private constructor) and having `RuntimeAuthority.Construct()` call `CompanionRuntime`'s private constructor instead (nested → enclosing private access *is* allowed, and is what makes this design possible at all). Caught by the local compiler, not left for CI.
- **`--test-mode` is new attack surface on the shipped app**, technically present in the real executable, not just test builds. It's inert unless explicitly passed, does nothing capture/persistence/network-related, and is scoped to lifecycle/window mechanics only — but flagging its existence explicitly in case the foreman would rather it be compiled out of non-test builds via a build configuration switch instead.
- **Branch protocol — restated, not resolved**: this submission is again on `claude/multi-ai-code-collab-o5qhj1`, not `claude/task-01-skeleton`. This is a procedural blocker, not a compliance claim: this session's environment pins pushes to one branch and I do not have permission to push a differently-named one without explicit user authorization, which I have not sought again this round given the session has been unstable (a background auto-check-in and a worker-process restart both interrupted this task while it was in progress). I'm reporting this plainly a second time rather than either claiming the required branch was used or silently repeating the same unaddressed gap without comment.

## Review focus

- Whether the integration-test design (a `--test-mode` flag plus process/stdout assertions) is what "a bounded test mode/harness" was meant to invite, or whether the foreman expected something else (e.g. actual UI automation).
- The `RuntimeAuthority`/`ClaimConstructionAuthority` structure — whether it actually closes the "any App type can construct a second instance" gap to the foreman's satisfaction.
- Whether the branch-protocol gap needs a different resolution than repeated, honest disclosure (e.g. the foreman or user explicitly waiving it, or granting this session permission to push the named branch).

## Repository state

- Branch: `claude/multi-ai-code-collab-o5qhj1`, tracked by PR #3 (draft, base `main`).
- This revision is committed on top of `bdb024e7b3e39ac5a33691b1edda8d4c6c048845` (submission 1), which sits on the merged `72ae422` (Task 0 approval + Task 1 authorization) and the accepted Task 0 baseline `58bbdf9915659b3887e311f7b9cdf819cc39fc13`.
- Worktree is clean except for the changes listed under Changed, all included in the commit this handoff accompanies.

## Next safe task

Smallest safe next action, not yet started: await foreman review of this revision on PR #3, ideally alongside the actual Windows CI run's result. Do not begin Task 2.

## Credit status

Not credit-related. Normal task-boundary stop: this session was interrupted mid-task twice (an autonomous check-in firing while work was in progress, and a worker-process restart) but resumed both times from the preserved working tree without losing work; this handoff reflects the completed, verified state after resuming, not a mid-task checkpoint.
