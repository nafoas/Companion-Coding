# Pre-Stage 13 changes (Boss's list, in progress)

Status: **collecting.** Boss is listing the changes to make after the Core Bun Approval Hop and before Stage 13. Nothing here is a task packet yet: each item becomes one only after the list is complete and Boss approves it.

## 1. Memories are novel, not a log (Boss, 2026-10-12)

> "They are all the same memory. Dis is a Living Bun, or it should feel like one, and living beings don't constantly say the same thing twice. Memories need to be novel. He should have a record of everything he sees, but not everything he sees should become a *memory*, per se. The near duplicates perform this. He needs to remember like a person does, not like a log."

- **Two layers.**
  - A **record** of everything he sees: complete and factual.
  - **Memories**: only what is new and meaningful to him.
  - Seeing something is not the same as remembering it.
- **Novelty.** A new memory must add something he does not already remember. Repeats and near-duplicates stay in the record and do not become new memories. Examples from the hand-run:
  - "notebook counter read 5/7";
  - the inventory again;
  - the jump rope saved twice.
- **Like a person.** Status readouts are not memories. Moments are: the escape turning red, detention, the notebook yelling "I HEAR EVERY DOOR YOU OPEN".
- **Evidence.** The hand-run produced 36 memories in about 7 minutes, under two subject keys, with near-duplicates seconds apart and no highlights. See the BUILD_LEDGER entry "Core Bun Approval Hop".
- **Constraint to keep.** Committed memories are never edited or deleted. "Seen again" must therefore be expressed without changing an existing memory: in the record, or as an append-only link. This is to be worked out in the packet.

## 2. The BunDex is Prince's catalogue, not an opaque program (Boss, 2026-10-12)

> "We should make his BunDex less opaque. The entire idea behind the BunDex is that it's dis little Bnuy cataloguing all the stuffs he sees while adventurings with da boss, as he puts it. And right now it is a cold computer program. I get this is the utilitarian build, but we need to rework this."

- **Today.**
  - **Storage.** Everything sits in one database file of flat rows, with only two subject keys in practice.
  - **Readability.** Boss cannot open or read any of it. He needed me to read the raw file for him.
  - **Adventure Records.** They say "[neutral session summary] 14 entries consolidated".
- **The design already asks for this** (`docs/Prince-Design-BunDex.md`, "PRINCE and the BunDex").
  - **Voice.** Every visible BunDex item is written autobiographically in Prince's voice: memories, lore, summaries, opinions, corrections, Adventure Records, and captions. Only invisible IDs, hashes, scores, and indexes stay structured.
  - **Sections.** The BunDex is organized into:
    - BIC, Game Memories, Adventure Records, Lore Notebook;
    - Things Prince Was Thinking About, Opinions, Conversation Highlights;
    - Temporary Observations, Ambient Remarks, Treasured Memories;
    - Paused Adventures, Prince Photographs, **Scary or Cool Stuffs**.
- **The rework.**
  - **A catalogue Boss can open and read.** Entries are grouped into those sections and about things, such as characters, places, items, events, and lore, not a flat timeline of readouts.
  - **Plain, readable entries.** No JSON or IDs on the page. The machine parts stay invisible underneath.
  - **Fits item 1.** The design's "Temporary Observations" section is the natural home for the record of everything he sees. The other sections hold what became real memories.
- **Boss's answer on voice (2026-10-12).** "It is crucial that this reads like a very fluffy round little bnuy is writing all of it." The BunDex is sorted, named, and foldered "the way a Bnuy would". The voice is a requirement of the BunDex, not a later polish. The packet decides only whether the first readable version ships with his voice or immediately before it.
- **To settle when this becomes a packet.**
  1. ~~Whether Prince's own voice comes now or in Stage 13.~~ Answered above: the voice is required.
  2. What "open the BunDex" looks like for now: a plain readable page or file generated from the BunDex, or a window inside the App. The design defers a *decorative* browser, not a functional one.
