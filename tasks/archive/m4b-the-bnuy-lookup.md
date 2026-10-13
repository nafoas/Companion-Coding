# M4b — The Bnuy lookup: "okay... now how find dis...?"

Status: **accepted**
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

## Judgments made while building (Personal Round Judgment)

- **J4. "Where he wrote lately" is the very place he wrote in,** not the big place around it, so that hunch names the inner place directly.
- **J5. A hunch walks only into inner places that feel right.** An inner place with no pull is reached only as where he wrote lately.
- **J6. A place name with machinery in it crosses with the machinery removed,** as every other name does; a name that claims his workings never crosses.
- **J7. The bell's lookup is for the page that rang the bell.** The subconscious locates it; his cues, including where he is, decide the path. Finding it is sure.
- **J8. Words shorter than four letters are not cues** (the M1c likeness rule), so "key" alone does not pull.

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

- **D1.** Nothing asks him to look something up deliberately yet ("remember when...?"). Conversation does so from M6, and his voice from Stage 13, through `LookFor`.
- **D2.** The pull of each place is recomputed per lookup over the whole notebook. That is fine for a Bnuy-sized notebook; measure it in the M10 hand-run.
- **D3.** "Thinks it was" and "found" are by shared words only. Stage 13 can let his voice judge closeness, still answering only from a page he found.

## Paw Gate

- **Candidate.** Head `36c03fbf1d3e26e551d1ec29206749e5f96cc055`, PR #102, against accepted `main` `f1e4a66082d8f95ef93e9c095e9a8f160edb0360`.
- **Local gate (Linux).** Locked restore; Release `/warnaserror`: 0 warnings, 0 errors; no vulnerable packages; 21 runnable suites, 1421 tests passed.
- **Mutation pass.** 31 mutants, all killed.
  - The first round left 14 survivors, and each got a test: the five-place bound; the fallback order; place-name, bell, and where weights; the recency tie-break; backing out of a path; the best-fitting page in a place; an unrelated page is not "a different one"; zero-pull places; inner places that don't feel right; where he is as a cue; the bell's own page; the rummage in the request.
  - The full set was rerun on the final tests: 31/31.
- **Curtain.** Every rummage line is a template (audited by reflection). The 600-situation curtain audit includes rummages with hostile place names. `LookStep` has no public constructor and only the membrane returns one.
- **Windows CI.** Push run `38456764265` and PR run `38456788329`, both green.
- **Merge-ref check.** Parents `f1e4a66` + `36c03fb`, tree `07c5d5de0cd1e3741e8b34a1ae31f4b3d94c4a6f`, equal to the local merge-tree.
- **Merge.** Squash-merged with an expected-head fence as `7af8283514dc070b0c723a4d2ffc8fc0f0717b94`.
- **Result.** Passed and accepted.
