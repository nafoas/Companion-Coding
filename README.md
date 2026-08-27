# Companion Coding

A staged, local-first Windows companion-engine project built through evidence-gated vertical slices.

Task 0's architecture, the R0 direct-build controls, and Tasks 1–6 are accepted. Stage 4's local peepers and bounded visual pipeline are complete. No task is active, and Task 7 remains unopened.

## Builder workflow

1. Read `AGENTS.md`.
2. Read `docs/Direct-Build-Workflow.md`.
3. Read `docs/Claude-Companion-Core-Task-Packet.md` (historical filename; current neutral-core authority).
4. Work on exactly the task in `tasks/active/`.
5. Use a dedicated `agent/task-XX-name` branch.
6. Run and report tests honestly.
7. Complete `tasks/review/HANDOFF.md` and perform a separate evidence-based Paw Gate review.
8. Advance only after the current gate is recorded as passed.

## Current checkpoint

Task 6: regions and attention sheets. The bounded worker-side pixel pipeline, exact-frame privacy admission, deterministic labeled PNG sheets, size-bounded RAM-only transport, manual region fence, resource watchdog, direct-owned-handle restart evidence, exact-tree review, and both Windows event gates passed. PR #13 merged the final evidence tree as `779ed4b0fab9cce8fdf978add388b6282010974a`. Semantic interpretation, API calls, attention meaning, conversation, ERPP implementation, personality, durable product images, and all Task 7+ behavior remain deferred.

## Important boundaries

- Neutral utilitarian core first; personality and final presentation later.
- Resettable Builder Prince validates both the core and the complete personality/launch candidate.
- Companion Prince awakens exactly once only after the entire launch-required build passes; no Builder memory transfers, and every later update preserves his continuity.
- Development data and eventual production data must remain physically separate.
- Durable identity and memory are local and append-only to automated/API systems.
- Capture requires authorization and remains restricted to one target.
- Live paid API use and credentials require an explicit stop for the user; earlier work uses mocks and replay fixtures.

See the documents in `docs/` for the complete specification and roadmap.
