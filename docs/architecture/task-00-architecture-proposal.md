# Task 0 — Architecture Proposal

Status: proposal, awaiting foreman review. No product code accompanies this document.

Reviewed documents: `AGENTS.md`, `docs/Claude-Companion-Core-Task-Packet.md`, `docs/Prince-Construction-Roadmap.md`, `docs/Prince-Design-BunDex.md`, `BUILD_LEDGER.md`.

## 1. Repository inventory

At the reviewed commit the repository contains only planning/control files: `AGENTS.md`, `BUILD_LEDGER.md`, `README.md`, `docs/Claude-Companion-Core-Task-Packet.md`, `docs/Prince-Construction-Roadmap.md`, `docs/Prince-Design-BunDex.md`, `docs/Shared-Codebase-Workflow.md`, `tasks/active/task-00-architecture.md`, `tasks/review/HANDOFF.md`, and `.gitignore`. No source tree, build files, or CI exist yet. Nothing here needs preservation or migration — Task 1 starts from a clean skeleton.

## 2. Recommended Windows stack

**C#, .NET 8, WPF for the shell UI.**

- `Windows.Graphics.Capture` (WGC), the API that satisfies "capture only the authorized target regardless of foreground/background," ships with first-class C#/WinRT projection. Doing this from C++ or Python means hand-rolling COM/WinRT interop for no benefit.
- GPU/native-resource lifecycle work (Direct3D11 surfaces, `IDirect3DDevice`) has mature managed wrappers (`Vortice.Windows` / `CsWin32`), which matters directly for the deterministic-disposal and resource-watchdog invariants.
- `System.Threading.Channels` plus a named `Mutex`/pipe give a straightforward, testable way to enforce "exactly one runtime" and bounded producer/consumer queues (capture ring, processing queue) without extra native dependencies.
- WPF over WinUI 3 for the placeholder shell: Task 1 needs a blank text box, placeholder icon, and status text — WPF gets there with `dotnet build`/`dotnet test` on a stock `windows-latest` CI runner and no MSIX packaging identity requirement. `PresentationAdapter` is the seam that isolates the UI framework from core logic, so this choice is not expensive to revisit later if the eventual personality layer wants WinUI 3/Fluent styling.

## 3. Credible alternatives and tradeoffs

| Option | Tradeoff | Verdict |
|---|---|---|
| C++/Win32 + Direct3D11 directly | Maximum control, but every invariant (single-instance, bounded queues, journal, database access) is hand-built or vendored; slower to a testable skeleton; native build friction makes unit-testing business logic (attention scoring, conversation coordination) harder. | Reconsider only if Task 12 profiling shows managed-runtime overhead is a real problem for this screenshot-cadence workload — no evidence of that yet. |
| Python (`pywin32` + `mss`/`dxcam`) | Fast to prototype, but `mss`/`dxcam` are full-screen/display-duplication based, not per-window with graceful occlusion handling; GIL contention between capture/semantic/UI threads; "clean checkout builds" is harder without bundling a Python runtime. | Fine for a throwaway spike, not for Task 1 onward. |
| Electron/Node | No first-class WGC access; would need native addons anyway, at which point C# is more direct. Wrong tool for native window capture and GPU-resource discipline. | Rejected. |
| WinUI 3 instead of WPF | Modern Fluent/Mica styling, closer to what a personality layer might eventually want. Costs MSIX/packaging-identity complexity and a Windows App SDK runtime dependency for a stage that only needs a blank text box. | Viable alternative for the *personality* layer later; not needed for the neutral core. |

## 4. Capture technology analysis

**Primary: `Windows.Graphics.Capture`** (`GraphicsCaptureItem.CreateFromWindow` + `Direct3D11CaptureFramePool`).

