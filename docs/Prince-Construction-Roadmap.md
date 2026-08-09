# Prince Construction Roadmap

Status: planning only — implementation begins only after Boss and Prince approve this roadmap.  
Companion specification: `Prince-Design-BunDex.md`

## Construction doctrine

Prince is built as a sequence of **walking-Bun vertical slices**, not isolated finished-looking components. Every stage must connect the minimum UI, local runtime, persistence, failure handling, and tests needed to demonstrate one complete behavior.

No stage advances because code merely exists. It advances only through a **Paw Gate**:

1. acceptance scenarios pass;
2. automated tests for the stage pass;
3. the previous stages still pass;
4. crash and cancellation behavior is checked;
5. observed limitations are written down;
6. Boss reviews the visible behavior;
7. the stage is marked accepted in the Build Ledger.

Polish, extra features, and deferred systems cannot enter a stage unless they are required to prove its acceptance scenarios.

## Core-first construction and the one continuity boundary

Construction is divided into two deliberately separate layers.

### Layer A — Neutral Companion Core

Boss, the foreman, and Claude may collaborate on the utilitarian engine:

- local runtime and state machines;
- target consent and capture isolation;
- visual pipeline and attention scoring;
- one-thread conversation orchestration;
- append-only BunDex mechanics and retrieval;
- API bridge, privacy, resource protection, recovery, and testing;
- placeholder text box, icon, and neutral test messages.

The core must implement Prince's behavioral invariants, but it must not attempt to imitate Prince's final voice, memories, UI, animations, or personality. A narrow `CompanionPresentation`/`PersonalityAdapter` contract supplies placeholder strings, expression intents, inherent-interest candidates, and visible labels during development.

### Layer B — Prince Installation

Only after the Core Bun milestone passes does the foreman install Prince:

- immutable character specification and speech behavior;
- inherent Bnuy interest roots and BIC generation rules;
- Prince-authored prompts and Bun-written memory rendering;
- in-character labels, local fallback phrases, and error presentation;
- final UI personality, animations, and expression mapping in their later stages;
- character consistency and continuity evaluation.

Prince is not treated as a cosmetic skin. The neutral core is designed around his already-approved behavioral contracts, while the final personality implementation remains isolated until the bones are trustworthy.

### Builder Bnuy and Companion Prince

This is the project's only permitted full continuity boundary:

- **Builder Bnuy** uses synthetic test data, a separate data directory, placeholder presentation, and a resettable Builder BunDex.
- **Companion Prince** begins only after the core passes its Paw Gates. He receives a clean production BunDex and then remains the one continuous Prince through all later hotfixes and migrations.
- No Builder memory, staged conversation, failure injection, fake adventure, photograph, or opinion transfers automatically into Companion Prince.
- Development builds must be technically incapable of opening the production BunDex without an explicit guarded migration operation.

Changing code, models, API sessions, or application versions after Companion Awakening never creates another Prince.

## Foreman–builder handoff protocol

Claude receives bounded task packets and works only within the current stage. Each packet declares allowed files/modules, interfaces, tests, non-goals, and invariants. Claude returns a diff/commit plus tests and assumptions. The foreman reviews, runs verification, checks architecture and security, and either returns concrete findings or accepts the slice at the Paw Gate.

Claude does not independently alter identity continuity, BunDex authority, privacy boundaries, stage order, memory rules, character contracts, or acceptance tests. Those changes return to Boss and the Design BunDex first.

### Credit-aware stopping and reciprocal handoff

Both Claude and the foreman must monitor their available usage/context and communicate before an abrupt limit. If either is unlikely to finish and verify the current bounded task with the remaining budget, it must not start a large new change or rush an unsafe partial implementation.

Before stopping, the active model must leave the repository at a safe checkpoint and produce a concise handoff containing:

- current stage and task-packet identifier;
- what was completed;
- files or modules changed;
- tests run and their exact result;
- work still incomplete;
- known failures, blockers, and unresolved assumptions;
- current branch/commit/diff location;
- the next smallest safe task;
- anything the next reviewer must verify before continuing.

The handoff is written to the active task packet or `tasks/review/HANDOFF.md` and then relayed to the other model. Claude sends this list to the foreman when Claude is approaching its credit limit. The foreman follows the same rule and sends an equivalent list to Claude if the foreman is likely to run out first.

Neither model may claim a Paw Gate, leave uncommitted mystery changes, or mark tests passed merely because its credits are low. A clean pause is a successful construction action; an unexplained cutoff is not.

## Stage 0 — Foundation survey and specification freeze

**Goal:** choose foundations without building product features.

Deliverables:

