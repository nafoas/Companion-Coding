# Task 9 — Conversation Coordinator and Seed Banks

Status: **accepted — Paw Gate passed 2026-10-10**. Combined Stage 7 behavior is finalized when ERPP-01 also passes.
Authorized: 2026-10-10 under Boss's standing direction to continue autonomously through the pre-API tasks under the Paw Gate model
Accepted remote base: `cf034f60ef15e175d071bf624d960ed8188beb06`
Working branch: `agent/task-09-conversation-coordinator`
Builder: Claude
Accepted gate head: `94554856910fce708b183d19dbbdcd17e83ce108`, tree `0e97a7c8f8e8959331f2b98675804b13f669e33e`
Pull request: #27, squash-merged as `0fe0932aaa54039fafd709e2cb120bda16fd7d6b`
Final CI: push run `38053270968`, PR run `38053274366`, each 546/546

## Objective

Build the neutral `ConversationCoordinator` for Roadmap Stage 7. It is a deterministic state machine that:

- owns exactly one Conversation Thread;
- enforces priority and the Player Conversation Lock;
- keeps the independent Game Observation and initiated-conversation Seed Banks on separate eligibility clocks behind one expression gate;
- never turns ambient expression into a thread without explicit interaction;
- seeds only substantive, unfinished conversations;
- interrupts for urgency, with a checkpoint and a later resumption offer;
- saturates follow-ups;
- treats non-response as strictly neutral.

It emits typed intents only, with no wording.

ERPP-01 (Session Transcript Continuity), authorized by Boss on 2026-08-11, is this task's companion. It gets its own packet and Paw Gate immediately after this one. Their combined behavior is finalized only when both pass.

## Entry evidence and authority

- Task 8 (Stage 6) is accepted. Post-merge `main` `cf034f6` passed 502/502.
- Neutral-core packet Task 9 and the `ConversationCoordinator` boundary define the scope:
  - exactly one thread;
  - priority and lock rules;
  - both Seed Banks with separate clocks;
  - ambient expression that requires explicit promotion;
  - substantive-engagement qualification;
  - settling and resumable seeds;
  - urgent interruption;
  - neutral non-response;
  - presentation-attempt counts without preference punishment.
- The acceptance tests must cover:
  - every priority combination;
  - locked-thread recovery;
  - exclusion of trivial engagement;
  - three neutral presentations;
  - interruption and resumption;
  - no negative learning from silence.
- Roadmap Stage 7 adds:
  - the 30-second initiated-conversation clock;
  - one expression gate, with game context favored;
  - ambient commentary and pep talks;
  - the sensitive-resumption check;
  - follow-up saturation;
  - rare associative detours ("Brain Farts").

  Its Paw Gate scenarios:
  - an ambient pep talk never creates a thread;
  - an initiated conversation cannot interrupt a game conversation;
  - an urgent event checkpoints an unlocked initiated conversation and later offers resumption;
  - a locked conversation survives attention escalation and restart;
  - trivial engagement never clogs a Seed Bank;
  - ignored seeds cause no preference change and leave the active pool after three suitable presentations;
  - Prince stops asking once relevant curiosity is exhausted.
- The Design BunDex "Conversation lifecycle" and "Expression" sections are the behavioral authority.

## Required implementation

- New `src/CompanionCore.Conversation` and `tests/CompanionCore.Conversation.Tests`, named in architecture §9, each with a committed lock file.
  - The project references only `CompanionCore.Attention`, for `AttentionState`.
  - It has no I/O, clock, network, memory, or presentation dependency.
  - All randomness comes from an injected deterministic chance source.
- **One thread.** At most one active thread, game or initiated, with game ranked above initiated. Ambient expression is never a thread.
- **Priority and lock.**
  - A game conversation replaces an unlocked initiated thread, which settles.
  - An initiated conversation never interrupts or replaces a game conversation, and initiated openings are withheld whenever any thread is active.
  - The Player Conversation Lock prevents replacement and automatic settling. Urgent alerts then use the ambient channel without touching the locked thread.
- **Seed Banks.**
  - Separate bounded active pools for game observations and initiated conversations.
  - A game observation enters only when its significance is high enough.
  - A higher-value candidate replaces a stale or duplicate lower-value one, deduplicated by topic.
  - Leaving the pool deletes nothing outside the coordinator.
