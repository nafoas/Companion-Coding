# Task Handoff

## Task

Task 1 — Reproducible Neutral Skeleton, per `tasks/active/task-01-skeleton.md`. Build the smallest reproducible Windows skeleton proving one local runtime, deterministic lifecycle behavior, a neutral presentation seam, and a synthetic capture-worker contract. Infrastructure only.

## Completed

A five-project solution (`CompanionCore.slnx`, the .NET 10 XML solution format) plus three test projects:

- **`CompanionCore.Runtime`** (`net10.0`) — `CompanionRuntime` with an `internal` constructor (only `CompanionCore.App` and `CompanionCore.Runtime.Tests` have compiler-enforced access via `InternalsVisibleTo` in `AssemblyInfo.cs` — no other assembly can construct a second instance, full stop). Deterministic `Start`/`Nap`/`Wake`/`Stop` transitions, each returning a `LifecycleTransitionResult` (valid/invalid, prior/resulting state, checkpoint-recovered flag) rather than throwing on an invalid transition. Idempotent `Dispose()` that cancels a `LifetimeToken` and moves to `Stopped`. A `SingleInstanceGuard` wrapping a named `Mutex` for second-process detection. A minimal `IDiagnosticsSink` abstraction (`NullDiagnosticsSink` by default, `ConsoleDiagnosticsSink` opt-in) — `CompanionRuntime` logs the raw event/state pair for an invalid transition only to whatever sink it's given, never into the presentation output.
- **`CompanionCore.Presentation`** (`net10.0`) — `IPresentationSink` (render-only), `IPersonalityAdapter`, and `NeutralPersonalityAdapter`: a direct, literal implementation of architecture §6.2.1's normative table, plus `PlaceholderStrings` mapping each content key to a neutral string.
- **`CompanionCore.Capture.Contracts`** (`net10.0`) — `ICaptureWorker` (bounded, cancellable, no raw full-screen capability anywhere in the surface).
- **`CompanionCore.Capture.Fake`** (`net10.0`) — `FakeCaptureWorker`, the only implementation shipped: in-process, deterministic, synthetic 1×1 frame metadata only, no real pixels, no process spawn, no identity/memory access.
- **`CompanionCore.App`** (`net10.0-windows`, WPF) — the composition root. `App.xaml.cs` acquires the `SingleInstanceGuard` before constructing anything else; a failed acquisition shuts down immediately without ever constructing a `CompanionRuntime`. `MainWindow` is a deliberately plain shell: one blank multi-line `TextBox`, one neutral placeholder icon (a plain gray `Ellipse` — no artwork asset), one status `TextBlock`, and Nap/Wake/Stop buttons (Start fires automatically on launch). `WpfPresentationSink` is the only place a mapped `PresentationContent` becomes visible text.

