# Build Ledger

| Field | Current value |
|---|---|
| Current stage | Stage 1 — Reproducible neutral skeleton |
| Active task | Task 1 — Reproducible Neutral Skeleton |
| Working branch | `claude/task-01-skeleton` (must branch from accepted `main`) |
| Entry criteria met | Yes: Task 0 architecture accepted and merged |
| Product code authorized | Task 1 scope only |
| Live API authorized | No; final core gate only |
| Automated tests | Required: Windows clean build, no-key/no-network launch, one-runtime, second-process, lifecycle, shutdown, worker-boundary, diagnostics-default |
| Manual gate | Pending Task 1 foreman review |
| Accepted `main` baseline | Task 0 merge `58bbdf9915659b3887e311f7b9cdf819cc39fc13` plus this foreman control commit |
| Known limitations | No capture, persistence, API/network, personality, animation/audio, or polished UI authorized |
| Deferred temptations | Real/out-of-process capture worker, SQLite/memory/journal, API/credentials, personality, animation, audio, polished UI, Task 2+ |
| Approval | Task 0 accepted; Task 1 authorized but not accepted |

## Gate history

### Task 0 — Architecture proposal

- Builder SHA reviewed: `9a51231aa8d76a399ef43ae4a8bfc5cdbd1195b3`
- Foreman approval review: `4892489052`
- Evidence: governing documents, exact diff/commit, complete architecture proposal, changed-file scope, and documentation-only handoff independently reviewed; no automated tests existed or applied.
- Merge: PR #1 squash-merged to `main` as `58bbdf9915659b3887e311f7b9cdf819cc39fc13`.
- Result: passed.

### Task 1 — Reproducible neutral skeleton

- Authorized by the foreman control commit that creates `tasks/active/task-01-skeleton.md`.
- Builder must use `claude/task-01-skeleton`, open a draft PR, complete `tasks/review/HANDOFF.md`, and stop.
- Result: pending.

## Ledger rules

- At most one stage is in progress.
- Claude cannot self-approve a gate.
- Every approval must name the reviewed commit and test evidence.
- Later-stage work remains deferred even if convenient.