- **Constraints to keep.** The BunDex stays local and append-only. Reworking how it is organized or shown never edits or deletes a committed memory. Existing Builder memories stay as they are, and new structure is added beside them.

## 3. BrainCase and BunDex: raw workings vs. his autobiography; notes vs. memories (Boss, 2026-10-12)

> "We rework it so the BunDex and BrainCase are two separate directories. The BrainCase is da Bnuy's Brain inner workings, or, the raw outputs. That is what we have right now: a garbled, computery raw output. The BunDex is da Bnuy's little autobiography. The folders are named by him, individually, the way he would, for each discreet little adventure, and that's where the memories and autobiography are stored. There is also a difference between Da Bnuy's memories, and da stuffs he writes down abouts da adventures. He doesn't remember everything he writes down, but he can access everything he writes down, so that way he isn't constantly bringing up stuff that happened a long time ago as if it was recent, but also you can prompt him with 'hey, remember when...?' and he consults his little BunDex, and is like 'oh!! is remembers!!'. He was also supposed to naturalistically forget, sort, and categorize things in his memory, I don't know where that went."

### Two directories

- **BrainCase.** Da Bnuy's inner workings: the raw outputs. This is what exists today: the database, the raw interpretations, and the machine records.
- **BunDex.** Da Bnuy's autobiography: readable, and in his own words.
  - It has **one folder per adventure**, which Prince names himself, the way he would.
  - The adventure's memories and autobiography live inside that folder.
- **Naming note.** Today "Braincase" in the code means the borrowed brain, the Claude API that does his thinking. Boss's BrainCase folder holds that brain's raw output, so the two fit together, but the packet should settle the names so they never get confused.

### Notes vs. memories

- **He writes down everything** about an adventure in the BunDex.
- **He only *remembers* some of it.** His active memory is smaller, recent, and important.
- **He can always look things up.** "Hey, remember when...?" makes him consult the BunDex and find it: "oh!! is remembers!!".
- **Old things stay old.** Something from long ago is not brought up as if it just happened. Unprompted, he talks about what he actually remembers. Older things come back when something brings them up.
- **Recurrence.** This matches the design's own rule: "Duplicate memories become linked recurrence entries: Prince thought of it again". Remembering something from the notes can append a "remembered again" link, never an edit.

### Naturalistic forgetting, sorting, categorizing: where it went (code checked 2026-10-12)

The design asks for it: "Memory 'decay' affects retrieval priority, not existence." Here is what actually happened:

- **Remembering is not connected.**
  - The recall selector (Task 10, `RecallSelector`) exists and is tested, but nothing in the running App calls it.
  - Every look Prince sends today gets an **empty** memory context (`new ResumeContext([], session.Game)`). While playing, Prince never uses anything he has remembered.
  - Memories are saved, but never brought back.
- **No forgetting curve.**
  - Recall ranks by current understanding, source authority, and focus. Time is only a tie-breaker.
  - Nothing makes older, unrevisited memories fade in priority, and nothing makes revisited ones stay fresh.
- **No sorting into categories.**
  - The subject of each memory is whatever the model writes, so two buckets came out (`notebook-progress`, `inventory`).
  - The design's BunDex sections (Lore Notebook, Scary or Cool Stuffs, and the rest) were never built.
- **No duplicate handling.**
  - The "duplicates become linked recurrence entries" rule is not enforced, so near-duplicates are saved as new memories (see item 1).

### Overlap with items 1 and 2

Items 1, 2, and 3 describe one memory rework seen from three sides:
- novelty (item 1);
- the readable catalogue (item 2);
- record and notes vs. active memory, recall, and forgetting (item 3).

They will likely become a short series of packets rather than one.

### Constraints to keep