`global.json` pins the SDK to `10.0.302` with `rollForward: disable` (exact patch, not a floating alias, per the task's requirement). `.github/workflows/ci.yml` builds and tests the solution on `windows-latest` via `actions/setup-dotnet` reading `global.json`. `scripts/build.ps1`/`scripts/test.ps1` wrap the same commands locally.

Nothing from the explicitly-forbidden list was implemented: no real capture/WGC/D3D/HWND code, no out-of-process worker, no SQLite/MemoryStore/journal/backup, no API bridge/network/credentials, no personality/themed text, no animation/audio/game integration/packaging.

## Changed

New files only — nothing pre-existing was modified:

- `global.json`
- `CompanionCore.slnx`
- `src/CompanionCore.App/**` (csproj, `App.xaml`/`.cs`, `MainWindow.xaml`/`.cs`, `WpfPresentationSink.cs`)
- `src/CompanionCore.Runtime/**` (csproj, `AssemblyInfo.cs`, `CompanionRuntime.cs`, `RuntimeState.cs`, `LifecycleEvent.cs`, `LifecycleTransitionResult.cs`, `SingleInstanceGuard.cs`, `Diagnostics/*.cs`)
- `src/CompanionCore.Presentation/**` (csproj, `IPresentationSink.cs`, `IPersonalityAdapter.cs`, `NeutralPersonalityAdapter.cs`, `PresentationContent.cs`, `ExpressionIntent.cs`, `PlaceholderStrings.cs`)
- `src/CompanionCore.Capture.Contracts/**` (csproj, `ICaptureWorker.cs`, `CaptureWorkerStatus.cs`, `CaptureWorkerStatusChanged.cs`, `CaptureFrameMetadata.cs`)
- `src/CompanionCore.Capture.Fake/**` (csproj, `FakeCaptureWorker.cs`)
- `tests/CompanionCore.Runtime.Tests/**`, `tests/CompanionCore.Presentation.Tests/**`, `tests/CompanionCore.Capture.Tests/**`
- `scripts/build.ps1`, `scripts/test.ps1`
- `.github/workflows/ci.yml`

## Verification

**Environment note, stated up front:** this session runs in a Linux container with no Windows machine and no dotnet SDK preinstalled. I installed .NET 10 SDK 10.0.302 locally (`dotnet-install.sh --channel 10.0`, matching what's pinned in `global.json`) specifically so I could actually build and run tests rather than push unverified code. Everything below that says "ran" was actually executed and its exact result is quoted. Windows-only behavior is called out explicitly as unverified here.

Commands run, from the repository root, with the local SDK on `PATH`:

```
dotnet build CompanionCore.slnx -p:EnableWindowsTargeting=true --configuration Release
```
→ `Build succeeded. 0 Warning(s). 0 Error(s).` — all 8 projects, including `CompanionCore.App` (the WPF project). `-p:EnableWindowsTargeting=true` is a Linux-only compile flag (it lets the Windows Desktop reference assemblies restore for compilation on a non-Windows OS); it is not needed on the real `windows-latest` CI runner and is not present in any committed project file — I only passed it on the command line for this local check.

```
dotnet test CompanionCore.slnx -p:EnableWindowsTargeting=true --configuration Release
```
→
```
Passed! - Failed: 0, Passed: 24, Skipped: 0, Total: 24 - CompanionCore.Runtime.Tests.dll (net10.0)
Passed! - Failed: 0, Passed: 19, Skipped: 0, Total: 19 - CompanionCore.Presentation.Tests.dll (net10.0)
Passed! - Failed: 0, Passed: 7,  Skipped: 0, Total: 7  - CompanionCore.Capture.Tests.dll (net10.0)
```
50 tests, 50 passed, 0 failed, 0 skipped, both in `Debug` (first pass, while iterating) and `Release` (final pass, quoted above).

**Two real bugs were caught by actually running this locally, not just by review**, and both are fixed in the committed code:
1. `SingleInstanceGuardTests` originally asserted a second same-named `Mutex` object fails to acquire while the first holds it — even from a separate thread, this doesn't hold on Linux in this environment (named-mutex cross-instance exclusion is genuine, well-established Win32 behavior on the real Windows target, but isn't reliable here). Rather than delete the coverage or assert something false, the two affected tests now guard with `if (!OperatingSystem.IsWindows()) return;` and carry a comment explaining why — they run for real on the Windows CI runner and are a documented no-op elsewhere.
2. `CompanionRuntime.LifetimeToken` originally re-read `_lifetimeCts.Token` on every access, which throws `ObjectDisposedException` once `Dispose()` has run — exactly when calling code is most likely to check "was this cancelled?" Fixed by caching the `CancellationToken` struct once at construction; a `CancellationToken` remains safely queryable after its source is disposed.

**Mapped against the task's 8 required tests/evidence items:**

1. **Clean build** — verified as above (with the Linux-only compile flag noted). Not yet verified with a literal fresh `git clone` + build on an actual Windows machine; that's what the CI workflow this revision adds will do on the first push.
2. **No-key/no-network launch** — verified by code review (no network/API/credential code anywhere in `CompanionCore.App`; `FakeCaptureWorker` touches no I/O). Not verified by actually launching the app, since WPF requires a Windows desktop session this environment doesn't have. CI builds it but doesn't launch/interact with the UI either — this specific criterion needs a human or a Windows integration-test harness to fully close.
3. **One runtime across windows** — `CompanionRuntime`'s constructor is `internal`, granted only to `CompanionCore.App` and its own tests via `InternalsVisibleTo`; no other assembly can compile a call to construct one, which is stronger than a runtime check. `ConstructionCount` plus a unit test verify the counting mechanism itself. Not verified by actually opening two windows in a running app (same environment limitation as #2).
4. **Second-process behavior** — `SingleInstanceGuard` is unit-tested for acquire/release across threads; true second-process exclusion is Windows-only, explicitly flagged as CI/manual-verification-required per bug #1 above.
5. **Deterministic lifecycle** — fully verified: 24 Runtime tests cover every valid and invalid transition; 19 Presentation tests cover every §6.2.1 row exactly plus totality (an out-of-range enum value still maps to `lifecycle.unknown` rather than throwing) and determinism (same input twice → identical output).
6. **Clean shutdown** — `CompanionRuntime.Dispose()` idempotency is unit-tested. The App's actual process-exit behavior (no window/process left behind) needs a running app to verify, same environment limitation as #2.
7. **Worker boundary** — `FakeCaptureWorker` is fully unit-tested (7 tests: start/stop/restart status sequencing, frame sequence numbers, cancellation, disposal). Code review confirms no out-of-process worker implementation exists anywhere in the solution.
8. **Diagnostics default** — unit-tested that `CompanionRuntime` only logs to whatever sink it's given (never logs anything when none is supplied, i.e. `NullDiagnosticsSink`); code review confirms `App.xaml.cs` only wires a real (`Console`) sink behind an explicit `--diagnostics` flag or `COMPANIONCORE_DIAGNOSTICS=1` environment variable.

In short: everything that doesn't require an actual Windows desktop session is verified, run, and passing. Everything that does (launching the real app, opening multiple real windows, an actual second OS process, genuine cross-process mutex exclusion) is implemented and unit-tested as far as it can be from here, explicitly flagged above, and is what the new `windows-latest` CI workflow — plus, ideally, a manual run — should close out.

## Remaining

- Items 2, 3 (window-opening half), 4 (Windows-only half), and 6 (process-exit half) above need verification on actual Windows — CI will run the build+test suite there; a human launching the built app once would close the rest.
- No Deferred Findings — nothing encountered that belongs to a later task rather than being addressed here.

## Risks and assumptions

- **"Neutral placeholder icon" interpreted as an in-window UI element** (a plain `Ellipse`), not an application/taskbar `.ico` asset — avoids fabricating binary artwork for a stage that explicitly wants no final art. Flagging in case the foreman wants an actual (still neutral) `.ico` file instead.
- **`SingleInstanceGuard` uses a fixed mutex name** (`Local\CompanionCore.Dev.SingleInstance`) with no dev/prod distinction — acceptable for Task 1 since dev/production data-root separation is explicitly Task 2+ scope (architecture §5.3), but this name will need to move alongside that work rather than staying hardcoded.
- **This environment cannot run WPF or spawn a genuine second OS process**, so the acceptance items above that need either are verified as far as possible (unit tests, code review, a working CI pipeline) but not by actually doing the thing. This is stated plainly rather than glossed over.
- **CI workflow is unverified by an actual run** — it's only ever been written, not executed, since that requires pushing to GitHub Actions. The first real signal on whether it's correctly configured (SDK version resolution, restore, build, test invocation) will be its first run against this branch.

## Review focus

- Whether the local-verification/CI-required split above is an acceptable way to hand off Task 1, or whether the foreman wants me to wait for an actual CI run's result before considering this handoff complete.
- Whether `CompanionRuntime`'s `internal` constructor + `InternalsVisibleTo` is a strong enough mechanism for "no window, worker, or view model may construct another," or whether a stricter mechanism is expected.
- The two bugs caught and fixed during local testing (§Verification) — worth double-checking the fixes are the right shape, not just that tests now pass.

## Repository state

- Branch: `claude/multi-ai-code-collab-o5qhj1` — **not** `claude/task-01-skeleton` as the task specifies. This session's environment pins it to this one branch and does not have permission to push a differently-named branch without explicit user authorization (the same deviation, for the same reason, as Task 0 — which the foreman reviewed and merged against this branch without issue).
- Based on the foreman's authorization commit `209ee5d5b4b08f1d0b5e59f986018a55f4e50c02` (merged into this branch via `git merge origin/main`, pushed as `72ae422`), which itself sits on the accepted Task 0 baseline `58bbdf9915659b3887e311f7b9cdf819cc39fc13`.
- Worktree is clean except for the new files listed under Changed, all included in the commit this handoff accompanies.
- Will open a draft PR against `main` after pushing, noting the branch-name deviation in the PR body.

## Next safe task

Smallest safe next action, not yet started: await foreman review of this Task 1 submission (and, ideally, the CI run's actual result once available). Do not begin Task 2 — persistence (`MemoryStore`, `SessionJournal`, `LocalWriteGate`) is explicitly out of scope until Task 1 is accepted.

## Credit status

Not credit-related. Normal task-boundary stop: Task 1's implementation, local verification, and handoff are complete, and the packet requires stopping for foreman review before Task 2.
