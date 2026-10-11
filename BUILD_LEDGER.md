# Build Ledger

| Field | Current value |
|---|---|
| Current stage | Stage 11 local hardening complete (CAL-01, CAL-02, CAL-03); final thresholds await Boss's measured report. Smaller deferred items in progress: DEF-01 (long-session consolidation, single-file Vault export) accepted. Live-provider parts are held for the Stage 12 stop |
| Active task | None between packets. Next: DEF-02 (full-resolution photographs); then stop before Stage 12 |
| Working branch | Accepted product/evidence baseline on `main`: `d510add3cc9b07b4bb4279c4138f051abb516e25` (DEF-01) |
| Entry criteria met | Complete; Tasks 4–11, ERPP-01, KEEP-01, KEEP-02, WIRE-01, WIRE-02, AUDIT-01, R2, R3, and R5 each passed their Paw Gates and were reconciled to accepted `main` |
| Product code authorized | No active packet |
| Live API authorized | No. The real provider is a transport-free shell; credentials and live calls remain Task 12 stop conditions |
| Automated tests | DEF-01 head `b0c5af7673aadc81e9c1cc90cfe73ce56634c57f`, tree `f09d22af0e4ffb5ef5ac71afe288be9d623bc963`, passed push run `38102695266` and PR run `38102704355` at 977/977 with verified artifacts. The local gate passed 960/960 on Linux. |
| Manual gate | DEF-01 replay-idempotency, export-safety, and mutation review passed. 24 mutants: 19 killed and 5 equivalent (defense in depth). PR #51 squash-merged through the expected-head fence. |
| Accepted `main` baseline | `d510add3cc9b07b4bb4279c4138f051abb516e25` — DEF-01 long-session consolidation and single-file Vault export (PR #51) |
| Known limitations | Minimized and exclusive-fullscreen WGC remain unsupported absent actual target-PC evidence. The watchdog thresholds, orientation budgets, Task 7 bridge bounds, App startup bound, and every attention, conversation, and transcript number are provisional until Stage 11 calibration. The synthetic capture source cannot emit `SourceResized`. The subsystems are composed by `CompanionOrchestrator` (WIRE-01) and run in the real App with Windows platform signals and neutral notice presentation (WIRE-02). The App has no interactive controls yet for camera, quiet-check, exit-decision, watch-task, or repair commands (WIRE-02 D1). Real lock and suspend need target-PC verification (WIRE-02 D4). Photographs are taken at sheet resolution (WIRE-01 D2); long sessions are consolidated completely in parts (DEF-01). Recall scoring weights and budgets, and Watchbun quiet thresholds, task bounds, and alert rates, are provisional until Stage 11; Consolidation idempotency is provided by the orchestrator's durable per-session intent queue (Task 10 J3, WIRE-01 J7). |
| Deferred temptations | A public backup/repair command (KEEP-02 D1; the single-file export exists since DEF-01) and importing an older export (DEF-01 D1, Boss's decision); personality work; live API and credentials (Task 12); durable images, production settings, final UI |
| Approval | Boss transferred direct construction to Claude with full authority on 2026-10-10, directed reliable orientation delivery with a retake failsafe, directed the R4 cleanup, and directed autonomous progression from Task 7 under the Paw Gate model, halting only for serious issues or real credentials. |

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
- The task was preserved at `tasks/paused/task-01-skeleton.md` until task R4 retired it; it remains in Git history, last present at `a7e68e165ba518c391f8c5e77f2eb3bf95ba3645`.
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
- Merge: PR #10 squash-merged to `main` as `a257d5a1d70d03a77f27582b9f0bbecd0194e67d`; accepted tree `b3a84d9a0a5be6cc81b4f517ac1b1fdb8537a23b` retained the gated behavior and evidence records.
- Result: passed, merged, and accepted; Task 6 became the final Stage 4 packet. Actual target-PC minimized/exclusive WGC feasibility remains unsupported deferred evidence, not a Task 5 defect.

### Task 6 — Regions and attention sheets

- Corrected candidate: local head `018e99b1802673051c116df63e3e6bc4891f62cf`, published equivalent `660d7d9c4ffc4592cf0c725c3686ce68735dab18`, exact shared tree `2876d61439087ecd41b4d967761c3ff447246a44`, based directly on accepted Task 5 `main` `a257d5a1d70d03a77f27582b9f0bbecd0194e67d`; draft PR #11.
- Scope: the observer-neutral evidence descendant has 44 changed paths and 4,544 additions/190 deletions; strict capture contracts/IPC, worker-local readback/change/geometry/composition/encoding/watchdog logic, bounded client reception, Task 4 privacy correlation, neutral manual-region controls, focused tests, synthetic artifact upload, and bounded control records. No package, lock, solution, memory, runtime, semantic/API, conversation, ERPP implementation, personality, or production-data surface changed.
- Behavior: one restart/resize-scoped orientation; normalized deterministic defaults and current-grant manual override; local luminance duplicate rejection; one-moment full context plus at most two labeled crops; deterministic lossless PNG; separate 8 MiB checksummed payload under an unchanged 64 KiB JSON ceiling; at most two sheets per worker/client owner; exact target/generation/source correlation through Privacy Guard; explicit 64 MiB visual accounting and a provisional sustained-growth watchdog.
- Local verification: pinned .NET SDK 10.0.302 locked restore passed (serialized project graph with `EnableWindowsTargeting=true`); all 20 Release projects built with 0 warnings/errors; the direct/transitive audit examined all 20 projects with 0 vulnerable findings; 296/296 locally executable invocations passed (Capture 14, Capture Worker 61, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 64). The capture suite includes a 216,000-frame/six-hour accelerated visual soak.
- Synthetic inspection: generated test-only PNG decoded structurally and was visually inspected at 792×621 RGBA with `FULL CONTEXT`, `CENTER`, and `LOWER / DIALOGUE` labels and synchronized crops; digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- Actual-diff corrections: restored sealed-grant assembly isolation; made payload ownership atomic and zeroing; bounded temporary WGC readback at 64 MiB; fenced resize/fault status ahead of new sheets; deferred start-time frames; coalesced newest sheet signals; held newer sheets until their own frame passed privacy; serialized manual-region response ordering; then rejected the initially green candidate until sheet dispatch followed source-frame dispatch, held payloads were disposed on resize/fault, exact source identity included timestamp/dimensions, and impossible BGRA geometry failed closed.
- Evidence-run correction: the exact docs-head runs exposed legitimate Windows PID reuse and a restart test that expected an older orientation from a newest-preserving queue. PID-plus-start-time identities and separate fresh-orientation/fresh-epoch evidence corrected those assumptions without changing product behavior.
- Corrected Windows evidence: run `33027192357`, job `98371213540`; locked restore, clean audit, all 20 Release builds, and 309/309 tests passed (App Integration 13, Capture 14, Capture Worker 61, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 64). Test-results artifact `9628886868` has digest `sha256:be57fa82e9bf07082417d931c4f7baee12be88156741c756c54c2456d888b388`; attention-sheet artifact `9628887307` has digest `sha256:f23f23afa38591f8378a645c0c829c5b677f240f2f0d7248e6dc14b75f578e43`.
- Final implementation evidence and merge: exact head `1e41ecfb69abbecb0c90c0215616470c864e4525`, tree `77590a151ef87b3725472cde3892ba4b6ec53754`, passed Windows run `33029731700` at 309/309 and merged through PR #11 as `8d14fe945871ce1f92dde987087147befa4a60b2` with an expected-head fence and unchanged tree.
- Acceptance correction: PR #12's exact-head run `33030534763` passed, but same-head push run `33030508085` failed twice because the process-wide VSTest/.NET host count rose 504→513 and 434→443. That heuristic cannot identify capture-client ownership. PR #12 was closed; the replacement directly retains and verifies closure of every client-owned process and named-pipe safe handle across 24 restarts and final stop while preserving fresh-child, bounded-metric, no-second-runtime, and no-live-child assertions.
- Resource correction evidence: local head `56767ccda7d315b966ba8db9434ec3f4a8b1a343`, published PR #13 head `9ce4e592dc812a5740b49a033268386b8d7d8f13`, and exact shared tree `aea3bd99744e07452a9db0412be6754928baa941`. Both event paths passed: push run `33031810624` on successful rerun job `98386519846`, and PR run `33031854365` on job `98385948525`. Each audited all 20 projects, built with 0 warnings/errors, and passed 309/309 tests. The first push attempt had two unrelated legacy App-integration timeouts under runner saturation while the corrected Capture Worker suite passed 61/61; the exact-head rerun passed completely.
- Artifacts: successful push test results `9630630196` (`sha256:c21c09498058bcc5d31ba718b502a5c219ea17cf5e61c06c987bffd344205e2b`) and sheet `9630630604` (`sha256:58bf55eabd208127f35fc5a7d9c3a9d7b99a40d6b3f3123d75f87da70cacf55f`); PR test results `9630555630` (`sha256:f09f7b109e5a73adc5a0e65388febb485655297924d5ce24f1b2689e25b91af1`) and sheet `9630556307` (`sha256:b26190678d4cbf881d00cb9244a1eb826a1c23826147edcda597b44c4d38f723`). Both TRX sets total 309 executed/passed with zero failures, errors, timeouts, or skips; both extracted PNGs retain `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd` and the visually reviewed 792×621 RGBA layout.
- Final evidence descendant: exact head `64c5cfd09bc326cf9ef4ef8c706268c0fa971bbf`, tree `6bbb372c258ae854f7a2feb028f4faeb5a516fc6`, passed push run `33032555656` (job `98388150311`) and PR run `33032557593` (job `98388156701`) on their first attempts. Both produced 309/309 TRX totals with zero failures/errors/timeouts/skips; the direct-owned-resource restart test passed in 12.61 s and 13.56 s respectively.
- Final artifacts: push test results `9630823333` (`sha256:60decf87f8e4c9e4c42dbfa89ad2aef22f1ccb9c252f166d92459fa018dbbb78`) and sheet `9630823761` (`sha256:c6c84952b3d4733cc859379ead67d5bedd45efe686cc26690b2114a105a6c93f`); PR test results `9630820661` (`sha256:3b82d0a32f03bdb2030f5184e4420b751792d15dac77d08a1edb60729733640e`) and sheet `9630821353` (`sha256:4973935183dfcac48010eeaea2a6a15cbaadf3a6dc7583c24352ef801427dc41`). Downloaded hashes matched GitHub. Both PNGs retained the reviewed digest, geometry, format, and synchronized labels.
- Merge: PR #13's test merge ref had exact parents `8d14fe945871ce1f92dde987087147befa4a60b2` and `64c5cfd09bc326cf9ef4ef8c706268c0fa971bbf` and exact tree `6bbb372c258ae854f7a2feb028f4faeb5a516fc6`. It was marked ready and squash-merged with an expected-head fence as `779ed4b0fab9cce8fdf978add388b6282010974a`; fetched remote `main` retained the exact tree.
- Result: passed, merged, archived, and accepted. Stage 4 is complete; no task is active and Task 7 remains unopened.

### DEF-01 — Long-session consolidation and single-file Vault export (smaller deferred items, part 1)

- Builder: Claude. Opened on 2026-10-11 under Boss's direction to do the smaller deferred items before Stage 12.
- **WIRE-01 D1 — long sessions.** A session's consolidation now streams every original through a read-only keyset page and commits in parts that each fit one atomic append. Before this, a session with many highlights could exceed the 128-record bound and fail on every replay.
  - Part 0 keeps the intent's id; later parts derive stable ids.
  - A replay appends nothing.
  - A refused part leaves the intent queued.
- **KEEP-02 D1 — single-file Vault export.** `CompanionHost.ExportVaultAsync` takes a fresh backup, then writes the validated archives, unchanged, with a checksummed manifest into one `.zip` outside the data root. It writes atomically, never overwrites, and verifies the result. `VerifyVaultExportAsync` checks an export.
- **Import of an older export is not done.** It would drop later memories and is refused by repairs (D1, Boss's decision).
- Evidence: head `b0c5af7` (runs `38102695266` and `38102704355`) passed 977/977 with verified artifacts. Mutation pass: 24 mutants, 19 killed and 5 equivalent.
- Merge: PR #51 squash-merged as `d510add3cc9b07b4bb4279c4138f051abb516e25`.
- Result: passed and accepted.

### CAL-03 — Failure-mode hardening and known-limitations report (Stage 11, part 3)

- Builder: Claude. Opened on 2026-10-11 under Boss's Stage 11 direction.
- **F1 fixed.** Unreadable state (damaged, or written by a newer build after a rollback) was silently replaced, and the conversation lineage lost. It is now preserved once under `<name>.unreadable`, the lineage is kept when readable, and the event is reported as a contained fault.
- **Failure suite** (7 tests):
  - a newer-build checkpoint;
  - an empty lineage;
  - a damaged checkpoint, with backups still working;
  - an unparseable sessions list;
  - a disk-full checkpoint write;
  - a crash loop of 5 restarts with no memory lost or duplicated;
  - a capture-worker fault and recovery.
- `docs/Known-Limitations.md`.
- Evidence: head `dcface9` (runs `38099942669` and `38099951518`) passed 963/963 with verified artifacts. Mutation pass: 8/8 killed.
- Merge: PR #49 squash-merged as `039a641fe5269d2b4c08650f775248df3b32c952`.
- Result: passed and accepted. **Stage 11 local work is complete.**

### CAL-02 — Calibration recorder, report, and soak protocol (Stage 11, part 2)

- Builder: Claude. Opened on 2026-10-11 under Boss's Stage 11 direction.
- Delivered:
  - **`CompanionCore.Calibration`:**
    - privacy-safe samples (numbers and state names only);
    - a bounded recorder (8 × 8 MiB, owned files only);
    - an analyzer: per-state baselines and peaks, sustained-growth leak detection after warm-up, and recommended soft 1.5× / restart 2× / Naptime 3× thresholds capped at a quarter of physical memory.
  - **App:** opt-in `--calibration-log` sampling beside the memory root, and `--test-mode=calibrate`.
  - **`tools/CompanionCore.CalibrationReport`.**
  - **`docs/Calibration-Protocol.md`.**
- Deferred: D1, applying the measured thresholds to the main-process watchdog after Boss's report.
- Evidence: head `ce22d99` (runs `38099088822` and `38099091132`) passed 956/956 with verified artifacts.
- Merge: PR #47 squash-merged as `002aec598d9cae5730743e8f787fd27113085f26`.
- Result: passed and accepted.

### CAL-01 — Agreed calibration numbers and cadence enforcement (Stage 11, part 1)

- Builder: Claude. Opened on 2026-10-11 under Boss's direction to start Stage 11 from the numbers agreed in the design conversation.
- **Agreed numbers found and confirmed in force:**
  - the 64 MB ring;
  - at most three full-resolution frames;
  - one active vision request;
  - the 1 h + 1 h Watchbun bedtime;
  - the 30 s BIC clock.
- **Newly enforced:**
  - the agreed 1 s local capture spacing;
  - a semantic cadence governor: Noticing 12 s (agreed 10–15 s) or triggered, Engaged 6 s (agreed 4–8 s), Bnuy Mode with a 3 s event floor.
- **Deferred to measurement:** the final resource thresholds (8–12-hour soak); the Bun Budget number (Stage 12).
- Evidence: head `3c731c4` (runs `38097858502` and `38097870359`) passed 943/943 with verified artifacts. Mutation pass: 12/12 killed.
- Merge: PR #45 squash-merged as `a330a3ca5fcbcfbf67d63f51910b2233e0daf708`.
- Result: passed and accepted.

### AUDIT-01 — Regression audit of every accepted task

- Builder: Claude. Opened on 2026-10-10 under Boss's direction: "Test every task previously and make sure it all works fine, bugfix".
- Scope: tests only, across Memory, TargetAuth, Capture.Worker, Api, and Orchestration; plus control records.
- Method:
  - five stressed full-suite runs;
  - a replay of every stored mutation pass (Tasks 7–11, ERPP-01, KEEP-01, KEEP-02, WIRE-01, WIRE-02);
  - a first 34-mutant invariant pass over Tasks 1–6.
- Result:
  - 420 mutants; 403 killed, 16 equivalent, 1 unobservable;
  - every replayed set matched its accepted record;
  - no product defect.
- Gaps closed:
  - A1: memory structural limits;
  - A2: the sensitive-category and stored-browser-entry denial, and stored-entry executable matching;
  - A3: the capture ring bounds;
  - A4: a credential echo outside the decoded fields;
  - A5: a deterministic single-flight burst.
- Evidence: head `8f02306` (runs `38084310013` and `38084321751`) passed 929/929 with verified artifacts.
- Merge: the merge ref had exact parents and an equal tree. PR #43 squash-merged through the expected-head fence as `c1787e03070588e7a2d46af1dda95f42ccaff332`.
- Result: passed and accepted.

### WIRE-02 — Windows platform signals and App composition

- Builder: Claude. Opened on 2026-10-10 under the same Boss direction, as WIRE-01 D3.
- Scope:
  - the new `CompanionCore.Platform.Windows` and its tests;
  - App composition and the `wired` test mode;
  - the presentation notice mapping;
  - the additive `OrchestratorSnapshot.ConversationLineage`;
  - App integration tests in one real-App collection;
  - solution wiring and control records.
- Delivered:
  - target-only, hook-free Windows platform signals (foreground minimization, last-input tick, strict exit ordering, bounded relaunch discovery, lock/sleep forwarding);
  - one host on the development root, with isolated test roots in test mode;
  - the offline Braincase;
  - a one-second tick;
  - non-blocking notice presentation.
- Personal Round Judgments J1–J10. Deferred findings:
  - D1: interactive controls;
  - D2: undiscoverable relaunches;
  - D3: temporary test roots;
  - D4: real lock and suspend on target hardware.
- Evidence:
  - Candidate `e63242c` (runs `38078377838` and `38078393543`) and evidence head `8a031b4` (runs `38078940123` and `38078943764`) each passed 908/908 with verified artifacts.
  - Mutation pass: 34 mutants, 34 killed.
- Merge: the merge ref had exact parents and an equal tree. PR #41 squash-merged through the expected-head fence as `111e15a2b51ee7691dc1acb7aa9971f7f2840ee6`.
- Result: passed and accepted. **Pre-API wiring is complete.**

### WIRE-01 — Companion orchestration core

- Builder: Claude. Opened on 2026-10-10 under Boss's direction: "wire everything together, and do a bunch of tests".
- Scope: 26 allowlisted paths, comprising:
  - the new `CompanionCore.Orchestration` project and `CompanionCore.Orchestration.Tests` (48 tests);
  - the C1 fix in Recall;
  - an internal Vault backup wrapper;
  - friend lines;
  - solution wiring and control records.
- Delivered:
  - `CompanionOrchestrator`: a single-consumer mailbox composing capture, the bridge, attention, conversation, transcript, memory, Recall, Watchbun, keepsakes, and the Vault;
  - `CompanionHost`: one data root, with Bnuy Repairs online and offline;
  - typed `CompanionNotice` output;
  - the `IPlatformSignals` seam;
  - a strict sheet-PNG photograph source;
  - a durable per-session consolidation intent queue.
- Composition defects C1–C8 were fixed, each with a regression test. C8 (a platform-dependent game reference) was found by Windows CI.
- Personal Round Judgments J1–J10. Deferred findings:
  - D1: per-session consolidation bounds;
  - D2: full-resolution photographs;
  - D3: Windows signals and the App root (WIRE-02).
- Evidence:
  - Fix head `2b1656f` (runs `38070682490` and `38070686736`) and evidence head `a8591b8` (runs `38070920738` and `38070923790`) each passed 793/793 with verified artifacts.
  - Mutation pass: 49 mutants; 45 killed, 3 equivalent, 1 unobservable.
- Merge: the merge ref had exact parents and an equal tree. PR #39 squash-merged through the expected-head fence as `be890e0e08cc55254e89d733e2645bac3ce6bd88`.
- Result: passed and accepted.

### KEEP-02 — Da Bun Vault inclusion and complete recovery (Stage 10, part 2)

- Builder: Claude. Opened on 2026-10-10 under Boss's standing direction as the second Stage 10 packet.
- Scope: 26 allowlisted paths, comprising:
  - minimal internal Memory changes (a companion hook, the companion archive path, read-only keyset paging);
  - the new `CompanionCore.Vault` project and `CompanionCore.Vault.Tests`;
  - Memory paging tests;
  - friend lines;
  - solution wiring and control records.
- Delivered:
  - a companion archive of photographs and checksummed state (settings and the active checkpoint) beside the unchanged memory archive, built and validated before any promotion, with companion-first promotion;
  - complete restoration: companion validation, the accepted memory repair, then photograph and state restoration against the restored records, with damaged copies preserved and an honest report;
  - keyset paging, which resolves KEEP-01 D1.
- Personal Round Judgments J1–J7. Deferred finding D1: single-file export and a public command at wiring.
- Evidence:
  - The first candidate's single test-only Windows failure was corrected in `25d5785`.
  - Fix head `25d5785` (runs `38062566372` and `38062568915`) and gate head `04e0649` (runs `38062794018` and `38062797018`) each passed 744/744 with verified artifacts.
- Merge: the merge ref had exact parents and an equal tree. PR #37 squash-merged through the expected-head fence as `e59b46b809f19325b425614e7e62d78b2a9c2b80`.
- Result: passed and accepted. **Stage 10 is complete.**

### KEEP-01 — Keepsake photographs (Stage 10, part 1)

- Builder: Claude. Opened on 2026-10-10 under Boss's standing direction as the first of two Stage 10 packets. The split mirrors Boss's separate gating of backup/repair authority.
- Scope: 20 allowlisted paths, comprising:
  - the new `CompanionCore.Keepsakes` project and `CompanionCore.Keepsakes.Tests`;
  - test-only friend lines in Capture.Contracts and Privacy;
  - solution wiring and control records.
- Delivered:
  - the only durable-image path, with a visible camera action paired with every write;
  - writes admitted only for a target-session, generation, and identity match, inside the action window, while privacy is current and the guard admits the frame;
  - rarity bounds;
  - box-downscaled compressed PNG;
  - an atomic file write, then a generation-bound record with a neutral caption and scope;
  - idempotent retry;
  - verified inspection;
  - authority-gated, record-first deletion that keeps the original record;
  - orphan and disk-growth reporting with no cleanup surface.
- Personal Round Judgments J1–J8. Deferred finding D1: listing paging.
- Evidence:
  - The first candidate's single test-only NTFS failure was corrected in `25e6178`.
  - Fix head `25e6178` (runs `38060543707` and `38060546479`) and gate head `7febbf6` (runs `38060811488` and `38060814801`) each passed 710/710 with verified artifacts.
- Merge: the merge ref had exact parents and an equal tree. PR #35 squash-merged through the expected-head fence as `05c5bbb7a6f9a0517a7b06a2abeb2becfcb45f08`.
- Result: passed and accepted. Stage 10 continues with KEEP-02.

### Task 11 — Application-bound Watchbun continuity

- Builder: Claude. Opened on 2026-10-10 under Boss's standing direction.
- Scope: 17 allowlisted paths, comprising:
  - the new `CompanionCore.Watchbun` project and `CompanionCore.Watchbun.Tests`;
  - one test-only friend line in Capture.Contracts;
  - solution wiring and control records.
- Delivered:
  - a deterministic, checkpointed engine bound to one authorized target;
  - target-only, non-focus-stealing, rate-bounded alerts;
  - foreground-gated input attribution;
  - Watchbun Tasks;
  - the two-stage quiet check with paused semantic spending;
  - indefinite watch;
  - close/crash classification with Boss decisions;
  - relaunch re-authorization by grant only;
  - Paused Adventures;
  - lock/sleep suspension with frozen clocks;
  - restart recovery without capture;
  - a strict synthetic structured-event adapter.
- Personal Round Judgments J1–J9.
- Evidence: implementation head `5b96720` (runs `38058141112` and `38058154820`) and gate head `6c6b7c7` (runs `38058347513` and `38058350655`) each passed 687/687 with verified artifacts.
- Merge: the merge ref had exact parents and an equal tree. PR #33 squash-merged through the expected-head fence as `97ea0e411c3d9da29d7841d6a810a9fb4773c6e9`.
- Result: passed and accepted. Stage 9 is complete.

### Task 10 — Memory consolidation and retrieval mechanics

- Builder: Claude. Opened on 2026-10-10 under Boss's standing direction.
- Scope: 21 allowlisted paths, comprising:
  - a read-only, bounded, filter-required `MemoryQuery` sharing the checksum-verified read path;
  - the new `CompanionCore.Recall` project and `CompanionCore.Recall.Tests`;
  - Memory query tests;
  - solution wiring and control records.
- Delivered:
  - append-only session consolidation (summaries `Source`-link every original; verbatim highlights; routine compressed into counts);
  - adventure statuses and hypotheses with Boss confirmation;
  - lore provenance with linked corrections;
  - grouped evolving beliefs;
  - user-correction precedence;
  - spoiler-aware bounded local recall;
  - immutable checksummed interest roots with validated seed storage.
- Retention decision: transcripts and every committed record are retained; there is no deletion.
- Personal Round Judgments J1–J9.
- Evidence: implementation head `755f8d3` (runs `38056481599` and `38056494760`) and gate head `033eef6` (runs `38056657903` and `38056660572`) each passed 621/621 with verified artifacts. The PNG kept `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd` at 792×621.
- Merge: the merge ref had exact parents and an equal tree. PR #31 squash-merged through the expected-head fence as `e529eb679a17dcc6059d2f83d33d850dc6a80c2f`.
- Result: passed and accepted. Stage 8 is complete.

### ERPP-01 — Session transcript continuity

- Builder: Claude. Proposed and authorized by Boss on 2026-08-11 as Task 9's companion; opened under Boss's standing direction on 2026-10-10.
- Scope: 17 allowlisted paths, comprising:
  - the new `CompanionCore.Transcript` project and `CompanionCore.Transcript.Tests`;
  - solution wiring;
  - one test-only friend line each in Memory and Privacy;
  - control records.
- Delivered:
  - a durable, session-scoped, append-only, checksummed transcript with typed `ConversationActive`, `BnuyModeInterrupted` (with resume point), `UrgentObservation`, `ReturnOffered`, `ConversationResumed` (referencing its interruption), and `ConversationSettled` events, plus utterances and coordinator checkpoints;
  - stable derived event IDs;
  - a lock-file writer fence with live shared reads;
  - privacy-generation and credential gates;
  - bounds and an explicit session lifecycle;
  - crash recovery;
  - reconstruction of the exact pre-interruption context without committed memory.
- Personal Round Judgments J1–J7 record:
  - its own project;
  - the fence and live-read design;
  - stable IDs;
  - gates on every content event;
  - bounds;
  - visible crash recovery;
  - the pre-interruption snapshot.
- Evidence:
  - The implementation and gate heads each passed both Windows paths at 563/563 with verified artifacts.
  - The merge ref had exact parents and an equal tree.
- Merge: PR #29 squash-merged with the expected-head fence as `7623b0e0f1f33734fed4cdc29ddbcb3b99c5737d`, tree `159c18b12cd930a0195f015423cba125a088f659`.
- Result: passed and accepted. With Task 9, **Stage 7 is final**. Task 10 is next.

### Task 9 — Conversation coordinator and seed banks

- Builder: Claude, under Boss's standing direction (2026-10-10).
- Scope: 14 allowlisted paths, comprising:
  - the new `CompanionCore.Conversation` project and `CompanionCore.Conversation.Tests`;
  - solution wiring;
  - control records.
- Delivered: a deterministic, I/O-free `ConversationCoordinator`, comprising:
  - exactly one thread, with game ranked above initiated, and the Player Conversation Lock;
  - independent, bounded, deduplicating Seed Banks on separate clocks behind one game-favored gate;
  - an Afterglow opening guarantee;
  - ambient expression promoted to a thread only explicitly;
  - substantive-only seeding;
  - urgent hold and resume, with ask-first for sensitive topics;
  - neutral presentation counting and a positive-only engagement profile;
  - follow-up saturation;
  - Brain Fart conditions;
  - a validated checkpoint and restore.
- Personal Round Judgments J1–J8 record:
  - two banks with origin;
  - provisional calibration;
  - deterministic identity and chance;
  - the gate claimed by every game check;
  - quiet settling of an unanswered resume;
  - the direct Afterglow opening;
  - ambient urgent alerts;
  - the user explicitly beating an unlocked thread.
- Evidence:
  - 32 of 32 rule mutations are killed.
  - The implementation and gate heads each passed both Windows paths at 546/546 with verified artifacts.
  - The merge ref had exact parents and an equal tree.
- Merge: PR #27 squash-merged with the expected-head fence as `0fe0932aaa54039fafd709e2cb120bda16fd7d6b`, tree `0e97a7c8f8e8959331f2b98675804b13f669e33e`.
- Result: passed and accepted. Combined Stage 7 behavior is finalized when the ERPP-01 companion gate also passes.

### Task 8 — Attention engine

- Builder: Claude, under Boss's standing direction to continue autonomously through the pre-API tasks (2026-10-10).
- Scope: 13 allowlisted paths, comprising:
  - the new `CompanionCore.Attention` project and `CompanionCore.Attention.Tests`;
  - solution wiring;
  - control records.

  No other source changed.
- Delivered: a deterministic, I/O-free `AttentionEngine`, comprising:
  - the Noticing, Engaged, High Attention, and Afterglow states, with hysteresis and dwell;
  - stepwise, level-dependent decay;
  - corroboration of weak evidence from independent sources;
  - decisive bypass;
  - deduplication of global transitions;
  - habituation with recovery;
  - location familiarity, explicit or learned, that never suppresses urgent danger;
  - adaptive, bounded Afterglow with one opening and suppression of unrelated initiated conversations;
  - false-alarm correction;
  - typed abstract intents and attention-event records;
  - validated configuration and inputs, and bounded tables.
- Personal Round Judgments J1–J9 record:
  - neutral state names;
  - provisional calibration, including the corroboration bonus raised to 2.0;
  - exposure-based habituation;
  - stepwise evaluation;
  - the weak-evidence hold;
  - always-signaled urgent evidence;
  - sticky explicit familiarity;
  - false alarms without Afterglow;
  - bounded test loops.
- Evidence:
  - Two engine defects were found and fixed by the Builder's bug hunt, and 23 of 23 key-guard mutations are killed.
  - The implementation and gate heads each passed both Windows paths at 502/502 with verified artifacts.
  - The merge ref had exact parents and an equal tree.
- Merge: PR #25 squash-merged with the expected-head fence as `3d63147a153d3e957ae97f2bff71ccf08ac0c5f6`, tree `d6b15b28dc673b5529538bf9afc07235fe0cc3cd`.
- Result: passed and accepted. Stage 6's neutral engine is complete. Task 9 is next.

### R5 — App integration cold-start bounds

- Builder: Claude. Routed by Task 7 deferred finding 5, under Boss's direction to fix root causes rather than relax tests.
- Root cause: `Shutdown_StopThenClose…` is always the first App launch in its run, so it absorbed the disk-bound cold start of WPF inside a 30 s bound meant to catch a process that does not exit. It hit the bound three times and reached 27.0 s on an accepted-lineage run before Task 7 existed.
- Change: the integration harness measures two bounded phases:
  - launch until the first marker, under a 90 s cold-start liveness bound;
  - marker until exit, under the unchanged 30 s bound.

  Durations are logged for each launch, and output draining is now bounded. Only the two harness files changed.
- Evidence:
  - Candidate push run `38033166320`: 28.983 s startup plus 2.026 s exit, a pass that would have failed before.
  - Gate head `5054f08`: push run `38033372491` and PR run `38033375072` each passed 453/453 with verified artifacts.
  - The merge ref had exact parents and an equal tree.
- Merge: PR #23 squash-merged with the expected-head fence as `3c078f58a46daf8f2f66e97592c39cf18d120f98`, tree `4d207ec0824eff49c3683074d40aa006a601e747`.
- Result: passed and accepted. Task 8 is next.

### Task 7 — Stateless Braincase bridge

- Builder: Claude, under Boss's standing direction to continue from Task 7 / Stage 5 (2026-10-10).
- Scope: 50 allowlisted paths, comprising:
  - the new `CompanionCore.Api` project (architecture §7) and `CompanionCore.Api.Tests`;
  - solution wiring;
  - one test-only friend line each in Capture.Contracts, Memory, and Privacy;
  - control records.

  No App, CI, presentation, capture, target-authorization, memory-behavior, privacy-behavior, runtime, or documentation source changed.
- Delivered:
  - strict schema-v1 contracts through one parser;
  - mock and replay providers, and a transport-free real-provider shell;
  - graceful configuration;
  - bounded, idempotent `ApiBridge` with grant and privacy-generation fencing through `LocalWriteGate`;
  - a local append-only allowlist;
  - a per-request local Resume Packet;
  - durable naptime with bounded probes;
  - a checksummed bridge journal;
  - RAM-only protected credentials with echo rejection.
- Personal Round Judgments J1–J15 record:
  - the project name;
  - deferred presentation mapping;
  - a zero-length queue;
  - whole-batch rejection;
  - the remote source-kind set;
  - no retry of invalid responses;
  - interrupted operations never re-sent;
  - the RAM-only credential store;
  - provisional bounds;
  - journal placement;
  - corruption handling;
  - no notice replay;
  - the default provider;
  - review-found fixes;
  - the shutdown-timeout diagnosis.
- Evidence:
  - The final head passed both Windows paths at 453/453 with verified artifacts.
  - The merge ref had exact parents and an equal tree.
- Merge: PR #21 squash-merged with the expected-head fence as `9b8d45f0939735a3281b06bec44af47518703a80`, tree `cc5536fc288157123305d59f8326b29d303c5a12`.
- Result: passed and accepted. Stage 5 is complete. R5 is next, then Task 8.

### R4 — Retired workflow cleanup

- Builder: Claude, at Boss's direction ("clean everything up", 2026-10-10). This completes the cleanup approved on 2026-08-10 that draft PR #8 left unfinished.
- Change:
  - standing rules name **the Builder** role;
  - `docs/Claude-Companion-Core-Task-Packet.md` is renamed `docs/Neutral-Core-Task-Packet.md`;
  - `docs/Shared-Codebase-Workflow.md`, `tasks/review/FOREMAN_REVIEW.md`, and `tasks/paused/task-01-skeleton.md` are removed, and their citations now note they remain in Git history at `a7e68e1`.

  No product or test change.
- Evidence: push run `38028570862` and PR run `38028579760` each passed 325/325 with verified artifacts on head `0f46817`.
- Repository hygiene: draft PR #8 was closed as superseded. Every stale remote branch was verified reachable through a pull-request ref before retirement.
- Merge: PR #19 squash-merged with the expected-head fence as `e6664d115b7864b588b8a908d541a222137472af`, tree `af31a27da877f931092ccfe5c52efbae1f8d495b`.
- Result: passed and accepted. No task is active and Task 7 remains unopened.

### R3 — Reliable orientation delivery

- Entry: post-merge `main` run `38026209974` failed `SyntheticWorker_ProducesBoundedOrientationAndManualRegionalSheets`. The same root cause produced five recorded failures since 2026-08-27 (runs `33027979425`, `33027981777`, `33029729188`, `33030041052`, `38026209974`), two of them earlier recorded as transient. A test-only relaxation exposed the same cause in run `38026568119`.
- Cause: the worker produced exactly one orientation per epoch, but it could be lost before the consumer saw it:
  - newest-preserving client retention;
  - newest-only notice coalescing;
  - start deferral that dropped its source frame;
  - a controller slot that let newer regional sheets displace it.

  A privacy-rejected source frame made it unrecoverable.
- Correction (Boss-directed, 2026-10-10):
  - **Client:** the orientation is pinned within the unchanged two-sheet ceiling and handed over first; its notice is sent exactly once, after its exact source frame; its source frame is protected during deferral; a stranded orientation is released.
  - **Controller:** a separate orientation slot.
  - **Failsafe:** a retake request after eight admitted frames, at most three per owed orientation, outside the privacy admission lease, then an honest failure count.
  - **Worker:** a grant-checked `RequestOrientation` command (IPC v3).

  Sixteen allowlisted paths changed: 8 product, 5 modified and 1 new test file, the packet, and the handoff. The R3 handoff on the merged head miscounted them as seventeen; this record corrects it. No sheet or source bound changed.
- Evidence:
  - Red head `8aaac5a`: both paths failed exactly the two intended cases out of 312 executed.
  - Candidate `ff2add8`: both paths 325/325.
  - Final head `975a7ce2a19956041996dc47037d07bca7f1e650`: push run `38027884572` and PR run `38027886917` at 325/325.

  Artifacts were hash-verified, with sheet digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd` unchanged.
- Personal Round Judgments J1–J7 cover: product fix over test relaxation, orientation-first `TakeLatest`, frame budget instead of timer, protocol v3, stranded release, internal shape validation, and protocol literals.
- Merge: PR #17 squash-merged with the expected-head fence as `755b11f2304ed8567011958f2de6e16448ef15ec`; fetched `main` retained tree `81523c004c6b2b791ceb67aa90a67071a4c11712`.
- Result: passed, merged, and accepted. Stage 4 is complete; no task is active and Task 7 remains unopened.

### R2 — Stage 4 recertification and closure

- Builder: Claude, which took over direct construction from Codex at Boss's instruction on 2026-10-10. The Paw Gate protocol is unchanged.
- Entry: Codex's interrupted R2 published only regression evidence `326afd4` on accepted `main` `04af51b`. Its candidate fixes were never published. Push run `33337966344` hung for six hours: on failure, the pressure regression never released its blocked processor, so disposal deadlocked. A local `--blame-hang-timeout` run named that test.
- Defects confirmed in accepted Stage 4 code:
  1. `CaptureFramePipeline` never retired the wake signal of an evicted or cleared pending frame, so signal debt grew with runtime.
  2. `OutOfProcessCaptureWorker` shared one 64-entry drop-oldest lane and one dispatcher across status, frame, and sheet events, so a blocked frame observer could delay or evict the `Stopped`/`Faulted`/resize fence.
- Correction:
  - one signal-retiring removal helper;
  - an independent bounded status lane and dispatcher;
  - a visual fence that rejects frames queued before any visual-invalidating status;
  - prompt-failing regressions;
  - CI `--blame-hang-timeout 10m` and a 60-minute job timeout.

  Frames and sheets keep one serialized dispatcher in source order. Six allowlisted paths changed, with 330 additions and 52 deletions; no contract, project, package, lock, or authority surface changed.
- Red evidence: head `bf0113c`, push run `38025402946`, job `114135078349`, 2 min 9 s. 311 executed, and exactly the three intended regressions failed (0 vs 2, 2 vs 4096, and a 3 s `Stopped` timeout) with no hang.
- Windows evidence:
  - Candidate `82a70df`: push run `38025519542` and PR run `38025588908`.
  - Final evidence descendant `a543301`, tree `5483a2d7ccd577447013c3844a2be22beeb55a44`: push run `38025762113` (job `114136151131`) and PR run `38025763952` (job `114136156632`).

  Every run passed restore, audit, all 20 Release builds with 0 warnings and 0 errors, 311/311 tests, and both artifact uploads on the first attempt.
- Artifacts: final push test results `11659363605` (`sha256:40408fc40f78b504453ce68ffa13b9ea91257ffe667ef940ba4639adfbd7ce45`) and sheet `11659258710` (`sha256:c0187a84c72d07a36b1d2a504f7f28a7022c198e85b59259230e1572047fa774`); final PR test results `11659288564` (`sha256:1b29948a872fc70330982024c59eb0701029d210be9d06277f71938ec4fd1e85`) and sheet `11659423502` (`sha256:fe69331b984c2fa6fed11563c956cb3db3bfade3f5d46464ec1b6abef6106cb5`). Downloaded hashes matched GitHub. Every PNG retained `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd` at 792×621 RGBA and was visually inspected.
- Personal Round Judgments J1–J6 cover the corrective label, one-for-one signals, the independent status lane, the visual fence, CI hang bounds, and builder continuity.
- Merge: the PR #15 merge ref had exact parents `04af51b` and `a543301` and a tree equal to the head tree. It squash-merged with the expected-head fence as `11cc752eae1e9457a12c9d847aacdeb073463b13`, and fetched `main` retained the exact tree.
- Result: passed, merged, and accepted. Stage 4 is recertified and closed; no task is active and Task 7 remains unopened.

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
