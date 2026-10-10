# Companion Coding

A staged, local-first Windows companion-engine project built through evidence-gated vertical slices.

Task 0's architecture, the R0 direct-build controls, Tasks 1–9, the R2 Stage 4 recertification, R3 reliable orientation delivery, and R5 App test timing are accepted. Stage 5's stateless Braincase bridge (mock and replay only), Stage 6's neutral attention engine, and Stage 7's conversation coordinator are in place. No task is active; ERPP-01 (Session Transcript Continuity) is next.

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

Stage 5 is complete. **Task 7** (PR #21) added `CompanionCore.Api`, the stateless faraway Braincase bridge:

- strict versioned request and response contracts;
- deterministic mock and fixture-replay providers;
- a real-provider shell with no transport, so no live or paid call is possible;
- bounded, idempotent retries;
- privacy-generation fencing;
- a local append-only allowlist in front of the write gate;
- a local Resume Packet rebuilt for every request;
- packaged naptime with bounded probes that survives restart;
- a checksummed local bridge journal;
- RAM-only protected credentials.

The final head passed both Windows event gates at 453/453 and merged as `9b8d45f0939735a3281b06bec44af47518703a80`.

**R5** (PR #23) found why the App shutdown integration test was timing out: the first App launch's cold start was being charged against the shutdown exit bound. Startup and exit are now separately bounded phases, and the 30 s exit bound is unchanged. It merged as `3c078f58a46daf8f2f66e97592c39cf18d120f98`.

**Task 8** (PR #25) added `CompanionCore.Attention`, a deterministic attention engine:

- Noticing, Engaged, High Attention, and Afterglow states, with hysteresis;
- corroboration of weak evidence;
- immediate escalation for decisive events;
- deduplication of loading screens and other global transitions;
- habituation that never dampens urgent danger;
- adaptive Afterglow;
- false-alarm correction.

All of it is typed abstract intents with provisional configuration. It passed both Windows gates at 502/502 and merged as `3d63147a153d3e957ae97f2bff71ccf08ac0c5f6`.

**Task 9** (PR #27) added `CompanionCore.Conversation`, a deterministic coordinator:

- exactly one Conversation Thread, with game outranking initiated conversation and a player lock;
- independent Seed Banks on separate clocks behind one game-favored expression gate;
- ambient expression that becomes a thread only through explicit interaction;
- substantive-only seeding;
- urgent hold and resume;
- strictly neutral non-response.

It passed both Windows gates at 546/546 and merged as `0fe0932aaa54039fafd709e2cb120bda16fd7d6b`.

ERPP-01, the durable session transcript that Boss authorized as Task 9's companion, comes next. Consolidation (Task 10), personality, live API use, durable product images, and the remaining later behavior stay deferred.

## Important boundaries

- Neutral utilitarian core first; personality and final presentation later.
- Resettable Builder Prince validates both the core and the complete personality/launch candidate.
- Companion Prince awakens exactly once only after the entire launch-required build passes; no Builder memory transfers, and every later update preserves his continuity.
- Development data and eventual production data must remain physically separate.
- Durable identity and memory are local and append-only to automated/API systems.
- Capture requires authorization and remains restricted to one target.
- Live paid API use and credentials require an explicit stop for the user; earlier work uses mocks and replay fixtures.

See the documents in `docs/` for the complete specification and roadmap.