- **Clocks and gate.**
  - The game-observation clock follows meaningful semantic scans only.
  - The initiated-conversation clock checks every 30 seconds, offset 15 seconds from the scan schedule, and catch-up is bounded.
  - Both pass through one expression gate, with game context favored.
  - Expression chance depends on attention state: rare in Noticing, occasional in Engaged, frequent ambient commentary in High Attention.
  - Afterglow guarantees at least one contextual game opening.
  - Unrelated initiated openings wait through High Attention and Afterglow.
- **Promotion.** Ambient commentary and pep talks become a thread only through an explicit user turn that references them, subject to priority and the lock.
- **Settling and seeding.**
  - A thread settles when it is closed, replaced by a higher priority, or idle past its timeout while unlocked.
  - On settling it becomes a resumable seed only if it was substantively engaged and not explicitly finished. Clicks, yes/no answers, and brief acknowledgements never qualify.
- **Urgent interruption.**
  - An urgent event during an unlocked thread checkpoints and suspends it with a hold intent.
  - Once urgency clears, resumption is offered; accepting restores the same thread, and declining settles it.
  - A seed or thread flagged sensitive is resumed only after a gentle ask.
- **Non-response.**
  - An unanswered opening counts one presentation and changes nothing else.
  - After three neutral presentations the seed leaves the active pool without penalty.
  - The engagement profile only ever accrues positive substantive engagement.
- **Follow-up saturation.** Each thread topic allows a bounded number of Prince-initiated follow-ups. Further requests are refused as saturated, so asking stops once curiosity is exhausted.
- **Brain Farts.** A rare associative detour, allowed only in a relaxed, non-sensitive thread while attention is Noticing, behind a cooldown and the chance gate. It never creates a seed.
- **Checkpoint and restore.** A complete, validated, serializable coordinator checkpoint, so a locked thread, a suspended thread, banks, and clocks survive restart exactly.

## Explicitly forbidden

- Wording, personality, presentation mapping, app or capture wiring, model calls, or seed generation from interests through the API.
- Durable transcript storage, which belongs to ERPP-01, and committed-memory writes.
- Any preference or relationship penalty for silence.

## Acceptance scenarios

1. Every priority combination is covered:
   - game over initiated;
   - initiated blocked during a game thread;
   - user-explicit game replacing an unlocked game thread;
   - a lock blocking every replacement;
   - ambient promotion respecting priority.
