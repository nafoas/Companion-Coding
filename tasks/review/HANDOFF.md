# Task Handoff

## Task

Task 1 — Reproducible Neutral Skeleton, per `tasks/active/task-01-skeleton.md`. This update covers the foreman's independent Windows re-verification of revision 2 (commit `7aaa40d`): **the implementation passes — 58/58 tests, including all four real-process integration tests, on an independently rerun Windows CI job.** Two purely procedural items remain before merge: moving to the authorized branch name, and refreshing this handoff to stop calling Windows integration "unverified" now that it's been verified (by the foreman's independent rerun, not by me — see below).

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

**`CompanionCore.App.IntegrationTests` could not be run by me in this session** — attempting `dotnet test` against it here fails immediately with "You must install or update .NET to run this application," because its apphost is a Windows executable and this session is Linux. What I verified locally instead: the project **compiles clean** (`dotnet build ... -p:EnableWindowsTargeting=true`), and the `ProjectReference`-copies-the-exe mechanism `AppProcess.Locate()` depends on was confirmed present (DLL, deps.json, runtimeconfig.json, apphost stub all landed in the test project's output directory).

**Now independently verified on real Windows, by the foreman, not by me**: PR #3's review of this exact commit (`7aaa40d`) reports an independent rerun of the `build-and-test` job on Windows — [CI run 31342500865](https://github.com/nafoas/Companion-Coding/actions/runs/31342500865), rerun job `93319600038` — with this exact result:

```
locked restore:  passed
Release build:   passed, 0 warnings, 0 errors
Runtime:                    24/24 passed
Presentation:                19/19 passed
Capture fake:                11/11 passed
App integration (real WPF):   4/4 passed
Total: 58/58 passed, 0 failed, 0 skipped
```

This closes out all four previously-missing acceptance items for real:
1. Real launch, no key/network/capture → `Ready_LaunchesWithNoKeyNetworkOrCapture_AndReachesReadyState` — passed on Windows.
2. Multiple real windows, one runtime → `MultiWindow_ThreeRealWindows_ShareExactlyOneRuntimeConstruction` — passed on Windows.
3. Genuine second process never builds a runtime → `SecondProcess_NeverConstructsARuntime_AndFirstProcessStaysUnaffected` — passed on Windows.
4. Clean shutdown, no process left behind → `Shutdown_StopThenClose_ExitsCleanlyWithStoppedStateAndNoLeftoverProcess` — passed on Windows.

## Remaining

- Nothing implementation-related. The foreman's re-review states: "Once the correct branch, draft PR, and current handoff are present, I can approve and merge without another implementation round."
- The branch-protocol item below is the only open item, and it is a permission question for the user, not an implementation gap.
- No Deferred Findings.

## Risks and assumptions

- **`RuntimeAuthority`'s construction had to move**: I initially tried making `RuntimeAuthority`'s constructor `private` and calling it from `CompanionRuntime.ClaimConstructionAuthority()` (a *different* member of the enclosing type) — this does not compile in C#; a nested type's private members are not automatically accessible from the enclosing type (I had this backwards). Fixed by moving the claim logic onto `RuntimeAuthority` itself as a static method (which can call its own private constructor) and having `RuntimeAuthority.Construct()` call `CompanionRuntime`'s private constructor instead (nested → enclosing private access *is* allowed, and is what makes this design possible at all). Caught by the local compiler, not left for CI.
- **`--test-mode` is new attack surface on the shipped app**, technically present in the real executable, not just test builds. It's inert unless explicitly passed, does nothing capture/persistence/network-related, and is scoped to lifecycle/window mechanics only — but flagging its existence explicitly in case the foreman would rather it be compiled out of non-test builds via a build configuration switch instead.
- **Branch protocol — explicit permission requested this round, not yet resolved.** This submission is still on `claude/multi-ai-code-collab-o5qhj1`, not `claude/task-01-skeleton`. This session's hosting environment pins pushes to this one branch as a hard operational constraint, separate from and outside this repository's own protocol — the same reason given in the two prior handoffs. Rather than restate it a third time without acting on it, I've now explicitly asked the user (who set this collaboration up and is the only party who can grant broader push permission or otherwise resolve it) whether to request that permission. That answer isn't in yet as of this commit. Per the foreman's own instruction ("If it is still impossible, report the exact permission blocker without starting Task 2") — that's what this bullet, and the PR comment accompanying this push, do.

## Review focus

- Whether the branch-protocol item can be resolved by the foreman/user accepting review-and-merge against `claude/multi-ai-code-collab-o5qhj1` as-is (the same branch Task 0 was reviewed and merged from without issue), rather than requiring the exact named branch.

## Repository state

- Branch: `claude/multi-ai-code-collab-o5qhj1`, tracked by PR #3 (draft, base `main`).
- This update is committed on top of `7aaa40db77e5f38738e5f33f2e58e99443b5f81c` (revision 2, independently verified by the foreman on Windows CI — 58/58), which sits on `bdb024e` (submission 1), the merged `72ae422` (Task 0 approval + Task 1 authorization), and the accepted Task 0 baseline `58bbdf9915659b3887e311f7b9cdf819cc39fc13`.
- Worktree is clean except for this handoff file, the only file this commit touches — no product code changed, per the foreman's instruction to make only repository/handoff corrections at this point.

## Next safe task

Smallest safe next action, not yet started: await the user's answer on the branch-permission question, then either push the reviewed content to `claude/task-01-skeleton` and open a draft PR there (if granted), or relay the foreman's own fallback (reviewing/merging against the current branch) if that's what's agreed instead. Do not begin Task 2 regardless of how the branch question resolves — that's a separate authorization the foreman hasn't given.

## Credit status

Not credit-related. Normal task-boundary stop: this session was interrupted mid-task twice earlier in this task (an autonomous check-in firing while work was in progress, and a worker-process restart) but resumed both times from the preserved working tree without losing work. This update reflects the current, foreman-verified state, with one genuine open question (branch permission) now explicitly routed to the user rather than deferred again.
