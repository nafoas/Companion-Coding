# Task R4 — Retired Workflow Cleanup

Status: **Accepted. PR #19 squash-merged to `main` as `e6664d115b7864b588b8a908d541a222137472af` after both exact Windows event paths passed 325/325 on the unchanged product tree.**
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

## Candidate evidence

- Head `0f46817cced647f8039e15059f0b3291011d25e0`, tree `af31a27da877f931092ccfe5c52efbae1f8d495b`. Push run `38028570862` (job `114144529480`) and PR run `38028579760` (job `114144554794`) each passed restore, audit, all 20 Release builds with 0 warnings and 0 errors, 325/325 tests, and both artifact uploads. Artifacts were hash-verified, and the sheet digest is unchanged.
- Static gates: allowlisted scope (11 paths), `git diff --check`, one active packet, and a clean stale-reference scan.
- Branch retirement precheck: all 18 non-`main` remote branch heads were reachable from pull-request refs `#2`–`#19`.
- Draft PR #8 was closed as superseded, with a comment.
- Merge: the merge ref had parents `a7e68e1` and `0f46817` and a tree equal to the head tree. It was squash-merged with the expected-head fence as `e6664d115b7864b588b8a908d541a222137472af`.

## Personal Round Judgments

- **J1 — Role name rather than model name.** Standing rules say "the Builder", so a future handoff needs no further rename. Reversal: name the model directly.
- **J2 — Delete rather than move retired files.** Git history preserves them exactly, with the last-present commit recorded where they are cited. Reversal: restore them from `a7e68e1`.
- **J3 — Merged task branches are retired as well as the obsolete ones.** Their heads stay reachable through pull-request refs, and the Ledger records every SHA. Reversal: recreate any branch from its PR ref.
