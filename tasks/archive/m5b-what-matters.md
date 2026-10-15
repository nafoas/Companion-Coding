# M5b — What matters: memories form the way ours do

Status: **accepted** (Boss approved the Remembering Novelty values and said "Start building.")
Authorized: 2026-10-14 by Boss ("Anyways, begin the packet"). Boss's decisions are pinned in the plan under "M5b also: memories form the way ours do", which supersedes items 7e, 7f, and 7g and the earlier M5 wording wherever they conflict.
Accepted remote base: `d203080cb8abb578a32c1a7838b898f1ca22f3c7`
Working branch: `agent/m5b-what-matters`
Builder: Claude
Plan items: 7e–7g (as reworked), "M5b also", "M4e. Remembering organically", and the R pins on learned loves. The vision, the Little Self, and the three layers apply throughout.

## Boss's principle

> "we don't consciously remember things with more priority than others- memories just sorta happen… the subconscious merely forms memories via the same way we do… We don't decide for him- we let his actions speak for him… there isn't a memory checklist, it reinforces in different ways every time."

- No tier is chosen by anyone: not Prince, not Boss, and not a rule.
- The subconscious only reads what he himself did and expressed. It interprets; it never invents a feeling or judges content.
- What he loves stays special because it is genuinely richer in his life. There are no caps, quotas, rankings, or artificial tapers.

## What exists today (code checked 2026-10-14)

- **Memory strength** (M4e, `BunDexRemembering.cs`) multiplies five fixed factors:
  - wrote: word count, plus 0.25 per addition, up to 4;
  - remembered: 0.25 per recall, up to 8;
  - heart: closeness to his cherished or thinking-about pages, or `MattersToHim`;
  - felt: the hidden ScaryOrCool kind counts 1.5×;
  - place: being back where it happened.

  The first, second, and fourth factors are a checklist with caps: exactly what Boss ruled out.
- **`MattersToHim`** is an empty socket.
- **The Cherished layer** (always clear) is reached only if a caller tags a page with the Cherished role. During play nothing does.
- **Recall's Treasured Episodes** come from a `Highlight` flag that nothing in the system sets.
- **Remembering Novelty already exists:**
  - the sighting record recognises repeats (M3a);
  - words he meets often become background (M4e);
  - patterns grow by magnitude (M4e).

  Its numbers lean toward getting used to things quickly.
- **Page source links** (M5a) let the subconscious trace a page to the memories behind it.
- Nothing named "That wasn't important", no hard rules, and no "Remember this" path exist in code. There is nothing to remove there; tests will keep it that way.

## Design

### 1. Experiences, recorded underneath (Subconscious)

- Each time he experiences a memory, the subconscious appends an experience to the hidden BrainCase ledger against that page. It records when, the words of the experience, and, if he expressed a feeling, what reading his own expression found.
- He experiences a memory when he:
  - writes it;
  - adds to it;
  - is reminded of it by something new happening;
  - looks it up;
  - expresses something about it.
- These are only the ways an experience can happen. None of them carries a weight of its own.
- Append-only. It survives a reopen and a Vault rebuild. Old ledger lines read as before, and old hashes are unchanged.

### 2. Reinforcement comes from what the experience actually was

