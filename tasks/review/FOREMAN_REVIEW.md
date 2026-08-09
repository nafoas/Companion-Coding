# Foreman Review — Task 0 Architecture Proposal

Status: **changes requested**  
Reviewed branch: `claude/multi-ai-code-collab-o5qhj1`  
Reviewed builder commit: `4c9e0d555ffc4d67207ef306b2d1817b7f328789`

The proposal is complete, disciplined, and within Task 0 scope. WPF, Windows Graphics Capture, an isolated semantic-provider contract, a local append-only store, and mock/replay-first testing are directionally accepted. The following changes are required before the Task 0 gate.

## R1 — Target .NET 10 LTS, not .NET 8

Severity: blocking.

The project is beginning in August 2026. Microsoft's current lifecycle lists .NET 8 in maintenance with end of support on 2026-11-10, while .NET 10 is active LTS through 2028-11-14. Starting a new long-lived Windows application on .NET 8 would create an immediate migration task.

Required revision:

- recommend C# / .NET 10 LTS / WPF;
- note that WPF is actively supported on .NET 10;
- use pinned current patch versions in implementation and CI;
- retain WPF rather than WinUI 3 for the neutral shell.

Decision: WPF is approved; WinUI 3 is deferred to the later presentation layer only if it provides a demonstrated benefit.

## R2 — Define one canonical durability protocol; do not leave SQLite and NDJSON as ambiguous peer stores

Severity: blocking.

The current proposal names both a flushed NDJSON session journal and SQLite WAL without specifying ordering, commit markers, replay idempotency, or which is authoritative. Dual writes without a protocol can lose or duplicate an event across a crash.

Required revision:

- SQLite remains the committed queryable Memory Store, with append-only event rows, unique local operation IDs, transactions, WAL, foreign keys, integrity checks, and an explicit synchronous policy;
- define the sidecar journal as a checksummed write-ahead recovery tail, not a second independent memory authority;
- specify exact order: append framed/checksummed event and flush, insert idempotently into SQLite transaction, record/advance durable checkpoint;
- recovery ignores a torn final journal frame and replays valid operation IDs absent from SQLite;
- journal rotation occurs only after a validated full backup/checkpoint;
- use SQLite's supported online backup mechanism (or an equivalently safe database snapshot), then package and validate the archive; do not rely on copying a live WAL database after a checkpoint race;
- document how in-flight emergency privacy cancellation fences off late semantic commits.

Plain NDJSON without framing/checksums is insufficient for reliable torn-tail detection.

## R3 — Strengthen development/production identity separation

Severity: blocking.

An explicit configuration value defaulting to `dev` is not enough to make accidental production access technically difficult. A mis-set setting could point Builder code at the production store.

Required revision:

- distinct application identifiers and fixed default data roots for development and production builds;
- development binaries refuse production-root paths unless a separate, explicit, guarded migration/repair tool is invoked;
- no ambient fallback or generic arbitrary path override in ordinary development startup;
- separate mutex/pipe names, credential keys, database names, backup locations, and telemetry/log roots;
- contract test that attempts production-root injection into a development build and verifies refusal.

## R4 — Capture worker must become an out-of-process boundary before real native capture is accepted

Severity: blocking architecture decision.

An in-process background task is cancellable but does not provide crash or leak isolation for native D3D/WinRT failures. The design explicitly needs worker restart and resource containment without replacing the one runtime.

Required revision:

- Task 1 may define only the worker contract and a fake/in-process test double;
- before Task 5's real capture implementation, create a dedicated capture-worker process;
- the main runtime owns identity, consent, target/session state, memory, and semantic requests;
- the worker owns HWND-bound WGC/D3D resources, frame pool, bounded frame buffers, crops/attention-sheet production, and capture metrics;
- IPC must be bounded, versioned, cancellable, and unable to mutate identity or memory;
- worker restart must drop disposable frames but preserve the local session and runtime;
- no raw full-screen capture capability is exposed to the worker contract.

This is not permission to implement the worker during Task 0 or Task 1; it is the required target architecture.

## R5 — Soften unsupported capture claims and add an early feasibility gate

Severity: blocking documentation correctness.

