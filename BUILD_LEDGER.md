# Build Ledger

| Field | Current value |
|---|---|
| Current stage | Stage 2 — append-only BunDex spine and crash safety |
| Active task | Task 2 — Append-Only Memory and Journal |
| Working branch | `agent/task-02-bundex` |
| Entry criteria met | Yes; Task 1 passed and merged, Stage 2 split explicitly approved, bounded Task 2 packet active |
| Product code authorized | Yes, only within Task 2's memory/journal and synthetic-test scope |
| Live API authorized | No |
| Automated tests | Accepted baseline passed 60/60 Windows tests; Task 2 exact-candidate tests pending |
| Manual gate | Task 2 pending implementation, crash/idempotency/root-isolation review, and fresh exact-head CI |
| Accepted `main` baseline | `29d0a24e05b58f8ef053c4ebe0b6cfeea7b1ea99` — Task 1 neutral skeleton accepted and merged |
| Known limitations | No durable memory is accepted yet; local environment has no .NET SDK, so Windows CI is the execution oracle |
| Deferred temptations | Task 3 Vault/Repairs/maintenance authority and all later capture/API/conversation/personality work |
| Approval | Task 2 active under the Boss-approved Stage 2 split and autonomous pre-API Paw Gates |

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
