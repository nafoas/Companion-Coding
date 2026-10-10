# Task R4 — Retired Workflow Cleanup

Status: **Active**
Accepted base: `a7e68e165ba518c391f8c5e77f2eb3bf95ba3645`, tree `1e33e9e64730f5acc942f3e54e0e63c0798f4b04`
Roadmap slice: control records only; no product behavior; Task 7 remains unopened
Builder: Claude. Boss directed "clean everything up" on 2026-10-10. This completes the cleanup Boss approved on 2026-08-10, which Codex's draft PR #8 left unfinished.

## Objective

Leave one coherent set of standing documents for the current direct-build workflow. Name the builder by role instead of by model, finish the neutral packet rename, retire the obsolete multi-agent collaboration records, and retire stale remote branches and the obsolete draft PR. No accepted evidence is lost.

## Required changes

1. Refer to the builder role as **the Builder** in standing documents. The Builder has been Claude since 2026-10-10, and Codex held the role before that.
2. Rename `docs/Claude-Companion-Core-Task-Packet.md` to `docs/Neutral-Core-Task-Packet.md` and update every reference.
3. Remove `docs/Shared-Codebase-Workflow.md`, `tasks/review/FOREMAN_REVIEW.md`, and `tasks/paused/task-01-skeleton.md`. Annotate the historical records that cite them with the commit at which they were last present.
4. Close draft PR #8 as superseded by this task.
5. After this task merges, delete the remote branches whose work is merged or retired. Before deleting any branch, verify that its head commit stays reachable through a pull-request ref, so every SHA recorded in the Ledger still resolves.

## Invariants

- No product, test, workflow, project, package, or lock change.
- Archived task packets and the Ledger's gate history keep their recorded evidence. Only the paths of removed files are annotated.
- No branch is deleted unless its head is reachable from a `refs/pull/*` ref. `main` and the Builder's session branch are kept.

## Allowed paths

`AGENTS.md`, `README.md`, `BUILD_LEDGER.md`, `docs/Direct-Build-Workflow.md`, `docs/Prince-Construction-Roadmap.md`, `docs/Claude-Companion-Core-Task-Packet.md` → `docs/Neutral-Core-Task-Packet.md`, `docs/Shared-Codebase-Workflow.md` (removed), `docs/architecture/task-00-architecture-proposal.md`, `tasks/review/FOREMAN_REVIEW.md` (removed), `tasks/paused/task-01-skeleton.md` (removed), `tasks/review/HANDOFF.md`, `tasks/active/task-r4-retired-workflow-cleanup.md`, `tasks/archive/task-r4-retired-workflow-cleanup.md`.

## Paw Gate

R4 passes when:

- the diff touches only allowed paths;
- no standing document names Codex as the current builder or references a removed path, except annotated history;
- `git diff --check` passes and exactly one packet is active;
- both exact Windows event paths pass the unchanged suite with both artifacts;
- the merge uses the expected-head fence and post-merge `main` CI is green;
- the branch retirement is verified ref by ref.

## Personal Round Judgments

- **J1 — Role name rather than model name.** Standing rules say "the Builder", so a future handoff needs no further rename. Reversal: name the model directly.
- **J2 — Delete rather than move retired files.** Git history preserves them exactly, with the last-present commit recorded where they are cited. Reversal: restore them from `a7e68e1`.
- **J3 — Merged task branches are retired as well as the obsolete ones.** Their heads stay reachable through pull-request refs, and the Ledger records every SHA. Reversal: recreate any branch from its PR ref.
