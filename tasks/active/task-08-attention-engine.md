# Task 8 — Attention Engine

Status: **active — implementation**
Authorized: 2026-10-10 under Boss's standing direction to continue autonomously through the pre-API tasks under the Paw Gate model
Accepted remote base: `8ec0a270ee4297562b766b06ee7e8af211398a0e`
Working branch: `agent/task-08-attention-engine`
Builder: Claude

## Objective

Build the neutral `AttentionEngine` for Roadmap Stage 6. It is a deterministic, configurable state machine that moves through Noticing, Engaged/Investigating, High Attention, and Afterglow from typed interest events.

Scoring incorporates:

- novelty, change, salience, urgency, persistence, corroboration, confidence, personal and lore relevance, habituation, location familiarity, and level-dependent decay;
- immediate bypass for decisive events;
- deduplication of global transitions;
- adaptive Afterglow;
- false-alarm correction that never suppresses urgent danger.

The engine emits typed, abstract intents only. Every number is a configuration value, provisional until prototype calibration.

## Entry evidence and authority

- Task 7 (Stage 5) and R5 are accepted. Post-merge `main` `8ec0a27` passed 453/453.
- Neutral-core packet Task 8 and the `AttentionEngine` boundary define the required behaviors and the attention-event data model.
- Roadmap Stage 6 deliverables and Paw Gate scenarios:
  - weak isolated motion decays without escalation;
  - corroborated enemy/health evidence raises attention;
  - a boss or major event enters High Attention immediately;
  - loading-screen changes deduplicate;
  - familiar harmless activity habituates while familiar urgent danger still alerts;
  - Afterglow decays adaptively, and unrelated initiated conversations stay suppressed during it.
- The Design BunDex "Attention system" section is the behavioral authority:
  - provisional thresholds 0–19 quiet, 20–39 piqued, 40–69 engaged, 70–100 high;
  - higher interest decays more slowly;
  - decisive evidence can act alone, while weak evidence needs corroboration;
  - immediate triggers: bosses, minibosses, unusually large enemy groups, major deaths, credits, rare achievements, watch-task completion, an explicit look request, and exceptional curiosity;
  - learned situational familiarity never suppresses urgent danger;
  - a false-alarm correction ends quickly and adjusts only the local trigger profile;
  - every completed High Attention episode yields at least one Afterglow opening, and unrelated initiated conversations wait.

## Required implementation

### Project boundary

- New `src/CompanionCore.Attention` (namespace `CompanionCore.Attention`) and `tests/CompanionCore.Attention.Tests`, named in architecture §9, each with a committed lock file.
- The engine references only Capture.Contracts, for `NormalizedRegion` and `AttentionRegionKind`. It has no I/O, clock, network, memory, capture, or presentation dependency.
- Time comes only from event and `Advance` timestamps, so identical input streams yield identical outputs.

### Interest events and records

- `InterestEvent` carries:
  - target session ID and timestamp;
  - normalized region;
  - signals: novelty, change, salience, urgency, persistence, confidence, personal relevance, and lore relevance, each a finite value in [0, 1];
  - kind: routine, global transition, or decisive (with a decisive reason);
  - optional topic key and location key;
  - independent evidence-source identity and evidence references;
  - global-transition identity.
- Invalid input is rejected with an argument error, never silently clamped.
- Each processed event returns an attention-event record with the post-event state, plus a disposition:
  - applied;
  - held for corroboration;
  - corroborated;
  - duplicate transition;
  - wrong session.

### Scoring, decay, and hysteresis

- A weighted signal blend is scaled by confidence and maximum contribution. Non-urgent evidence is further scaled by topic habituation and location familiarity. Urgent evidence (urgency at or above its threshold) is never reduced by habituation or familiarity.
- Decay is exponential, with a half-life that grows with the current score, so higher interest decays more slowly. Integration is deterministic and bounded for arbitrarily long gaps.
- Hysteresis:
  - Engaged is entered at 40 and exited below 32;
  - High Attention is entered at 70 and exited below 60, after a minimum dwell.
- An event timestamp earlier than the last processed time is applied at the last processed time. Time never moves backwards.

