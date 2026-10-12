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
- **To settle in the packet.**
  1. Whether Prince may *reorganize* (move a page into a new folder he invents later), and how that stays append-only. For example, he adds a new folder that points to the page, or the move is recorded so the original location is never lost.
  2. Whether Boss can edit or remove BunDex pages. The design conversation mentions "Forget This, Prince", a confirmed deletion by Boss only.

## 6. (next item from Boss)
