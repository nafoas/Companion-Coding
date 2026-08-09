# Task 0 — Architecture Proposal

Status: revision 5, addressing the PR #1 re-review of revision 4 (commit `0d7fca6275e3354cc7826694fa988d7c0e1033f7`), which called it one final contract definition short of approval. No product code accompanies this document.

Reviewed documents: `AGENTS.md`, `docs/Claude-Companion-Core-Task-Packet.md`, `docs/Prince-Construction-Roadmap.md`, `docs/Prince-Design-BunDex.md`, `BUILD_LEDGER.md`, `tasks/review/FOREMAN_REVIEW.md`, PR #1 review comments.

## Revision note

Revision 2 responded to R1–R10 in `tasks/review/FOREMAN_REVIEW.md` (mapped: R1 → §2; R2 → §5; R3 → §5; R4 → §6, §8; R5 → §4, §12; R6 → §6, §8; R7 → §6; R8 → §13; R9 → §11; R10 → §15). Revision 3 addressed four bounded follow-up corrections (backup-cut protocol, presentation diagram, §11/`MaintenanceStore` wording, §13's task-order map). Revision 4 unified `NeutralPersonalityAdapter` naming and fixed the §8 backup-footer wording. The foreman's re-review of revision 4 confirmed the backup footer, six-step cut, generation fencing, memory authority, presentation flow shape, binding task order, changed-file scope, and handoff all pass, and flagged exactly one remaining gap: `NeutralPersonalityAdapter` was described as mapping typed input to placeholder content only in the abstract, without a concrete, implementable mapping. Fixed in this revision (5) by adding §6.2.1, a normative table covering every Task-1 lifecycle event (`start` cold and with recovered checkpoint, `nap`, `wake`, `stop`, and a deterministic fallback for anything else) with its content key, expression intent, consulted context fields, and validity notes — referenced from §8's presentation-flow diagram.

## 1. Repository inventory

The repository contains only planning/control and review files: `AGENTS.md`, `BUILD_LEDGER.md`, `README.md`, `docs/Claude-Companion-Core-Task-Packet.md`, `docs/Prince-Construction-Roadmap.md`, `docs/Prince-Design-BunDex.md`, `docs/Shared-Codebase-Workflow.md`, `tasks/active/task-00-architecture.md`, `tasks/review/HANDOFF.md`, `tasks/review/FOREMAN_REVIEW.md`, and `.gitignore`. No source tree, build files, or CI exist yet. Nothing here needs preservation or migration — Task 1 starts from a clean skeleton.

## 2. Recommended Windows stack

**C#, .NET 10 LTS, WPF for the shell UI.** *(Revised per R1 — the original proposal recommended .NET 8. Construction begins August 2026; Microsoft's published lifecycle has .NET 8 in maintenance with support ending 2026-11-10, while .NET 10 is active LTS through 2028-11-14. Starting a long-lived Windows application on a runtime scheduled to leave support within months would create an immediate, avoidable migration task, so .NET 10 LTS is the corrected baseline.)*

- `Windows.Graphics.Capture` (WGC), the API that satisfies "capture only the authorized target regardless of foreground/background," ships with first-class C#/WinRT projection. Doing this from C++ or Python means hand-rolling COM/WinRT interop for no benefit.
- GPU/native-resource lifecycle work (Direct3D11 surfaces, `IDirect3DDevice`) has mature managed wrappers (`Vortice.Windows` / `CsWin32`), which matters directly for the deterministic-disposal and resource-watchdog invariants.
- `System.Threading.Channels` plus a named `Mutex`/pipe give a straightforward, testable way to enforce "exactly one runtime" and bounded producer/consumer queues (capture ring, processing queue) without extra native dependencies.
- WPF over WinUI 3 for the placeholder shell: Task 1 needs a blank text box, placeholder icon, and status text — WPF is actively supported and maintained on .NET 10, and gets there with `dotnet build`/`dotnet test` on a stock `windows-latest` CI runner and no MSIX packaging identity requirement. `PresentationAdapter`/`IPresentationSink` (see §6) is the seam that isolates the UI framework from core logic, so this choice is not expensive to revisit later if the eventual personality layer wants WinUI 3/Fluent styling — WinUI 3 remains deferred to the presentation layer only if it demonstrates a concrete benefit there.
- CI, local dev setup, and any published SDK/runtime references pin the current .NET 10 patch version rather than a floating major-version alias, so the toolchain doesn't silently drift mid-project.

## 3. Credible alternatives and tradeoffs

| Option | Tradeoff | Verdict |
|---|---|---|
| C++/Win32 + Direct3D11 directly | Maximum control, but every invariant (single-instance, bounded queues, journal, database access) is hand-built or vendored; slower to a testable skeleton; native build friction makes unit-testing business logic (attention scoring, conversation coordination) harder. | Reconsider only if Task 12 profiling shows managed-runtime overhead is a real problem for this screenshot-cadence workload — no evidence of that yet. |
| Python (`pywin32` + `mss`/`dxcam`) | Fast to prototype, but `mss`/`dxcam` are full-screen/display-duplication based, not per-window with graceful occlusion handling; GIL contention between capture/semantic/UI threads; "clean checkout builds" is harder without bundling a Python runtime. | Fine for a throwaway spike, not for Task 1 onward. |
| Electron/Node | No first-class WGC access; would need native addons anyway, at which point C# is more direct. Wrong tool for native window capture and GPU-resource discipline. | Rejected. |
| WinUI 3 instead of WPF | Modern Fluent/Mica styling, closer to what a personality layer might eventually want. Costs MSIX/packaging-identity complexity and a Windows App SDK runtime dependency for a stage that only needs a blank text box. | Viable alternative for the *personality* layer later; not needed for the neutral core. |

## 4. Capture technology analysis

*(Revised per R5 — the prior revision described several states as "fully supported," which overstates what the platform guarantees. WGC is the selected per-window mechanism, not a promise that a target keeps producing fresh frames once it's unfocused, occluded, or minimized. The table below is corrected to distinguish "the API call succeeds" from "the application is actually still rendering fresh content," and every non-trivial row is now marked as requiring feasibility confirmation rather than assumed.)*

**Selected mechanism: `Windows.Graphics.Capture`** (`IGraphicsCaptureItemInterop::CreateForWindow` + `Direct3D11CaptureFramePool`). It officially targets exactly one HWND and requires Windows 10 version 1903 (build 18362) or later — that part is a documented platform guarantee. What is *not* guaranteed is that the target application keeps rendering fresh frames into that HWND once it isn't focused, isn't visible, or is minimized; many apps and games throttle or stop rendering in those states independent of anything the capture API does.

| Target state | What WGC guarantees | What is not guaranteed / must be spiked |
|---|---|---|
| Visible, foreground | Frames delivered, GPU-accelerated. | — |
| Visible, occluded by other windows, still rendering | WGC delivers the window's own composited surface regardless of what's on top of it — this is the mechanism that makes "watch a background app without observing the foreground app" possible at all. | Whether the *target application itself* keeps rendering at full fidelity while occluded and unfocused is app-dependent and must be confirmed per target, not assumed from the API contract. |
| Minimized | — | Treated as **unsupported** until the pre-Task-5 feasibility spike (§12) proves otherwise on the actual target OS build. Some later Windows 11 builds are reported to support this, but that is not something this proposal asserts as given. |
| Borderless windowed | Behaves like any other composited window from WGC's perspective. | Same rendering-while-unfocused caveat as the occluded row above. |
| Exclusive fullscreen | — | Treated as **unsupported**. Apps that bypass the DWM with their own exclusive swapchain are not reliably reachable by any polite capture API, WGC included. This is a platform ceiling, not an implementation gap. |

Design consequences, independent of which row eventually tests out supported:

- Stale/no-signal detection is a **core capability**, not an enhancement: `CaptureWorker` must be able to tell "target minimized/unfocused and not producing new frames" apart from "target producing frames that happen to be static," and report the former as a neutral status rather than silently feeding empty/stale data into the attention pipeline.
- Minimized and exclusive-fullscreen support are not claimed until the §12 spike demonstrates them on the real target hardware/OS build. If the spike fails, the documented stop condition (§12) applies: the core reports/pauses honestly, and any deeper integration for those states becomes a later, explicitly scoped enhancement rather than a silent gap.

**Rejected fallback: `PrintWindow`/`BitBlt`.** Works for some DWM-composited apps but frequently returns black/stale frames for GPU-rendered or fullscreen content, and doesn't give the same clean per-window isolation guarantee. Not a general fallback; could be a narrow, explicitly-flagged compatibility shim for one specific supported title in a later stage, but that's out of scope for the core. Rejected for the reason above, independent of this revision.

**Rejected for the core path: Desktop Duplication API (`IDXGIOutputDuplication`).** Display-wide, not per-window — cropping to the target's screen rect breaks target isolation the moment another window overlaps that rect. Rejected for an isolation reason, not a reliability one, and that reasoning is unchanged by this revision.

## 5. Local database and crash-journal choice

*(Revised per R2 and R3 — the prior revision named SQLite and an NDJSON journal as two peer stores without specifying which is authoritative, in what order they're written, or how replay avoids loss/duplication across a crash. This revision defines one canonical durability protocol and treats the journal as a recovery tail to that protocol, not an independent memory authority. It also strengthens dev/production separation per R3.)*

### 5.1 One canonical durability protocol

**SQLite is the single committed, queryable authority.** Every durable memory record and API operation record is an append-only row in SQLite (`Microsoft.Data.Sqlite`), in WAL mode, with foreign keys and integrity checks enabled, an explicit `synchronous` policy (`FULL` for the committed database, not `NORMAL`, since durability here outranks write throughput at this event rate), and a unique local operation ID as a `UNIQUE` constraint so a re-applied append is a no-op rather than a duplicate row.

**`SessionJournal` is a checksummed write-ahead recovery tail for that authority, not a second store.** Its only job is to survive the gap between "an event happened" and "SQLite committed it." Concretely:

1. **Append** — the event is serialized, framed with a length prefix and a checksum, and written to the journal file, followed by `Flush(flushToDisk: true)`. A torn write (process killed mid-write) produces a frame that fails its checksum or is truncated; recovery detects and discards exactly that one trailing frame, nothing earlier.
2. **Commit** — the same event, keyed by its local operation ID, is inserted into SQLite inside a transaction. Because the operation ID is unique, replaying this insert after a crash and retry is idempotent — it either inserts once or is rejected as a duplicate, never both.
3. **Checkpoint** — once SQLite has durably committed the transaction, a checkpoint marker advances in the journal, meaning "everything before this point is confirmed in SQLite and no longer needs replay."

Recovery on startup: read the journal from the last checkpoint marker forward, ignore a torn/invalid final frame if present, and for every valid frame whose operation ID is not already present in SQLite, replay the commit step. This makes recovery deterministic and duplicate-safe by construction, not by convention, and directly satisfies the packet's "kill during append and recover prior records" and "retry an operation and commit at most once" acceptance criteria.

**Journal rotation** happens only immediately after a validated full backup, using the exact cut protocol defined in §5.2 — never on a timer or size threshold alone — so a rotated-away journal segment is only ever discarded once its contents are provably durable in a validated backup, not merely in the live database.

**Emergency privacy cancellation fencing**: the privacy-stop path (§11) revokes the current session generation before this protocol's append step runs for any in-flight semantic result. A late-arriving result tagged with a revoked generation is discarded before it ever reaches step 1 above — the durability protocol only ever sees writes that were already privacy-cleared, it does not itself decide what's admissible.

### 5.2 Backup mechanism and the durability cut

*(Revised — the prior revision named the backup mechanism but left the cut between "what's in the backup" and "what's still only in the journal" undefined; a write committed after the snapshot began but before rotation could have been silently lost. This revision specifies the exact protocol so that is provably impossible.)*

Backups use SQLite's supported online backup mechanism (the `sqlite3_backup` API, exposed in .NET via `Microsoft.Data.Sqlite`'s `SqliteConnection.BackupDatabase`) or an equivalent safe snapshot — never a raw file copy of a live WAL-mode database, since that races with concurrent writers and can capture an inconsistent set of pages. Producing a backup is a six-step protocol, not a single API call:

1. **Establish the cut.** `MemoryStore` has a single serializing writer (this is a single-process, single-writer workload), so establishing a cut is a momentary pause of new commits, not a long hold: record the highest committed local operation ID / journal checkpoint position at that instant as the **backup cut sequence**, then immediately resume accepting new commits. This pause covers one metadata read, not the snapshot itself.
2. **Drain to the cut.** Before taking the snapshot, confirm every journal frame at or before the cut sequence is already committed to SQLite, replaying any that aren't yet (via the §5.1 recovery procedure). The snapshot must never start while a frame provably at-or-before the cut is still uncommitted.
3. **Snapshot.** Once step 2 holds, take the SQLite online backup. Normal operation (including new writes after the cut) continues concurrently — the backup mechanism's whole purpose is that this is safe against a WAL-mode database. Writes after the cut are, by design, simply not part of this backup; step 6 is why that's safe.
4. **Manifest.** Record the cut sequence (the operation ID / journal position from step 1) in the backup's manifest, alongside the existing schema version and per-file checksums.
5. **Validate and promote.** Validate the new archive (checksums, manifest, schema version, health check against the source) and only then atomically replace the "current backup" pointer. A backup is never allowed to overwrite a known-good one with output from a store that failed a health check first (Task 3's existing acceptance criteria already require this end-to-end).
6. **Rotate.** Only journal frames at or before the *promoted* backup's recorded cut sequence may be discarded. Every frame after the cut remains in the live journal untouched, regardless of how long the backup took to validate — it either stays in the live journal (if it arrived after this cut) or becomes coverable by the *next* backup's cut. This is what makes "a write committed after the snapshot began but before rotation" provably safe: such a write is always after the cut sequence by construction, so rotation in step 6 never touches it.

An equivalent protocol is acceptable only if it proves the same property: no event committed after a given backup's recorded cut can ever be discarded by that backup's rotation.

### 5.3 Dev/production separation

An explicit configuration value defaulting to `dev` is necessary but not sufficient — a mis-set value could still point a development binary at the production store. The corrected design makes that technically difficult, not just discouraged:

- **Distinct application identifiers and fixed default data roots** per build configuration — e.g. `CompanionCore.Dev` vs. `CompanionCore` as the app identity, with each identity's default root baked in, not read from a shared generic setting.
- **Development binaries refuse to open a path recognized as the production root**, full stop, unless a separate, explicitly invoked, guarded migration/repair tool performs the access — ordinary `dotnet run`/debug startup has no code path that can reach the production root, guarded or not.
- **No ambient fallback and no generic arbitrary-path override** in ordinary development startup — there is no "just pass a path" escape hatch in the normal launch flow.
- **Separate namespacing end to end**: distinct single-instance mutex/pipe names, credential-store keys, database file names, backup locations, and telemetry/log roots between dev and production, so a dev and a production instance cannot collide even if run side by side.
- **Contract test**: a Task 2 test that constructs a development-configured runtime and asserts it rejects an attempt to inject the production root path through every normal configuration surface, not just the happy path.

## 6. Process and worker isolation design

- `CompanionRuntime` lives in the main process and is the only place identity/lifecycle state is held. It is registered once via a startup-time named-`Mutex` (or named-pipe) single-instance guard in `CompanionCore.App`, checked before any other subsystem initializes.
- `ApiBridge` calls are async and cancellable, isolated behind an interface so a hung/failed request cannot block the runtime's own state machine; retries and backoff are owned by `ApiBridge`, not by callers.
- The boundary that matters for the "no second identity" invariant: only `CompanionCore.App`'s composition root may construct a `CompanionRuntime`. Worker restarts, `ApiBridge` failures, and additional UI windows all reference the existing runtime instance via dependency injection; none of them can construct a new one. This is enforced by constructor visibility (internal factory, no public constructor) plus a Task 1 acceptance test that opens multiple windows and asserts one runtime.

### 6.1 `CaptureWorker` — required out-of-process boundary (revised per R4)

The prior revision proposed an in-process background task for `CaptureWorker`. That is cancellable but gives no crash or leak isolation for native D3D/WinRT failures, which contradicts the packet's requirement that a worker restart independently without ever threatening the one-runtime invariant. This revision corrects the target architecture; it is **not** authorization to build the worker process now:

- **Task 1** defines only the `ICaptureWorker` contract (start/stop/restart, frame/status events, bounded request surface) and an in-process fake/test double that satisfies it with synthetic data. No real capture, no separate process, in Task 1.
- **Before Task 5's real capture implementation**, `CaptureWorker` becomes a dedicated out-of-process capture worker. The main runtime process owns identity, consent, target/session state, memory, and semantic requests; it never touches HWND-bound WGC/D3D resources directly. The worker process owns the WGC session, the D3D device and frame pool, bounded frame buffers, crop/attention-sheet production, and its own capture metrics.
- **IPC** between runtime and worker is bounded (backpressure on frame delivery, no unbounded queues across the process boundary), versioned (so a worker restart on a new build doesn't desync silently), cancellable end to end, and structurally unable to mutate identity or memory — the worker's IPC surface has no operation that reaches `LocalWriteGate` or `CompanionRuntime`'s identity state, only frame/status data flowing one way and control commands (start/stop/retarget-within-authorization) flowing the other.
- **Worker restart** drops all disposable in-flight frames but never touches the local session, journal, or memory — restart is scoped entirely to capture-side state, matching the packet's "capture and semantic work should be isolated so a worker can restart without creating another runtime identity."
- **No raw full-screen capture capability is exposed anywhere in the worker contract** — not as a debug path, not as a fallback — since that would be a standing violation of target isolation regardless of how carefully it were gated.

### 6.2 Presentation boundary — split sink from personality (revised per R6)

The prior revision's diagram implied `PresentationAdapter` "never receives character-voice text," which is backwards: the eventual presentation layer must display personality-produced content, so the correct boundary is that *generation* stays out of UI infrastructure, not that the UI is blind to it. Two separate contracts replace the single `PresentationAdapter`:

- **`IPresentationSink`** — the UI-facing contract. Renders opaque strings/labels, status text, and typed expression intents (`observing`, `investigating`, `urgent`, `taking_note`, `privacy_paused`, `recovering`, …). It does not interpret, generate, or filter content; it displays whatever it's handed.
- **`IPersonalityAdapter`** — the content-producing contract, installed at the Stage 13 Companion Awakening boundary. During all neutral-core stages, `NeutralPersonalityAdapter : IPersonalityAdapter` is the only implementation wired in. It deterministically maps typed semantic events/context to placeholder opaque content plus expression intents — it may pass expression intents through unchanged, but it does not pass its typed input straight to `IPresentationSink`, because `IPersonalityAdapter`'s input contract (typed events/context) and `IPresentationSink`'s input contract (opaque content/intents) are intentionally different types, not the same shape wearing two names. Core services (`AttentionEngine`, `ConversationCoordinator`, etc.) only ever emit typed semantic events and structured content to this seam — never character-specific literals — so swapping `NeutralPersonalityAdapter` for Prince's real `IPersonalityAdapter` implementation later is a Stage 13 configuration change, not a core rewrite, and Task 1 has exactly one presentation abstraction to scaffold, not two conflated ones.

#### 6.2.1 `NeutralPersonalityAdapter`'s deterministic mapping for Task 1 (added — normative, implementable directly)

Task 1 only produces `CompanionRuntime`'s four lifecycle states (start, nap, wake, stop) plus whatever the runtime hasn't recognized. `NeutralPersonalityAdapter`'s mapping for these is a pure function of `(lifecycleEvent, context) → (contentKey, expressionIntent)` — table-driven, no randomness, no clock-dependent variation, and total (every input has a defined output, including inputs the table doesn't otherwise name). Later tasks add rows for attention/conversation events (`observing`, `investigating`, `urgent`, `taking_note`, `privacy_paused`); this table is Task 1's complete scope, not the full eventual vocabulary.

| Lifecycle event | Content key (stable identifier; string is placeholder-neutral and swappable at Stage 13) | Expression intent | Context fields consulted | Notes |
|---|---|---|---|---|
| `start`, first run (no prior checkpoint) | `lifecycle.started` | none | `hadRecoveredCheckpoint = false` | Baseline cold start. |
| `start`, checkpoint recovered | `lifecycle.recovering` | `recovering` | `hadRecoveredCheckpoint = true` | Same `start` event, routed to a different row by the one context field that distinguishes cold start from restart-with-recovery — this is the mapping's only branch, and it's on a boolean, not open-ended state. |
| `nap` | `lifecycle.napping` | none | none | Task 1's `nap` is a plain idle lifecycle state; it is not yet tied to Watchbun quiet-hour semantics (Stage 9), so no context field affects it. |
| `wake` | `lifecycle.waking` | none | `priorState` (must be `nap`; anything else routes to the unknown-event fallback below rather than emitting wake content for an invalid transition) | |
| `stop` | `lifecycle.stopped` | none | `isCleanShutdown` (recorded for diagnostics only; does not change the emitted content key in Task 1 — there's no UI visible after stop to differentiate) | |
| any event not in `{start, nap, wake, stop}`, or a validity precondition above fails (e.g. `wake` with `priorState != nap`) | `lifecycle.unknown` | none | the unrecognized event's raw name (logged behind the Task 1 diagnostics switch, never rendered) | The deterministic fallback required by this section: the adapter must never throw or leave `IPresentationSink` with nothing to render for an input it doesn't recognize. |

Expression intents in this table are `none` for every ordinary lifecycle transition — `recovering` is the only Task-1 intent, reserved for the one case (checkpoint-recovered start) the packet explicitly names as needing a distinct neutral label. Content keys are stable identifiers for tests and later Stage-13 replacement to key off; the actual placeholder strings behind them (e.g. "Ready.", "Resuming from last checkpoint.") are implementation detail, not an architectural decision, and may be adjusted freely in Task 1 as long as the key → intent → context mapping above holds.

### 6.3 Memory-write authority — runtime path vs. maintenance path (revised per R7)

`LocalWriteGate` cannot literally be the *only* code that ever writes to `MemoryStore` — backup restore, schema migration, and any future explicit user-initiated deletion/correction have to write too, and none of those should become a loophole an automated/API path could exploit. Two distinct write paths, not one:

- **Runtime path**: every automated and API-originated write goes through `LocalWriteGate` as an append-only proposal. This is unchanged from the prior revision — no `Update`/`Delete` capability exists on this path at all.
- **Maintenance path**: a separate, capability-scoped `MaintenanceStore` that is only reachable while normal runtime writes are stopped (e.g. during Task 3's restore flow, or a future explicit user-initiated deletion feature), requires local user intent or versioned migration authority to invoke, and every operation through it is audit-logged. `ApiBridge`, `ISemanticProvider` implementations, and any other automated/API-facing component have no reference to `MaintenanceStore` and no way to construct or resolve one — it is not merely undocumented, it is unreachable from that side of the composition graph. Automated corrections and summaries remain strictly append-only through the runtime path; anything resembling deletion is exclusively a later, explicit, human-initiated maintenance-path feature, not something Task 2 needs to build now.

## 7. Semantic-provider interface, mock/replay-first

```
interface ISemanticProvider
{
    Task<SemanticResult> InterpretAsync(SemanticRequest request, CancellationToken ct);
}
```

- `MockSemanticProvider`: deterministic scripted responses keyed by request shape, for unit tests of `ConversationCoordinator`/`AttentionEngine` wiring without any I/O.
- `ReplaySemanticProvider`: reads sanitized recorded request/response fixture pairs from disk, for integration tests that exercise the full `ApiBridge` contract (retry, idempotency, write-gate proposals) without network access.
- `RealSemanticProvider`: contract-complete shell, constructed but disabled (throws/returns a neutral "unavailable" result) whenever credentials are absent — which is every stage before Task 12. Selecting it without credentials must be a supported, tested state, not a crash.
- `ApiBridge` depends only on `ISemanticProvider`; nothing above the interface knows which implementation is active. Provider selection is a configuration value, not a compile-time branch, so the same binary used in CI (mock/replay) is the one that eventually runs with the real provider.

## 8. Module dependency diagram

*(Revised across revisions 2–4 — reflects the out-of-process `CaptureWorker` boundary and the separate `MaintenanceStore` path (revision 2), the presentation flow showing `IPersonalityAdapter` explicitly (revision 3), and now, in revision 4, a single consistently-named `NeutralPersonalityAdapter` implementation plus a backup footer that matches §11's read-only-snapshot-vs-restore/migration distinction exactly.)*

```
                                  ┌─────────────────────┐
                                  │   CompanionRuntime    │  (single authoritative
                                  └──────────┬────────────┘   instance, main process)
      ┌────────────┬───────────────┼───────────────┬─────────────┬─────────────┐
      ▼            ▼               ▼               ▼             ▼             ▼
 TargetAuth   ConversationCoord  AttentionEngine  CaptureWorker  MemoryStore  ResourceWatchdog
  Service            │                 │           (IPC, Task 5+  ▲             │
      │              ▼                 │            out-of-       │             ▼
      │        VisualPipeline ─────────┘            process;      │      (restarts workers,
      │       (via PrivacyGuard for                  Task 1 ships │       clears disposable
      │        non-game targets)                     in-process   │       data only, never
      │                                              fake behind  │       touches committed
      │        ApiBridge ── ISemanticProvider:        same        │       memory)
      │              │       Mock / Replay /          ICaptureWorker
      │              │       (disabled Real)          contract)
      │              ▼                                            │
      │        LocalWriteGate ────────────────────────────────────┘
      │              │  (append-only proposals, runtime path)
      │              ▼
      │        SessionJournal ──▶ MemoryStore (checkpoint/commit protocol, §5.1)
      │
      │        MaintenanceStore  (separate capability, offline/guarded path only —
      │              ▲            backup restore, migration, future explicit user
      │              │            deletion; unreachable from ApiBridge/ISemanticProvider)
      │              │
      └── target/session metadata only; no capture, no memory writes.

Presentation flow (separate from the box above — this is what R6/this revision's
review comment corrected):

  AttentionEngine, ConversationCoordinator
              │  typed semantic events (observing/investigating/urgent/...)
              │  and structured context — never character-specific literals
              ▼
       IPersonalityAdapter   NeutralPersonalityAdapter is the only implementation
              │               wired in during core stages; Prince's real adapter
              │               is installed at Stage 13. It deterministically maps
              │               typed input to placeholder opaque content, and may
              │               pass expression intents through unchanged — it does
              │               not pass its typed input straight to IPresentationSink,
              │               since the two contracts have different input/output
              │               types by design. The exact mapping table for Task 1's
              │               scope (CompanionRuntime's start/nap/wake/stop lifecycle
              │               events, since AttentionEngine/ConversationCoordinator
              │               don't exist until Tasks 8–9) is §6.2.1, normative.
              │  opaque content + expression intents (never the typed input)
              ▼
       IPresentationSink     (renders only — never generates or interprets)

BackupRecoveryService producing a backup reads MemoryStore + SessionJournal through a
read-only snapshot interface and the SQLite online-backup mechanism/cut protocol (§5.2);
it never resolves MaintenanceStore and has no write capability at all. Only restore and
migration resolve MaintenanceStore, gated on normal runtime writes being stopped (§6.3,
§11 trust boundary 4). Neither path runs in the runtime's live request path.
```

Dependency rules this is meant to enforce structurally, not just by convention:

- `AttentionEngine`/`ConversationCoordinator` depend only on `IPersonalityAdapter`'s typed-event input contract, never on capture, memory, or API internals.
- `IPersonalityAdapter` is the only place typed events become content/phrasing; `NeutralPersonalityAdapter` is the only implementation wired in during core stages, and it deterministically maps typed input to placeholder opaque content (not a literal passthrough of the typed input itself — `IPersonalityAdapter` and `IPresentationSink` have different input/output types by design). Swapping it for Prince's real adapter at Stage 13 is a configuration change, not a core rewrite.
- `IPresentationSink` depends only on `IPersonalityAdapter`'s output (opaque content/intents) — it cannot reach `AttentionEngine`, `ConversationCoordinator`, capture, memory, or API internals directly, and it does not itself interpret or generate anything.
- `ApiBridge` never holds a direct reference to `MemoryStore`; it only produces proposals `LocalWriteGate` accepts or rejects, so "API output can't bypass the write gate" is structural.
- `ApiBridge` and `ISemanticProvider` implementations have no reference to `MaintenanceStore` — the maintenance/offline write path is unreachable from anywhere API-facing, by construction.
- `CaptureWorker`'s IPC surface has no call that reaches `LocalWriteGate` or `CompanionRuntime`'s identity state — only frame/status data and bounded control commands cross the process boundary.
- `ResourceWatchdog` holds references only to workers/queues it may restart or clear, and has no reference into `MemoryStore`'s commit path or `MaintenanceStore`.

## 9. Proposed source/test directory structure

```
/src
  CompanionCore.Runtime/          CompanionRuntime, lifecycle states, single-instance guard
  CompanionCore.Presentation/     IPersonalityAdapter, NeutralPersonalityAdapter, IPresentationSink, WPF shell (blank textbox/icon/status)
  CompanionCore.Capture.Contracts/ICaptureWorker, IPC message/event contracts, versioning (shared by both sides)
  CompanionCore.Capture.Worker/   real out-of-process worker host (Task 5+): WGC/D3D, frame pool, ring, VisualPipeline, PrivacyGuard
  CompanionCore.Capture.Fake/     in-process ICaptureWorker test double used through Task 1–4
  CompanionCore.TargetAuth/       TargetAuthorizationService
  CompanionCore.Attention/        AttentionEngine
  CompanionCore.Conversation/     ConversationCoordinator, seed banks
  CompanionCore.Memory/           MemoryStore, LocalWriteGate, SessionJournal, MaintenanceStore, BackupRecoveryService
  CompanionCore.Api/              ApiBridge, ISemanticProvider, Mock/Replay providers, real-provider shell
  CompanionCore.Diagnostics/      ResourceWatchdog, structured logging
  CompanionCore.App/              entry point, DI composition root, dev/prod data-root selection
/tests
  CompanionCore.Runtime.Tests/
  CompanionCore.Capture.Tests/    exercises ICaptureWorker via the fake in Task 1–4; against the real worker process from Task 5
  CompanionCore.Attention.Tests/
  CompanionCore.Conversation.Tests/
  CompanionCore.Memory.Tests/     includes the §5 durability-protocol and dev/prod-separation contract tests
  CompanionCore.Api.Tests/
  CompanionCore.Fixtures/         synthetic event streams, replay fixtures, ICaptureWorker test doubles
CompanionCore.sln
/scripts
  test.ps1
  build.ps1
```

`docs/architecture/**` (this proposal and future decision records) and the existing `docs/`/`tasks/` planning structure are unaffected by this layout.

## 10. Build, test, packaging, Windows-version strategy

- **Build/test**: xUnit, `dotnet build` / `dotnet test` on the pinned .NET 10 LTS SDK, GitHub Actions `windows-latest` runner on every push to the working branch. Business-logic tests (`AttentionEngine`, `ConversationCoordinator`, `MemoryStore`, `LocalWriteGate`, `ApiBridge` against Mock/Replay) require no capture, network, or credentials, satisfying "no automated test may require paid API access."
- **Capture-layer tests**: through Task 4, `CaptureWorker`/`VisualPipeline` are tested entirely against the in-process `ICaptureWorker` fake (§6.1, §9) with synthetic fixtures — no real capture, no separate process, in CI at this stage. From Task 5 onward, the same contract is exercised against the real out-of-process worker in integration tests; real-WGC-against-a-real-window verification stays manual/nightly rather than a PR gate, since it's fragile headlessly.
- **Local script**: `scripts/test.ps1` wraps the same `dotnet test` invocation CI runs, so a human or the foreman can reproduce results locally.
- **Packaging**: none required at this stage (explicitly out of scope per non-goals). `dotnet publish -r win-x64 --self-contained` is sufficient for a runnable artifact when a human wants to try the skeleton; no installer/updater.
- **Windows version target**: Windows 10 1903 (build 18362)+, the documented minimum for `IGraphicsCaptureItemInterop::CreateForWindow`. Minimized-window and exclusive-fullscreen support are treated as unproven until the §12 spike, per §4 — not assumed available on any particular later build.

## 11. Privacy and threat-boundary summary

- **Trust boundary 1 — capture**: `CaptureWorker` may only ever hold a handle to the single `TargetAuthorizationService`-approved HWND. It has no API to enumerate or capture any other window. `TargetAuthorizationService` itself may enumerate process/window metadata (for the consent prompt) without capturing pixels — enumeration and capture are separate capabilities so a bug in one can't silently grant the other.
- **Trust boundary 2 — content**: `PrivacyGuard` sits between `VisualPipeline` and semantic interpretation for authorized non-game targets, high-threshold and independently unit-testable in isolation from the rest of the pipeline. Trusted game targets may explicitly bypass content-level filtering, but never target isolation (boundary 1 still applies).
- **Trust boundary 3 — remote**: `ApiBridge` is the only component with network access. It never receives raw frames beyond what a request explicitly needs, never receives credentials in a form that could be logged, and every response is a proposal, not an authority — `LocalWriteGate` is boundary 4.
- **Trust boundary 4 — memory authority** *(corrected — the prior wording said `LocalWriteGate` is "the sole writer to `MemoryStore`," which contradicted §6.3's separately-approved `MaintenanceStore` capability)*: `LocalWriteGate` is the sole **live-runtime/automated/API** writer to `MemoryStore` — anything upstream of it on that path, human-triggered or model-originated, can only produce append proposals; update/delete isn't a capability that exists on that side of the boundary at all. The capability-scoped, offline `MaintenanceStore` (§6.3) is the *only other* writer, and it is structurally unreachable from `ApiBridge`, `ISemanticProvider` implementations, or any other semantic/API-facing component. Within that maintenance surface, two different operations are also worth distinguishing: `BackupRecoveryService` producing a backup is a **read-only snapshot** of `MemoryStore` (§5.2) — it never writes to the live store — while **restore and migration** are the actual `MaintenanceStore` write path, gated on normal runtime writes being stopped and requiring local user intent or versioned migration authority (§6.3).
- **Emergency boundary**: the stop-only privacy hotkey is a cross-cutting control that must reach `CaptureWorker` (stop, clear buffers), `VisualPipeline`/`ApiBridge` (cancel pending work), and `MemoryStore` (pause writes) without going through the normal event pipeline, so it stays effective even if another subsystem is misbehaving.
- **Cancellation and late-result fencing** *(added per R9)*: cancellation is cooperative, so a capture-worker frame or a remote semantic result can still arrive after the hotkey fires, mid-flight. Every capture request, worker frame, and semantic result carries a target-session **generation ID** alongside its local operation ID. Privacy stop atomically increments/revokes the current generation, clears bounded buffers, cancels outstanding cancellation tokens, and pauses runtime writes. Any result — a capture frame from the worker process, a semantic interpretation from `ApiBridge` — that arrives tagged with a generation older than the current one is discarded before it can reach `IPresentationSink`, Seed creation, `SessionJournal`, or `MemoryStore`; the durability protocol in §5.1 only ever sees writes that already carry a current generation. Explicit resume creates a new generation rather than reusing or draining anything withheld under the old one, so a resumed session can never surface a frame or result that was captured before the stop.

## 12. Risk register with feasibility spikes

| # | Risk | Impact | Spike / stop condition |
|---|---|---|---|
| 1 | Minimized-window capture is unproven on the actual target OS build *(per R5, no longer assumed available on "later builds")* | Capture silently stops on minimize | **Required spike before Task 5 begins**, on the actual target PC: attempt capture against a minimized test window. Stop condition — if unsupported, the paused-state UX (§4) is the accepted behavior, not a defect to keep chasing. |
| 2 | Exclusive-fullscreen games unsupported by any polite capture API | Some target apps simply can't be watched | **Required spike before Task 5 begins**, same target PC, against one exclusive-fullscreen test app. Stop condition — the no-signal heuristic (§4) is a hard Task 5 requirement regardless of spike outcome, since even a future OS update could regress a currently-working case. |
| 3 | SQLite has no native write-immutability | A `LocalWriteGate` bug could silently violate append-only | Task 2 acceptance tests already require asserting update/delete proposals are rejected; no separate spike needed, but flagged here since it's the invariant most likely to regress silently in refactors. |
| 4 | Dev build accidentally opens production data root | Cross-contaminates the eventual one-time production memory | Task 2 acceptance test covers this directly; stop condition — Task 2 does not pass review if the dev/prod root is inferable from ambient state rather than explicit configuration. |
| 5 | Global hotkey (`Ctrl+Shift+F12`) collision with another app | Privacy stop hotkey silently fails to register | Spike: verify `RegisterHotKey` failure is detectable at startup on a dev machine; if undetectable in some configuration, that's an architecture-affecting finding to bring back before Task 4. |
| 6 | GPU/native resource leaks under sustained capture | Slow memory/handle growth, eventual crash | Deterministic `Dispose`/`using` on every surface/texture; Task 5's multi-hour soak test is the stop condition — no sustained growth allowed to pass. |
| 7 | Backup interrupted mid-write | Corrupt archive replacing a good one | Task 3 acceptance criteria already require atomic build-validate-replace; no separate spike needed. |
| 8 | CI can't exercise real WGC capture against a real window headlessly | Capture-layer bugs slip past CI | `ICaptureWorker` abstraction (§6.1, §9) with the in-process fake for CI through Task 4, and against the real out-of-process worker in integration tests from Task 5; real-capture verification against actual hardware stays manual/nightly, not a PR gate — accepted limitation, not something to solve in the core. |
| 9 | Single-instance guard implemented incorrectly | Two runtimes could both claim authority | Task 1 acceptance test ("multiple windows cannot create multiple runtimes") is the stop condition. |
| 10 | Retry logic in `ApiBridge` double-commits a memory append | Duplicate memory records from a flaky network | Every request carries a local operation ID; append-by-operation-ID is idempotent at `LocalWriteGate`/`MemoryStore` — Task 2 and Task 7 acceptance criteria already cover this. |
| 11 | Raw file copy of a live WAL-mode SQLite database races with concurrent writers | Corrupt or inconsistent backup archive | Resolved by design (§5.2): backups use `SqliteConnection.BackupDatabase`/the online backup mechanism, never a raw copy. Task 3 acceptance test should include a backup taken while a write is in flight. |
| 12 | Out-of-process `CaptureWorker` IPC has a bug that lets a frame/status message reach identity or memory state | Would silently reintroduce a "second identity" or write-gate-bypass risk despite the process boundary (§6.1) | Task 5 contract test: assert the IPC surface exposes no operation reachable from the worker side that can construct a runtime or call `LocalWriteGate`/`MaintenanceStore`; this is a structural (compile-time surface) check, not just a runtime test. |
| 13 | A capture frame or semantic result arrives after a privacy-stop generation revocation | Privacy-stopped content could leak into presentation or memory if fencing is implemented inconsistently | Task 4/Task 7 acceptance test: fire privacy stop mid-request, then let the in-flight result arrive, and assert it never reaches `IPresentationSink`, journaling, or memory append (§11). |

## 13. Task execution order

*(Revised — revision 2 still showed Tasks 3, 4, and 7 branching from Task 2 despite claiming sequential execution. The master packet authorizes exactly one linear order; that's the only map with any authority over what may start next. A separate, explicitly non-authoritative logical-dependency reference follows for context.)*

### 13.1 Authorized execution order (binding)

```
0 → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12
```

This is the only order in which tasks may begin. Each arrow means "the foreman has gated the task on the left before the task on the right may start" — not "the task on the right needs code from the task on the left," which is a separate question addressed in §13.2. No task in this sequence is ever authorized out of order, including when a later task's code has no dependency on an intervening one (e.g. Task 4 does not need Task 3's backup code, but Task 4 still may not start until Task 3 is gated, because the packet permits at most one task in progress at a time).

### 13.2 Logical dependency reference (non-authoritative)

The graph below records which task's *code* actually depends on which other task's code — useful for understanding why a task is structured the way it is, and for spotting a task that's accidentally been given work it doesn't need. It does not authorize skipping ahead or reordering; §13.1 alone governs what may start next.

```
Task 0 (this proposal)
  └─▶ Task 1 (skeleton: runtime, single instance, blank UI, ICaptureWorker contract + fake)
        └─▶ Task 2 (memory store, journal, write gate — §5.1 durability protocol)
              ├─▶ Task 3 (backup/repair — §5.2, needs Task 2's store shape)
              ├─▶ Task 4 (consent/target authorization — no code dependency on Task 2, sequenced anyway per §13.1)
              │     └─▶ Task 5 (out-of-process capture worker — §6.1, needs an authorized target from Task 4)
              │           └─▶ Task 6 (regions/attention sheets — needs frames from Task 5)
              │                 └─▶ Task 8 (attention engine — scores events Task 6 produces)
              └─▶ Task 7 (API bridge — write-gate proposals need Task 2's gate to exist)
                    Task 8 + Task 7 ─▶ Task 9 (conversation coordinator — needs attention events + API bridge)
                                          └─▶ Task 10 (memory consolidation — needs Task 2 store + Task 9 output)
                                          └─▶ Task 11 (background continuity — needs Task 4 target auth + Task 9)
Task 3, 6, 10, 11 all feed ─▶ Task 12 (hardening + core gate)
```

Task 2 and Task 4 have no code-level mutual dependency — Task 4 is sequenced after Task 2 purely by §13.1, not because its code needs anything Task 2 produces.

## 14. Invariant and non-goal acknowledgement

- **One runtime, one identity**: enforced structurally (single-instance guard, internal-only runtime construction, §6), not just as a UI affordance.
- **One conversation thread, neutral non-response**: `ConversationCoordinator`'s data model tracks presentation-attempt counts, not "ignored" penalties, so silence structurally cannot mutate interest/sentiment state.
- **Local memory authority, append-only**: `ApiBridge` and every automated path produce proposals only, through `LocalWriteGate`, which has no `Update`/`Delete` capability at all; corrections/supersession are new linked records, never mutations. The separate `MaintenanceStore` path (§6.3) exists for backup restore, migration, and future explicit human-initiated operations, and is structurally unreachable from `ApiBridge`/`ISemanticProvider` — it is not a second way for automated output to reach committed memory. No task after this one should introduce an `Update`/`Delete` path reachable from the runtime/API side.
- **Consent and target isolation**: capture is opt-in per target, tab/foreground changes never retarget, browsers are denied by default, the privacy hotkey is stop-only with mandatory explicit resume.
- **Screenshot/resource lifetime**: raw frames are RAM-only, ring-bounded, deterministically disposed; no durable screenshot storage in the core.
- **Failure behavior**: journaling, checkpointing, idempotent retries required from Task 2 onward, not deferred to hardening.
- **Non-goals**: no character voice/personality, final art/animation, audio/mic capture, multi-monitor support beyond a single-monitor guard, game-specific mods, unapproved browser-tab capture, distribution/updater/sync, cloud memory, model auto-switching, or pre-profiling numeric tuning — at any point before the Task 12/Stage 12 gate, and not even then, since that gate explicitly stops before personality/UI work (Stage 13 is a separate, later approval).

## 15. Decisions (revised per R10 — resolved, not open)

`tasks/review/FOREMAN_REVIEW.md` resolved all five items this proposal previously listed as open. Recorded here as the accepted decisions this revision builds on:

1. **Stack**: WPF + .NET 10 LTS is approved (§2). WinUI 3 remains deferred to the presentation layer, only if it demonstrates a concrete benefit there.
2. **`CaptureWorker` process isolation**: an out-of-process capture worker is required before real WGC capture is accepted (§6.1). Task 1 defines the `ICaptureWorker` contract and an in-process fake only; the real out-of-process worker is built starting Task 5, not before.
3. **Local store**: SQLite is approved as the single committed authority, with the checksummed-journal recovery-tail protocol specified in §5.1–§5.2 replacing the earlier ambiguous SQLite/NDJSON peer-store description.
4. **Minimized/exclusive-fullscreen handling**: graceful no-signal/paused behavior is approved as the design (§4), but is no longer asserted as available — both states are treated as unsupported until the required pre-Task-5 feasibility spike (§12, rows 1–2) proves otherwise on the actual target PC.
5. **Task scheduling**: Task 2 and Task 4 remain strictly sequential (§13); no parallelization of gated tasks, regardless of code-level dependency.

No product code, project scaffolding, or executable example accompanies this proposal, in line with the task's non-goals.
