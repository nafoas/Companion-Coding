# M1c — A Living Bun: his BunDex is entirely Bnuy-built

Status: **accepted**
Authorized: 2026-10-13 by Boss. This revises M1's keeper after the emulation (M1b D1–D3).
Accepted remote base: `b704a35c1139561ba458c0199eaddc8ea5e9d3db`
Working branch: `agent/m1c-living-bun`
Builder: Claude

## Boss's direction (verbatim, abridged)

> "Places shouldn't flip between games, because he should have the Bnuy knowledge of what he is doing and be able to mentally separate and categorize these things into individual folders… don't just fix it by that brute force method… holistically give the little Bun the knowledge, agency, and autonomy needed to go 'oh!! Dis somethings else, should writes it dere'… He needs to be a Living Bun, just writing stuff down as he wants to."
>
> "That second thing is alright." (the pull relaxes)
>
> "I want the categories of things The Little Bun writes about to be organic as well… the categories of things he stores things in serve as references for the types of ways folders are classified, but never as a quiet blueprint dictating how many folders he makes or in what order."
>
> "Reorganization is only ever something Da Bnuy should do himself, or with gentle player prompting like 'hey, little guy, maybe we should do some spring cleaning?'"

## Scope

1. **No blueprint.** Role-home registration is removed. Old `RoleRegistered` ledger entries are read and ignored. The kind-of-memory tags stay on pages only as the BrainCase's private reference for later remembering. They never choose, name, count, or order a folder.
2. **Knowledge for his choices.** Each moment carries:
   - what happened;
   - where he is, as he perceives it;
   - the feeling;
   - what comes to mind.

   He sees his whole notebook, including the line he wrote about each place.
3. **What comes to mind is remembering.** It is up to three of his own pages that feel like this moment (written where he is now, or sharing its words), each with its place, where it happened, and a glimpse. It is never a place assigned to a kind of thing.
4. **Lines about places.** When he makes a new place he may write what it is for. The line is kept in the ledger and shown back to him.
5. **The pull relaxes.** Only "not now"s since he last kept that kind of thing count.
6. **Tidying is only his.** He can move and rename his own folders and pages while he writes, or when Boss invites it, and he may decline. Nothing is ever moved for him; nothing is ever removed. A step he cannot do is skipped.

## Acceptance

- The same kind of thing goes to different places when he chooses: one folder for everything, folders per game, or loose pages, all exactly as he says.
- Lore from an unrelated place brings nothing to mind. His own similar pages do come to mind, at most three, most alike first.
- An old role home in the ledger is ignored.
- The pull goes Gentle, Warm, Strong, then back to Gentle once he keeps one. It survives a reopen and is separate per kind.
- Lines about places are stored, shown, and survive a reopen. A secret line is refused, leaving no empty place.
- Tidying:
  - keeping never invites it;
  - Boss's words reach him;
  - "not now" changes nothing;
  - moves and renames are applied, and invalid steps are skipped;
  - pages stay found by their tags;
  - he can tidy while writing.
- No member of the store or keeper can assign a place (reflection test).
- The writer boundary carries no IDs, roles, tags, or times (reflection test).
- Every other suite passes.

## Personal Round Judgments

- **J1 — Association is plain and local.** "Where he is now" plus shared words, scored simply, until Stage 13 gives him his voice. It only reminds him; his choice is final.
- **J2 — Tidying invitations come in as Boss's own words.** Wiring them from conversation is later (M3).

## Emulation rerun (temporary, scratch only)

Builder played Prince again over a fresh temporary BunDex, the same day of play as M1b. Each choice was made only from what he was given.
- Farm lore brought nothing scary to mind, and he made "Farm Fings" himself ("Sunny Acres!! cozy. nofing scawy here").
- Scary-house lore brought his own clock-man page to mind, so there was no flip.
- Twice he chose differently from what came to mind:
  - a shortcut went to "lil notes 4 later" (his own line: "useful fings so Prince can help Boss");
  - a farm contest win went to "Big Adventures!!" ("da biggest best fings dat happen to me n Boss").
- The pull went Gentle, Warm, Strong, then Gentle again after he kept one.
- When Boss invited spring cleaning, he moved the rusty key next to the moon keyhole and renamed it "rusty key (4 da moon door)".
- 10 pages in 6 top-level places, all rebuilt byte-identically from the ledger.

## Deferred findings

- **D1:** association is word overlap plus place until Stage 13. A richer sense of "this feels like that" belongs with his real voice.
- **D2:** Boss's tidying invitation is reachable only through the keeper API until conversation wiring (M3).

## Paw Gate

- **Candidate.** Head `5d4fffa5c4f82bfd58ccf5084ead5a243818af36`, PR #89, against accepted `main` `b704a35c1139561ba458c0199eaddc8ea5e9d3db`.
- **Local gate (Linux).** Locked restore; Release `/warnaserror`: 0 warnings, 0 errors; no vulnerable packages; all 19 runnable suites passed (BunDex 44, with the keeper tests rewritten for the Living Bun contract).
- **Mutation pass.** 15 mutants (pull relaxing, recall threshold, place, bound, and order, about lines, pre-validation, tidying). The first round left 2 survivors (relaxing on an add; a secret line on a nested new place), and a test was added for each. Final: 15/15 killed.
- **Windows CI.** Push run `38333524867` and PR run `38333539992`, both green.
- **Merge-ref check.** Parents `b704a35` + `5d4fffa`, tree `85ec186f9c531a40fb6c2830a00dc4d9a14aa978`, equal to the local merge-tree.
- **Merge.** Squash-merged with an expected-head fence as `c6013390299aaf12d360f20d28e78dd5a1dccf62`.
- **Result.** Passed and accepted.
