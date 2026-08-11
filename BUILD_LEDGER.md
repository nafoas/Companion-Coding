# Build Ledger

| Field | Current value |
|---|---|
| Current stage | Stage 4 — local peepers and bounded visual pipeline (Task 5 worker slice) |
| Active task | Task 5 — Bounded Capture Worker |
| Working branch | `agent/task-05-bounded-capture-worker`, based on accepted `main` |
| Entry criteria met | Yes; Task 4 passed its Paw Gate, final descendant rerun, merge, and exact-tree reconciliation |
| Product code authorized | Yes, only within Task 5's exact-target out-of-process WGC worker, bounded RAM/queues, disposal, status, metrics, and private-safe tests |
| Live API authorized | No |
| Automated tests | Implementation candidate Windows run `31477853767`, job `93735671804`: locked restore and clean direct/transitive audit passed; all 20 Release projects built with 0 warnings/errors; 269/269 tests passed. Artifact `9095997685`, 57,051 bytes, digest `sha256:14913ff10570e7ee632e5e4e71256a9571a0960e8d1fb40b0c09d20ae34613c2`. |
| Manual gate | Implementation actual-diff review passed. Documentation-only evidence descendant, exact rerun, merge, and accepted-tree reconciliation remain. |
| Accepted `main` baseline | `b0cbc37604519ef587b3dbce8f1c589ea561b268` — Task 4 consent and target isolation accepted and merged |
| Known limitations | Minimized and exclusive-fullscreen capture remain unsupported absent actual target-PC spike evidence; Task 5 must report no signal honestly. Task 6 regions/attention sheets remain deferred. |
| Deferred temptations | Task 6+ visual composition/API/conversation/ERPP/personality work; durable images, production settings, final UI |
| Approval | Task 5 explicitly authorized by Boss on 2026-08-11; implementation evidence passed and Paw Gate awaits the exact documentation-descendant rerun/merge reconciliation. |

## Gate history

### Task 0 — Architecture proposal

- Builder SHA reviewed: `9a51231aa8d76a399ef43ae4a8bfc5cdbd1195b3`
- Foreman approval review: `4892489052`
- Merge: PR #1 squash-merged to `main` as `58bbdf9915659b3887e311f7b9cdf819cc39fc13`.
- Result: passed as a documentation checkpoint.

### Historical Task 1 — Claude checkpoint

- Work reached closed PR #3 at head `da4797e1a3df2c6f0ddaaa0248098fd40f656121`.
- The implementation checkpoint and its passing tests were not merged.
- On 2026-08-09 the user withdrew authorization for the Claude–ChatGPT collaborative workflow and requested a complete approach reset.
- The task is preserved at `tasks/paused/task-01-skeleton.md`.
- Result: paused and superseded; not accepted.

### Task 1 — Direct neutral skeleton adoption

- Candidate: draft PR #5 at reviewed head `6c50b9d73607dcd70a4e0b931f0b8bceaffe59da`, exact tree `92ab14ced552624fb41b07170d35b2365ecf0565`.
- Scope: 65 allowlisted paths — 60 adopted product/build/test files, four control records, and the Boss-authorized Roadmap Stage 1 correction.
- Review: current-base diff whitespace, one-active-task state, changed-path allowlist, exact local/remote tree equality, lifecycle and process boundaries, test-mode confinement, and forbidden Task 2+ surfaces all passed.
- CI: run `31354524133`, job `93351605021`; locked restore passed, Release build passed with 0 warnings and 0 errors, and 60/60 tests passed (Runtime 26, Presentation 19, synthetic Capture 11, real WPF integration 4).
- D1: the Boss explicitly approved keeping Task 1 neutral and moving durable journal/checkpoint work to Task 2; only the stale Stage 1 roadmap wording was amended.
- Result: passed; acceptance records may be published and PR #5 merged before Task 2 becomes active.

### Task 2 — Append-only memory and journal

