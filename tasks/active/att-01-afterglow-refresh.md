# ATT-01 — Afterglow stays while things keep happening

Status: **in progress**
Authorized: 2026-10-12 by Boss: "I think the 30 seconds to at most 3 minutes still works, with the cooldown resetting. The harmony works."
Accepted remote base: `cea13e8385f614a989aff9f6fc29bd00454b7171`
Working branch: `agent/att-01-afterglow-refresh`
Builder: Claude

## Objective

In the design conversation, Boss asked for a cooldown that "should run from the most recent interesting event, not merely from when Engaged mode began. Every new development resets it", so Prince doesn't keep switching between "watching intently" and "never mind" during a long quest, conversation, match, or cutscene.

Task 8 built the hysteresis and an adaptive Afterglow:

- Engaged is entered at 40 and left below 32; High Attention is entered at 70 and left below 60, with a 5 s minimum.
- Afterglow lasts 30 s + 0.5 s per peak point + 0.25 × High Attention seconds, at most 3 minutes.

But the Afterglow timer was set once, when High Attention ended, and only a return to High Attention reset it. A steady stream of smaller developments could not keep him in Afterglow.

## Scope

1. **Afterglow length is unchanged:** 30 s to 3 min, adapted to the episode (Boss).
2. **Each interesting event during Afterglow restarts its countdown** at the same length. An event is interesting when the engine applies or corroborates it, and its own contribution (before any score ceiling) is at least the weak-evidence floor (`WeakContribution`, 8). So, by construction:
   - held weak evidence, habituated repetition (a ticking HUD clock), and duplicate transitions do not restart it;
   - a decisive event still returns him to High Attention, as before.
3. **Nothing else changes:** thresholds, hysteresis, the Afterglow opening (still exactly one per episode), the false-alarm correction, and the conversation suppression rules.

## Acceptance

- Interesting events during Afterglow keep him in it past the original deadline, each one restarting the full Afterglow length from its own time. When the events stop, Afterglow ends one length after the last one.
- Weak held evidence, habituated familiar events, duplicate transitions, and events from another session never extend it.
- Exactly one Afterglow opening per episode, however often it is extended.
- Every existing attention test passes unchanged.
- The whole suite passes.

## Personal Round Judgments

- **J1 — "Interesting" reuses the engine's own weak-evidence floor.** No new tuning number; habituation already makes familiar repetition fall below it.

## Deferred findings

_(none yet)_

## Paw Gate

### Mutation pass

7 mutants against the attention suite; 5 are caught.

- **First run:** 3 caught.
- **One real gap, now closed:** the interest floor (two mutants). A faint scene transition skips the weak-evidence hold, so without the floor it could extend Afterglow. `NoiseDoesNotExtendIt` now covers it.
- **Two survivors are equivalent defenses:**
  - the disposition check (held, duplicate, and other-session evidence already contributes 0);
  - the `Max` guard (the length is fixed, so a restart can never move the deadline earlier).

### Gate

_(local gate and CI evidence below)_
