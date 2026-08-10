# Companion Core — Direct Build Packet

The filename is retained for stable historical links. This packet no longer assigns work to Claude or a separate foreman; `Direct-Build-Workflow.md` governs execution and review.

## How to use this packet

Build the local Windows companion engine through one bounded active task and one Paw Gate at a time. Codex performs implementation and a distinct evidence-based gate pass under the authority boundaries in `AGENTS.md` and `Direct-Build-Workflow.md`.

Do **not** implement this entire packet in one pass. Work on exactly one explicitly assigned task at a time. After implementation, produce the required handoff and conduct the current candidate's Paw Gate. Do not begin the next task until that gate is recorded as passed.

This packet covers the utilitarian core only. Do not add a character personality, character voice, themed labels, final artwork, animation, audio capture, microphone access, polished UI, or decorative behavior. Use neutral placeholders and typed behavioral events. A separate presentation/personality layer will be installed and validated on Builder Prince after the core is accepted.

## Product goal

Build a local-first Windows desktop companion engine that can, with explicit consent:

1. attach to one chosen application;
2. capture only that application's visual output;
3. detect and semantically interpret relevant visual changes;
4. maintain adaptive attention states;
5. offer ambient observations and one continuous conversation thread;
6. keep append-only, autobiographical local memory across sessions;
7. watch an authorized background application without observing the foreground application;
8. recover safely from crashes and API outages;
9. protect privacy and bound resource usage;
10. remain one continuous local identity regardless of API session, game, save, restart, or update.

The first interface is only a blank text box, placeholder icon, status text, and the minimum controls required to test the engine.

## Non-negotiable invariants

These are architecture contracts, not preferences.

### One runtime and one identity

- Exactly one local companion runtime is authoritative.
- A conversation window, game, save, API request, model session, or restart must never instantiate another companion identity.
- Identity and continuity live locally, never in an API session.
- Development and production use physically separate data roots.
- Development/test builds must not open the production data root accidentally.
- The eventual one-time production awakening occurs only after the complete core, personality, presentation, and launch candidate pass. It starts a clean production memory store; development memories never transfer automatically.

### One conversation thread

- At most one active conversation thread exists.
- Game-related conversation outranks independent conversation seeds.
- Ambient observations never become a conversation without explicit user interaction.
- An explicit user lock prevents automatic replacement or settling of a thread.
- Non-response is neutral: it must not change interests, preferences, relationship state, or sentiment.

### Local memory authority

- All durable memory is local.
- The API is stateless from the application's perspective.
- Every API request receives a locally generated context packet.
- API outputs are proposals that pass through a local allowlisted write gate.
- Automated/API operations may append but may not update or delete committed memories.
- Corrections, changed opinions, summaries, recurrence, and supersession are appended as linked records.
- Later validated understanding is retrieved ahead of superseded conceptions, but originals remain.
- Resource cleanup must never delete, rewrite, or compact away committed memory.

### Consent and target isolation

- No capture occurs before explicit authorization, except where the user has explicitly configured standing authorization.
- Only one selected application/window target may be captured.
- Tabbing away must not change the capture target.
- Unrelated windows, notifications, tabs, and foreground applications must never enter the capture stream.
- Browsers are denied by default unless a safe, explicit tab-specific integration exists.
- A stop-only global privacy hotkey (`Ctrl+Shift+F12`) immediately stops capture, cancels pending visual work where possible, clears image buffers, pauses memory writes, and requires explicit UI interaction to resume.

### Screenshot lifetime

- Ordinary screenshots are RAM-only.
- Use a fixed-capacity byte-bounded ring.
- Raw frames, crops, attention sheets, streams, GPU/native resources, and failed request payloads must be disposed deterministically.
- Durable screenshots are not part of the initial core stages.

### Failure behavior

- Session text/events are journaled incrementally.
- Writes and checkpoints are transactional and restart-safe.
- API outages cannot erase local state.
- Retries are bounded, cancellable, and idempotent.
- A failed remote operation cannot duplicate a local append.
- Capture and semantic work should be isolated so a worker can restart without creating another runtime identity.

## Explicit non-goals for the core

Do not implement:

- final character prompts or language style;
- character-specific names, catchphrases, or themed UI;
- final animations or sprite integration;
- audio/game-sound capture;
- microphone or voice-chat access;
- multi-monitor behavior beyond a safe one-monitor guard;
- dedicated game mods/plugins;
- browser-tab capture without an approved integration;
- distribution, updater, or multi-user sync;
- cloud memory storage or remote resume capsules;
- model auto-switching;
- final numerical tuning before profiling.

## Required architecture boundaries