### Corroboration, decisive bypass, and transition deduplication

- Weak evidence (non-urgent, with low confidence or low contribution) is held per topic and moves nothing alone. It escalates only when at least two independent evidence sources corroborate it within the window, with a corroboration bonus. Held evidence expires.
- Decisive events raise the score straight into High Attention, bypassing corroboration, habituation, and familiarity.
- A global transition such as a loading screen is applied once per transition identity within its window, with a capped contribution. Repeats are duplicates that contribute nothing.

### Habituation, familiarity, and false-alarm correction

- Per-topic habituation decays with each harmless exposure, down to a floor, and recovers over time. The table is bounded, evicting the least recently seen topic.
- Location familiarity (hazardous, unknown, cleared, safe) scales non-urgent evidence, while hazardous ground can only raise urgent evidence. It is set explicitly or learned:
  - a High Attention episode that saw urgent evidence marks its location hazardous;
  - a sustained quiet period in a location marks it cleared.

  The table is bounded.
- `CorrectFalseAlarm` immediately returns to Noticing without Afterglow, emits one all-clear intent, and applies a bounded habituation penalty only to the topics that drove the escalation. Urgent evidence still escalates afterwards.

### Afterglow and intents

- Leaving High Attention enters Afterglow and emits exactly one Afterglow opening.
- Afterglow duration grows with the episode's peak score and time spent in High Attention, and is bounded.
- During High Attention and Afterglow, the snapshot reports that unrelated initiated conversations are suppressed.
- Afterglow ends to Engaged or Noticing by score. Renewed escalation returns to High Attention.
- Typed abstract intents:
  - observing;
  - investigating, with the region, as an abstract request for a synchronized investigation burst;
  - urgent;
  - High Attention started, with focus regions, as an abstract request for high-fidelity focus crops;
  - High Attention ended;
  - Afterglow opening;
  - all clear;
  - returned to Noticing.

  No capture, presentation, or conversation wiring is added.

## Explicitly forbidden

- Wiring the engine to the capture worker, semantic bridge, app, or presentation.
- Conversation threads, Seed Banks, or initiated-conversation scheduling (Task 9+).
- Personality wording, final animation or labels, or tuning against personality.
- Any I/O, persistence, or network.

## Acceptance scenarios