| Target state | Behavior | Design consequence |
|---|---|---|
| Visible, foreground | Fully supported, GPU-accelerated. | Baseline case. |
| Visible, occluded by other windows | Fully supported — WGC captures the window's own composited surface, not the screen region. This is what makes "watch a background app without observing the foreground app" possible at all. | Drives Task 4/Task 11 design directly. |
| Minimized | Not captured on older Windows 10/11 builds; support for capturing minimized windows was added on some later Windows 11 builds but is not guaranteed on the user's OS. | `CaptureWorker` must treat "target minimized" as a first-class paused state, not an error (Task 5 acceptance criteria already require resize/minimize/render-stall handling); `PresentationAdapter` gets a neutral status string for it. |
| Borderless windowed | Fully supported — behaves like any other composited window. | No special handling needed. |
| Exclusive fullscreen | Not reliably captured by any polite capture API, WGC included, when the app bypasses the DWM with its own swapchain. This is a genuine ceiling, not an implementation gap. | Detect no-signal/stale-frame condition and report a neutral "target not visible" status rather than silently producing empty attention data. Document as a known limitation; recommend borderless-windowed mode to the user for affected targets. |

**Rejected fallback: `PrintWindow`/`BitBlt`.** Works for some DWM-composited apps but frequently returns black/stale frames for GPU-rendered or fullscreen content, and doesn't give the same clean per-window isolation guarantee. Not a general fallback; could be a narrow, explicitly-flagged compatibility shim for one specific supported title in a later stage, but that's out of scope for the core.

**Rejected for the core path: Desktop Duplication API (`IDXGIOutputDuplication`).** Display-wide, not per-window — cropping to the target's screen rect breaks target isolation the moment another window overlaps that rect.

## 5. Local database and crash-journal choice

Two layers, matching the packet's `SessionJournal`/`MemoryStore` split:

- **`SessionJournal`**: append-only newline-delimited JSON file per session, `FileStream.Flush(flushToDisk: true)` after each write, periodic checkpoint markers. Recovery replays everything after the last valid checkpoint. No native dependency, human-inspectable, satisfies "kill during append and recover prior records" directly.
- **`MemoryStore`**: SQLite via `Microsoft.Data.Sqlite`, WAL mode, for the committed queryable store and derived indexes. SQLite has no built-in row immutability, so append-only is enforced entirely at the application layer: `LocalWriteGate` is the only code holding a write-capable connection, and its public surface has no `Update`/`Delete` method at all — the contract is "the capability doesn't exist," which Task 2's rejection tests then confirm holds under an adversarial proposal too.
- **Dev/production separation**: distinct root data directories (e.g. `%LOCALAPPDATA%\CompanionCore\dev` vs. `...\CompanionCore\prod`), selected by an explicit configuration value defaulting to `dev`, never inferred from ambient state (debugger attach, `dotnet run`, etc.).
- **Backups**: WAL checkpoint + file copy into a temp path, packaged as a compressed archive with a manifest (schema version, per-file checksums) written last; atomic rename of the "current backup" pointer only after the new archive validates. Never overwrite a known-good backup with output from a store that failed a health check first.

## 6. Process and worker isolation design

- `CompanionRuntime` lives in the main process and is the only place identity/lifecycle state is held. It is registered once via a startup-time named-`Mutex` (or named-pipe) single-instance guard in `CompanionCore.App`, checked before any other subsystem initializes.
- `CaptureWorker` runs on a dedicated background task/thread pool context (not a separate OS process, initially) so it can be cancelled and restarted independently without tearing down `CompanionRuntime`. Its GPU/D3D11 resources are scoped to its own lifetime; restarting it disposes and recreates the device/frame-pool rather than reusing possibly-corrupt state.
- `ApiBridge` calls are async and cancellable, isolated behind an interface so a hung/failed request cannot block the runtime's own state machine; retries and backoff are owned by `ApiBridge`, not by callers.
- The boundary that matters for the "no second identity" invariant: only `CompanionCore.App`'s composition root may construct a `CompanionRuntime`. `CaptureWorker` restarts, `ApiBridge` failures, and additional UI windows all reference the existing runtime instance via dependency injection; none of them can construct a new one. This is enforced by constructor visibility (internal factory, no public constructor) plus a Task 1 acceptance test that opens multiple windows and asserts one runtime.
- Open question for a later stage (not blocking Task 0): whether `CaptureWorker` should eventually move to a separate OS process for stronger crash isolation (a worker crash currently could still take down the host process if unhandled). Flagged in §14 as an open decision rather than decided here, since it affects IPC design the packet doesn't yet require.

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

