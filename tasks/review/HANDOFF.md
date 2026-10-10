# Task Handoff

## Task

Task R4 — Retired Workflow Cleanup (`tasks/active/task-r4-retired-workflow-cleanup.md`). This control-only packet is based directly on accepted `main` `a7e68e165ba518c391f8c5e77f2eb3bf95ba3645`, at which Stage 4 is complete. Task 7 remains unopened.

Builder: Claude. Boss directed "clean everything up" on 2026-10-10.

## Completed

- Standing rules (`docs/Direct-Build-Workflow.md`, `docs/Prince-Construction-Roadmap.md`, `docs/Neutral-Core-Task-Packet.md`) now name the role **the Builder**. The Builder has been Claude since 2026-10-10, and Codex held it before that. The workflow states there is one lead writer, and that multi-agent collaboration and foreman monitoring are retired.
- Finished the packet rename Boss approved on 2026-08-10: `docs/Claude-Companion-Core-Task-Packet.md` is now `docs/Neutral-Core-Task-Packet.md`. Every reference was updated, and the "historical filename" caveats in `AGENTS.md` and `README.md` were removed.
- Removed the retired multi-agent records: `docs/Shared-Codebase-Workflow.md`, `tasks/review/FOREMAN_REVIEW.md`, and the superseded `tasks/paused/task-01-skeleton.md`. The accepted Task 0 proposal and the Ledger line that cite them now record that they remain in Git history, last present at `a7e68e1`.

## Changed

Eleven paths, all within the allowlist: 6 standing documents modified, 1 rename, 3 removals, and the packet. No product, test, workflow, project, package, or lock file changed.

## Verification

- `git diff --check` passed, and exactly one packet is active.
- Stale-reference scan: outside annotated history, no document references a removed path or the old packet name, and no standing document names Codex as the current builder. The two remaining mentions are deliberate history statements.
- The product tree is unchanged, so the Windows suite must reproduce 325/325 on both event paths.

## Remaining

- Pass both exact Windows event paths, then merge with an expected-head fence.
- After merge: archive R4 through a docs-only reconciliation, close draft PR #8 as superseded, and retire stale remote branches. Before each deletion, verify the branch head is reachable from a pull-request ref.

## Risks and assumptions

- Removed files remain recoverable from `a7e68e1`. Every SHA recorded in the Ledger stays resolvable through pull-request refs after branch retirement.

## Personal Round Judgments

J1–J3 are recorded in the packet: a role name rather than a model name, deleting rather than moving retired files, and retiring merged branches.

## Review focus

- No accepted evidence is altered; only removed-path citations are annotated.
- The builder-role wording is consistent across the three standing documents.

## Repository state

- Branch `agent/task-r4-retired-workflow-cleanup` against `main`.

## Next safe task

Merge R4, archive it, retire the stale PR and branches, then hold the roundtable with Boss before opening Task 7.
