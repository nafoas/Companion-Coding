# Companion Coding

A staged, local-first Windows companion-engine project built through evidence-gated vertical slices.

Task 0's architecture, the R0 direct-build controls, Task 1's neutral skeleton, and Task 2's append-only memory/journal spine are accepted. Task 3's neutral atomic-backup and guarded-repair Paw Gate passed on PR #7; merge and acceptance recording remain before any later task.

## Builder workflow

1. Read `AGENTS.md`.
2. Read `docs/Direct-Build-Workflow.md`.
3. Read `docs/Claude-Companion-Core-Task-Packet.md` (historical filename; current neutral-core authority).
4. Work on exactly the task in `tasks/active/`.
5. Use a dedicated `agent/task-XX-name` branch.
6. Run and report tests honestly.
7. Complete `tasks/review/HANDOFF.md` and perform a separate evidence-based Paw Gate review.
8. Advance only after the current gate is recorded as passed.

## Current task

Task 3: atomic backup and repair. Work is restricted to the validated SQLite backup cut, canonical/checksummed archive, atomic promotion, covered-prefix journal rotation, damaged-source preservation, guarded repair, post-cut replay, and synthetic tests. Consent/capture and all later systems remain deferred.

## Important boundaries

- Neutral utilitarian core first; personality and final presentation later.
- Resettable Builder Prince validates both the core and the complete personality/launch candidate.
- Companion Prince awakens exactly once only after the entire launch-required build passes; no Builder memory transfers, and every later update preserves his continuity.
- Development data and eventual production data must remain physically separate.
- Durable identity and memory are local and append-only to automated/API systems.
- Capture requires authorization and remains restricted to one target.
- Live paid API use and credentials require an explicit stop for the user; earlier work uses mocks and replay fixtures.

See the documents in `docs/` for the complete specification and roadmap.
