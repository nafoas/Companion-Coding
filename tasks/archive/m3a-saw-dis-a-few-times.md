# M3a — "Saw dis a few times": a complete record, novel memories, getting used to things

Status: **accepted**
Authorized: 2026-10-13 by Boss ("Start M3"). This is the first half of step M3 of the approved plan.
Accepted remote base: `bb762c0ddcd063acb45f8b8570a10571fbeadc01`
Working branch: `agent/m3a-saw-dis-a-few-times`
Builder: Claude
Plan items: 1, 7a, 12.

## Why M3 is split (Personal Round Judgment J1)

M3 covers the record and novelty (items 1, 7a, 12), plus fear and trust (the trust parts of items 17 and 18). Fear and trust need the eyes to report threats and fight outcomes, which they do not do today. To keep each Paw Gate meticulous, M3 is done in two steps:
- **M3a** (this step): the record, impressions, the novelty gate, getting used to things, and the danger exemption.
- **M3b** ("Da Boss has got it covered"): threat and outcome reporting, fear that eases per context, and trust in Da Boss.

## What exists today (code checked 2026-10-13)

- **Memory proposals.** The eyes' memory proposals pass the safety allowlist and go straight into the write gate. There is no novelty check, which is why the hand-run produced 36 memories in about 7 minutes under two subject keys.
- **Novelty.** Novelty is a first-time flag per session (`ActiveSession.Topics`): 0.8 the first time, then 0.2 forever, with no fade, recovery, or context.
- **Fast habituation.** It exists only in the reflexes (pixel cells: about 20 s; scene labels: about 60 s).
- **What the eyes report.** They report no *kind* of thing, and no danger.

## Design

1. **The sighting record** (BrainCase: `Braincase\sightings-v1\`) is the complete, factual record of every sighting: every observation and every memory proposal the eyes make.
   - **What each sighting holds:** when, the game, the situation (the current scene, when known), the thing, its kind, whether it looked dangerous, and the eyes' words.
   - **How it is written.** Append-only JSON lines with crash-safe append; a torn tail is dropped.
   - **Privacy.** It is written only under the same privacy admission as memories, so nothing is recorded during a privacy stop.
   - **Backups.** It is backed up in Da Bun Vault, and restored only when the live record is missing (never overwritten).
2. **Impressions: "saw dis a few times."** Repeats of the same thing in the same game are one growing impression: the count, first and last time, and the latest words. They are never one memory each. The membrane gains "Prince saw this again / a few times / lots of times".
3. **The novelty gate** sits between the safety allowlist and the write gate. A memory proposal becomes a memory only if it adds something he does not already remember:
   - the *first* time;
   - a *meaningful change*, where the words differ in substance, not just in numbers.
   - **Repeats and near-duplicates** (the same words, or only the numbers changed, like "counter 5/7" then "counter 6/7") stay in the record and grow the impression.
   - **Committed memories are untouched.** They are never edited or deleted.
4. **Getting used to things (habituation)** replaces the first-time flag with gradual familiarity.
   - **By thing and by kind.** It works for each thing, and more gently for its kind, per game.
   - **Recovery.** Familiarity fades with time, so rare recurrences feel new again.
   - **Re-sensitizing.** A familiar thing in a new situation feels fresh again.
   - **Danger exemption.** Something that looks dangerous never drops below a high floor of interest. Urgency and essential warnings are never touched by habituation.
5. **The eyes say what kind of thing, and whether it looks dangerous.** Each observation gains `kind` (creature, character, item, place, text, interface, event, other) and `danger` (true or false). Both are optional when parsing, so older answers still parse.

## Values (Personal Round Judgment J2, accepted defaults adjusted on evidence)

| Value | Default |
|---|---|
| Same thing, as a repeat | word overlap ≥ 0.6 after numbers are ignored |
| Novelty from a thing seen *n* times (effective) | 0.9 / (1 + 0.6·n) |
| Kind's gentler effect | × (0.5 + 0.5 / (1 + 0.1·n_kind)) |
| Thing familiarity half-life | 3 days |
| Kind familiarity half-life | 7 days |
| New situation | familiarity counts half |
| Danger floor | novelty never below 0.5 |
| Floor otherwise | 0.05 |
| "a few" / "lots" | 3+ / 10+ sightings |

## Acceptance

- **Aggregation.** One hundred sightings of the same thing yield one memory, one growing impression ("lots of times"), and one hundred record entries. A meaningful change yields a new memory; a numbers-only change does not.
- **Desensitization:**
  - interest falls with repeats;
  - it falls by kind as well as by thing;
  - it recovers after a long gap;
  - it re-sensitizes in a new situation;
  - it is independent per game.
- **Danger exemption.** A familiar dangerous thing stays above the floor, and urgency is never lowered.
- **Safety.**
  - The record survives a reopen, a torn tail, and a Vault restore when it is missing; an existing record is never overwritten.
  - Nothing is recorded during a privacy stop.
  - Committed memories are never edited.
- **Compatibility.** The eyes' new fields are in the strict schema, and older answers without them still parse.
- **Every existing suite passes.**

## Deferred findings

- **D1 — Familiarity is rebuilt by replaying the whole record on open.** Old exposure has long faded, so a checkpoint or a replay window can bound start-up time once the record grows large.
- **D2 — Impressions reach Prince only once M4/M9 route them through the membrane.** The "saw this a few times" sense exists and is tested, but nothing hands it to him yet.
- **D3 — The 7a session aggregates ("Defeated several ordinary bandits") are not yet written.** Impressions hold the counts; the Adventure Record wording is M4/M5.
- **D4 — The eyes' new kind and danger fields** need a look on Boss's PC with live calls (M10). Older answers default to "other" and "not dangerous".

## Paw Gate

- **Candidate.** Head `5be865e47e9c76c20ce679c5e33bf83b0a131883`, PR #95, against accepted `main` `bb762c0ddcd063acb45f8b8570a10571fbeadc01`.
- **Local gate (Linux).** Locked restore; Release `/warnaserror`: 0 warnings, 0 errors; no vulnerable packages; 21 runnable suites, 1333 tests passed.
  - Sightings: 27 tests (new).
  - Api: 5 new tests, covering the bridge gate and the eyes' fields.
  - Orchestration: 5 new end-to-end tests:
    - six repeated looks become one memory;
    - a privacy stop after a look records nothing;
    - the situation is recorded;
    - the Vault round trip works;
    - novelty falls.
- **Mutation pass.** 22 mutants, all killed in the end.
  - The first round left three real gaps, each closed by a test: near-duplicates, the orchestrator privacy fence, and situation wiring.
  - Two earlier kills came only from an order-flaky test. That test was fixed and is stable over three runs, and a sharper numbers-only test was added.
  - One unreachable parser check was removed.
- **Windows CI.** Push run `38416238772` and PR run `38416276596`, both green.
- **Merge-ref check.** Parents `bb762c0` + `5be865e`, tree `edb0775d1f654f2d355d74e702ea06c068006c99`, equal to the local merge-tree.
- **Merge.** Squash-merged with an expected-head fence as `816bfeece03724f1dcc1940d4ed0c90696e45c00`.
- **Result.** Passed and accepted.