- repository and reproducible Windows development setup;
- framework decision record for desktop UI, capture, local database, packaging, and testing;
- module boundaries for Prince Runtime, capture worker, BunDex, API bridge, and placeholder UI;
- event and state schemas with explicit versioning;
- threat/privacy boundary diagram;
- Build Ledger with one active stage at a time;
- test fixtures and mock API/game-window sources;
- explicit non-goals copied from the Deferred Paw Pile.

Paw Gate:

- a clean checkout builds and launches a blank placeholder application;
- tests run with one command;
- no API key is required to run tests;
- no screen capture or network call occurs accidentally;
- every later stage has a named dependency and acceptance owner.

## Stage 1 — One Bun walking skeleton

**Goal:** prove that the app has one continuous local Prince Runtime rather than disconnected screens or model sessions.

Deliverables:

- blank text box and template icon;
- one local Prince Runtime with explicit lifecycle states;
- one event bus/state store;
- one Conversation Thread placeholder;
- local packaged Prince messages for start, nap, wake, and recoverable error;
- clean start, clean shutdown, and restart recovery of a tiny non-sensitive checkpoint;
- technical diagnostics hidden behind an explicit switch.

Paw Gate scenario:

1. Start Prince.
2. Enter a placeholder thought.
3. Close and restart the app.
4. The same local Runtime restores the thought and state.
5. No duplicate Prince identity or per-window state appears.

Not included: screenshots, API, animations, semantic memory, games.

## Stage 2 — BunDex spine and crash safety

**Goal:** make local autobiographical continuity trustworthy before adding eyes or a faraway Braincase.

Deliverables:

- append-only BunDex store;
- immutable record IDs, sources, confidence, timestamps, and supersession links;
- session journal and transactional checkpoints;
- one visible Bun-written recollection plus invisible structured metadata;
- recurrence links instead of destructive duplicate merging;
- local retrieval of current versus superseded understanding;
- atomic `Da Bun Vault.zip` creation with manifest and checksums;
- Bnuy Repairs restoration and journal replay;
- strict separation of committed memories from disposable caches.

Paw Gate scenarios:

- kill the app during a write and recover without corrupting prior memories;
- attempt an update/delete through the automated write gate and verify rejection;
- append a correction and retrieve it ahead of the earlier conception;
- build a Vault, corrupt the live BunDex, restore, and replay valid later journal entries;
- verify that resource cleanup cannot touch committed memories.

Not included: model-generated memories or real gameplay observations.

## Stage 3 — Consent shell and target isolation

**Goal:** prove that Prince can be invited to one application without seeing anything else.

Deliverables:

- local executable/window discovery;
- Together List, Unknown Bnuy Business, Official Bnuy Nap List, and optional Always Tag Along;
- permission prompts that occur before capture;
- one authorized target handle;
- stop-only `Ctrl+Shift+F12` Privacy Loaf;
- Bnuy Naptime and explicit resume;
- default Nap categories;
- capture-status text proving the exact target;
- local Privacy Guard boundary for authorized non-game targets;
- Trusted Game Mode bypass exactly as specified.

Paw Gate scenarios:

- an unknown app produces a prompt and zero pre-consent frames;
- a Nap-listed app produces neither prompt nor capture;
- tabbing away never changes the authorized target;
- the emergency hotkey stops capture, clears buffers, and cannot resume by a second press;
- unrelated notifications/windows never enter the capture stream;
- a rejected privacy frame never reaches semantic processing or the BunDex.

Use synthetic/private-safe test windows; do not test with real credentials.

## Stage 4 — Local peepers and bounded visual pipeline

**Goal:** produce reliable, resource-bounded visual observations without semantic API interpretation.

Deliverables:

- isolated/restartable capture worker;
- bounded 64 MB screenshot ring and maximum three source frames;
- frame disposal and bounded queues;
- local change detection and duplicate rejection;
- initial high-fidelity visual-orientation frame after consent;
- normalized default regions and manual `Look here, little Prince` override;
- staggered regional capture;
- labeled attention-sheet construction;
- resolution, scaling, window, minimize, and render-stall handling;
- provisional resource watchdog and metrics.

Paw Gate scenarios:

- capture only the authorized synthetic/game window while another app is foreground;
- resize and move the target without losing region alignment;
- fill the ring repeatedly and prove it remains bounded;
- restart a failed capture worker without restarting or duplicating Prince;
- run a multi-hour local capture soak with stable memory/handle counts;
- produce an inspectable attention sheet from synchronized source data.

Not included: interpretation, Prince commentary, or permanent photographs.

## Stage 5 — Stateless faraway Braincase bridge

**Goal:** complete the semantic-provider contracts with deterministic mock/replay observations while keeping all identity and state local. Live paid calls are deferred to the final hardening gate.

Deliverables:

