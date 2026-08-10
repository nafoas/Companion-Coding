# Task Handoff

## Task

Task 1 — Reproducible Neutral Skeleton Adoption. Active and not yet gate-approved.

## Completed

- Started from accepted R0 `main` at `74e68218ffa7ef7680701c03971916278535c16c`.
- Imported exactly 60 allowed product/build/test files from preserved checkpoint `da4797e1a3df2c6f0ddaaa0248098fd40f656121`; no stale ledger, task history, governing document, or prior handoff was imported.
- Verified the initial 60-file import byte-for-byte against the preserved checkpoint.
- Reviewed the runtime, lifecycle, mutex guard, neutral presentation, synthetic capture worker, WPF composition root, `--test-mode` harness, tests, scripts, workflow, projects, and lock files.
- Corrected disposal-before-start so it ends in `Stopped`, cancels lifetime, and rejects all later transitions instead of permitting a false restart on a cancelled lifetime.
- Made same-guard mutex reacquisition idempotent so recursive Windows mutex ownership cannot leak past the guard's single release.
- Added regression coverage for both lifecycle corrections and a public-runtime post-dispose assertion.
- Corrected two comments that still described Stage 13 personality installation as Companion Awakening; it now correctly occurs on resettable Builder Prince.

## Changed

- 60 Task 1 product/build/test files under the active packet's allowed paths were adopted.
- `src/CompanionCore.Runtime/LifecycleStateMachine.cs` — terminal disposal semantics and post-dispose guard.
- `src/CompanionCore.Runtime/SingleInstanceGuard.cs` — idempotent same-guard reacquisition.
- `src/CompanionCore.Presentation/NeutralPersonalityAdapter.cs` and `PresentationContent.cs` — current Builder-phase continuity terminology only.
- `tests/CompanionCore.Runtime.Tests/CompanionRuntimeConstructionTests.cs`, `LifecycleStateMachineTests.cs`, and `SingleInstanceGuardTests.cs` — regression evidence for the corrections.
- `BUILD_LEDGER.md`, `README.md`, `tasks/active/task-01-skeleton.md`, and this handoff — current control state and the discovered roadmap conflict.

Seven of the 60 imported files now deliberately differ from the preserved checkpoint; they are exactly the four corrected source files and three corrected test files listed above. The other 53 still byte-match.

## Verification

- Source file-list comparison against preserved checkpoint — passed; all and only the 60 allowed import files are present.
- Initial source blob comparison — passed, 60/60 exact before deliberate corrections.
- Post-review comparison — exactly seven explained differences and no unexplained mismatch.
- Lock-file JSON structure check — passed for every committed `packages.lock.json`.
- Static forbidden-surface search — passed: production source contains no HTTP/socket/network client, process spawning, real WGC/screen/window capture, native interop, filesystem/data-root access, SQLite, `MemoryStore`, or `SessionJournal` implementation.
- Manual `--test-mode` review — scenarios are limited to ready, multiwindow, shutdown, and bounded second-process hold mechanics; they do not start capture, access persistence, use a network, change identity authority, or bypass the pre-runtime mutex guard.
- Staged candidate checks — passed: `git diff --cached --check`, one active task, 64-file allowed-scope list (60 adopted product/build/test files plus four control records), and no out-of-scope path.
- Local build/tests — not run because this Linux environment has neither the pinned .NET SDK nor PowerShell and cannot run WPF equivalently.
- Fresh Windows CI — pending on the exact draft PR candidate; historical 58/58 is not claimed as current evidence. Two added facts make the expected minimum 60 passing tests.

## Remaining

- Stage and review the complete current-base diff.
- Publish the draft PR and require its locked restore, Release build, and full Windows/WPF test run.
- Resolve Deferred Finding D1 before accepting the Paw Gate: Roadmap Stage 1 still requires checkpoint/thread/message behaviors that conflict with the accepted, more specific Task 1/Task 2/Task 9 boundaries.

## Risks and assumptions

- The shipping app contains an explicit `--test-mode` harness. It is a minor local denial-of-service surface (`hold`) but is inert with respect to private data, capture, persistence, network, identity creation order, and memory authority. Removing or compile-gating it would reduce integration-test fidelity and is not currently recommended.
- `SingleInstanceGuard` is intentionally acquired and disposed on the WPF UI thread because Windows mutex ownership is thread-affine.
- CI is the execution oracle for WPF and Windows named-mutex behavior; no local Linux result is represented as equivalent.
- D1 is an actual specification conflict and cannot be resolved through Personal Round Judgment under the accepted R0 stop rules.

## Review focus

- Composition-root ordering: mutex acquisition precedes runtime/subsystem construction.
- One-shot `CompanionRuntime` authority and multi-window sharing.
- Disposal/cancellation terminality and mutex release correctness.
- Neutral §6.2.1 mapping and absence of character/personality behavior.
- Synthetic-only capture boundary and absence of network/production-root code.
- Exact CI result and D1's user-directed resolution before gate acceptance.

## Repository state

- Local branch: `agent/task-01-skeleton-local`; intended remote branch: `agent/task-01-skeleton`.
- Base: `74e68218ffa7ef7680701c03971916278535c16c`.
- Worktree will be committed and published after the staged diff check.

## Next safe task

Publish and verify Task 1 only. Do not begin Task 2 until fresh Windows CI passes, the full diff is accepted, D1 is resolved, and the Task 1 gate is recorded.

## Credit status

Not credit-related.