```
                        ┌─────────────────────┐
                        │   CompanionRuntime    │  (single authoritative instance)
                        └──────────┬────────────┘
          ┌───────────┬────────────┼────────────┬─────────────┬─────────────┐
          ▼           ▼            ▼             ▼             ▼             ▼
 PresentationAdapter  TargetAuth  ConversationCoord  AttentionEngine  MemoryStore   ResourceWatchdog
          ▲           Service           │                  ▲             ▲             │
          │              │              │                  │             │             │
          │              ▼              ▼                  │             │             ▼
          │        CaptureWorker ──▶ VisualPipeline ────────┘             │      (restarts workers,
          │              │  (via PrivacyGuard for                        │       never touches
          │              │   non-game targets)                           │       committed memory)
          │              ▼                                               │
          │        (native frame/GPU resources,                          │
          │         ring buffer, bounded queue)                          │
          │                                                              │
          │        ApiBridge ─── ISemanticProvider: Mock / Replay / (disabled Real)
          │              │                                               ▲
          │              ▼                                               │
          │        LocalWriteGate ───────────────────────────────────────┘
          │              │
          │              ▼
          │        SessionJournal ──▶ MemoryStore (checkpoint/commit)
          │
          └── receives typed semantic events (observing/investigating/urgent/...) from
              AttentionEngine and ConversationCoordinator; never receives raw frames or
              character-voice text.

BackupRecoveryService reads MemoryStore + SessionJournal, writes archives; never in the
runtime's live request path.
```

Dependency rules this is meant to enforce structurally, not just by convention:

- `PresentationAdapter` depends only on typed events, never on capture, memory, or API internals — what keeps the personality layer swappable at the Stage 13 boundary.
- `ApiBridge` never holds a direct reference to `MemoryStore`; it only produces proposals `LocalWriteGate` accepts or rejects, so "API output can't bypass the write gate" is structural.
- `ResourceWatchdog` holds references only to workers/queues it may restart or clear, and has no reference into `MemoryStore`'s commit path.

## 9. Proposed source/test directory structure

```
/src
  CompanionCore.Runtime/          CompanionRuntime, lifecycle states, single-instance guard
  CompanionCore.Presentation/     PresentationAdapter, WPF shell (blank textbox/icon/status)
  CompanionCore.Capture/          TargetAuthorizationService, CaptureWorker, VisualPipeline, PrivacyGuard
  CompanionCore.Attention/        AttentionEngine
  CompanionCore.Conversation/     ConversationCoordinator, seed banks
  CompanionCore.Memory/           MemoryStore, LocalWriteGate, SessionJournal, BackupRecoveryService
  CompanionCore.Api/              ApiBridge, ISemanticProvider, Mock/Replay providers, real-provider shell
  CompanionCore.Diagnostics/      ResourceWatchdog, structured logging
  CompanionCore.App/              entry point, DI composition root, dev/prod data-root selection
/tests
  CompanionCore.Runtime.Tests/
  CompanionCore.Capture.Tests/
  CompanionCore.Attention.Tests/
  CompanionCore.Conversation.Tests/
  CompanionCore.Memory.Tests/
  CompanionCore.Api.Tests/
  CompanionCore.Fixtures/         synthetic event streams, replay fixtures, ICaptureSource test doubles
CompanionCore.sln
/scripts
  test.ps1
  build.ps1
```

`docs/architecture/**` (this proposal and future decision records) and the existing `docs/`/`tasks/` planning structure are unaffected by this layout.

## 10. Build, test, packaging, Windows-version strategy

- **Build/test**: xUnit, `dotnet build` / `dotnet test`, GitHub Actions `windows-latest` runner on every push to the working branch. Business-logic tests (`AttentionEngine`, `ConversationCoordinator`, `MemoryStore`, `LocalWriteGate`, `ApiBridge` against Mock/Replay) require no capture, network, or credentials, satisfying "no automated test may require paid API access."
- **Capture-layer tests**: `CaptureWorker`/`VisualPipeline` tested against an `ICaptureSource` abstraction with synthetic fixture implementations for CI. Real-WGC-against-a-real-window tests are valuable but fragile headlessly; treat as manual/nightly verification, not a PR gate.
- **Local script**: `scripts/test.ps1` wraps the same `dotnet test` invocation CI runs, so a human or the foreman can reproduce results locally.
- **Packaging**: none required at this stage (explicitly out of scope per non-goals). `dotnet publish -r win-x64 --self-contained` is sufficient for a runnable artifact when a human wants to try the skeleton; no installer/updater.
- **Windows version target**: Windows 10 1903+ (minimum for `Windows.Graphics.Capture`), with the minimized-window and exclusive-fullscreen limitations documented per §4. No attempt to support pre-1903 Windows 10.