Use equivalent names if the selected language/framework has strong conventions, but preserve these responsibilities.

### `CompanionRuntime`

Owns the single identity, lifecycle state, active application session, attention state, conversation state, and orchestration. No UI window or worker may create a second authoritative runtime.

### `PresentationAdapter`

Provides placeholder visible strings, labels, and typed expression intents. Core services emit semantic events such as `observing`, `investigating`, `urgent`, `encouraging`, `taking_note`, `privacy_paused`, or `recovering`; they do not contain final character wording.

### `TargetAuthorizationService`

Tracks recognized/unknown/denied/standing-authorized executables, permission state, one target identity, and target-only metadata access.

### `CaptureWorker`

Captures only the authorized target, builds frames/crops, owns native visual resources, and can restart independently.

### `VisualPipeline`

Performs local change detection, duplicate rejection, normalized region handling, attention-sheet composition, buffer/queue bounds, and focus nomination.

### `AttentionEngine`

Maintains Noticing, Engaged/Investigating, High Attention, and Afterglow. Scoring incorporates novelty, habituation, urgency, salience, persistence, corroboration, personal/lore relevance inputs, confidence, and decay. Numerical weights remain configuration values.

### `ConversationCoordinator`

Owns exactly one thread, priority, locks, settling, interruption, ambient-to-conversation promotion, and seed eligibility. It must enforce neutral non-response.

### `MemoryStore`

Append-only committed event store plus derived indexes. Supports sources, confidence, scopes, recurrence, corrections, supersession, grouped beliefs, retrieval priority, and references from summaries to original records.

### `SessionJournal`

Crash-safe incremental event/checkpoint log. Recovery is deterministic and duplicate-safe.

### `ApiBridge`

Builds stateless requests from local context, uses protected credentials, bounds retries, assigns local operation IDs, validates structured responses, and never owns identity or durable memory.

Define the semantic service behind an interface from the first skeleton. Provide at least:

- `MockSemanticProvider` for deterministic scripted responses;
- `ReplaySemanticProvider` for sanitized recorded request/response fixtures;
- a disabled real-provider contract whose live adapter is added only at the final core gate.

No automated test may require paid API access. Missing credentials must be a supported development state, not a startup failure.

### `LocalWriteGate`

Allowlisted append operations only. Explicitly reject update, delete, overwrite-store, replace-checkpoint, or source-metadata mutation proposed by automated/API code.

### `PrivacyGuard`

Operates before semantic interpretation for authorized non-game targets. It must be high-threshold and independently testable. Trusted game targets may explicitly bypass content-level filtering while target isolation remains enforced.

### `ResourceWatchdog`

Tracks memory, frame count, queues, native handles, GPU resources when available, active requests, and sustained growth. It may clear disposable work, restart workers, pause processing, or stop capture. It may never modify committed memory.

### `BackupRecoveryService`

Builds one atomic validated compressed full backup with manifest/checksums. Never replace a valid backup with output from an unhealthy source store. Restore must preserve the damaged source for diagnosis and replay valid later journal entries.

## Data-model minimums

Use versioned schemas from the beginning.

### Durable memory record

Minimum fields:

- immutable record ID;
- schema version;
- created timestamp;
- memory scope (`session`, `save`, `game`, `general`);
- source kind (`observed`, `read`, `told`, `remembered`, `inferred`, `guess`, `user_correction`, `integration`);
- confidence;
- subject/entity references;
- application/game/save/session references;
- visible recollection payload (placeholder-neutral during core development);
- structured retrieval metadata;
- links to source records;
- supersedes/corrects/recurs-with relationships;
- local operation ID;
- committed flag/checksum as appropriate.

### API operation record

- local operation ID;
- operation kind;
- request timestamp;
- local checkpoint ID;
- context references supplied;
- response status;
- retry count;
- proposed appends;
- commit result;
- no raw API credential or sensitive rejected-frame data.

### Attention event

- target/session ID;
- timestamp;
- normalized region;
- novelty/change/salience/urgency/persistence/corroboration/confidence inputs;
- inferred topic/event identity if available;
- evidence/source references;
- deduplication/global-transition identity;
- current attention state.

## Stage task sequence

The reviewer will assign one task below at a time. Do not self-advance.

### Task 0 — Repository survey and architecture proposal

Do not implement product behavior yet.

Produce:

- current repository inventory;
- viable Windows stack recommendation with alternatives and tradeoffs;
- capture technology options and limitations for occluded/minimized/fullscreen windows;
- local database/journal recommendation;
- test and packaging strategy;
- module dependency diagram;
- proposed directory structure;
- risk register;
- staged dependency map;
- explicit confirmation that the non-goals and invariants are understood.

