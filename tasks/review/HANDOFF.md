# Task Handoff

## Task

Task 8 — Attention Engine (`tasks/active/task-08-attention-engine.md`), Roadmap Stage 6. Branch `agent/task-08-attention-engine`, based on accepted `main` `8ec0a270ee4297562b766b06ee7e8af211398a0e`.

Builder: Claude.

## Completed

- New `CompanionCore.Attention`: a deterministic, I/O-free `AttentionEngine` with Noticing, Engaged, High Attention, and Afterglow states. It provides:
  - hysteresis and a dwell minimum for High Attention;
  - level-dependent decay, integrated in bounded steps with evaluation at each step;
  - a weighted signal blend;
  - corroboration of weak evidence from independent sources within a window;
  - decisive bypass;
  - deduplication of global transitions, with a capped contribution;
  - per-topic habituation with recovery;
  - location familiarity, explicit or learned, which never suppresses urgent evidence;
  - adaptive, bounded Afterglow with exactly one opening and suppression of unrelated initiated conversations;
  - false-alarm correction;
  - typed abstract intents;
  - attention-event records;
  - validated configuration and inputs;
  - bounded tables.
- New `CompanionCore.Attention.Tests`: 49 deterministic synthetic-stream tests covering acceptance scenarios 1–11 and the Stage 6 Paw Gate scenarios.

## Changed

- `CompanionCore.slnx`
- `src/CompanionCore.Attention/**` (5 files, including the lock file)
- `tests/CompanionCore.Attention.Tests/**` (4 files, including the lock file)
- this handoff and the active packet

## Verification

Local, on the pinned SDK 10.0.302 (Linux cross-build):

- Locked restore passed and the audit of all 24 projects is clean.
- The strict Release build had 0 warnings and 0 errors.
- 489/489 executable tests passed:

  | Suite | Tests |
  |---|---|
  | Api | 128 |
  | Attention | 49 |
  | Capture | 14 |
  | Capture Worker | 68 |
  | Memory | 68 |
  | Presentation | 50 |
  | Privacy | 13 |
  | Runtime | 26 |
  | TargetAuth | 73 |

- The bug hunt fixed J3 and J4. The mutation pass killed all 23 key-guard mutations; see the packet.
- Windows CI on implementation head `6101fe1`: push run `38034930893` (job `114163281939`) and PR run `38034940743` (job `114163311101`) each passed 502/502 on the first attempt (Attention 49/49).
  - Both artifact digests verified on each path.
  - The attention-sheet PNG keeps the accepted digest `5eb11c96…b046dd`.

## Remaining

- CI on this evidence descendant, the merge-ref check, merge, closure records, and post-merge `main` CI.

## Risks and assumptions

- All numbers are provisional calibration (J2) until Stage 11.
- The engine is not yet wired to capture, the semantic bridge, conversation, or presentation (deferred finding 1).

## Personal Round Judgments

J1–J9 are recorded in the packet.

## Review focus

- Urgent-evidence exemptions in `Factor`.
- Stepwise `AdvanceTo` and `Evaluate` transitions.
- Corroboration bookkeeping.
- False-alarm scope.

## Repository state

- Branch `agent/task-08-attention-engine`, draft PR #25. Implementation `6101fe1`, then this evidence descendant.

## Next safe task

Complete this Paw Gate. Then open Task 9 (conversation coordinator and seed banks, Stage 7).