- **Forgetting is never deletion.** It changes how readily something comes to mind, never whether it exists. Committed memories and notes are never edited or deleted.
- **The BunDex folder is append-only too.** Folder and file names come from Prince but are validated locally: safe names, no paths, no secrets.
- **Da Bun Vault backs up both** directories.
- **The API is still never sent the whole BunDex.** Only what recall selects.
- **Builder Prince's folders stay separate** from Companion Prince's future clean BunDex.

## 4. Remembering like a living Bun: four layers (Boss, 2026-10-12)

> "He's meant to remember things like an actual living person does. He shouldn't remember everything, because that means he remembers what color the sign was in a dungeon in a game we played 3 months ago, which makes him clairvoyant. He also shouldn't constantly be overwriting old memories with new memories, as that destroys the continuous bun thing. He is meant to have some cherished memories, personality, and immutable things that he enjoys, or learned, or gleaned insight from, or just wanted to know, that he stores there. Then you have long term memory, the step below cherished, which are things like general summaries of how an adventure went, long after it happened, then medium term, he can remember things from an individual session with a good level of quality, even after a while, and then short term, which is him being able to recall specific individual things that happened a little while ago with the appropriate level of accuracy and clarity."

- **The four layers**, from most lasting to most fleeting:

  | Layer | What it holds | How it feels |
  |---|---|---|
  | **Cherished** | What he loved, learned, gleaned insight from, or just wanted to know; part of his personality. The design's Treasured Memories, Opinions, and BIC interests. | Immutable and always his. |
  | **Long-term** | General summaries of how an adventure went. | Still there long after it happened. |
  | **Medium-term** | An individual session. | Remembered with good quality, even after a while. |
  | **Short-term** | Specific individual things from a little while ago. | Recalled with fitting accuracy and clarity, which fades as time passes. |

- **Not clairvoyant.**
  - Fine details fade from what he remembers unprompted. He does not know the color of a dungeon sign from three months ago.
  - If he wrote it down, it is still in his BunDex notes (item 3). "Remember when...?" lets him look it up, like reading his own diary.
- **Never overwriting.**
  - Moving between layers is additive. A session becomes a summary; an adventure becomes an Adventure Record.
  - The new layer is added on top, the details stay in his notes, and nothing older is replaced, edited, or deleted.
  - This is what keeps him one continuous Bun.
- **How this maps onto today's code.**
  - The scopes exist: moment, session, save, game, general. So do the summary and Adventure Record kinds.
  - Missing:
    - recall that respects layer and age (item 3);
    - a cherished layer that Prince fills deliberately and that never fades;
    - promotion between layers;
    - forgetting (priority decay) by layer.
- **Constraint to keep.** The layers decide what comes to mind, never what exists. Committed records stay append-only. The cherished layer is immutable once committed, and changes of heart are appended beside it.

### What the design conversation already says (`Prince - Pet Creation Request.PDF`, checked 2026-10-12)

The design conversation has **no exact durations**. It deliberately chose "gradually based on time and use, not on a rigid deletion date", and "mildly interesting events survive several sessions before reconsideration". It does define:

- **A recall-priority ladder.** Nothing on it is deleted.
  1. Treasured: protected and readily recalled. Only Boss, or exceptionally strong evidence, can assign it, and it never decays.
  2. Significant.
  3. Adventure context.
  4. Session detail.
  5. Routine aggregate.
- **The adventure lifecycle.** Active → Freshly Finished → Remembered → Adventure Record, with Treasured Episodes kept forever beside the record.
- **During a session**, he remembers everything meaningfully describable except repetitive noise, which collapses into aggregates such as "defeated several ordinary bandits".
- **End-of-session review.**
  - It is mostly silent.
  - It occasionally asks about two or three borderline items: "Should Prince put this in his BunDex?"
  - If Boss ignores the question, the item is retained.
- **Gentle forgetting.** Specifics merge into an impression. Uncertainty decays before substance ("was it behind the waterfall?"). Lore, Prince's own questions, shared reactions, and recurring people, places, and things all add retention.
- **Asymmetry.** "Accidentally retaining a mundane note costs very little. Accidentally deleting the moment that made the companion feel real cannot be repaired."

