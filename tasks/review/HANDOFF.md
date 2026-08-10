# Task Handoff

## Task

Task 1 — Reproducible Neutral Skeleton Adoption. Paw Gate passed 2026-08-10; acceptance-record publication and merge remain.

## Completed

- Started from accepted R0 `main` at `74e68218ffa7ef7680701c03971916278535c16c`.
- Imported exactly 60 allowed product/build/test files from preserved checkpoint `da4797e1a3df2c6f0ddaaa0248098fd40f656121`; no stale ledger, task history, governing document, or prior handoff was imported.
- Verified the initial 60-file import byte-for-byte against the preserved checkpoint.
- Reviewed the runtime, lifecycle, mutex guard, neutral presentation, synthetic capture worker, WPF composition root, `--test-mode` harness, tests, scripts, workflow, projects, and lock files.
- Corrected disposal-before-start so it ends in `Stopped`, cancels lifetime, and rejects all later transitions instead of permitting a false restart on a cancelled lifetime.
- Made same-guard mutex reacquisition idempotent so recursive Windows mutex ownership cannot leak past the guard's single release.
- Added regression coverage for both lifecycle corrections and a public-runtime post-dispose assertion.
- Corrected two comments that still described Stage 13 personality installation as Companion Awakening; it now correctly occurs on resettable Builder Prince.
- Updated GitHub's official actions to their Node 24 generations after the first fresh run reported Node 20 deprecation warnings: `checkout@v5`, `setup-dotnet@v5`, and `upload-artifact@v6`.
- Resolved Deferred Finding D1 after the Boss explicitly approved Prince's recommendation: corrected only Roadmap Stage 1 so persistence begins at Stage 2, conversation remains later, and personality remains Stage 13.
- Performed a separate exact-candidate Paw Gate review and recorded Task 1 as accepted.

## Changed

- 60 Task 1 product/build/test files under the active packet's allowed paths were adopted.
- `src/CompanionCore.Runtime/LifecycleStateMachine.cs` — terminal disposal semantics and post-dispose guard.
- `src/CompanionCore.Runtime/SingleInstanceGuard.cs` — idempotent same-guard reacquisition.
- `src/CompanionCore.Presentation/NeutralPersonalityAdapter.cs` and `PresentationContent.cs` — current Builder-phase continuity terminology only.
- `tests/CompanionCore.Runtime.Tests/CompanionRuntimeConstructionTests.cs`, `LifecycleStateMachineTests.cs`, and `SingleInstanceGuardTests.cs` — regression evidence for the corrections.
- `docs/Prince-Construction-Roadmap.md` — Boss-authorized Stage 1 boundary correction only.
- `BUILD_LEDGER.md`, `README.md`, `tasks/archive/task-01-skeleton.md`, and this handoff — final control and gate records.

Eight of the 60 imported files now deliberately differ from the preserved checkpoint: the workflow, four corrected source files, and three corrected test files listed above. The other 52 still byte-match.

## Verification

- Source file-list comparison against preserved checkpoint — passed; all and only the 60 allowed import files are present.
- Initial source blob comparison — passed, 60/60 exact before deliberate corrections.
- Post-review comparison — exactly eight explained differences and no unexplained mismatch.
- Lock-file JSON structure check — passed for every committed `packages.lock.json`.
- Static forbidden-surface search — passed: production source contains no HTTP/socket/network client, process spawning, real WGC/screen/window capture, native interop, filesystem/data-root access, SQLite, `MemoryStore`, or `SessionJournal` implementation.
- Manual `--test-mode` review — scenarios are limited to ready, multiwindow, shutdown, and bounded second-process hold mechanics; they do not start capture, access persistence, use a network, change identity authority, or bypass the pre-runtime mutex guard.
- Candidate checks before the D1 amendment — passed: diff whitespace, one active task, 64-file allowed-scope list (60 adopted product/build/test files plus four control records), and no out-of-scope path.
- Local build/tests — not run because this Linux environment has neither the pinned .NET SDK nor PowerShell and cannot run WPF equivalently.
- First fresh Windows CI run on head `6a2842b0d9462b3d8e7354d068d18c851e0f96d5` — locked restore passed; Release build passed with 0 compiler warnings and 0 errors; 60/60 tests passed (Runtime 26, Presentation 19, Capture 11, real WPF integration 4). Its action-runtime warnings triggered the Node 24 update and second run below.
- Second fresh Windows CI run `31345693135` on exact head `59d23b829e3b605fcbf0675e4d714da1d35af0bc` — locked restore passed; Release build passed with 0 compiler warnings and 0 errors; 60/60 tests passed (Runtime 26, Presentation 19, Capture 11, real WPF integration 4), with no action-runtime deprecation warnings.
- Final candidate Windows CI run `31354524133`, job `93351605021`, on exact head `6c50b9d73607dcd70a4e0b931f0b8bceaffe59da` — locked restore passed; Release build passed with 0 compiler warnings and 0 errors; 60/60 tests passed (Runtime 26, Presentation 19, synthetic Capture 11, real WPF integration 4).
- Exact-tree review — passed: local and published remote tree are both `92ab14ced552624fb41b07170d35b2365ecf0565`; PR #5 is open, draft, and mergeable at the reviewed head.
- Final current-base scope review — passed: 65/65 changed paths are allowlisted, exactly one task was active during implementation, and diff whitespace is clean.
- Final forbidden-surface review — passed: production source contains no network client/socket, process spawning, real capture/WGC/D3D, native interop, filesystem/production-root access, SQLite, `MemoryStore`, or `SessionJournal` implementation.

## Remaining

- Publish the acceptance-record-only commit and require its exact-head Windows CI.
- Mark PR #5 ready and squash-merge it without changing the accepted tree contents.

## Risks and assumptions

- The shipping app contains an explicit `--test-mode` harness. It is a minor local denial-of-service surface (`hold`) but is inert with respect to private data, capture, persistence, network, identity creation order, and memory authority. Removing or compile-gating it would reduce integration-test fidelity and is not currently recommended.
- `SingleInstanceGuard` is intentionally acquired and disposed on the WPF UI thread because Windows mutex ownership is thread-affine.
- CI is the execution oracle for WPF and Windows named-mutex behavior; no local Linux result is represented as equivalent.
- D1 required and received explicit Boss direction; it was not resolved through autonomous Personal Round Judgment.

## Review focus

- Composition-root ordering: mutex acquisition precedes runtime/subsystem construction.
- One-shot `CompanionRuntime` authority and multi-window sharing.
- Disposal/cancellation terminality and mutex release correctness.
- Neutral §6.2.1 mapping and absence of character/personality behavior.
- Synthetic-only capture boundary and absence of network/production-root code.
- Acceptance-record-only diff, exact final-head CI, and squash merge without product changes.

## Repository state

- Local branch: `agent/task-01-skeleton-local`; draft PR branch: `agent/task-01-skeleton`.
- Base: `74e68218ffa7ef7680701c03971916278535c16c`.
- Accepted candidate: `6c50b9d73607dcd70a4e0b931f0b8bceaffe59da`, exact tree `92ab14ced552624fb41b07170d35b2365ecf0565`.
- Draft PR: #5, open and mergeable at the accepted candidate before this acceptance-record-only commit.

## Next safe task

Publish and merge Task 1 without further product changes. After the merge, create the bounded Task 2 packet before implementing append-only memory or journal behavior.

## Credit status

Not credit-related.