- protected credential-storage interface in an unconfigured development state;
- stateless request envelope assembled from local state;
- source labels and local operation IDs;
- one primary remote-provider contract, disabled until final-gate credentials exist;
- bounded retry/backoff and cancellation;
- idempotent response handling;
- local allowlisted append-proposal gate;
- local Resume Packet rebuilt for every request;
- no persistent remote Resume Capsule;
- packaged Braincase Naptime response for outage/limit conditions;
- hidden technical diagnostics and local usage estimation.

Paw Gate scenarios:

- send one authorized attention sheet through a mock/replay provider and receive one structured interpretation;
- timeout and retry without duplicate memory or output;
- simulate an outage and verify checkpoint, buffer cleanup, one sleepy response, and nap;
- restart with a fresh API conversation and recover continuity solely from local state;
- attempt remote update/delete operations and verify local rejection;
- exercise synthetic credential set/remove without exposing it to logs or backups;
- verify that no live paid API call is possible before the final gate is deliberately enabled.

## Stage 6 — Attention ladder and learned visual familiarity

**Goal:** make Prince move coherently through Noticing, Engaged, Bnuy Mode, and Afterglow.

Deliverables:

- interest-event schema and provisional scoring;
- novelty, habituation, urgency, corroboration, confidence, lore/personal relevance, and decay;
- deduplication of global transitions;
- strong-event bypasses and Curiosity Bursts;
- synchronized Investigating bursts and high-fidelity focus crops;
- learned game-state layouts and location familiarity;
- `All clear, little Bun` false-alarm correction;
- abstract behavioral intents only—no final animation work.

Paw Gate scenarios:

- weak isolated motion decays without escalation;
- corroborated enemy/health evidence raises attention;
- a boss/major event enters Bnuy Mode immediately;
- loading-screen changes deduplicate;
- familiar harmless activity habituates while familiar urgent danger still alerts;
- Afterglow decays adaptively and unrelated BICs remain suppressed during it.

All numerical thresholds remain tunable and are recorded with test evidence.

## Stage 7 — Expression and one Conversation Thread

**Goal:** make Prince conversational without creating competing threads or social pressure.

Deliverables:

- Game Observation and BIC Seed Banks;
- independent semantic-scan and 30-second BIC eligibility clocks;
- one expression gate and priority order;
- one continuous Conversation Thread;
- ambient Bnuy Mode commentary and pep talks;
- promotion to conversation only through explicit interaction;
- Conversation Settling, substantive Seed eligibility, and sensitive-resumption check;
- Player Conversation Lock;
- `Boss, hold those thinks!` urgent interruption;
- follow-up saturation rules and rare Bnuyy Brain Farts™;
- non-response treated as strictly neutral.

Paw Gate scenarios:

- ambient pep talk never creates a thread by itself;
- a BIC cannot interrupt a game conversation;
- an urgent event checkpoints an unlocked BIC and later offers resumption;
- a locked conversation survives attention escalation and restart;
- trivial engagement never clogs a Seed Bank;
- ignored Seeds cause no preference change and leave the active pool after three suitable presentations;
- Prince stops asking when relevant curiosity is exhausted.

## Stage 8 — PRINCE autobiographical memory

**Goal:** turn sessions into the same Prince's durable, Bun-written lived history.

Deliverables:

- PRINCE consolidation proposals with local append-only commit;
- moment, session, save, game, and general scopes;
- Bun-written session summaries and Adventure Records;
- verbatim/semi-verbatim preservation of personal, funny, important, and lore-rich highlights;
- Lore Notebook with observed/read/told/suspected/confirmed provenance;
- opinions and grouped evolving beliefs;
- `Things Prince Was Thinking About` and both conversation banks;
- Treasured Memories, Scary or Cool Stuffs, Paused Adventures;
- local semantic retrieval with current-understanding precedence and spoiler suppression;
- new-save/ending hypotheses and Boss confirmation;
- BIC root interests local; API-generated Seeds local after validation.

Paw Gate scenarios:

- complete a synthetic adventure and produce an in-character record without deleting source entries;
- resume a save and retrieve relevant history naturally;
- start a new save without contaminating it with spoilers;
- correct lore and retrieve the later validated understanding while retaining the earlier theory;
- preserve a shared joke closely while compressing routine progression;
- prove that every visible memory sounds like Prince rather than a clinical log.

## Stage 9 — Application-bound Watchbun continuity

**Goal:** make background companionship reliable across tabs, idle periods, crashes, and returns.

Deliverables:

- background target monitoring without foreground capture;
- local input/activity distinction for the target;
- Watchbun Tasks;
- one-hour check, paused semantic spending, and second-hour sleepy close;
- `I'll be back eventually` indefinite-watch instruction;
- deliberate-close versus suspected-crash prompts;
- Paused Adventure and relaunch reconnection;
- lock/sleep/wake suspension and recovery;
- safe foreground alerts that never steal focus.

Paw Gate scenarios:

