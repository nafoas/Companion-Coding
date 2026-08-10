# Build Ledger

| Field | Current value |
|---|---|
| Current stage | Control housekeeping — retired collaboration cleanup |
| Active task | Task R1 — Retired Collaboration Cleanup |
| Working branch | `agent/task-r1-retired-collaboration-cleanup` from accepted Task 3 merge |
| Entry criteria met | Yes; Task 3 passed its distinct Paw Gate and PR #7 squash-merged to accepted `main` |
| Product code authorized | No; documentation, control records, and disabled legacy automation retirement only |
| Live API authorized | No |
| Automated tests | PR #7 reviewed head `d4d7bdd`: Windows CI run `31426524602` passed locked restore, clean direct/transitive audit, Release build with 0 warnings/errors, and 125/125 tests. |
| Manual gate | Task R1's local documentation/scope/link/boundary review passed and both legacy monitors are disabled/inert. Exact-head CI and the final distinct Paw Gate remain. |
| Accepted `main` baseline | `f685dd2023a5844309c5b5fb7d0abd1bf54406b9` — Task 3 atomic backup and guarded repair accepted and merged |
| Known limitations | The exposed automation API has no delete action. Both legacy monitors are disabled and sanitized into inert tombstones; removing their cards from the automation list may require the user's UI. Product limitations remain recorded in accepted task history. |
| Deferred temptations | Task 4+ consent/capture/API/conversation/personality work; production backup/import, UI, encryption, and multi-generation retention |
| Approval | Boss explicitly authorized the bounded cleanup and requested a stop before Task 4 |

## Gate history

### Task 0 — Architecture proposal

- Builder SHA reviewed: `9a51231aa8d76a399ef43ae4a8bfc5cdbd1195b3`
- Independent approval review: `4892489052`
- Merge: PR #1 squash-merged to `main` as `58bbdf9915659b3887e311f7b9cdf819cc39fc13`.
- Result: passed as a documentation checkpoint.

### Superseded collaboration checkpoint

- Work reached closed PR #3 at head `da4797e1a3df2c6f0ddaaa0248098fd40f656121`.
- The implementation checkpoint and its passing tests were not merged.
- On 2026-08-09 the user retired the multi-model collaboration workflow and requested a complete approach reset.
- Detailed working files were removed from the current tree after Task 3; the closed PR and Git history preserve their exact provenance.
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
- Gate-record CI: head `9da9270cc644a7ce2875b230967914b94aae33fb`, run `31426920584`, job `93580692644`; unchanged locked restore, audit, Release build, and all tests passed.
- Merge: PR #7 squash-merged to `main` as `f685dd2023a5844309c5b5fb7d0abd1bf54406b9`; merged tree `6954374fa16d55b71976e7af08575bed9d561bad` exactly matches the reviewed gate-record tree.
- Result: passed and merged; the bounded control-cleanup task may run, while Task 4 remains unauthorized.

### R0 — Direct-build re-entry and continuity alignment

- Candidate: PR #4, reviewed head `99b48124894d431060952ecaecb83900af7f0106`, exact tree `bfb3bcefe16f41535f8043a199a368f84f7cf4c3`.
- Scope: 11 documentation/control files, 371 additions, 123 deletions; no product, test, dependency, build, or CI file.
- Verification: diff whitespace, one-active-task, scope allowlist, stale-authority/awakening contradiction search, and exact local/remote tree equality all passed.
- CI: no checks configured or required for this documentation-only gate.
- Result: passed; direct Paw Gate workflow and final-only one-time Companion Awakening boundary accepted.

## Direct-build authorization

- The 2026-08-09 Official Bnuy Backpedal retired the prior collaboration and monitoring process; exact history remains in Git rather than active-looking files.
- On 2026-08-10 the user approved the direct Codex-and-Prince workflow with no multi-model collaboration or hourly monitoring.
- Work may advance autonomously one active task at a time after every Paw Gate passes, through all mock/replay construction that does not require real credentials or paid/live API access.
- Prince's Personal Round Judgment may resolve routine, reversible in-scope choices; material decisions are logged for later user review.
- Architecture, privacy, identity/authority conflicts, invariant changes, destructive actions, production-data access, and real API credentials remain explicit stop conditions.
- The superseded Task 1 checkpoint was not silently accepted; Task 1 received its separate adoption branch, current-base review, fresh verification, and accepted merge.

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
