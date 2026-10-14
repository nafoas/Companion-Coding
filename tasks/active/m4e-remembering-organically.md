# M4e — Remembering organically

Status: **in progress**
Authorized: 2026-10-14 by Boss ("turn this entire thing I mentioned into like, M4E, and make sure everything to the effect that I described it… is all in there"). Boss's words are pinned in the plan under "M4e. Remembering organically". Boss approved the M4 values chosen as Personal Round Judgment ("the values you picked were good").
Accepted remote base: `0f7fde6a40d1f02ffc3470b900c4fb5bd106b478`
Working branch: `agent/m4e-remembering-organically`
Builder: Claude
Plan items: 1, 4, 7i, 12, 13–15.

## What Boss asked for, and where each part lives

| Boss's words | What it means | Where |
|---|---|---|
| "not arbitrarily throwing stuff out due to… word length" | No word is dropped for its length or by a fixed filter. | `BunDexStore.Words`, `Weight` |
| "his subconscious subconsciously filtering some words and patterns as being something Da Bnuy has seen before" | Each word weighs by how familiar it is to him, learned from his own pages and from what his eyes saw. Everyday words are familiar from the start. | `BunDexRemembering.Weight`; `SightingStore.TimesSeen` → `BunDexStore.SeenInTheWorld` (connected by the host) |
| "interprets it as another part of said pattern, not a salient memory on his own… doesn't announce or write it down every time" | A repeat adds to its pattern, not a new memory (the M3a novelty gate). A pattern is offered to keep only when it grows into a new magnitude. | `SightingStore.SeeAndNotice` / `RecordAndNotice` |
| "they do still accumulate… 'saws MANYS trees!!'… the magnitude of things he saw can be appreciated" | Every sighting still counts. The gist crosses as once, again, a few times, lots of times, so many times, and never as a number. What he writes keeps the magnitude. | `Membrane.SawAgain`, `SawSoManyTimes`, the stand-in writer |
| "based on how long ago Da Thing was" | Both clocks, per layer (M4a). | `PageClarity` |
| "how much Da Bnuy wrote on it" | Writing beyond a brief note, and coming back to add, make a page stay longer. | `Strength` |
| "whether he'd realistically know it… personality, traits, things he was thinking about before" | Closeness to his cherished pages, his thinking-about pages, and what he wrote this session. Learned significance (M5b) and interests (M6) plug into `MattersToHim`. | `Care`, `MattersToHim` |
| "it should depend on context" | Being where it happened. How strongly he felt (scary or cool). | `Strength` |
| "some things it'd remember better, some… a refresher on, some… consult da widdle Bnuy BunDex for" | Clear; rings a bell plus a little lookup when it matters to him or is on the tip of his tongue; only by looking it up. | `ComeToMind`, `WorthLookingUp`, `LookFor` |
| "not… a diceroll… not an AI simply pretending it can't remember" | Nothing is random. His consciousness is only given what the subconscious genuinely surfaces, and finds the rest by looking. | the curtain; the no-randomness tests |

## Design

1. **Word familiarity, weighed but never thrown out.**
   - A word's weight is `1 / (1 + met / 3)`. "Met" counts the pages of his containing it, plus the sightings it was part of, plus a head start for everyday words (300) that any speaker knows.
   - A brand-new word weighs 1, a word met three times ½, and "the" about 0.01.
   - Length never matters.
2. **Weighted cues everywhere he remembers or looks.**
   - A page comes to mind at a likeness of 1.0. That is the shared distinctive weight, plus 1.0 for being where it happened: one distinctive word and the place, or a couple of distinctive words.
   - A lookup's fit is the share of the distinctive weight of what he is after that a page holds. There must be at least 0.5 of distinctive weight to look for.
   - Hunches weigh his place, page, and where cues the same way.
3. **Patterns by magnitude.**
   - An impression grows: once, again, a few times (3), lots of times (10), so, so many times (30).
   - The sighting store reports when a pattern grows into a new magnitude. That is the moment to offer him a keep.
   - The keeper and the stand-in writer keep the magnitude in the moment and on the page.
   - Live play calls this from M5a, together with the pinned source links in the BrainCase index.
4. **Memory strength.** A page's layer window (M4a) is stretched by the product of:
   - **how much he wrote:** up to ×1.5 for writing beyond 20 words, plus ¼ for each time he came back to add (up to 4);
   - **remembering:** ¼ more for each time he remembered it (up to 8);
   - **close to his heart:** 1 + care, where care is the share of the page's distinctive weight that it shares with his cherished pages, his thinking-about pages, and what he wrote this session, or what matters to him (`MattersToHim`), up to 1;
   - **feeling:** ×1.5 for scary or cool things;
   - **place:** ×1.5 when he is where it happened.

   Clear within the stretched window, a bell within twice it (long-term pages always ring a bell), otherwise only by looking it up.
5. **The bell's lookup, by context.** A bell leads to a little lookup when the page is close to his heart, he felt strongly about it, or it is on the tip of his tongue (within 1.5 times its stretched window). Otherwise the bell stays a passing feeling.
6. **Nothing is ever edited or deleted.** Nothing is random, and the curtain is unchanged except for the new "so, so many times" line.

## Values (Personal Round Judgment, within Boss's direction)

- **J1.** Familiarity halves a word's weight after 3 meetings. Everyday words start at 300.
- **J2.** Thresholds: comes to mind at 1.0, the same place is worth 1.0, and a meaningful cue is 0.5.
- **J3.** Magnitudes fall at 3, 10, and 30.
- **J4.** Strength factors as listed in design item 4. A brief note (20 words or fewer) gets no writing boost.
- **J5.** A bell is on the tip of his tongue within 1.5 times its stretched window.
- **J6.** A page he is thinking about, or wrote this session, counts as close to his heart itself. What is on his mind stays on his mind.
- **J7.** Live play offers patterns to keep from M5a, together with the pinned source links in the BrainCase index. Until then the host connects only word familiarity.

## Acceptance

- **Words.** No word is dropped for its length: "egg", "orb", and "key" count. A familiar word weighs less, learned from his own pages and from what he saw. A word he has seen hundreds of times is background, while the same word is vivid to a Bnuy who has never seen it.
- **Patterns.** A pattern is offered once per new magnitude, never per sighting. It crosses as a magnitude without a number. What he writes keeps the magnitude. Every sighting is still recorded.
- **Strength.** Each source measurably changes remembering: age, how much he wrote, coming back to add, remembering, his cherished pages, his thinking-about pages, what he wrote this session, `MattersToHim`, feeling, and place.
- **The bell's lookup** happens when the page matters to him, he felt strongly, or it is on the tip of his tongue, and not otherwise.
- **The host** connects what he saw to word familiarity.
- **No randomness** in the BunDex or the sighting record. The curtain audit covers the new line.
- **Every existing suite passes.** Expectations that change because of Boss's direction are updated and listed in the Paw Gate:
  - a hundred sightings now read "so, so many times";
  - one M4b lookup test cue is retuned to the new word weights.

## Deferred findings

- **D1.** Word likeness is still by words, not meaning: "sapling" and "tree" are different words. Meaning comes in Stage 13, when his own mind judges "is dis da same fing?" over what his subconscious offers. A bundled local meaning model stays an option if the hand-run shows it is needed.
- **D2.** Patterns are offered to keep during live play from M5a (J7).
- **D3.** `MattersToHim` is filled by learned significance (M5b) and his interests (M6).