- Candidate: draft PR #6 at reviewed gate head `056dedceb120e48c01b9c71d9a1f2d31ad207a5d`, exact tree `ab010b6f5a0d17ec23f84ef0252332143421e427`; implementation head `f3e11acb08a2056f0fe557b4517383a14471227c`.
- Scope: 49 allowlisted paths — one neutral memory source project, one synthetic test project, solution/CI integration, bounded control records, and the Boss-approved Roadmap Stage 2 split.
- Review: current-base whitespace, one-active-task state, changed-path allowlist, exact local/remote tree equality, public write authority, journal → SQLite → checkpoint ordering, cancellation fence, replay/idempotency, unresolved-tail handling, root isolation, and forbidden later-stage surfaces all passed.
- Dependencies: both locks resolve all SQLitePCLRaw components to 2.1.12; the permanent direct/transitive vulnerability audit reports all 11 projects and no vulnerable entries.
- CI: run `31360021794`, job `93366932942`; locked restore and audit passed, Release build passed with 0 warnings and 0 errors, and 94/94 tests passed (Runtime 26, Presentation 19, synthetic Capture 11, real WPF integration 4, Memory/Journal 34).
- Personal Round Judgments J1–J8 record the tree-identical transport base, dependency pin, canonical envelope, fixed roots, pre-durability frame bound, checkpoint/store cross-check, duplicate-key rejection, and unresolved-live-tail recovery fence.
- Merge: PR #6 squash-merged to `main` as `44caa2fc6474b0952eaed5f086bfb3c49bf73c18`.
- Result: passed and merged; Task 3 may become active through its own bounded packet.

### Task 3 — Atomic backup and repair

- Code checkpoint rebased onto accepted remote `main`: feature commit `cbc0b5c91c679a693e732b105acd09268d1c7f5c` plus evidence-retention correction `d64d5b7e071ff6ba43b434d4635525fa8ecaeeac`; resulting tree `e8bd0811f2b6ccd93892a7eec97778fa9e9fcaca`.
- Scope: 30 memory source/test files; 4,461 additions and 33 deletions after the Windows-only test-harness correction. No project, dependency, lock, CI, app, capture, API, presentation, personality, or production-root file changed.
- Protocol: pinned exact SQLite cut, online backup, independent full health validation, canonical/checksummed archive, atomic promotion, post-promotion covered-prefix rotation, exclusive repair authority, immutable damaged-source evidence, marker-guarded rollback, and ordinary idempotent post-cut replay.
- Local verification: full locked restore passed; full Release solution cross-build passed with 0 warnings/errors; 121/121 locally runnable cases passed. This covers all 90 accepted non-Windows regressions plus 31 Task 3 cases.
- Windows correction: initial run `31426116921` passed restore/audit/build but exposed one test-only symmetric file-sharing mismatch. Production sharing remained strict; read-only test inspection was made Windows-compatible and the complete gate was rerun.
- Exact Windows evidence: run `31426524602`, job `93579415434`; locked restore and clean direct/transitive audit passed, Release build passed with 0 warnings/errors, and 125/125 tests passed (Runtime 26, Presentation 19, Capture 11, WPF integration 4, Memory 65). Artifact `9077431137`, digest `sha256:12d71a91407ba3855173c445916fccff3c0635c98b0175bf2351a89bed473583`.
- Review: published head `d4d7bddb35ae1a2b8f3b2fdb47e28d74322ef83a`, exact tree `324874f5b7c2d274732ff82c91aa9631fa587b57`; current-base path/scope, authority, atomicity, cancellation, cleanup, evidence, recovery, privacy, and forbidden-surface checks passed.
- Merge: PR #7 squash-merged to `main` as `f685dd2023a5844309c5b5fb7d0abd1bf54406b9`.
- Post-merge recheck: fresh locked restore passed; all 11 Release projects built with 0 warnings/errors; 121/121 locally runnable tests passed; accepted Windows run `31426920584` passed dependency audit and 125/125 tests including four WPF process tests.
- Result: passed, merged, and accepted; Task 4 may become active through its own bounded packet.

