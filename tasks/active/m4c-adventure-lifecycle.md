# M4c — The adventure lifecycle: Active → Freshly Finished → Remembered → Adventure Record

Status: **in progress**
Authorized: 2026-10-13 by Boss ("Finish M4, then let's roundtable").
Accepted remote base: `d96109078a30a85f8a1cad3e56af94d56b01b517`
Working branch: `agent/m4c-adventure-lifecycle`
Builder: Claude
Plan items: 4, 7h (the lifecycle and the BrainCase ladder).

## What exists today (code checked 2026-10-13)

- **Adventure status.** `AdventureStatus` {Active, Paused, Finished} is planned append-only with a Supersedes link (`ConsolidationPlanner.PlanAdventureStatus`).
  - The orchestrator records only **Paused**, when Boss keeps a paused adventure after the game exits.
  - Nothing records **Active**, so returning to a game does not reactivate its adventure.
- **Finishing** ("Did our adventure finish?") is asked in M7 (7k). The planner can already record Finished.
- **Consolidation.** Sessions consolidate into Save-scope summaries. Highlights are kept verbatim at Game scope with their save.
- **Recall ranking** puts summaries and adventure context (+150) above highlights (+100), which is the opposite of the ladder.
- **M4a** gave Prince's own pages the four layers and two clocks, and records a durable session clock (`SessionBegan`) in the BrainCase ledger.

## Design

1. **The stage is derived, never stored** (`AdventureLifecycle`, in the BrainCase). It comes from the adventure's current status record, the days since it, and the play sessions since it.
   - Active or Paused is **Active**.
   - Finished is **Freshly Finished** within the medium window (5 sessions or 3 weeks, whichever is longer).
   - Then **Remembered** within the long window (10 sessions or 60 days).
   - Then **Adventure Record**.
   - The windows are M4a's medium and long layers, and both clocks apply, so time away never moves an adventure along.
2. **What can still come up about an adventure, by stage** (`RecallSelector.SelectAdventure`). The lifecycle decides only what is offered; nothing is ever deleted or edited.

   | Stage | Treasured Episodes (highlights) | Adventure context (status, session summaries) | Session detail |
   |---|---|---|---|
   | Active, Freshly Finished | yes | yes | yes |
   | Remembered | yes | yes | resting |
   | Adventure Record | yes | the adventure's record only; summaries resting | resting |

   Resting records are counted and still in the store. A lookup or a later reactivation brings them back.
3. **The ladder in BrainCase recall.** Treasured Episodes rank above adventure context, which ranks above session detail. "Significant" arrives with learned significance (M5), and routine aggregates live in the sighting record (M3a).
4. **Returning reactivates.** When a session begins for a game whose adventure is not currently Active (new, Paused, or Finished), an Active status is appended, superseding the old one. This brings its detail forward again.
   - It is append-only.
   - A failed write never stops the session.
   - Privacy holds: the write follows the authorization that began the session.
5. **Prince's own pages are untouched.** His BunDex pages already follow the M4a layers. The lifecycle governs only the BrainCase memory system's recall about an adventure.

## Judgments made while building (Personal Round Judgment)

- **J1. The windows reuse M4a's medium and long layers.** Freshly Finished lasts 5 sessions or 3 weeks, and Remembered 10 sessions or 60 days. The session clock is the BrainCase's durable `SessionBegan` count.
- **J2. Treasured Episodes are the preserved highlight records.** A highlighted original is session detail, and it rests with the rest.
- **J3. What is asked about still comes first.** The focus bonus outranks the ladder, so Scenario 2 now expects the asked-about status, then the Treasured Episode, then the summary. Before, the summary came before the highlight, the opposite of the ladder.
- **J4. A session start reactivates only when the adventure is not already Active,** so a second session adds nothing.

## Acceptance

- Each stage follows the windows on both clocks, and time away alone never moves an adventure along.
- Active and Paused are Active; an adventure with no status record has no stage.
- What is offered by stage matches the table, with resting records counted. Treasured Episodes are always offered.
- The ladder: a treasured episode outranks adventure context, which outranks session detail.
- A session beginning appends Active once. Pausing then returning appends Paused, then Active, each superseding the last, and the earlier records remain.
- A failed reactivation write is contained and the session still starts.
- Nothing is ever edited or deleted.
- Every existing suite passes. A test whose expectation changes because of this design (one status record becoming a chain) is updated to assert the new, stricter chain.

## Deferred findings

- **D1.** Finishing an adventure is recorded when Boss confirms "Did our adventure finish?" (M7, 7k). Today only the planner can record Finished.
- **D2.** Nothing asks for an adventure's selection at runtime yet. Talking about past adventures (M6/M7) and his voice (Stage 13) will use `SelectAdventure` with the stage.
- **D3.** Save identity is still the single default save per game until M7 (7k).
- **D4.** "Significant" on the ladder arrives with learned significance (M5).