## 11. Privacy and threat-boundary summary

- **Trust boundary 1 — capture**: `CaptureWorker` may only ever hold a handle to the single `TargetAuthorizationService`-approved HWND. It has no API to enumerate or capture any other window. `TargetAuthorizationService` itself may enumerate process/window metadata (for the consent prompt) without capturing pixels — enumeration and capture are separate capabilities so a bug in one can't silently grant the other.
- **Trust boundary 2 — content**: `PrivacyGuard` sits between `VisualPipeline` and semantic interpretation for authorized non-game targets, high-threshold and independently unit-testable in isolation from the rest of the pipeline. Trusted game targets may explicitly bypass content-level filtering, but never target isolation (boundary 1 still applies).
- **Trust boundary 3 — remote**: `ApiBridge` is the only component with network access. It never receives raw frames beyond what a request explicitly needs, never receives credentials in a form that could be logged, and every response is a proposal, not an authority — `LocalWriteGate` is boundary 4.
- **Trust boundary 4 — memory authority**: `LocalWriteGate` is the sole writer to `MemoryStore`. Anything upstream of it, human or model-originated, can only produce append proposals; update/delete simply isn't a capability that exists on the other side of that boundary.
- **Emergency boundary**: the stop-only privacy hotkey is a cross-cutting control that must reach `CaptureWorker` (stop, clear buffers), `VisualPipeline`/`ApiBridge` (cancel pending work), and `MemoryStore` (pause writes) without going through the normal event pipeline, so it stays effective even if another subsystem is misbehaving.

## 12. Risk register with feasibility spikes

| # | Risk | Impact | Spike / stop condition |
|---|---|---|---|
| 1 | WGC can't capture minimized windows on some OS builds | Capture silently stops on minimize | Spike: capture a minimized WPF test window on the actual dev/CI OS build before Task 5 starts; if unsupported, confirm the paused-state UX is acceptable rather than treating it as a defect to fix. |
| 2 | Exclusive-fullscreen games unsupported by any polite capture API | Some target apps simply can't be watched | Spike: attempt WGC capture against one exclusive-fullscreen test app early in Task 5; stop condition — if it silently returns stale/black frames rather than a detectable signal, the no-signal heuristic becomes a hard Task 5 requirement, not a nice-to-have. |
| 3 | SQLite has no native write-immutability | A `LocalWriteGate` bug could silently violate append-only | Task 2 acceptance tests already require asserting update/delete proposals are rejected; no separate spike needed, but flagged here since it's the invariant most likely to regress silently in refactors. |
| 4 | Dev build accidentally opens production data root | Cross-contaminates the eventual one-time production memory | Task 2 acceptance test covers this directly; stop condition — Task 2 does not pass review if the dev/prod root is inferable from ambient state rather than explicit configuration. |
| 5 | Global hotkey (`Ctrl+Shift+F12`) collision with another app | Privacy stop hotkey silently fails to register | Spike: verify `RegisterHotKey` failure is detectable at startup on a dev machine; if undetectable in some configuration, that's an architecture-affecting finding to bring back before Task 4. |
| 6 | GPU/native resource leaks under sustained capture | Slow memory/handle growth, eventual crash | Deterministic `Dispose`/`using` on every surface/texture; Task 5's multi-hour soak test is the stop condition — no sustained growth allowed to pass. |
| 7 | Backup interrupted mid-write | Corrupt archive replacing a good one | Task 3 acceptance criteria already require atomic build-validate-replace; no separate spike needed. |
| 8 | CI can't exercise real WGC capture against a real window headlessly | Capture-layer bugs slip past CI | `ICaptureSource` abstraction with synthetic fixtures for CI; real-capture verification is manual/nightly, not a PR gate — accepted limitation, not something to solve in the core. |
| 9 | Single-instance guard implemented incorrectly | Two runtimes could both claim authority | Task 1 acceptance test ("multiple windows cannot create multiple runtimes") is the stop condition. |
| 10 | Retry logic in `ApiBridge` double-commits a memory append | Duplicate memory records from a flaky network | Every request carries a local operation ID; append-by-operation-ID is idempotent at `LocalWriteGate`/`MemoryStore` — Task 2 and Task 7 acceptance criteria already cover this. |