1. Weak isolated motion decays without escalation.
2. Corroborated enemy/health evidence from independent sources raises attention to Engaged. The same evidence from one source, or arriving outside the window, does not.
3. A boss or major event enters High Attention immediately, with urgent and started intents.
4. Loading-screen change bursts deduplicate to one applied transition. A new transition after the window applies again.
5. Familiar harmless activity habituates until it no longer escalates, while familiar urgent danger on the same topic still alerts.
6. Afterglow follows every completed High Attention episode with exactly one opening and suppression of unrelated initiated conversations. Its duration adapts to peak and dwell within its bound. It ends by score and re-escalates on renewed danger.
7. Hysteresis prevents state flapping around thresholds. Higher interest decays more slowly. Long gaps decay in bounded work.
8. A false-alarm correction ends promptly, adjusts only the triggering topics, and never blocks urgent danger.
9. Location familiarity scales non-urgent evidence and never suppresses urgent evidence. Learned hazardous and cleared transitions work.
10. Wrong-session events are ignored. Out-of-order timestamps are applied without moving time backwards. Invalid input and configuration are rejected. Tables stay bounded.
11. Identical synthetic streams produce identical update sequences.
12. All 453 accepted tests still pass on Windows CI.

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Attention/**`
- `tests/CompanionCore.Attention.Tests/**`
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Pending. The gate requires:

- every acceptance scenario;
- a strict build with 0 warnings;
- the full local suite;
- both Windows CI paths with verified artifacts;
- actual-diff review;
- an exact merge-ref check;
- recorded judgments and every provisional threshold.

## Personal Round Judgments

All are inside the packet and reversible by configuration or a later bounded packet.

### J1 — Neutral state names

The engine uses Noticing, Engaged, HighAttention, and Afterglow. "Bnuy Mode" is presentation wording, which the adapter maps during Stage 13.

### J2 — Provisional calibration

Every number lives in `AttentionConfiguration`, and the tests record each one as evidence:

- signal weights: novelty 1.0, change 0.6, salience 1.0, urgency 1.4, persistence 0.6, personal 0.6, lore 0.4;
- maximum contribution: 45;
- Engaged entered at 40 and exited below 32; High Attention entered at 70 and exited below 60, with a 5 s dwell;
- decay half-life of 8 s plus 0.25 s per score point;
- urgent at 0.7;
- weak below 0.5 confidence or 8 contribution;
- corroboration: 10 s window, two independent sources, bonus ×2.0;
- decisive margin 15;
- transition window 30 s, capped at 10;
- habituation ×0.85 per exposure, floor 0.2, recovery half-life 10 min;
- false-alarm penalty ×0.6, with the score capped at 10;
- familiarity factors: hazardous 1.3, cleared 0.7, safe 0.5; cleared after 5 quiet minutes;
- Afterglow: 30 s, plus 0.5 s per peak point, plus 0.25 × High Attention seconds, at most 3 min.

The corroboration bonus was raised from an initial 1.5 to 2.0. Independent confirmation is strong evidence, and at 1.5 two corroborated moderate sightings could not reach Engaged.

### J3 — Every harmless exposure habituates

Habituation counts exposure, not score movement. A version that habituated only applied evidence stalled around 0.5 as soon as habituated evidence became too weak to apply. The test suite caught this.

### J4 — Transitions are evaluated at each decay step

`Advance` integrates decay in up to 1,000 bounded steps and evaluates state after each one, so dwell, exit, Afterglow expiry, and location learning happen when the score actually crosses. The first version evaluated only at the end of a long gap, which mistimed transitions. The test suite caught this too.

### J5 — Weak isolated evidence contributes nothing until corroborated

Held evidence adds zero, so isolated weak motion cannot creep upwards through accumulation. This matches "weaker evidence requires corroboration".

### J6 — Urgent evidence emits an urgent intent every time it is applied

It is never reduced by habituation, safe or cleared familiarity, or false-alarm penalties; hazardous ground can only raise it. A single urgent sighting may not reach Engaged by itself, but it always raises the score at full strength and is always signaled.

### J7 — Explicit familiarity is sticky

A familiarity set explicitly, for example from a semantic "safe town" reading, is never overwritten by learning. Learning sets hazardous only after a High Attention episode that saw urgent evidence, and sets cleared only after sustained quiet in the current location.

### J8 — False-alarm correction never produces Afterglow

It skips Afterglow and penalizes only the topics that drove the current escalation. All corroboration evidence still held is discarded.

### J9 — Bounded test loops

Every state-driven test loop has an iteration guard, so a regression fails promptly instead of hanging CI. A mutation of stepwise evaluation hung the unguarded suite before the guards were added.

## Bug hunt and mutation evidence

- Implementation review found and fixed J3 and J4 before publication.
- A mutation pass disabled each of 23 key guards. All 23 are killed by the suite:
  - urgent bypass of habituation and familiarity;
  - the weak hold, independent sources, and the corroboration window;
  - transition deduplication and its cap;
  - decisive bypass;
  - Engaged hysteresis and High Attention dwell;
  - the Afterglow opening and its bound;
  - level-dependent decay;
  - false-alarm scope and its no-Afterglow rule;
  - sticky explicit familiarity and cleared learning;
  - the wrong-session check and monotonic time;
  - the score ceiling and the table bound;
  - signal validation;
  - stepwise evaluation.

  Two of them, Engaged hysteresis and stepwise evaluation, were caught only after the suite gained a hysteresis-band test and bounded loops.

## Deferred findings

1. **Engine wiring (Task 9+ or app composition):** attention sheets and semantic interpretations become `InterestEvent`s. `Investigating` and `HighAttentionStarted` intents drive synchronized capture bursts and high-fidelity focus crops in the worker. Afterglow and suppression feed the conversation coordinator. Intents map through the presentation adapter.
2. **Stage 11 calibration:** every J2 number and the learned-familiarity rules, against real play.
