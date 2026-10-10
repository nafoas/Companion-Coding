# Companion Coding

A staged, local-first Windows companion-engine project built through evidence-gated vertical slices.

Task 0's architecture, the R0 direct-build controls, Tasks 1–11, ERPP-01, the R2 Stage 4 recertification, R3 reliable orientation delivery, and R5 App test timing are accepted. Stage 5's stateless Braincase bridge (mock and replay only), Stage 6's neutral attention engine, Stage 7's conversation coordinator with its session transcript, Stage 8's memory consolidation and recall mechanics, and Stage 9's Watchbun continuity are complete. No task is active; Stage 10 keepsakes and complete recovery are next.

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

**ERPP-01** (PR #29) added `CompanionCore.Transcript`, a durable, session-scoped transcript of typed conversation events. From it Prince can reconstruct exactly what was being discussed before an interruption, without committed memory. It passed both Windows gates at 563/563 and merged as `7623b0e0f1f33734fed4cdc29ddbcb3b99c5737d`. With Task 9, Stage 7 is final.

**Task 10** (PR #31) added a bounded read-only `MemoryQuery` and `CompanionCore.Recall`: append-only session consolidation with verbatim highlights, adventure records, lore provenance, evolving beliefs, user-correction precedence, spoiler-aware local recall, and immutable interest roots. It passed both Windows gates at 621/621 and merged as `e529eb679a17dcc6059d2f83d33d850dc6a80c2f`. Stage 8 is complete.

**Task 11** (PR #33) added `CompanionCore.Watchbun`, a checkpointed engine that keeps one authorized target attached across tab-aways, quiet hours, exits, relaunches, and lock/sleep, plus a synthetic structured-event adapter. It passed both Windows gates at 687/687 and merged as `97ea0e411c3d9da29d7841d6a810a9fb4773c6e9`. Stage 9 is complete.

Stage 10 keepsakes and complete recovery come next. Personality, live API use, durable product images, and the remaining later behavior stay deferred.

## Important boundaries

- Neutral utilitarian core first; personality and final presentation later.
- Resettable Builder Prince validates both the core and the complete personality/launch candidate.
- Companion Prince awakens exactly once only after the entire launch-required build passes; no Builder memory transfers, and every later update preserves his continuity.
- Development data and eventual production data must remain physically separate.
- Durable identity and memory are local and append-only to automated/API systems.
- Capture requires authorization and remains restricted to one target.
- Live paid API use and credentials require an explicit stop for the user; earlier work uses mocks and replay fixtures.

See the documents in `docs/` for the complete specification and roadmap.