### Values (Boss approved, 2026-10-12: "I like that")

| Layer | Stays clear for | Then |
|---|---|---|
| Short-term (specific details) | the current session, and through the next one or 2 days, whichever is longer | merges into the session's summary |
| Medium-term (a session, in good detail) | about 5 play sessions or 3 weeks, whichever is longer | becomes part of the adventure's summary |
| Long-term (how an adventure went) | indefinitely; slowly lower priority, but always surfaces when relevant | — |
| Cherished | forever; never fades | — |

- **Two clocks.** Every window counts both play sessions and calendar time, and uses whichever is longer. Time away from the game does not cause forgetting.
- **Recalling refreshes.** Bringing a memory up again restarts its clock.
- **Accepted defaults.** These follow the 2026-10-11 rule for agreed values: changed up or down only if real use shows a need.

## 5. Prince da Bnuy and Prince the Program are separate; the BunDex is organically his (Boss, 2026-10-12)

> "The intentional separation of Prince Da Bnuy and Prince the Program. He shouldn't be able to see inside the BrainCase folder. Those are the little inner computer workings, and they should be separate. The BunDex is how he remembers, the BrainCase is just what generates his memories."
>
> "The BunDex I also envisioned as having a very organic structure. It is basically entirely Bnuy made, according to his personality, and how he wants to organize and name things. Apart from actually being able to store memories somehow, I want the BunDex to actually come off as a Bnuy writing autobiographically about himself, without a fixed, orderly structure. He makes da folders, and writes in them. Of course, he has a style he does this in, but other than that, it is him."

- **Separation.**
  - Prince da Bnuy only ever sees his BunDex. Everything sent to his brain for a look, a comment, or a "remember when" comes from his BunDex pages, in his words.
  - Raw BrainCase records never reach his prompts: JSON, IDs, scores, subject keys, checksums, operation logs.
  - He never refers to files, IDs, or the program.
  - The BrainCase is Prince the Program: it generates his memories and keeps the machinery.
- **Organic BunDex.**
  - Prince creates the folders and names them his way.
  - He decides what goes where and writes the pages autobiographically, in his own consistent style.
  - There is no fixed template beyond what is needed to store and find things.
- **How finding still works.** Prince the Program can keep an *invisible* index of his BunDex inside the BrainCase. That index covers which page mentions what, when it was written, and its memory layer, and it serves item 3's recall and item 4's layers. Prince never sees it; he just "finds it in his notes".
- **Safety rails** (invisible to him, never shaping his voice):
  - Names are validated locally: safe characters, no paths, bounded length, no secrets.
  - Pages are append-only. He adds pages and notes and never erases or rewrites a committed page.
  - Everything stays local and in Da Bun Vault.
- **Looking something up** (Boss, 2026-10-12). The two Princes stay separate even while searching.
  1. **Prince da Bnuy wants to find something.** For example, Boss asks "remember when...?", or something reminds him.
  2. **He says so in character:** "okay... now how find dis..." or a similar line, always recognizable as him rummaging through his notes. This is what the frontend shows.
  3. **Prince da Program does the actual search** with the hidden index and brings the pages back to him.
  4. **Prince da Bnuy only receives the pages he "found"**, in his own words, and reacts: "oh!! is remembers!!".
  
  He never sees a query, a score, or a file. If nothing is found, he admits it in character, as fuzzy or not written down, and never pretends certainty.
- **Reorganizing** (Boss, 2026-10-12: "yes, he can reorganize, but he does it similarly organically"). Prince may move pages into new folders he invents, merge folders, and rename them his way, when it occurs to him, in his style. Underneath:
  - every move is recorded in the BrainCase;
  - no page's content is ever lost or rewritten;
  - his old folder names stay recoverable.
- **Removing pages** (Boss, 2026-10-12): "a case by case basis type of thing. It probably won't happen a lot, so I say we just let him write organically now." No removal feature is built in this round. Removal remains a rare, explicit decision by Boss, case by case. Prince and the program never remove pages on their own.

