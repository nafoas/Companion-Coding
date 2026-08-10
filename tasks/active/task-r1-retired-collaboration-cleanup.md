# Task R1 — Retired Collaboration Cleanup

Status: **active; local documentation/scope review passed, exact-head CI pending**
Authorized: 2026-08-10 by Boss after Task 3 passed and merged
Accepted remote base: `f685dd2023a5844309c5b5fb7d0abd1bf54406b9`
Working branch: `agent/task-r1-retired-collaboration-cleanup`

## Objective

Remove obsolete multi-model collaboration and monitoring clutter without changing any product behavior or losing the neutral requirements, accepted architecture, Paw Gate discipline, or one-time Companion Awakening boundary.

## Required changes

- Rename the authoritative neutral-core packet to `docs/Neutral-Core-Task-Packet.md` and update every operative link.
- Delete the superseded shared workflow, paused duplicate Task 1 packet, and obsolete architecture-review working file; their full provenance remains in Git history.
- Archive accepted Task 3 and record PR #7 plus merge SHA in current controls.
- Remove obsolete collaboration wording from operative instructions while retaining only concise historical facts needed to interpret accepted commits.
- Keep legacy monitor automations disabled. Because the exposed controls provide no delete action, sanitize them to inert tombstones and record that final card removal may require the user's UI.
- Rewrite the current handoff for this bounded cleanup.

## Explicitly forbidden

- Product, test, dependency, lock, CI, architecture-contract, roadmap, Design BunDex, privacy, identity, memory, capture, personality, or production-data changes.
- Task 4 activation or implementation.
- Rewriting accepted history or deleting evidence from Git history.

## Required evidence

1. Exactly one active task packet exists.
2. All operative required-reading links resolve and no current document depends on a deleted file.
3. The diff contains documentation/control changes only and passes `git diff --check`.
4. Neutral-core requirements, Direct-Build Paw Gates, Personal Round Judgment limits, and one-time Companion Awakening rules remain intact.
5. Exact-head CI passes before merge; no product-test count change is expected.

## Paw Gate

This task passes when the repository has one neutral direct-build authority path, obsolete working-tree clutter is gone, Git history remains the historical source, exact diff review is clean, and exact-head CI passes. Stop after merge; do not begin Task 4.

## Personal Round Judgment R1-J1 — Delete working copies, preserve Git provenance

- **Question:** Should superseded workflow files remain in an archive directory?
- **Decision:** No. Delete them from the current tree because they are the clutter Boss asked to remove; accepted Git history already preserves exact provenance.
- **Rationale:** An archive directory would keep dead instructions searchable and visually authoritative while adding no recovery value beyond Git.
- **Reversibility:** Any deleted historical file can be recovered from commit `f685dd2023a5844309c5b5fb7d0abd1bf54406b9` or earlier history.
