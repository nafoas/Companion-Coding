# Task Handoff

## Task

CAL-03 — Failure-Mode Hardening and Known-Limitations Report (`tasks/active/cal-03-failure-hardening.md`), Stage 11 part 3.

- Branch: `agent/cal-03-failure-hardening`.
- Base: `main` `07a2d5b`.
- Builder: Claude.

## Changed

- **Orchestration (F1):** unreadable state entries are preserved under `<name>.unreadable`, the conversation lineage is kept when readable, and the event is reported as a contained fault.
- **Tests:**
  - `OrchestrationFailureTests` (7 tests);
  - the harness worker can raise a status.
- **`docs/Known-Limitations.md`.**
- The packet.

## Verification

- **Local:**
  - 0 warnings, 0 vulnerable packages;
  - 946/946 on Linux.
- **Mutation:** 8/8 killed.
- **Windows CI:** pending (expected 963).

## Remaining

- CI, merge, and closure.
- Then the smaller deferred items.
- Stop before Stage 12.

## Personal Round Judgments

CAL-03 J1–J2, recorded in the packet.

## Review focus

- Preservation never deletes and never replaces the first copy.
- A preserved entry is a valid state entry, so Vault backups proceed.

## Next safe task

DEF-01 — The smaller deferred items.
