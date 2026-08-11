# Task Handoff

## Task

Task 5 — Bounded Capture Worker. The exact implementation candidate passed Windows CI; this documentation-only evidence descendant and final Paw Gate reconciliation remain pending.

## Completed

- Added a dedicated out-of-process capture client/worker boundary; constructing the client launches nothing, while an authorized start creates one exact disposable child and same-user bounded pipe.
- Implemented WGC for only the authorized HWND with PID/executable revalidation, no enumeration or fallback capability, honest minimized/no-signal/fault status, resize clearing, and deterministic WGC/D3D/frame/process ownership.
- Implemented version-1 strict IPC with a 64 KiB ceiling, a random pipe and 256-bit nonce, correlated/serialized commands, monotonic control sequences, metadata-only notifications, terminal fail-closed behavior, and a second late-event admission fence.
- Implemented the 64 MiB/three-source-frame hard limits, queue capacity two with oldest-pending eviction, byte-bounded retained ring, exact-once disposal, immutable metrics, and a client event dispatcher that cannot block protocol responses.
- Wired the normal WPF app to `OutOfProcessCaptureWorker`; the in-process fake remains test-only.
- Added 28 worker/protocol/pipeline/engine/isolation/process tests, including a 216,000-frame accelerated six-hour soak, twelve fresh-child restarts, crash recovery, blocking-observer control isolation, and unchanged runtime-construction count.
- Added a private-safe WGC harness that authorizes only its own synthetic pulsing window, verifies visible/occluded metadata, and exposes optional real minimized/exclusive experiments without persisting pixels.
- Recorded Personal Round Judgments J1–J8; Task 6, conversation, ERPP implementation, personality, API, durable images, and production data remain absent.

## Changed

- Capture contract extensions plus new `CompanionCore.Capture.Client` and `CompanionCore.Capture.Worker` projects.
- Normal app composition/build-copy wiring and test-only fake compatibility.
- New worker tests, one app composition regression, and `CompanionCore.Capture.Spike`.
- Solution/project/lock integration and bounded Task 5 control-record updates only; no Task 6+ files or accepted design authorities changed.

## Verification

- Locked restore: passed across all 20 projects with the pinned .NET 10.0.302 SDK and `EnableWindowsTargeting=true`.
- Dependency audit: 20 projects examined; 0 direct/transitive vulnerable-package findings.
- Release build: all 20 projects passed with 0 warnings and 0 errors.
- Locally runnable tests: 256/256 passed — Capture 14, Capture Worker 28, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 57.
- Task 5 soak: 216,000 generated frames (six virtual hours at 10 fps), hard bounds preserved, zero owned frames/bytes after clear/dispose.
- Structural review: worker references only Capture Contracts; no runtime/memory/target-discovery/presentation authority; client/worker cannot issue sealed grants; no foreground/enumeration/monitor/fallback/PrintWindow/BitBlt/Desktop Duplication/title/durable-image capability found.
- `git diff --check`: passed.
- Exact implementation candidate: published head `a274001d4c40ce003fcbd0087b70c103025b4b23`, tree `08b70eeb4a41147eb2f5f271fd6dc876f3c79ae0`.
- Windows CI: run `31477853767`, job `93735671804`, passed restore/audit/build and 269/269 tests — App Integration 13, Capture Worker 28, Capture 14, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 57.
- Windows process proof: TRX confirms twelve fresh restarts (6.35 s), exact metadata/stop, unexpected crash/restart, and blocking-observer metrics/stop cases executed and passed.
- Artifact: `9095997685`, 57,051 bytes, digest `sha256:14913ff10570e7ee632e5e4e71256a9571a0960e8d1fb40b0c09d20ae34613c2`.

## Remaining

- Commit/publish this documentation-only evidence descendant and pass its exact Windows rerun.
- Perform final exact-tree review, mark PR #10 ready, squash-merge, and reconcile accepted `main`.

## Risks and assumptions

- Minimized and exclusive-fullscreen support remain unproven without actual target-PC spike evidence and are deliberately reported unsupported/no-signal by default.
- CI can exercise private-safe synthetic child processes but cannot establish real target-PC WGC behavior. The visible/occluded/minimized/exclusive harness must be run later on the actual machine before support claims change.
- The Linux environment cannot execute the WindowsDesktop app tests; no result is inferred for them before the Windows gate.

## Review focus

- Verify all Windows process tests actually execute (not merely discover), including exact metadata, twelve fresh restarts, parent/child handle non-growth, unexpected crash recovery, blocking observer, and child cleanup.
- Recheck grant-issuance isolation, same-user pipe/nonce length, malformed/current-target-mismatched teardown, acquired-WGC-frame disposal on concurrent stop, and terminal-status late-event closure.
- Recheck the 64 MiB/three-frame global accounting, queue capacity two, production WGC retention ceiling two, no raw pixel IPC/disk path, exact HWND-only interop, and no Task 6+/ERPP/personality/API surface.

## Repository state

- Branch: `agent/task-05-bounded-capture-worker`
- Base: `b0cbc37604519ef587b3dbce8f1c589ea561b268`
- Local implementation commit: `7b41822126470b2919bcc69b37a08f93a2f64e74`.
- Published implementation head/tree: `a274001d4c40ce003fcbd0087b70c103025b4b23` / `08b70eeb4a41147eb2f5f271fd6dc876f3c79ae0`.
- Evidence-descendant commit: pending this update.

## Next safe task

- Gate and merge the documentation-only descendant; do not begin Task 6.

## Credit status

No credit-related stop.