Acceptance: reviewer approves the stack and module contracts before Task 1.

### Task 1 — Reproducible skeleton

Implement:

- buildable Windows application;
- blank text box and placeholder icon;
- one `CompanionRuntime` registered exactly once;
- lifecycle start/nap/wake/stop states;
- neutral `PresentationAdapter`;
- structured local diagnostics behind an explicit switch;
- automated test command and CI/local script where appropriate.

Acceptance tests:

- clean checkout builds;
- app launches without API key, network, or capture;
- multiple windows cannot create multiple runtimes;
- clean shutdown leaves no worker/process behind.

### Task 2 — Append-only memory and journal

Implement:

- versioned `MemoryStore`;
- append-only `LocalWriteGate`;
- `SessionJournal` and transactional checkpoint;
- recurrence, correction, and supersession relationships;
- retrieval favoring later validated understanding;
- developer/production data-root separation;
- synthetic test fixtures only.

Acceptance tests:

- update/delete proposals are rejected;
- kill during append and recover prior records;
- retry an operation and commit at most once;
- retrieve a correction ahead of its superseded interpretation;
- dev build cannot open the production path through normal configuration.

### Task 3 — Atomic backup and repair

Implement:

- one complete compressed backup archive;
- manifest, checksums, schema version, and health validation;
- atomic temporary-build/validate/replace sequence;
- restore with damaged-source preservation;
- replay of valid post-backup journal entries.

Acceptance tests:

- invalid new backup cannot replace valid old backup;
- corrupt live store restores from archive;
- interruption during backup leaves previous archive valid;
- committed memories survive repair exactly.

### Task 4 — Consent and target isolation

Implement:

- executable/window discovery without capture;
- four authorization categories: familiar-ask, unknown-ask, denied/sleep, explicit standing authorization;
- one authorized target;
- stop-only privacy hotkey;
- target status display;
- no notification/unrelated-window metadata collection.

Acceptance tests:

- zero frames before authorization;
- denied target produces no capture;
- foreground changes never change target;
- emergency stop clears buffer and requires explicit resume;
- one-monitor guard prevents unexpected display expansion.

### Task 5 — Bounded capture worker

Implement:

- isolated restartable target capture;
- 64 MB byte-bounded ring;
- no more than three source frames;
- bounded processing queue;
- deterministic native-resource disposal;
- resize/minimize/render-stall behavior;
- metrics for memory, handles, queues, and frame lifetime.

Acceptance tests:

- only authorized test target appears in output;
- ring and queue never exceed limits;
- repeated worker restart does not create a second runtime;
- multi-hour synthetic capture does not show sustained handle/memory growth.

### Task 6 — Regions and attention sheets

Implement:

- initial orientation frame after consent;
- normalized default regions;
- manual region override;
- staggered regional capture;
- synchronized source extraction;
- labeled attention-sheet composition;
- local motion/change scoring and duplicate-frame rejection;
- resolution/scaling remap.

Acceptance tests:

- region alignment survives supported resize/scaling changes;
- synchronized crops correspond to one source moment;
- global frame and high-detail crops are correctly labeled;
- raw/intermediate images are disposed after use.

### Task 7 — Stateless API bridge

Implement:

- protected credential-storage interface and an unconfigured development state;
- local context/resume packet builder;
- structured request/response schemas;
- unique local operation IDs;
- bounded retry/backoff/cancellation;
- idempotent response processing;
- local allowlisted append proposals;
- neutral packaged outage/limit message and processing pause;
- no remote persistent memory or resume capsule.
- deterministic mock and replay providers that exercise the same request/response contracts without credentials;
- graceful configuration that keeps the application usable in development when no real key exists.

Do not make live paid API calls in this task. A contract-complete disabled real-provider shell is sufficient. Real credentials, live calls, and provider-specific validation are reserved for Task 12 after all preceding local/mock gates pass.

Acceptance tests:

- fresh remote session receives continuity from local packet;
- timeout/retry cannot duplicate output or memory;
- malicious/invalid update/delete proposal is rejected;
- synthetic credentials never appear in logs, backups, or diagnostics;
- outage preserves local state and stops repeated requests.
- the complete bridge contract, retry logic, write gate, and recovery tests pass using the mock provider with no network or key;
- selecting the real provider without credentials produces a neutral unavailable state rather than a crash.

### Task 8 — Attention engine

Implement configurable:

- attention states and hysteresis;
- interest accumulation/decay;
- decisive-event bypass;
- weak-evidence corroboration;
- global-transition deduplication;
- novelty/habituation;
- adaptive Afterglow;
- false-alarm correction;
- typed expression intents only.