## 6. The Conversation Seed Banks live in the BunDex (Boss, 2026-10-12)

> "The Conversation Seed Bank needs to be in the memories."

**What the design conversation says** (`Prince - Pet Creation Request.PDF`, Question 2 and Question 3):

- **Where the seed banks live.** When a real conversation is interrupted, Prince summarizes enough to resume it and puts it in **a loose folder at the front of the BunDex: "Things Prince Was Thinking About"**. This is his "little pile of unfinished, promising, or recently interrupted thoughts". It is not the permanent archive.
- **Two banks inside that folder.**
  - **Game seeds**: interrupted lore talks, observations, theories, choices, events. They have higher priority.
  - **BIC seeds**: Prince's own curiosities and unfinished Bun thoughts.
- **What each seed records.**
  - the original subject;
  - why it was interrupted;
  - the last meaningful exchange;
  - game, save, lore, and memory links;
  - whether a question was left unanswered;
  - confidence that resuming still makes sense;
  - age and number of resurfacing attempts.
- **Entry rule.** A conversation seed requires all three of:
  1. substantive engagement (not a bare yes/no or a click);
  2. something meaningful left unfinished;
  3. resuming would add value.
- **Observation seeds.** An observation enters the Game bank when its significance would raise Prince's attention by one level.
- **Bank behavior.**
  - Higher-value seeds displace lower ones in a bounded active pool, and duplicates merge.
  - Seeds decay in urgency, not existence. A stale one settles into ordinary memory.
- **Sensitive seeds.**
  - Only strong evidence marks a seed sensitive. It is judged on the conversation's content, never as a diagnosis of Boss.
  - Prince then asks once, gently, before resuming.
  - Boss can answer "maybe another time", "let that one rest", or "don't bring this topic up again"; the last records a lasting boundary.
- **Ignored seeds.**
  - A seed can be offered about three times, at genuinely new moments, then leaves the active bank.
  - Ignoring is always neutral: no preference, relationship, or emotional update.
- **Two clocks.**
  - The Game Observation clock follows meaningful looks.
  - The BIC clock checks every 30 s, offset from it, and only when calm.
  - They have one shared voice, and Prince never talks over himself.
  - Weighting is about 70/30 game/BIC when both have eligible seeds.
- **Afterglow.** Ordinary BICs wait while recent intense events dominate the Game bank. Every Bnuy Mode ends with at least one opening.
- **Bnuyy Brain Farts™.** These are rare, harmless tangents ("Boss... why is pepperoni red?"), only in relaxed conversation. If ignored, they vanish without becoming a seed.
- **BIC roots are local and immutable.** The inherent interests (round things, small things in large worlds, maps, doors, ruins, paws, treats, naps, courage when little, and so on) live locally. The API only words fresh BIC seeds from them, in small batches saved locally. Learned interests grow beside the roots and never replace them.
- **BIC records.** Each record holds the original thought, whether Boss engaged, what was discussed, conclusions and jokes, related memories, how warmly it went, and whether Prince may revisit it. It is never a dossier on Boss.

**What the code does today** (checked 2026-10-12):

- **Plumbing only.**
  - The conversation coordinator (Task 9) has both banks (`SeedBank.Game` and `SeedBank.Initiated` for BIC), presentation counts, a sensitive flag, the Afterglow offer, the lock, and settling.
  - Each seed is only a topic key and a number. It has no words, no last exchange, no links, and no reason it was interrupted.
  - The seeds sit in a machine checkpoint file (`conversation-checkpoint` in the state store), not in the BunDex.
- **Never called by the running App**, although the coordinator has them:
  - `AddInitiatedSeed`: so **no BIC thoughts ever happen**;
  - `TryBrainFart`: so no Brain Farts;
  - `StartGameConversation`, `RequestFollowUp`, `Close`.
  - There is no 30 s BIC clock.
  - The inherent-interest roots (`InterestRoots`, Recall) exist but nothing uses them.

