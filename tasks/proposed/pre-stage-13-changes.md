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
- **To settle when this becomes a packet.**
  1. Whether Prince's own voice comes now or in Stage 13. Stage 13 owns his voice, so the structure and a readable view could come first, with the wording swapped in later.
  2. What "open the BunDex" looks like for now: a plain readable page or file generated from the BunDex, or a window inside the App. The design defers a *decorative* browser, not a functional one.
- **Constraints to keep.** The BunDex stays local and append-only. Reworking how it is organized or shown never edits or deletes a committed memory. Existing Builder memories stay as they are, and new structure is added beside them.

## 3. (next item from Boss)
