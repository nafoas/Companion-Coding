# M4b — The Bnuy lookup: "okay... now how find dis...?"

Status: **in progress**
Authorized: 2026-10-13 by Boss ("begin m4b"; the lookup design was approved and pinned in the plan, "M4 also: looking things up like a little Bnuy").
Accepted remote base: `f1e4a66082d8f95ef93e9c095e9a8f160edb0360`
Working branch: `agent/m4b-the-bnuy-lookup`
Builder: Claude
Plan items: 4 (lookup), 5, 13–15.

## What exists today (code checked 2026-10-13)

- M4a: pages fade from clear to "rings a bell" to not offered unprompted; a bell crosses only as a feeling.
- Nothing lets Prince look something up. A faded page can only stay where it is.

## Design

1. **One way to look things up:** `BunDexStore.LookFor(whatHeIsAfter)`. It returns his rummage, step by step, as membrane experiences, and the page he found, if any. His voice (Stage 13) and conversation (M6) use the same path.
2. **What he is after.** The subconscious knows which pages truly fit: a page fits by how many of the words he is after are in its name and text.
   - A **sure** fit is most of them (J1: at least 80%).
   - A **thinks it was** fit is enough of them (J1: at least 60%).
   - Anything that shares a word but fits less is only **a different one**.
   - If no page fits at all, nothing was written down: no hunch comes, and he says honestly that he doesn't think he wrote it down.
3. **Hunches come from his own cues, never the exact location.** A place comes to mind by how much what he is after resonates with:
   - the place's name and the line he wrote about it;
   - the names of the pages in it and where they happened, weighted by how clear each still is (fading makes a page's cue weaker);
   - and, through it, the places inside it.

   Page text is never a cue: he only reads it when he looks. Ties go to the place he wrote in most recently, then by name. When no cue fits any remaining place, the next place to come to mind is wherever he wrote most recently ("where was I writing lately?").
4. **The hunch walks his own path.** A hunch names one of his own places at a time: "maybe it's in "Scawy Stuffs"... maybe look there?" When he looks in a place, he looks over its pages:
   - if the page he is after is there, he finds it ("found" or "thinks it was this one");
   - if only a similar page is there, he notices it is a different one;
   - otherwise the hunch goes deeper into the places inside it that feel right, and then back out to the next likely place.
5. **Bounded.** He checks at most five places (J2), then gives up gently ("Prince can't find it right now"), even if the page exists.
6. **Never wrong on purpose.** Nothing in the lookup is random, and nothing counts mistakes. The same notebook and the same question always give the same rummage. Imperfection comes only from honest sources: where he filed things, how close his cues are, and fading.
7. **Truthful.** Only a page that truly fits is ever found, with certainty matching the fit. A similar but different page is never presented as the one asked for.
8. **Finding refreshes.** A found page is remembered (append-only `PageRecalled`), so it comes back clearer next time.
9. **"Dat rings a bell... should look dis up."** When a bell rings while he is keeping something, he does a little lookup for what is in front of him. The rummage goes with the moment. The bell stays only a feeling unless he actually finds the page.
10. **The curtain.**
    - Every rummage line is a fixed template with his own place and page names in their slots.
    - No location, tag, fit, layer, or count ever crosses.
    - The curtain audit and the template audit include every line.

## Values (Personal Round Judgment)

- **J1.** A fit is sure at 80% of the words he is after, and "thinks it was" at 60%.
- **J2.** At most five places are checked per lookup.
- **J3.** Cue weights: place name ×2, his line about the place ×1, a page's name and where it happened ×1 times its clarity (clear 1, bell ½, faded ¼). A place's pull is its own plus the strongest pull inside it.

## Acceptance

- **His own places only.** The hunch is always one of his own folder names, crossing as his own thought. No index, tag, fit, layer, count, or ID crosses.
- **Filing decides the path.**
  - A page filed where it naturally belongs is found on the first hunch.
  - A page filed somewhere unexpected is found after checking the place that felt right first.
  - A nested page is found by walking his path one step at a time.
- **Deterministic.** The BunDex assembly references no randomness at all. The same notebook and question give the same rummage.
- **Truthful.**
  - He answers only from a page he actually found, with matching certainty.
  - A similar but different page is noticed, never presented.
  - Something never written down brings no hunch.
- **Bounded.** He gives up after five places.
- **Fading makes finding harder.** A faded page's cue pulls more weakly than a clear one's.
- **Finding refreshes it**, and the refresh survives a reopen.
- **"Rings a bell" leads to a little lookup** in the keeper's moment.
- **Nothing is ever edited or deleted** by looking.
- **Every existing suite passes.**

## Deferred findings

_(none yet)_