Use deterministic synthetic event streams for tests. Do not tune against personality.

Acceptance tests cover weak decay, corroborated escalation, immediate major-event escalation, loading-screen deduplication, habituation, urgent familiar danger, and Afterglow.

### Task 9 — Conversation coordinator and seed banks

Implement:

- exactly one thread;
- priority and lock rules;
- Game Observation and independent-conversation Seed Banks;
- separate eligibility clocks;
- ambient expression that requires explicit promotion;
- substantive-engagement qualification;
- settling and resumable seeds;
- urgent interruption;
- neutral non-response;
- presentation-attempt count without preference punishment.

Acceptance tests cover all priority combinations, locked-thread recovery, trivial engagement exclusion, three neutral presentations, interruption/resumption, and no negative learning from silence.

### Task 10 — Memory consolidation and retrieval mechanics

Implement personality-neutral mechanics for:

- session/save/game/general scopes;
- summaries referencing originals without deletion;
- grouped evolving beliefs/opinions;
- lore provenance;
- active/finished adventure records;
- spoiler-aware save retrieval;
- local relevant-context selection;
- user correction precedence;
- immutable local interest roots plus validated generated seed storage.

Visible prose remains placeholder-neutral. Final autobiographical voice is explicitly out of scope.

### Task 11 — Application-bound background continuity

Implement:

- background target monitoring;
- target-specific input/activity tracking;
- temporary watch tasks;
- first quiet-hour check and paused semantic spending;
- second quiet-hour safe closure;
- indefinite-watch override;
- close/crash distinction with user confirmation;
- paused-session/relaunch recovery;
- lock/sleep/wake suspension;
- non-focus-stealing neutral alerts.

Use a synthetic structured-event adapter, not a dedicated game mod.

### Task 12 — Resource hardening and core gate

Implement and document:

- resource watchdog actions limited to disposable data/workers;
- final measured thresholds based on prototype behavior;
- forced API/capture/disk/database/sleep/crash-loop failure tests;
- 8–12-hour soak test;
- privacy regression suite;
- database migration/rollback test;
- complete placeholder-interface end-to-end demonstration.
- real remote semantic-provider adapter and protected credential storage;
- first live semantic calls only after every local/mock regression gate passes;
- live response-contract, latency, cancellation, outage, and estimated-usage validation.

Core gate must demonstrate consent, target capture, attention escalation, ambient output, one conversation, append-only memory, background watch, session consolidation, restart continuity, archive recovery, privacy stop, real-API success and outage pause, and stable resources.

Stop this packet after this task. Do not install personality, begin final UI/animation work, or awaken Companion Prince under the neutral-core packet. The roadmap's later Builder personality and launch-validation stages require their own active packets.

## Task execution rules

For every assigned task:

1. Read this master packet and the task-specific acceptance criteria.
2. Inspect existing code and preserve unrelated/user changes.
3. State assumptions before making an architecture-affecting choice.
4. Add or update tests with the implementation.
5. Do not weaken an invariant to make a test pass.
6. Do not expand into a later task.
7. Run focused tests, then the full available regression suite.
8. Produce the handoff below, then perform the distinct Paw Gate review before advancing.

If blocked by an architectural contradiction, privacy risk, missing authority, or required invariant change, stop and ask Boss. Do not improvise a silent redesign. Routine reversible choices may use and record Prince's Personal Round Judgment as defined by the direct workflow.

## Required completion handoff

Return a concise report with exactly these sections:

### Task

Current task ID and objective.

### Completed

Concrete implemented behavior.

### Changed

Files/modules and why.

### Verification

Commands run and exact pass/fail results. Never claim unrun tests.

### Remaining

Anything incomplete or intentionally deferred.

### Risks and assumptions

Known issues, performance concerns, platform limitations, or architecture assumptions.

### Review focus

What the Paw Gate review should inspect most carefully.

### Repository state

Branch, commit/diff, and whether the worktree is clean.

### Next safe task

The smallest safe next action, without beginning it.

## Credit-aware stopping

Monitor available usage/context. If you are unlikely to implement **and verify** the current task before reaching the limit:

- do not start a large new edit;
- reach the safest available checkpoint;
- do not rush, weaken tests, or claim acceptance;
- leave the repository understandable;
- produce the required handoff early;
- explicitly say that the stop is credit-related.

The same checkpoint rule applies before any anticipated context or service interruption.

## Initialization history

The packet originally instructed the builder to execute Task 0 only before product code. Task 0 and its architecture approval are complete. This paragraph is historical context, not current authorization; the single packet in `tasks/active/` always determines the next action.
