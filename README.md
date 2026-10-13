# Companion Coding

A staged, local-first Windows companion-engine project built through evidence-gated vertical slices.

Task 0's architecture, the R0 direct-build controls, Tasks 1–11, ERPP-01, KEEP-01, KEEP-02, WIRE-01, WIRE-02, AUDIT-01, the R2 Stage 4 recertification, R3 reliable orientation delivery, and R5 App test timing are accepted. Stage 5's stateless Braincase bridge (mock and replay only), Stage 6's neutral attention engine, Stage 7's conversation coordinator with its session transcript, Stage 8's memory consolidation and recall mechanics, and Stage 9's Watchbun continuity are complete. Stage 10's keepsakes and complete recovery (KEEP-01, KEEP-02) are complete. WIRE-01 composes every subsystem into one tested orchestration core, and WIRE-02 runs it in the real Windows App. Stage 11 local hardening (CAL-01–03) and the smaller deferred items (DEF-01, DEF-02) are complete. Boss accepted the agreed calibration values as the configuration, adjusted only on evidence. **Stage 12 is in progress:** Builder Prince's Braincase is Claude (LIVE-01), and the Finalized Bun will use ChatGPT. Each has his own BunDex.

## Builder workflow

1. Read `AGENTS.md`.
2. Read `docs/Direct-Build-Workflow.md`.
3. Read `docs/Neutral-Core-Task-Packet.md` (neutral-core authority).
4. Work on exactly the task in `tasks/active/`.
5. Use a dedicated `agent/task-XX-name` branch.
6. Run and report tests honestly.
7. Complete `tasks/review/HANDOFF.md` and perform a separate evidence-based Paw Gate review.
8. Advance only after the current gate is recorded as passed.

## Current checkpoint