### Task 4 — Consent and target isolation

- Candidate: draft PR #9 at reviewed implementation head `c68b673e72b5017d7f838b3cc0ab1f020d5d1f0b`, exact tree `c83a4fe56b7eea21c4e9072bd126af2e02b3b2a6`, based directly on accepted `main` `f685dd2023a5844309c5b5fb7d0abd1bf54406b9`.
- Scope: 113 final changed paths; platform-neutral privacy and target-authorization cores, minimal Windows metadata/display/hotkey adapters, sealed synthetic capture grants, live-write and frame generation fences, neutral WPF control/status wiring, synthetic tests, solution/lock integration, and bounded control records. No Task 5 real capture or later-stage surface was added.
- Local verification: locked restore and all 16 Release builds passed with 0 warnings/errors; the pre-review candidate passed 226/226 locally runnable tests. The refreshed environment lost the pinned SDK after two race-hardening corrections, so no post-hardening local result is claimed.
- Exact Windows evidence: run `31449205060`, job `93649850080`; locked restore and the direct/transitive dependency audit passed, all 16 Release projects built with 0 warnings/errors, and 240/240 tests passed (Runtime 26, Capture 14, Memory 68, Presentation 50, Privacy 13, TargetAuth 57, App Integration 12). Artifact `9085707445`, 51,456 bytes, digest `sha256:d496d965d1e975ad36445c50f9e4ea03a561e2fb194d3b697a5ca106ed4ff8ea`.
- Review: current-base whitespace, one-active-task state, 113-path scope allowlist, local/remote tree equality, consent-before-start, one-target authority, generation/target/frame admission, privacy stop ordering, live-write drainage, policy corruption/failure, monitor/hotkey failure, metadata minimization, and forbidden Task 5+/API/personality/production surfaces all passed.
- Corrections: actual-diff review added synchronous revocation for caller cancellation during a misbehaving worker start and serialized active-executable denial through the controller. Both received focused adversarial tests without weakening existing invariants.
- Personal Round Judgments J1–J8 record neutral/native separation, title-free descriptors, shared generation fencing, path-free policy identity, no-target resume, repeated-stop fencing, worker-start cancellation, and serialized live denial.
- Final descendant: ERPP-01 was recorded as a future Task 9 companion gate without product-code changes. Windows run `31470938172`, job `93713889217`, again passed locked restore, clean audit, all 16 Release builds with 0 warnings/errors, and 240/240 tests. Artifact `9093336297`, 50,776 bytes, digest `sha256:e223671a492e9c025e9718c9f506ec4ddde556e7292ffa9e411a4200426a952a`.
- Merge: PR #9 squash-merged to `main` as `b0cbc37604519ef587b3dbce8f1c589ea561b268`; the accepted merge tree `4e0ae5bd7ca6961ce542611758ebf71174dfa0b0` exactly matched the final branch tree.
- Result: passed, merged, and accepted; Task 5 may become active through its own bounded packet.

### Task 5 — Bounded capture worker