## 13. Staged dependency map

```
Task 0 (this proposal)
  └─▶ Task 1 (skeleton: runtime, single instance, blank UI)
        ├─▶ Task 2 (memory store, journal, write gate)
        │     └─▶ Task 3 (backup/repair — depends on Task 2's store shape)
        │     └─▶ Task 7 (API bridge — write-gate proposals need Task 2's gate to exist)
        ├─▶ Task 4 (consent/target authorization — independent of Task 2/3)
        │     └─▶ Task 5 (bounded capture worker — needs an authorized target from Task 4)
        │           └─▶ Task 6 (regions/attention sheets — needs frames from Task 5)
        │                 └─▶ Task 8 (attention engine — scores events Task 6 produces)
        └─▶ (Task 7, see above)
              Task 8 + Task 7 ─▶ Task 9 (conversation coordinator — needs attention events + API bridge)
                                    └─▶ Task 10 (memory consolidation — needs Task 2 store + Task 9 output)
                                    └─▶ Task 11 (background continuity — needs Task 4 target auth + Task 9)
Task 3, 6, 10, 11 all feed ─▶ Task 12 (hardening + core gate)
```

Task 4 and Task 2 have no mutual dependency and could in principle be parallelized; whether to do so is a foreman scheduling call, not something the builder decides unilaterally.

## 14. Invariant and non-goal acknowledgement

- **One runtime, one identity**: enforced structurally (single-instance guard, internal-only runtime construction, §6), not just as a UI affordance.
- **One conversation thread, neutral non-response**: `ConversationCoordinator`'s data model tracks presentation-attempt counts, not "ignored" penalties, so silence structurally cannot mutate interest/sentiment state.
- **Local memory authority, append-only**: `ApiBridge` and every automated path produce proposals only; `LocalWriteGate` is the sole writer; corrections/supersession are new linked records, never mutations. No task after this one should introduce an `Update`/`Delete` path on committed memory.
- **Consent and target isolation**: capture is opt-in per target, tab/foreground changes never retarget, browsers are denied by default, the privacy hotkey is stop-only with mandatory explicit resume.
- **Screenshot/resource lifetime**: raw frames are RAM-only, ring-bounded, deterministically disposed; no durable screenshot storage in the core.
- **Failure behavior**: journaling, checkpointing, idempotent retries required from Task 2 onward, not deferred to hardening.
- **Non-goals**: no character voice/personality, final art/animation, audio/mic capture, multi-monitor support beyond a single-monitor guard, game-specific mods, unapproved browser-tab capture, distribution/updater/sync, cloud memory, model auto-switching, or pre-profiling numeric tuning — at any point before the Task 12/Stage 12 gate, and not even then, since that gate explicitly stops before personality/UI work (Stage 13 is a separate, later approval).

## 15. Open decisions requiring foreman approval

1. **WPF vs. WinUI 3** for the placeholder shell (§2) — recommending WPF; confirm before Task 1 scaffolding.
2. **`CaptureWorker` process isolation** (§6) — recommending in-process background task for now, with cross-process isolation deferred as a later hardening option rather than a Task 1 requirement. Confirm this deferral is acceptable given the "worker can restart without creating another runtime identity" invariant is satisfiable either way.
3. **SQLite vs. an alternative embedded store** (§5) — recommending SQLite + NDJSON journal; flagging in case the foreman has an existing preference (e.g. LiteDB) from prior related work.
4. **Minimized-window and exclusive-fullscreen handling** (§4, §12 items 1–2) — recommending graceful paused/no-signal states rather than attempting a `PrintWindow` fallback; confirm this is an acceptable ceiling for the core rather than something to spike further before Task 5.
5. **Task 4/Task 2 parallelization** (§13) — flagging that these two tasks have no mutual dependency; confirm whether the foreman wants them sequential (as task numbering implies) or is open to parallel bounded subtasks.

No product code, project scaffolding, or executable example accompanies this proposal, in line with the task's non-goals.