Stage 5 is complete. **Task 7** (PR #21) added `CompanionCore.Api`, the stateless faraway Braincase bridge:

- strict versioned request and response contracts;
- deterministic mock and fixture-replay providers;
- a real-provider shell with no transport, so no live or paid call is possible;
- bounded, idempotent retries;
- privacy-generation fencing;
- a local append-only allowlist in front of the write gate;
- a local Resume Packet rebuilt for every request;
- packaged naptime with bounded probes that survives restart;
- a checksummed local bridge journal;
- RAM-only protected credentials.

The final head passed both Windows event gates at 453/453 and merged as `9b8d45f0939735a3281b06bec44af47518703a80`.

**R5** (PR #23) found why the App shutdown integration test was timing out: the first App launch's cold start was being charged against the shutdown exit bound. Startup and exit are now separately bounded phases, and the 30 s exit bound is unchanged. It merged as `3c078f58a46daf8f2f66e97592c39cf18d120f98`.

**Task 8** (PR #25) added `CompanionCore.Attention`, a deterministic attention engine:

- Noticing, Engaged, High Attention, and Afterglow states, with hysteresis;
- corroboration of weak evidence;
- immediate escalation for decisive events;
- deduplication of loading screens and other global transitions;
- habituation that never dampens urgent danger;
- adaptive Afterglow;
- false-alarm correction.

All of it is typed abstract intents with provisional configuration. It passed both Windows gates at 502/502 and merged as `3d63147a153d3e957ae97f2bff71ccf08ac0c5f6`.

**Task 9** (PR #27) added `CompanionCore.Conversation`, a deterministic coordinator:

- exactly one Conversation Thread, with game outranking initiated conversation and a player lock;
- independent Seed Banks on separate clocks behind one game-favored expression gate;
- ambient expression that becomes a thread only through explicit interaction;
- substantive-only seeding;
- urgent hold and resume;
- strictly neutral non-response.

It passed both Windows gates at 546/546 and merged as `0fe0932aaa54039fafd709e2cb120bda16fd7d6b`.

**ERPP-01** (PR #29) added `CompanionCore.Transcript`, a durable, session-scoped transcript of typed conversation events. From it Prince can reconstruct exactly what was being discussed before an interruption, without committed memory. It passed both Windows gates at 563/563 and merged as `7623b0e0f1f33734fed4cdc29ddbcb3b99c5737d`. With Task 9, Stage 7 is final.

**Task 10** (PR #31) added a bounded read-only `MemoryQuery` and `CompanionCore.Recall`: append-only session consolidation with verbatim highlights, adventure records, lore provenance, evolving beliefs, user-correction precedence, spoiler-aware local recall, and immutable interest roots. It passed both Windows gates at 621/621 and merged as `e529eb679a17dcc6059d2f83d33d850dc6a80c2f`. Stage 8 is complete.

**Task 11** (PR #33) added `CompanionCore.Watchbun`, a checkpointed engine that keeps one authorized target attached across tab-aways, quiet hours, exits, relaunches, and lock/sleep, plus a synthetic structured-event adapter. It passed both Windows gates at 687/687 and merged as `97ea0e411c3d9da29d7841d6a810a9fb4773c6e9`. Stage 9 is complete.

**KEEP-01** (PR #35) added `CompanionCore.Keepsakes`, the only durable-image path. Each write is paired with a visible camera action, admitted only for the authorized target and current privacy generation, compressed, verified on inspection, and deletable only on Boss's explicit request. It passed both Windows gates at 710/710 and merged as `05c5bbb7a6f9a0517a7b06a2abeb2becfcb45f08`.

**KEEP-02** (PR #37) added `CompanionCore.Vault`. Da Bun Vault now carries photographs, settings, and the active checkpoint beside the unchanged memory archive, and one recovery path restores everything and reports honestly. It passed both Windows gates at 744/744 and merged as `e59b46b809f19325b425614e7e62d78b2a9c2b80`. Stage 10 is complete.

**WIRE-01** (PR #39) added `CompanionCore.Orchestration`, which connects every accepted subsystem through one single-consumer mailbox:

- authorized capture and attention sheets;
- the Braincase bridge (mock or offline only);
- the attention engine and conversation coordinator;
- the transcript and memory;
- Recall consolidation;
- Watchbun continuity;
- keepsake photographs;
- the Vault, including Bnuy Repairs.

Composition exposed eight defects. Each was fixed with a regression test. WIRE-01 passed both Windows gates at 793/793 and merged as `be890e0e08cc55254e89d733e2645bac3ce6bd88`.

**WIRE-02** (PR #41) added `CompanionCore.Platform.Windows` and the App composition. The real App now runs the full neutral core with:

- target-only, hook-free Windows signals;
- one host on the development root (isolated roots in test mode);
- the offline Braincase;
- a bounded tick;
- neutral notice presentation.

Two real launches restore the same conversation lineage. It passed both Windows gates at 908/908 and merged as `111e15a2b51ee7691dc1acb7aa9971f7f2840ee6`.

**AUDIT-01** (PR #43) re-verified every accepted task against the wired `main`:

- five stressed full-suite runs;
- 420 mutants replayed or added (403 killed, the rest recorded equivalents);
- five test gaps closed.

No product defect was found. It passed both Windows gates at 929/929 and merged as `c1787e03070588e7a2d46af1dda95f42ccaff332`.

**CAL-01** (PR #45) began Stage 11. It enforces the cadence agreed in the design conversation:

- 1 s local capture spacing;
- Noticing 12 s or triggered;
- Engaged 6 s;
- a 3 s Bnuy Mode floor.

It also pins every agreed number as the running default. Merged as `a330a3ca5fcbcfbf67d63f51910b2233e0daf708`.

**CAL-02** (PR #47) added privacy-safe calibration recording (`--calibration-log`), a report tool, and the soak protocol in `docs/Calibration-Protocol.md`. Merged as `002aec598d9cae5730743e8f787fd27113085f26`.

**CAL-03** (PR #49) hardened failure modes:

- unreadable state is preserved and never silently replaced;
- the conversation lineage is kept;
- crash-loop, disk-full, rollback, and capture-fault tests were added.

It also published `docs/Known-Limitations.md`. Merged as `039a641fe5269d2b4c08650f775248df3b32c952`.

**DEF-01** (PR #51) consolidates long sessions completely, in idempotent parts. It also adds a verified single-file Vault export (`CompanionHost.ExportVaultAsync`). Merged as `d510add3cc9b07b4bb4279c4138f051abb516e25`.

**DEF-02** (PR #53) takes keepsake photographs from the authorized target's native frame, within every existing bound, and never uploads them. Merged as `98ae435ff71ddb75d4a9e847d7520cd82966146f`.

Stage 11 and the smaller deferred items are complete; the agreed calibration values are the accepted configuration, adjusted only on evidence.

**LIVE-01** (PR #56) adds the Claude Braincase adapter for Builder Prince. It is the only networking assembly, it is off by default, and every reply still passes the strict parser and the append-only allowlist. Merged as `eeed55cb93e13ef43240611171926d7d589a0159`. **FIX-01** (PR #57) keeps a photograph's notice from being coalesced away.

**LIVE-02** (PR #59) adds three things:

- Builder Prince uses the SDK's own sign-in, and stores no key.
- A dollars-per-day Bun Budget.
- A settings file that keeps live calls off by default.

Boss's one-time setup and first live test are in `docs/Builder-Braincase-Setup.md`. Merged as `f3115ef1087b9b80df65184201e601f84627fa05`.

**CORE-12** (PR #61) adds placeholder commands for every orchestrator action, plus `docs/Core-Prince-Milestone.md`, Boss's guided run of the ten Stage 12 milestone checks. Merged as `18b5215e3bc5810d76937465314a55ed57ba323a`.

**FIX-02** (PR #63) makes the orchestration test harness wait by wall clock instead of by poll count, and report bridge diagnostics if it ever times out. It fixes a crash-loop test that timed out on a loaded Windows runner. Test-only. Merged as `288c81f3373a6b2a170c655e1ade5590cb4e9da1`.

**VIS-01** (PR #65) is the first part of Boss's Vision upgrade, the Sniff:

- Every changed frame is read on Boss's PC within 100 ms, before any image work.
- Prince looks 4 to 10 times a second, depending on his attention.
- His attention moves at once, calmly: a looping animation habituates, and only understanding enters Bnuy Mode.
- RAM budgets were raised, so 4K works.

Merged as `2defb17a1ccda22cac3318098faf6890ab178d56`.

**VIS-02** (PR #67) adds the Glance, Prince's quick peripheral vision on Haiku. It adds to his attention without replacing his main focus: the detailed Inspect, the only source of memories. Both lanes share one dollars-per-day Bun Budget. `BraincaseCheck --live` now times Glances on Boss's PC. Merged as `da7ea8293c0fce7f46fe3aa86b45af4ea2429c9c`.

**VIS-03** (PR #69) adds the Text Sniff: Windows' built-in text reader looks at the dialogue band and the upper corners. New words perk Prince's ears and bring his next Glance forward. The words themselves never leave the capture worker; only "the words changed" does, through the same privacy admission. Merged as `86deb5f0e2e54c8b516567e5d80a1d2c65aed627`.

**VIS-04** (PR #72) completes the Vision upgrade with the Scene Sniff: a vision model built into the app (Google's SigLIP, Apache-2.0) that recognizes what kind of scene is on screen and notices when it becomes a different one. It runs on the GPU through DirectML, or on the CPU. Only how far the scene moved, and a word from a fixed neutral vocabulary, leave the capture worker. The model is fetched at build with a pinned digest, never committed. See `THIRD-PARTY-NOTICES.md`. Merged as `3567519c4d4cc827a7e7f25f0d0fa29c6e2e12f2`.

**ATT-01** (PR #74): Prince's Afterglow now stays while things keep happening. It keeps its adaptive 30 s to 3 min length, but each interesting event restarts the countdown, so a long quest or cutscene doesn't flip him back to "never mind". Merged as `4dcf04af2e6df9bff2d5556d2a86e1e060aa0480`.

**Stage 12 is complete: Boss gave the Core Bun Approval Hop on 2026-10-12** after the ten milestone checks passed on his PC with live calls on. Next: Boss's memory-quality changes (M1–M10), then Stage 13, personality installation on Builder Prince.

**M1** (PR #85) gives Prince two homes: his BunDex, an organic notebook of folders and pages he names himself, and the BrainCase's append-only ledger it is built from. Da Bun Vault backs up the ledger, and a Builder reset tool clears Builder Prince's development data on Boss's exact confirmation. Merged as `0202c7fd32390e73af69faf79a8410def91f2c24`.

**M1b** (PR #87) finishes the separation: every machine store (memories, keepsakes, state, transcripts, journals, the ledger) lives in the BrainCase, and the data root holds only the BunDex and the BrainCase beside Boss's consent and calibration folders. Older data moves in automatically, never copied or deleted. Merged as `96250752290e340f0d6ed60e4017e0cee28cd5aa`.

**M1c** (PR #89) makes the BunDex entirely Bnuy-built:
- Prince decides where everything goes from what he knows: where he is, his own notebook (including the lines he wrote about each place), and a few of his own pages the moment reminds him of.
- The BrainCase never assigns, counts, or orders his folders.
- Tidying happens only when he wants to, or when Boss invites it.

Merged as `c6013390299aaf12d360f20d28e78dd5a1dccf62`.

**M2** (PR #92) builds the curtain: an intermediate layer between Prince's subconscious and Da Bnuy.
- **Raw data stays below.** IDs, scores, and the eyes' machinery never reach him.
- **What reaches him.** He is given only "Prince saw", "Prince read «…»", "Boss said «…»", and "Prince remembers", his own pages, and where he is.
- **World text stays quoted.** Game text is quoted, never woven into his inner voice.
- **His words are checked.** Everything he says or writes passes a validator first.

Merged as `e272da26332351f9b3af7b2cc0268dd253ba01fc`.

**M3a** (PR #95) makes Prince remember like a living Bun, not a log:
- The BrainCase keeps a complete record of everything his eyes see.
- Only the first time, or a meaningful change, becomes a memory. Repeats grow one impression ("Prince saw this a few times").
- He gradually gets used to things, by thing and by kind. Familiar things feel new again after a long gap or in a new situation, and danger never fades.

Merged as `816bfeece03724f1dcc1940d4ed0c90696e45c00`.

## Important boundaries

- Neutral utilitarian core first; personality and final presentation later.
- Resettable Builder Prince validates both the core and the complete personality/launch candidate.
- Companion Prince awakens exactly once only after the entire launch-required build passes; no Builder memory transfers, and every later update preserves his continuity.
- Development data and eventual production data must remain physically separate.
- Durable identity and memory are local and append-only to automated/API systems.
- Capture requires authorization and remains restricted to one target.
- Live paid API use and credentials require Boss's explicit direction (given for Stage 12). Live calls run only on Boss's PC with his own key and budget; tests use mocks, replay fixtures, and a fake transport.

See the documents in `docs/` for the complete specification and roadmap.