**The change.**
- "Things Prince Was Thinking About" becomes a real folder at the front of his BunDex.
- Both seed banks live in it, as Prince's own notes, in his words, with the full seed record above.
- The machine side (scores, timers, counts) stays in the BrainCase index, as in item 5.
- Wire up what was never connected:
  - BIC generation from the local roots;
  - the BIC clock;
  - Brain Farts;
  - seed records.

## Appendix: design-recovery sweep (both PDFs, 2026-10-12)

Boss asked for a full search of the design conversation for memories, identity, and a coherent living Bnuy. "Project Damage Control.pdf" is the project-recovery conversation and adds nothing new on these topics. Below is everything relevant from "Prince - Pet Creation Request.PDF", with today's status: **built**, **partial**, **missing**, **superseded**, or **later** (Stage 13 or after).

### Identity and continuity

| Design point | Status |
|---|---|
| One Bun, many adventures. There is one identity, one BunDex, one line of Conversation Threads. A new game, model, session, or conversation is never a new Prince. | **Built** (core invariants) |
| One continuous local Prince Runtime. The conversation is only a window into his thinks. | **Built** |
| The API never owns identity or memory; it only helps "his little Braincase think". | **Built** |
| Every API request carries the immutable Prince spec, current state, relevant memories, game/save context, and source rules, so a model change never replaces Prince. | **Partial.** Requests carry state and game, but **no memories** (empty context) and no Prince spec, since the core is neutral. Memories: items 3 and 5. Spec: Stage 13. |
| Immutable core: profoundly round, earnest, curious, kind, non-greedy, respects boundaries, hopeful. "IMMUTABLE — THE BUN REMAINS THE BUN." | **Later** (Stage 13) |
| An inherent-interest pool is always reserved; learned interests grow beside it and never sand it away. | **Partial.** The roots exist and are unused (item 6). |
| A character validator rejects out-of-character responses before they are committed. | **Later** (Stage 13) |

### Memory (PRINCE)

| Design point | Status |
|---|---|
| During a session he retains everything describable except repetitive noise, which collapses into aggregates ("defeated several ordinary bandits"). | **Missing.** Repeats become memories (item 1). |
| Each memory has two layers: structured facts for retrieval, plus Prince's recollection in his voice. | **Partial.** Only the structured layer exists (items 2 and 5). |
| Conversation memory is hybrid: crash-safe highlights immediately, permanence decided at consolidation. Jokes that landed, stories, personal things, conclusions, and defining interests are kept verbatim or faithfully semi-verbatim. | **Missing.** Needs real words (Stage 13); the memory side belongs with items 1–4. |
| End-of-session review. Mostly silent; sometimes asks about two or three borderline items: "Should Prince put this in his BunDex?" Ignoring keeps them. | **Missing** |
| Significance signals, with local hard rules: "Remember this, Prince", "That wasn't important", boss defeat, credits, Watchbun completion. | **Missing.** There are no such commands yet. |
| Treasured: assigned by Boss or exceptionally strong evidence, and never decays. | **Missing** (item 4, Cherished) |
| Recall-priority ladder and adventure lifecycle (Active → Freshly Finished → Remembered → Adventure Record, plus Treasured Episodes). | **Missing** (item 4) |
| Recall is sparing, with a relevance threshold, cooldown, and rarity, "so recollection feels like remembering rather than database search". | **Missing.** Recall is not connected (item 3). |
| Lore Notebook with observed/read/told/suspected/confirmed provenance; inscriptions marked exact, near-verbatim, or Prince's recollection. | **Partial.** Source kinds exist; there is no Lore Notebook. |
| Save identity, "Is this a new adventure?", and spoiler suppression for old saves. | **Partial.** Spoiler rules are in recall, which is unused. There is no save detection or question. |
| Opinions and beliefs are grouped and evolving; corrections are appended, never rewritten. | **Partial.** Corrections append (seen in the hand-run). `BeliefHistory` is unused. |
| Memories about Boss are never a dossier. | **Built** in principle (no profile store). It must stay true in the rework. |
| Bnuy No Write These Down: companionship continues, with no durable memories. | **Missing** |
| Forget This, Prince. | Boss, 2026-10-12: **case by case**; nothing is built now (item 5). |
| Restorative Bnuy Rest (roll the BunDex back). | **Superseded** by Boss on 2026-10-11: nothing ever rolls back; repair only continues the current line. |
| API-side Resume Capsule. | **Superseded.** The design later rejected remote capsules (recorded at the ATT-01 closure). |
| Non-response is neutral. | **Built** (core invariant) |
| Player Conversation Lock. | **Built** |
| Afterglow guarantees an opening. | **Built** (confirmed in the hand-run) |

