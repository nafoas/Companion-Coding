# Companion Coding

A staged, local-first Windows companion-engine project built through evidence-gated vertical slices.

Task 0's architecture, the R0 direct-build controls, Tasks 1–6, the R2 Stage 4 recertification, and R3 reliable orientation delivery are accepted. Stage 4's local peepers and bounded visual pipeline are complete. No task is active, and Task 7 remains unopened.

## Builder workflow

1. Read `AGENTS.md`.
2. Read `docs/Direct-Build-Workflow.md`.
3. Read `docs/Neutral-Core-Task-Packet.md` (neutral-core authority).
4. Work on exactly the task in `tasks/active/`.
5. Use a dedicated `agent/task-XX-name` branch.
6. Run and report tests honestly.
7. Complete `tasks/review/HANDOFF.md` and perform a separate evidence-based Paw Gate review.
8. Advance only after the current gate is recorded as passed.

## Current checkpoint

Stage 4 is closed through two corrective gates.

**R2** (PR #15):
- fixed wake-signal debt;
- gave worker status its own delivery lane, with a visual fence;
- bounded CI hangs.

**R3** (PR #17) made the one orientation sheet per visual epoch reliably reach the consumer:
- it is pinned within the two-sheet ceiling;
- its notice is never coalesced away;
- its source frame is protected during start;
- it has its own controller slot.

A bounded failsafe asks the worker to retake it, at most three times, only when delivery still fails. Red evidence failed exactly the intended cases, and the final head passed both Windows event gates at 325/325. It merged as `755b11f2304ed8567011958f2de6e16448ef15ec`.

Semantic interpretation, API calls, attention meaning, conversation, ERPP implementation, personality, durable product images, and all Task 7+ behavior remain deferred.

## Important boundaries

- Neutral utilitarian core first; personality and final presentation later.
- Resettable Builder Prince validates both the core and the complete personality/launch candidate.
- Companion Prince awakens exactly once only after the entire launch-required build passes; no Builder memory transfers, and every later update preserves his continuity.
- Development data and eventual production data must remain physically separate.
- Durable identity and memory are local and append-only to automated/API systems.
- Capture requires authorization and remains restricted to one target.
- Live paid API use and credentials require an explicit stop for the user; earlier work uses mocks and replay fixtures.

See the documents in `docs/` for the complete specification and roadmap.
