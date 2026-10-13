# M4a — Remembering like a Bun: four layers, two clocks, recall that refreshes

Status: **in progress**
Authorized: 2026-10-13 by Boss ("yes, M4"; lookup design approved and pinned in the plan).
Accepted remote base: `a43b4ade12401be91f397c4206f91346d53e3e50`
Working branch: `agent/m4a-remembering-like-a-bun`
Builder: Claude
Plan items: 3, 4, 7h (ladder), 7i.

## Why M4 is split (Personal Round Judgment J1)

- **M4a** (this step): the layers, the two clocks, refresh on recall, clear → "rings a bell" → not offered unprompted, sparing recall, and the priority ladder.
- **M4b:** the Bnuy lookup (hunches in his folder names, an organic imperfect search, truthful finding), started by a "rings a bell".
- **M4c:** the adventure lifecycle (Active → Freshly Finished → Remembered → Adventure Record), reconciled with the existing adventure status code.

## What exists today (code checked 2026-10-13)

- **Recall code.** `RecallSelector` (scope and spoiler filtering) exists but nothing calls it at runtime.
- **What comes to mind (M1c).** It is the only thing reaching Prince: his own similar pages, with no idea of layer, age, sessions, refresh, or cooldown.
- **Page tags.** Pages carry `Layer` (unused) and `CreatedAtUtc`.

## Design

1. **A page's layer** is the BrainCase's private reference, never shown to him. It is the page's tagged layer, or by default from its kind:
   - short: short-term, things thinking about, little requests;
   - medium: medium-term;
   - long: long-term, adventures, lore, opinions, relationship, scary or cool;
   - cherished: cherished.
2. **Two clocks: play sessions and calendar time.** Each window lasts *whichever is longer*. Time away from the game never causes forgetting: with no sessions played, nothing fades. Sessions are counted from a new append-only `SessionBegan` ledger entry the orchestrator writes when a session starts.
3. **Clarity**, measured from when the page was last written to or last remembered (Boss's approved values):

   | Layer | Clear for | Then "rings a bell" for | Then |
   |---|---|---|---|
   | Short | the next session or 2 days | the same again | not offered unprompted |
   | Medium | 5 sessions or 3 weeks | the same again | not offered unprompted |
   | Long | 10 sessions or 60 days (J2) | always | still surfaces, as a bell |
   | Cherished | forever | — | — |

4. **Not clairvoyant.** A faded page never comes to mind unprompted; it is still in his BunDex for a lookup (M4b). A "rings a bell" page carries no details: it crosses only as the feeling "Something about this rings a bell."
5. **Remembering refreshes.** When a page comes to mind clearly or rings a bell, that is recorded (`PageRecalled`, append-only) and its clock restarts. So does writing to it. Nothing is ever edited or deleted.
6. **Sparing.**
   - The likeness threshold (M1c) stays.
   - A page that just came to mind won't again for 15 minutes.
   - At most three clear pages and one bell come to mind per moment.
7. **The ladder** breaks ties between equally fitting pages: cherished, then long, then medium, then short, then most recent.
8. **Recall is offered for his looks and conversations** through one store method (`ComeToMind`). The keeper uses it now; his voice (Stage 13) and M6/M9 use the same path.

## Values (J2, accepted defaults adjusted on evidence)

- **Approved by Boss:** short (next session or 2 days), medium (5 sessions or 3 weeks), and cherished (forever).
- **Mine:** long stays clear for 10 sessions or 60 days, then always rings a bell; the bell window equals the clear window; the cooldown is 15 minutes.

## Acceptance

- **Layers follow the approved values with two clocks:**
  - a month away with no sessions forgets nothing;
  - sessions alone fade short-term pages after the window;
  - whichever clock is longer wins.
- **Remembering refreshes the clock,** and the refresh survives a reopen.
- **Not clairvoyant.** A faded page is not offered unprompted. A bell page carries no details.
- **Sparing and ranked.** The cooldown is respected, at most three pages and one bell come to mind, and the ladder breaks ties.
- **Nothing is ever deleted or edited** by aging or recall: page bytes and earlier ledger entries are unchanged.
- **The curtain.** No layer, count, or time ever crosses; the bell is a feeling line; the curtain audit includes it.
- **Every existing suite passes.**

## Deferred findings

_(none yet)_
