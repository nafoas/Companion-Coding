# Companion Coding

A staged, local-first Windows companion-engine project built through evidence-gated vertical slices.

Task 0's architecture, the R0 direct-build controls, Task 1's neutral skeleton, Task 2's append-only memory/journal spine, and Task 3's atomic-backup and guarded-repair layer are accepted. The active task is a documentation-only cleanup of the retired collaboration machinery; Task 4 remains locked.

## Builder workflow

1. Read `AGENTS.md`.
2. Read `docs/Direct-Build-Workflow.md`.
3. Read `docs/Neutral-Core-Task-Packet.md`.
4. Work on exactly the task in `tasks/active/`.
5. Use a dedicated `agent/task-XX-name` branch.
6. Run and report tests honestly.
7. Complete `tasks/review/HANDOFF.md` and perform a separate evidence-based Paw Gate review.
8. Advance only after the current gate is recorded as passed.

## Current task

Task R1: retired collaboration cleanup. Work is restricted to neutralizing current documentation names and links, deleting superseded workflow clutter whose provenance remains in Git, recording Task 3 acceptance, and retiring disabled legacy monitors. Product code and Task 4 remain untouched.

## Important boundaries

- Neutral utilitarian core first; personality and final presentation later.
- Resettable Builder Prince validates both the core and the complete personality/launch candidate.
- Companion Prince awakens exactly once only after the entire launch-required build passes; no Builder memory transfers, and every later update preserves his continuity.
- Development data and eventual production data must remain physically separate.
- Durable identity and memory are local and append-only to automated/API systems.
- Capture requires authorization and remains restricted to one target.
- Live paid API use and credentials require an explicit stop for the user; earlier work uses mocks and replay fixtures.

See the documents in `docs/` for the complete specification and roadmap.