- An experience reinforces a memory by what it brought that the memory did not already hold.
- Each word of the experience counts by:
  - how distinctive it is to him (M4e's familiarity);
  - how new it is *to this memory*, with the same gentle formula, so the same thing again counts less and less but never as nothing.
- How strongly he felt, of any kind, as he expressed it, keeps a memory longer (J4). Delight and fright both make memories stick.
- So:
  - going into the carrot patch, eating the carrot, the soft dirt, the smell: each brings something new and reinforces a lot;
  - "nice carrot" for the fifth time reinforces a little;
  - a reminder by something brand new reinforces more than the same reminder again.
- **This richness replaces M4e's flat "wrote" and "remembered" counts and their caps.** It also replaces the felt factor from the hidden ScaryOrCool kind: how strongly he felt now comes from what he expressed.
- Place stays: being back where it happened is a cue of the moment, not a reinforcement.

### 3. Fondness, and what matters to him

- Fondness grows the same way, but only from experiences where his own expression read as fond: his delight, his "so sweet!!".
- A page's fondness strengthens it (heart).
- Fondness spreads organically into `MattersToHim`. A new moment that shares distinctive words with things he is fond of matters more (carrots!), so it is remembered better and its bells get looked up.
- **Fondness fades gently like a memory and comes home on recognition** (the learned-loves rule):
  - its value never decreases;
  - how present it is follows how clearly that page is with him now;
  - being reminded of the page brings it back.
- `MattersToHim` stays as a socket for his interests (M6) and inherent core (R1).

### 4. Cherished is grown into

- Past a high bar of fondness, a page is cherished: always clear to him, and never demoted.
- The bar is reached only through his own fond experiences.
- Fondness only grows, so cherished stays cherished without any extra state.
- Nothing assigns it, and he never declares it.
- The memory records behind his cherished pages (through M5a's source links) are what Recall treats as Treasured Episodes. Both memory systems then agree on what he holds dear.

### 5. Reading his expressions

- A neutral reader looks only at his own words and reports what feeling, if any, they express, and how strongly. It never invents one.
- **Until Stage 13, the stand-in reader reads nothing:** fondness stays empty and only richness grows. Tests use scripted readings.
- Stage 13's real reader belongs with Boss's R leads: a neutral prompt, insulated actions, or a local model.
- His expressions arrive through his writing now. A neutral "he expressed this about it" channel is built for his voice to use from Stage 13.

### 6. Remembering Novelty leans curious

These values need Boss's approval before they change:

| Value | Today | Proposed | What it means |
|---|---|---|---|
| A word he met *n* times weighs (BunDex, M4e `FamiliarAfter`) | half at 3 | **half at 10** | After 30 trees, "tree" still weighs 0.25 (today 0.09). |
| How new the same thing feels after *n* sightings (Sightings `ThingRate`) | 0.6 | **0.25** | Seen once before: 0.70 (today 0.54); twice: 0.57 (0.38); five times: 0.36 (0.19); ten: 0.21 (0.10). |
| How much a familiar *kind* of thing dulls it (`KindRate`) | 0.1 | **0.05** | Gentler. |
| The lowest interest anything can fall to (`Floor`) | 0.05 | **0.1** | Nothing ever becomes nothing to him. |
| Kept as they are | | | "Is it the same thing again" (`SameAt` 0.6), the magnitudes (3, 10, 30), and how long familiarity lasts (3 days for a thing, 7 for a kind). |

"Counts as new" (0.5) is unchanged. With the proposed rates, a thing feels new for its first three sightings instead of only the first.

### 7. "Remember this, Prince"

- An earnest ask he hears, never a command.
- A neutral command path delivers Boss's words to him with what just happened: "Boss said: …". What he does with it is his.
- It changes nothing underneath by itself. If he then writes about it, delights in it, or comes back to it, those are his experiences, and they count like any other.

### 8. No rules decide

- No code path assigns a tier, cherished status, fondness, or significance from a rule, a game event kind (boss, credits, Watchbun), Boss's command, or the hidden kind of a page.

## Acceptance

- **Novelty, not checklists.** New, varied experiences of a memory strengthen it more than repeating the same one. Repeats still count a little, never nothing. No kind of experience carries a weight of its own.
- **Fondness** forms only from his own expressions read as fond. It strengthens the page and what shares its distinctive words. It fades with the page's clarity and comes home when he is reminded.
- **Cherished** forms only past the bar, from his own fond experiences. It is always clear and never demoted, across a reopen and a Vault rebuild.
- **Recall** treats the memories behind cherished pages as Treasured Episodes.
- **"Remember this, Prince"** reaches him as Boss's words. By itself it changes nothing underneath (strength, fondness, and cherished are identical before and after).
- **The wall.** Nothing he is given names significance, fondness, strength, tiers, or cherished. The curtain audit covers every new line.
- **No rule decides.** No randomness. No caps, quotas, or rankings on what he can love.
- **Remembering Novelty** uses the approved values, and the M3a/M4e suites pass with them.
- **The memory store is untouched** (byte-identical), and every existing suite passes. Tests whose fixed numbers came from M4e's flat counts are re-derived from the new principle, never weakened.

## Judgments made while building (Personal Round Judgment)

- **J1. How rich keeps how long.**
  - A brief note's worth is 20 distinctive things, M4e's brief note: anything at or below it stays its plain window.
  - Each further 15 keeps it one more whole window.
  - Five vivid additions, about 38 new things, now keep a short-term page clear one more session.
  - At 20 per window, five additions fell just short, which was too stingy for coming back with new things.
- **J2. New to this memory.** Within one memory, a thing it already holds counts half the second time, a third the third, and so on. The same delight 200 times stays far below cherished, yet it is never nothing.
- **J3. The high bar.** Cherished at 40 fondness: about a dozen varied, wholly fond experiences, more when he is only a little fond.
- **J4. Feeling keeps it longer directly.** His strongest expressed feeling of any kind multiplies how long a memory stays, by up to 1.5×. That is M4e's approved value, now sourced from his own expression instead of a hidden kind, so a brief note he was very frightened or delighted about still sticks. Fondness grows by what a fond experience brought, times how fond.
- **J5. Everyday words stay background.** Everyday familiarity rises to 1000 alongside the curious lean, so "the" weighs exactly what it did before. The curious lean is for the things he meets.
- **J6. What an experience brought is kept as it was then.**
  - The ledger records each experience's gain at the time.
  - A reopen therefore remembers exactly, even though how familiar the world is to him has changed since.
  - Lines from before M5b are replayed with the weights at replay.
- **J7. Reminders.**
  - A remembering keeps up to 64 words of what reminded him, or of what he looked for.
  - A remembering from before M5b counts as his own page's name coming back to him.
  - Expressing something about a memory refreshes its clock, like remembering it.
- **J8. What "this" means.**
  - It is the last moment that reached him this session, within the 10 minutes a moment waits, and never one from before a privacy stop. Otherwise he hears Boss's words alone.
  - It goes first in the queue, and still never during a conversation or an intense moment.
- **J9. Treasured Episodes in Recall.**
  - Only the memory record behind a cherished page is treasured. A session summary covering it is not.
  - Consolidated highlights stay treasured as before.
- **J10. Fondness spreads by presence.** How present a fond memory is follows its own clocks and experiences (clear 1, a bell ½, faded ¼, the same as the lookup's). Its own heart is left out, so two fond memories never lean on each other.

## Deferred findings

- **D1.** Stage 13's real reader of his expressions; until then, fondness stays empty in live play.
- **D2.** His interests (M6) and inherent core (R1) plug into `MattersToHim`.
- **D3.** M5c reconsiders the end-of-session review's question to Boss ("Should Prince put this in his BunDex?") under this principle.

## Paw Gate

- **Candidate.** Head `2a379f84d27803d9810ef2aa853dbd39e50fdff9`, PR #116, against accepted `main` `d203080cb8abb578a32c1a7838b898f1ca22f3c7`.
- **Local gate (Linux).** Locked restore; Release `/warnaserror` gave 0 warnings and 0 errors. No vulnerable packages. 21 runnable suites, 1545 tests passed.
- **Mutation pass.** 59 mutants; each later fix was re-checked against its mutants.
  - **57 are killed.** Seven first survived and each got a test:
    - another kind of hidden source shaped like a memory's is never taken for one;
    - what he writes is read both when he creates a page and when he adds to it;
    - expressing something refreshes a memory's clock;
    - a lookup with something new is an experience of it;
    - the host's expression reader reaches his keeper;
    - the gentler kind rate is pinned.
  - **1 exposed redundant code**, now removed: a cherished page was also counted as "dear", which its fondness already covers exactly.
  - **1 is equivalent:** "Remember this" checks that the last moment is from this session, but ending a session already clears it. The check is kept as a guard against a moment leaking across sessions.
- **Changed expectations (Boss's direction, not weakened).** Tests built on M4e's flat counts, on the hidden Cherished and ScaryOrCool kinds, and on the old novelty numbers were re-derived from the new principle:
  - coming back with new things keeps a page clear, and the same thing again brings less;
  - only grown cherishing and expressed feeling count;
  - the approved curious values are pinned exactly.
- **CI.**
  - **Head:** push run `38665596281` and PR run `38665601486`, both green.
  - **Earlier on the branch:**
    - `ba2ce6a` failed on the two orchestration tests that still expected the old novelty numbers; they were fixed in the next commit.
    - PR run `38663413064` on `0fd914c` failed only on the known Windows sniff-latency timing test, which M5b does not touch; its push run `38663409559` passed.
- **Merge-ref check.** Parents `d203080` + `2a379f8`, tree `1093efb09e9bf36c25729b7c1961f3559e764bf7`, equal to the local merge-tree.
- **Merge.** Squash-merged with an expected-head fence as `12e1596dd0bd79dfc204a44a54c7388f008c4c73`.
- **Result.** Passed and accepted. Next: M5c (the end of a session).