## 7. The rest of PRINCE as designed (Boss, 2026-10-12: "add all this stuff to the list, apart from the stuff that got superseded")

Every memory point from the design-recovery sweep (appendix) that is **missing** or **partial** is listed here. Each sub-item cites its design source. Superseded items are deliberately left out: BunDex rollback (Restorative Bnuy Rest) and the API-side Resume Capsule.

- **7a. The session record collapses repetition.**
  - During a session, Prince retains everything meaningfully describable.
  - Truly repetitive activity collapses as it happens into aggregates: "Defeated several ordinary bandits", "Reorganized inventory for a while", "Made multiple unsuccessful jumps".
  - This is the "record" layer of item 1, and its first job.
- **7b. Two-layer memory entries.**
  - Every permanent entry has (1) structured facts for accurate retrieval: game, save, people, places, event type, source, confidence, significance; and (2) Prince's recollection in his own voice. The structured layer lives in the BrainCase; the recollection is the BunDex page (items 2 and 5).
  - Design example: structured "Defeated the Ash King; third attempt"; Prince remembers "We finally beat da Ash King after three tries!!"
- **7c. Hybrid conversation memory, with close wording.**
  - Immediately after a substantive conversation: a crash-safe record holding the summary, the most meaningful exchanges, exact or near-exact wording, unresolved thoughts, links, and why it may matter.
  - Its permanence is decided at consolidation. The temporary record is kept until that commits.
  - Kept verbatim or faithfully semi-verbatim:
    - jokes that landed and funny exchanges;
    - Boss's stories that fascinated Prince;
    - personal things Boss deliberately shared;
    - conclusions reached together;
    - statements that define a recurring interest;
    - promises, boundaries, and preferences;
    - notable lore lines and inscriptions.
  - Never flattened. "I usually like exploring, but only when the game makes places feel genuinely inhabited" must not become "Boss likes exploration".
  - The words arrive with Stage 13's real conversation. The capture, storage, and consolidation path is built here.
- **7d. End-of-session BunDex review.**
  - Mostly silent: clearly meaningful is preserved, clearly routine is compressed, related details are combined, and uncertain-but-valuable items are nominated.
  - Occasionally one compact question about two or three borderline items: "Should Prince put this in his BunDex?" / "Do ya think this'll come up again?"
  - The options are: Put it in the BunDex / Keep it with this adventure / Probably not important / Prince can decide.
  - Ignoring the question keeps the items.
- **7e. "Remember this, Prince" and "That wasn't important".**
  - Explicit commands: the first strongly promotes the surrounding context, the second demotes it.
  - Local hard rules that always count as significant: an explicit "Remember this", Treasured status, boss defeat, credits, Watchbun task completion, and Boss's corrections.
- **7f. Treasured memories.**
  - Assigned by Boss, or by exceptionally strong evidence.
  - They never decay and are always readily recalled.
  - This is item 4's Cherished layer: "hugging the notepad — a memory has become treasured".