2. An ambient pep talk or commentary never creates a thread without explicit interaction.
3. An urgent event checkpoints an unlocked initiated thread, offers resumption once urgency clears, and accepting restores the same thread. Declining settles it into a seed when it was substantive.
4. A locked conversation survives attention escalation, urgent events, idle time, and a checkpoint/restore restart. The urgent alert goes to the ambient channel.
5. Trivial engagement (clicks, yes/no, brief acknowledgements) never produces a seed. Substantive unfinished engagement does.
6. Three neutral presentations retire a seed from the active pool. The engagement profile and every other seed are unchanged.
7. Follow-up requests saturate at their bound.
8. The two clocks are independent, catch-up is bounded, game context is favored at the shared gate, and initiated openings wait through High Attention and Afterglow. Afterglow guarantees one opening.
9. Banks stay bounded, deduplicate by topic, and evict lower-value candidates.
10. Brain Farts occur only under their conditions.
11. A sensitive seed or thread asks first.
12. Identical inputs with an identical chance sequence produce identical outputs. Invalid inputs and configuration are rejected.
13. All 502 accepted tests still pass on Windows CI.

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Conversation/**`
- `tests/CompanionCore.Conversation.Tests/**`
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Gate result: **PASS** on 2026-10-10. The separate review confirmed:

**Acceptance.** Scenarios 1–12 and all seven Roadmap Stage 7 Paw Gate scenarios pass through 44 deterministic tests.

**Actual-diff review.**
- The 14-path change is allowlisted.
- The coordinator references only Attention and has no I/O or wiring.
- Priority and lock checks, expiry, idle, and resume ordering, seeding qualification, neutral non-response, and checkpoint validation were verified.

**Bug hunt.** The immediate re-offer was fixed (J5). 32 of 32 rule mutations are killed after four test strengthenings and one rule correction (J4).

**Local gate.**
- Locked restore and a clean audit on 26 projects.
- Strict build with 0 warnings and 0 errors.
- 533/533 executable tests.

**Windows CI.**
- Implementation head `a0e5efe`: push run `38053076912` and PR run `38053089128`.
- Gate head `9455485`: push run `38053270968` (job `114216541373`) and PR run `38053274366` (job `114216551132`).
- Every run passed 546/546 on the first attempt, with verified artifacts and the accepted attention-sheet digest.

**Merge ref.** Exact parents (`cf034f6`, `9455485`) and a tree equal to the head tree.

## Personal Round Judgments

All are inside the packet and reversible by configuration or a later bounded packet.

### J1 — Two banks, with origin recorded

The design names Game Conversation Seeds and Bun-initiated (BIC) Seeds as the two conversation banks, and also gives game observations their own pool. Game observations, settled game conversations, and Afterglow openings therefore share the game-context bank with an origin field, separate from the initiated bank. Both banks are independent, bounded, and on separate clocks.

### J2 — Provisional calibration

Every number lives in `ConversationConfiguration`:

- 16 seeds per bank;
- game-seed threshold 0.5;
- settled-seed value 0.8; Afterglow-seed value 0.9;
- 2 substantive turns to seed;
- 3 presentations; 3 follow-ups;
- initiated clock every 30 s, offset 15 s;
- offer timeout 60 s; ambient promotion window 2 min, at most 8 kept;
- idle settle after 5 min; resume delay 30 s;
- expression chances:
  - game: 0.1 Noticing, 0.3 Engaged, 0.5 Afterglow;
  - commentary in High Attention: 0.8;
  - initiated: 0.25 Noticing, 0.1 Engaged;
- Brain Fart chance 0.05, with a 10 min cooldown.

### J3 — Deterministic identity and chance

Identifiers derive from the coordinator identity and a counter, and chance comes from an injected source; the default is seeded xorshift64*. Identical inputs therefore reproduce exactly, and a checkpoint resumes the same identifier sequence.

### J4 — A game check claims the shared gate for its instant, spoken or not

The first version claimed the gate only when the game check spoke. A mutation showed that condition was always redundant with other guards. The rule now makes game context genuinely favored: an initiated opening never shares an instant with a game check.

### J5 — An unanswered resume settles quietly and counts as one presentation

The settled seed is never offered straight back in the same instant. Review found the earlier behavior pushy: it re-offered the topic at the very moment it had just been ignored.

### J6 — Afterglow banks and offers its opening directly, bypassing chance

This is the guaranteed opening. It still never interrupts an existing thread or pending offer: it waits in the bank as a high-value seed.

### J7 — An urgent alert is ambient when there is nothing to suspend

With a locked thread or no thread, the alert goes to the ambient channel and the thread is untouched.

### J8 — The user explicitly beats an unlocked thread

A user-started game conversation, or a promotion from ambient, replaces an unlocked thread of either kind. That thread settles and is seeded if substantive. A lock refuses both.

## Bug hunt and mutation evidence

- J5's immediate re-offer was found and fixed during implementation. Two analyzer errors that had briefly hidden a stale-binary test run were found and fixed, and every count since comes from clean builds.
- A mutation pass disabled 32 coordinator rules:
  - priority, lock, promotion, and idle exemptions;
  - urgent sparing of a locked thread;
  - the resume delay and the attention wait;
  - the substantive threshold, finished-never-seeds, and trivial-turn exclusion;
  - the presentation limit and silence neutrality;
  - the follow-up bound;
  - the Brain Fart conditions;
  - ask-first;
  - bounded catch-up;
  - the Afterglow guarantee;
  - ambient-never-a-thread;
  - eviction, deduplication, and the significance threshold;
  - no immediate re-offer;
  - same-thread resume;
  - checkpoint validation.

  27 were killed at once. The other five exposed four weak tests and one redundant rule (J4); all five were fixed and are now killed.

## Deferred findings

1. **ERPP-01 (next packet):** the durable, session-scoped conversation transcript with typed structural events: `ConversationActive`, `BnuyModeInterrupted`, `UrgentObservation`, `ReturnOffered`, and `ConversationResumed`. It must hold stable identifiers and the pre-interruption resume point, and the combined Task 9 + ERPP-01 behavior is finalized only when both pass.
2. **Wiring (later):** attention intents and states into the coordinator; coordinator intents into the presentation adapter; seed generation from local interests through the bridge as proposals; checkpoint persistence through the session journal.
3. **Stage 11 calibration** of every J2 number.