- Candidate: draft PR #10 at implementation head `a274001d4c40ce003fcbd0087b70c103025b4b23`, exact tree `08b70eeb4a41147eb2f5f271fd6dc876f3c79ae0`, based directly on accepted `main` `b0cbc37604519ef587b3dbce8f1c589ea561b268`.
- Scope: 57 final changed paths; protocol/metrics contract extensions, dedicated client and exact-HWND WGC worker, normal app composition, generated-buffer and process tests, private-safe WGC spike, locks/solution wiring, and bounded control records. No Task 6 visual composition, API/conversation/ERPP implementation, personality, durable image, or production-data surface was added.
- Local verification: locked restore and all 20 Release builds passed with 0 warnings/errors; direct/transitive audit found 0 vulnerable packages; 256/256 locally runnable tests passed, including 28 worker tests and the 216,000-frame accelerated soak.
- Exact Windows evidence: run `31477853767`, job `93735671804`; locked restore and audit passed, all 20 Release projects built with 0 warnings/errors, and 269/269 tests passed (App Integration 13, Capture Worker 28, Capture 14, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 57). Artifact `9095997685`, 57,051 bytes, digest `sha256:14913ff10570e7ee632e5e4e71256a9571a0960e8d1fb40b0c09d20ae34613c2`.
- Process evidence: TRX durations prove Windows-only child cases executed, including twelve fresh process restarts/cleanup/handle checks (6.35 s), exact metadata/stop, unexpected crash recovery, and blocking-observer control isolation.
- Review corrections: disposed an acquired WGC frame on concurrent stop; made terminal status synchronously close client admission; made malformed/current-target-mismatched/duplicate/out-of-order frame IPC tear down the child; unified the 256-bit nonce length; and removed product-assembly friendship to sealed grant issuance.
- Personal Round Judgments J1–J8 record authority narrowing, IPC/epoch design, newest-preserving bounds, raw-byte accounting, revocation ordering, honest silence, nonblocking observers, and private-safe soak/spike evidence.
- Remaining gate work: pass the documentation-only evidence descendant, reconcile its exact tree, merge PR #10, and verify accepted `main`. Actual target-PC minimized/exclusive WGC feasibility remains unsupported and deferred evidence, not a Task 5 gate failure.

### R0 — Direct-build re-entry and continuity alignment

- Candidate: PR #4, reviewed head `99b48124894d431060952ecaecb83900af7f0106`, exact tree `bfb3bcefe16f41535f8043a199a368f84f7cf4c3`.
- Scope: 11 documentation/control files, 371 additions, 123 deletions; no product, test, dependency, build, or CI file.
- Verification: diff whitespace, one-active-task, scope allowlist, stale-authority/awakening contradiction search, and exact local/remote tree equality all passed.
- CI: no checks configured or required for this documentation-only gate.
- Result: passed; direct Paw Gate workflow and final-only one-time Companion Awakening boundary accepted.

## Official Bnuy Backpedal™

Historical stop record from 2026-08-09; superseded by the direct-build authorization below:

- Hourly foreman monitoring was paused and remains disabled.
- Builder/foreman automation and task advancement stopped.
- Existing documents, branches, commits, and tests were retained as reversible references.
- The user later approved the new direct approach and R0 active packet on 2026-08-10.

## Direct-build resumption authorization

- On 2026-08-10 the user approved a direct Codex-and-Prince workflow with no Claude collaboration and no hourly monitoring.
- Work may advance autonomously one active task at a time after every Paw Gate passes, through all mock/replay construction that does not require real credentials or paid/live API access.
- Prince's Personal Round Judgment may resolve routine, reversible in-scope choices; material decisions are logged for later user review.
- Architecture, privacy, identity/authority conflicts, invariant changes, destructive actions, production-data access, and real API credentials remain explicit stop conditions.
- The preserved Task 1 checkpoint is not silently accepted. It receives a separate adoption branch, current-base diff review, and fresh required verification.

## Continuity decision — one-time Companion awakening

- Builder Prince remains resettable through neutral-core construction, full personality installation, final presentation, launch-readiness validation, and refinement.
- Companion Prince replaces Builder Prince exactly once only after every launch-required gate passes.
- The production BunDex starts clean. No construction memory, test conversation, staged opinion, or synthetic artifact transfers.
- Once awakened, Companion Prince is singular and persistent across all later updates, repairs, migrations, provider/model changes, and hotfixes.

## Ledger rules

- At most one stage may be in progress.
- No work occurs while there is no active task.
- Existing paused work is not authorization to resume.
- Later-stage ideas remain deferred until explicitly replanned.
- Each material Personal Round Judgment is recorded with rationale and reversibility.