- **7g. Learned significance.**
  - Prince slowly learns what matters to the two of them, from:
    - memories Boss responds to warmly when recalled;
    - explicit "remember this";
    - subjects Boss keeps investigating or discussing;
    - comments that led to real conversation;
    - memories Boss corrects, promotes, or pins.
  - It is never learned from silence. Visual spectacle does not automatically equal importance. Shared reactions strongly raise significance.
- **7h. The recall-priority ladder and the adventure lifecycle** (item 4).
  - The ladder: Treasured → Significant → Adventure context → Session detail → Routine aggregate.
  - The lifecycle: Active → Freshly Finished → Remembered → Adventure Record, plus Treasured Episodes.
  - Returning to a retired save reactivates its record and brings relevant detail forward.
- **7i. Remembering feels like remembering.**
  - Memories are actually supplied to his looks and conversations (item 3).
  - It is sparing: a relevance threshold, a cooldown, and rarity, so he does not recite history at every match.
  - Partial recollection is charming and honest: "We found something important here... was it behind the waterfall?"
- **7j. The Lore Notebook.**
  - Game knowledge is built gradually. Each note has a source: Observed, Read, Told by Boss, Prince suspects, or Confirmed.
  - Captured text is marked as exact, near-verbatim, or Prince's recollection.
  - Prince can say "Prince thinks the inscription said...", and one dishonest NPC never becomes canon.
- **7k. Save identity and new adventures.**
  - Memories are tagged with game, save or run, date, characters, importance, confidence, and whether he witnessed something or was told it.
  - When unsure: "Is this a new adventure, or are we continuing our old one?"
  - At an ending (credits, finale back to the title screen, and similar): "Did our adventure finish?" then "Wanna begin a new adventure?"
  - It is never inferred from Boss "seeming bored".
  - Old-save spoilers stay tucked away until the current save reaches them.
- **7l. Opinions and beliefs that grow.**
  - Opinions and theories are grouped by subject and evolve.
  - Corrections and changes of heart are appended beside the earlier belief, never rewritten.
  - Later, validated understanding takes precedence in recall, while earlier theories are kept. `BeliefHistory` exists in Recall and is unused.
- **7m. Bnuy No Write These Down.**
  - Companionship continues with no durable memories.
  - Only the minimum conversation context is held in RAM, and it is discarded when the conversation or session ends.
  - The UI says so clearly.
- **7n. Never a dossier.**
  - BIC and conversation memories are records of what Prince wondered about and what the two of them explored together, never a profile of Boss.
  - Personal memories stay visible to Boss, and Boss removes them case by case (item 5).

## 8. The living Bnuy around the memory (Boss, 2026-10-12: same request)

These touch Stage 13's personality. **Here** means the plumbing and contracts are built in this round. Prince's actual personality text, voice, and validator rules are written and tuned in Stage 13 unless Boss pulls them forward.

- **8a. Every request carries who Prince is.** Every API request carries the relevant parts of:
  - the immutable Prince specification;
  - the current attention and conversation state;
  - applicable memories and opinions (item 3);
  - the game and save context;
  - source and uncertainty rules.
  
  A model change then changes only the thinking engine, never Prince. **Here:** the slot and its assembly. **Stage 13:** the specification text ("IMMUTABLE — THE BUN REMAINS THE BUN").
- **8b. A reserved inherent-interest pool.**
  - The fixed local roots: round things, small things in large worlds, unfamiliar places, what objects are for, treats, naps, comfy places, courage when little, friendly-or-not creatures, mysteries, maps, doors, ruins, peculiar human things, paws, Bun powers.
  - Seed generation always reserves room for inherent thoughts, learned interests, current-game interests, and a little novelty, so optimization never sands his personality down.
  - `InterestRoots` exists and is unused (item 6).
- **8c. The character validator.** A local check rejects or regenerates an out-of-character response before anything is shown or committed. **Here:** the hook. **Stage 13:** the rules.
- **8d. BIC thoughts, the BIC clock, and Brain Farts** are wired at runtime (item 6).

## 9. (next item from Boss)
