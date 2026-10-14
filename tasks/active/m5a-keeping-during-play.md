# M5a — Keeping during play: he decides, the BrainCase links

Status: **in progress**
Authorized: 2026-10-14 by Boss ("Do M5A, and keep everything I just said in mind"). Boss's decisions are pinned in the plan: "M5a also: he decides what is worth keeping", and "Three layers: Conscious, Metaconscious, Subconscious".
Accepted remote base: `8b25736bb7aabe3ca977d53d553911fb3227e7b9`
Working branch: `agent/m5a-keeping-during-play`
Builder: Claude
Plan items: 10, 13–15; "M5 also: keeping during play", "M5 also: what M4e hands to M5", "M5a also: he decides". The vision and the Little Self roundtable apply throughout.

## What exists today (code checked 2026-10-14)

- **The keeper** (M1c, M4a, M4b, M4e) works, but nothing calls it during play. The host composes no writer.
- **Memories** proposed by the eyes are sifted by novelty (M3a) and committed to the memory store. Patterns are noticed when they grow (M4e), but nothing offers them.
- **M1c's keep pull** tells him how worth keeping a moment is ("Prince feels this might be / is / is very much worth keeping"), and its strength depends on declines per kind of memory. Under Boss's decision, that is the subconscious weighing in, so it is replaced.
- **Pages carry hidden tags**, but nothing links a page to the memories or sightings behind it.

## Design

1. **What reaches him** (Subconscious → Metaconscious → Prince):
   - every memory newly committed from the eyes' looks, as "Prince saw…";
   - every pattern that grows into a new magnitude, as "Prince has seen this a few times / lots of times / so, so many times…".

   Each moment carries only what happened, where he is, any real feelings he has, his own pages that come to mind (M4e), and the urge to scribble when he has it. **Nothing tells him how worth keeping it is, and nothing mentions not writing.**
2. **He decides.** Whether to write, how much, and where are his (M1c). The stand-in writer earnestly writes about every moment until Stage 13.
3. **The urge to scribble comes from his nature.**
   - The keep pull is replaced by an urge that grows after a stretch of moments without his writing anything, regardless of kind. It relaxes when he writes anything.
   - No urge line at first, "Prince feels like scribbling in his notepad." after a few moments, and "Prince really feels like scribbling in his notepad." after more.
   - It is never about the particular moment.
4. **One moment at a time, never pushy.**
   - Moments queue in arrival order.
   - One is offered only while a session is active and current, no conversation thread is open, and attention is not in its high, intense state.
   - Each moment is kept off the orchestrator's critical path, one in flight.
   - The queue is bounded; the oldest moment fades when it is full, and moments fade after a while. Faded moments are still in his subconscious memory.
   - A privacy stop clears the queue and nothing is kept.
5. **The BrainCase links every page to its sources, in the hidden index only.**
   - Each page records the memory records ("memory:…") or the sighting pattern ("sightings:…") it came from, in its hidden tags and the ledger.
   - The index traces page → sources and source → pages, across adding to a page, moves, renames, a reopen, and a Vault rebuild.
   - Nothing he is given or can say reaches the links.
6. **The memory system is untouched.** Keeping only reads memory records; the store stays byte-identical. A damaged (read-only) BunDex keeps nothing, and a failure is contained.

## Acceptance

- A new memory during play is offered and kept, with its page linked to the memory record, traced both ways and across move, rename, reopen, and Vault rebuild.
- Thirty of the same thing during live play offer three keep moments, never thirty, and the pages keep the magnitude.
- Nothing he is given carries a worth verdict or mentions not writing. The template audit checks for it.
- The urge comes from moments without writing, of any kind, and relaxes when he writes.
- No moment is offered during a conversation thread or high attention, or after a privacy stop. The queue is bounded and moments fade.
- The wall test: no source link, record ID, tag, kind, or time reaches anything he is given.
- The memory store's records are byte-identical after keeping. A damaged BunDex keeps nothing. A keeping failure never breaks the session.
- No randomness in any changed assembly, and every existing suite passes.
- After the gate: the temporary emulated live Bnuy run, never committed.

## Deferred findings

_(none yet)_