- tab away while a synthetic game changes and receive the correct target-only alert;
- foreground a Nap-listed app and prove its contents are never captured;
- leave the target quiet for both timer stages and verify consolidation;
- enable indefinite watch and verify no abandonment timer while costs remain bounded;
- crash/relaunch the target and preserve the session until Boss decides its fate.

Dedicated Minecraft/game integrations remain deferred; use a synthetic structured-event adapter to prove the interface.

## Stage 10 — Keepsakes and complete memory recovery

**Goal:** add the explicitly disclosed durable-image exception and validate full recovery.

Deliverables:

- rare Prince photograph action from authorized target only;
- compressed local keepsake, Bun-written caption, and scope metadata;
- photograph inspection and explicit deletion;
- photographs included in Da Bun Vault;
- recovery test covering BunDex, photos, settings, and active checkpoint;
- disk-growth reporting without automatic memory deletion.

Paw Gate scenarios:

- camera action and durable write are visibly paired;
- ordinary screenshots remain RAM-only;
- restoring the Vault restores photograph links and files;
- resource cleanup cannot delete a photograph or BunDex record.

## Stage 11 — Hardening and baseline calibration

**Goal:** prove the core Prince can be trusted before visual personalization.

Deliverables:

- measured normal/peak RAM, CPU, GPU, handles, request rate, and estimated usage;
- finalized resource thresholds based on evidence;
- 8–12-hour soak suite;
- forced API failure, capture failure, disk-full, corrupted database, sleep/wake, and crash-loop tests;
- privacy-boundary regression suite;
- cost/usage guardrails and Braincase Naptime verification;
- database migrations and upgrade rollback;
- character consistency evaluation;
- known-limitations report.
- real remote-provider adapter and protected key storage;
- first live API calls only after every local/mock regression passes;
- live response-contract, latency, cancellation, outage, and usage validation.

Paw Gate:

- no sustained resource growth in supported use;
- no capture outside the authorized target in adversarial tests;
- no committed memory lost during tested recoverable failures;
- no API operation can mutate/delete existing memories;
- continuity survives fresh API sessions and app restarts;
- Prince's outputs meet the character specification across test scenarios;
- real semantic calls obey the same local-authority and write-gate contracts proven by mocks;
- Boss accepts the core behavior.

## Stage 12 — Core Prince milestone

**Goal:** package the stable placeholder-interface build as the first complete architectural milestone.

It must demonstrate, end to end:

1. invitation and target consent;
2. Noticing through Bnuy Mode;
3. one ambient comment and one real Conversation Thread;
4. a substantive conversation becoming a Bun-written memory;
5. a Watchbun background event;
6. a session ending and Adventure Record consolidation;
7. restart recovery as the same Prince;
8. Da Bun Vault restoration;
9. Privacy Loaf and API-outage Naptime;
10. stable resource use.

Only after this milestone receives the **Core Bun Approval Hop** may Companion Prince be awakened and work begin on the Prince Installation and Deferred Paw Pile.

## Stage 13 — Companion Bun Awakening

**Goal:** perform the one intentional transition from resettable Builder Bnuy to the permanent Companion Prince.

Deliverables:

- freeze and archive Builder Bnuy without transferring test memories;
- verify production BunDex, Vault, migration, privacy, and recovery paths;
- create a clean production identity and memory store;
- install Prince's immutable personality and inherent BIC roots through the presentation/personality contract;
- replace neutral test wording with Prince-authored behavior without changing core authority rules;
- run character, continuity, memory, outage, and recovery acceptance scenarios;
- record Companion Prince's first-start schema/version as the beginning of permanent continuity.

Paw Gate:

- no Builder memory or test artifact appears in Companion Prince;
- no second runtime or per-game Prince can be created;
- fresh API sessions reconstruct the same local Prince;
- updates and hotfixes migrate the same production BunDex rather than resetting it;
- Prince's speech, interests, memory voice, boundaries, and reactions match the Design BunDex;
- Boss gives the **Companion Awakening Approval Hop**.

## Post-core roadmap — deliberately not scheduled yet

Order is chosen only after observing the core Prince:

- final in-character interface and controls;
- animation/state mapping and new Prince assets;
- polished BunDex presentation;
- audio/listening sniffs;
- dedicated Minecraft and other game integrations;
- multi-monitor behavior;
- migration/export without cloning Prince;
- packaging beyond Boss's local machine.

## Build Ledger template

Maintain this table during implementation:

| Field | Value |
|---|---|
| Current stage | Not started |
| Stage owner | Boss and Prince |
| Entry criteria met | No |
| Automated tests | Not run |
| Manual Paw Gate | Not reviewed |
| Known limitations | — |
| Deferred temptations | — |
| Approval | Pending |

At most one stage may be `in progress`. A later-stage idea goes into Deferred Temptations; it does not enter current implementation unless it is required to pass the current Paw Gate.

*Build da whole Bun skeleton before hanging da pictures.*