`CreateForWindow` officially targets one HWND and requires Windows 10 version 1903 or later. However, “fully supported” for occluded/background game capture overstates what the platform guarantees: a target application may throttle or stop rendering when unfocused, occluded, or minimized. Minimized and exclusive-fullscreen behavior must remain capability-tested rather than described as generally available on later builds.

Required revision:

- describe WGC as the selected per-window mechanism, not a guarantee that every game continues producing fresh frames;
- treat minimized and exclusive-fullscreen capture as unsupported unless the actual target/OS feasibility spike proves otherwise;
- treat stale/black/no-signal detection as a core capability;
- retain the rejection of Desktop Duplication and generic `PrintWindow` fallback because they violate isolation or reliability;
- add a pre-Task-5 spike on the actual target PC for visible, occluded, unfocused, minimized, borderless, and exclusive-fullscreen states;
- define a stop condition: if background fresh frames cannot be obtained, the core reports/pauses honestly and structured game integrations remain a later enhancement.

## R6 — Correct the presentation boundary

Severity: blocking contract ambiguity.

The diagram says `PresentationAdapter` never receives character-voice text. The eventual presentation must display model/personality-produced content; the core should prevent generation from leaking into UI infrastructure, not prevent the UI from rendering an opaque message.

Required revision:

- split `IPresentationSink` (renders opaque text/status/expression intents) from the later `IPersonalityAdapter` (turns semantic events/context into character-facing content);
- during neutral-core stages, a neutral personality/presentation source emits placeholders;
- UI rendering never interprets or generates personality;
- core services emit typed semantic events and structured content, not character-specific literals.

## R7 — Clarify privileged maintenance writes

Severity: blocking authority-model ambiguity.

`LocalWriteGate` cannot literally be the only writer if backup restore, schema migration, and user-explicit deletion/correction operations exist. Those privileged paths must not become a loophole for API output.

Required revision:

- runtime automated/API path: append proposals through `LocalWriteGate` only;
- offline/maintenance path: a separate capability-scoped `MaintenanceStore` available only while normal runtime writes are stopped;
- maintenance operations require local user intent or versioned migration authority and are audit logged;
- API/semantic components cannot resolve or construct the maintenance capability;
- automated corrections and summaries remain append-only; user deletion remains an explicit later interface operation.

## R8 — Keep Task 2 and Task 4 sequential

Severity: scheduling decision.

Do not parallelize them. The project requires one active stage/task and a reviewed gate before advancing. Task 2 must pass before Task 4 begins, even if their code dependencies are independent.

## R9 — Add cancellation and late-result fences to the privacy boundary

Severity: blocking privacy detail.

Cancellation is cooperative. A remote or worker result may arrive after the privacy hotkey.

Required revision:

- every capture/request/result carries target-session generation and local operation IDs;
- privacy stop increments/revokes the generation, clears bounded buffers, cancels tokens, and pauses runtime writes;
- responses from a revoked generation are discarded before display, Seed creation, journaling, or memory append;
- explicit resume creates a new generation and never reuses withheld frames.

## R10 — Update the task map and open decisions

Severity: required cleanup.

Revise the proposal so §15 records resolved decisions:

1. WPF + .NET 10 LTS approved.
2. Out-of-process capture worker required before real WGC capture acceptance.
3. SQLite approved with the clarified checksummed recovery-tail protocol.
4. Graceful no-signal behavior approved; real capability spike required before Task 5.
5. Tasks remain sequential; no Task 2/Task 4 parallelization.

Do not begin Task 1. Commit only the revised architecture proposal and updated handoff on the current branch, then stop for re-review.

## Sources checked by foreman

- Microsoft .NET support policy: .NET 10 LTS active through 2028-11-14; .NET 8 maintenance ends 2026-11-10.
- Microsoft `IGraphicsCaptureItemInterop::CreateForWindow`: targets one window; minimum client Windows 10 version 1903 build 18362.
- Microsoft WPF documentation: .NET 10 WPF is current and actively maintained.
- SQLite documentation: WAL files are part of persistent state; live-database backup should use the supported online backup mechanism or equivalent safe snapshot.

## Gate decision

Task 0 is **not yet approved**. The proposal is promising and requires one bounded documentation revision. No product code is authorized.
