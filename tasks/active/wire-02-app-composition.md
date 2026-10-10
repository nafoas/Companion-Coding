# WIRE-02 — Windows Platform Signals and App Composition

Status: **active — implementation**
Authorized: 2026-10-10 by Boss ("For now, wire everything together, and do a bunch of tests to make sure everything works as it should…"). This is the second wiring packet, named as D3 in the accepted WIRE-01 packet.
Accepted remote base: `125060236c62e221c9d22bb04d8e625d4e30ddf0`
Working branch: `agent/wire-02-app-composition`
Builder: Claude

## Objective

Make the real Windows App run the accepted orchestration core end to end:

- real OS platform signals for the one authorized target;
- one `CompanionHost` on the development data root, or an isolated test root in test mode;
- the offline Braincase shell (no credentials, no network);
- a bounded tick;
- typed notices rendered through the presentation adapter as neutral placeholders.

## Authority

- Boss's 2026-10-10 direction.
- Task 11 J9: real foreground, input, process-exit, and lock/sleep hooks were deferred to "the wiring task".
- WIRE-01 D3.
- Task 12 (credentials and live API) remains a stop condition and is not touched.

## Required implementation

### New `CompanionCore.Platform.Windows` project

`WindowsPlatformSignals : IPlatformSignals` sits behind an injectable `IWindowsPlatformNative` seam, so all its logic is testable off-Windows.

- **Foreground (target-only minimization).** The adapter reports `ForegroundWindow(target window, target process)` when the bound target is foreground, and `ForegroundWindow(0, 0)` otherwise. No other application's window or process identity ever leaves the adapter. No titles, text, or content are read.
- **Input.** The adapter polls the system last-input tick (`GetLastInputInfo`): timing only, never keys, never hooks. Watchbun attributes input to the target only while the target is foreground (Task 11 J5).
- **Target exit.**
  - A process handle is held from `WatchTarget`, giving the exit code. The window's existence and hung state are polled.
  - `WindowClosedFirst` is true only when the window was observed gone in an *earlier* poll than the exit.
  - `WasHung` is the last hung state observed while the window existed.
  - Ambiguity therefore errs toward a suspected crash (the conservative Watchbun prompt).
  - A process that cannot be opened or queried reports exit code −1.
- **Relaunch.** While a relaunch is awaited, the adapter polls title-free discovery (`ITargetDiscovery`) on a bounded interval. A same-executable candidate (same fingerprint and executable name) with a different process raises `TargetLaunched` once per process.
- **Lock and sleep.** `ReportSuspended` and `ReportResumed` are fed by the App's `SystemEvents` (session lock/unlock, power suspend/resume).
- **Polling.** One background loop with bounded intervals. Polling runs only while a target or relaunch is watched. It disposes cleanly.

### App composition (`CompanionCore.App`)

- **`CompanionHost` data root.**
  - Normal runs use `DevelopmentDataRootPolicy` under `%LOCALAPPDATA%\CompanionCore.Dev`.
  - In `--test-mode`, the host uses an isolated `TestDataRootPolicy` root: `--test-data-root=<absolute path>` and `--test-run=<guid>`, or a fresh temporary root. A test run never touches development data.
  - The provider is `RealSemanticProviderShell` over an `InMemoryCredentialStore` (offline). The platform signals are `WindowsPlatformSignals`.
- **Tick.** A dispatcher timer calls `TickAsync`.
- **Notices.** Notices are marshalled with `BeginInvoke` (never blocking the mailbox) to `MainWindow`, which renders them through `IPersonalityAdapter.Map(CompanionNotice)`.
- **`SystemEvents`.** Subscribed for lock and sleep, and unsubscribed at exit.
- **Shutdown order.** Host, then platform signals, then target controller, then runtime.

### Presentation

- `IPersonalityAdapter.Map(CompanionNotice)` and its neutral implementation.
- Content keys:
  - `companion.<kind>` for fixed notices;
  - `companion.<family>.<member>` for typed intents.
- Placeholder templates are resolved per family. Every enum member resolves to a defined placeholder.

### App integration scenario

`--test-mode=wired` starts the full composition on an isolated test root. It prints:

- that the host started;
- the fault count;
- the conversation thread id;
- that nothing is unconsolidated.

It then stops cleanly. A second launch on the same root restores the same conversation thread (real-process restart persistence).

## Explicitly forbidden

- Live or paid API, and real credentials.
- Global keyboard or mouse hooks, and reading window titles or text.
- Capturing anything but the authorized target.
- Production data roots, and test runs touching the development root.
- Personality wording.
- Weakening any accepted invariant or test.

## Acceptance

1. **Platform logic** (fake native):
   - target-only foreground minimization;
   - input changes;
   - exit with deliberate, crash, and hung classifications, including same-poll ambiguity;
   - unopenable processes;
   - relaunch detection, once per process;
   - lock and sleep forwarding;
   - no polling while nothing is watched;
   - idempotent dispose.
2. **Real native on Windows CI.** A real child process's exit code is observed through the same loop. The foreground and last-input queries succeed.
3. **Presentation.** Every notice kind and intent member maps to a non-unknown placeholder. Mapping is total and deterministic.
4. **App.**
   - `--test-mode=wired` starts the full host and reports zero faults.
   - A second launch restores the same conversation thread.
   - The accepted ready, multiwindow, shutdown, and second-process scenarios still pass within their bounds.
5. **Regression.** All 793 accepted tests pass on Windows CI.

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Platform.Windows/**` and `tests/CompanionCore.Platform.Windows.Tests/**`
- `src/CompanionCore.App/**`
- `src/CompanionCore.Presentation/**` and `tests/CompanionCore.Presentation.Tests/**`
- `tests/CompanionCore.App.IntegrationTests/**`
- Friend lines, and any composition-exposed defect fix in an accepted component, with a regression test
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Gate result: **PASS** on 2026-10-10 (merge-ref check and merge are recorded at closure).

**CI evidence.**
- **Candidate `e63242c`:** push run `38078377838` and PR run `38078393543` each passed **908/908** on the first attempt:
  - 18 test projects;
  - Platform.Windows 22, including the real-Win32 child-process exit-code and running-process checks;
  - Presentation 141;
  - App Integration 15, including both `wired` scenarios;
  - Orchestration 48.
- **Real-process evidence.** Two launches on one isolated test root printed `WIRED ROOT:Test FAULTS:0 LINEAGE:79a6dccba8a042ef8d2f32d05c56bf15 UNCONSOLIDATED:0 CONSTRUCTIONS:1` with the identical lineage. Startup was about 1.2 s, and exit 0.2–0.4 s.
- **Artifacts:**
  - test-results and attention-sheet archive digests verified;
  - attention-sheet PNG: 792×621, `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.

**Local evidence.**
- Locked restore and a `--no-incremental` Release build with `/warnaserror`: 0 warnings, 0 errors. 0 vulnerable packages.
- 893/893 on Linux. The Windows-only native and App tests early-return there.
- **Mutation pass:** 34 mutants over the platform signals, the notice mapping, and placeholder resolution. The first pass left 3 survivors; each became a sharper test:
  - the input baseline across an unavailable tick;
  - a same-fingerprint, renamed-executable relaunch;
  - a non-companion key that merely contains a family prefix.

  Final result: **34 killed**.

**Actual-diff review.**
- Changes stay inside the allowed scope.
- The only edit to an accepted component outside the new projects and the App is the additive `OrchestratorSnapshot.ConversationLineage`, with Scenario 7 asserting that it survives a restart.
- No credentials, network, hooks, titles, or text. Test runs never open the development root.

## Personal Round Judgments

- **J1 — Target-only foreground.** Only "the target is foreground" or `ForegroundWindow(0, 0)` leaves the adapter. Watchbun needs nothing more, and no other application's identity is ever propagated.
- **J2 — `WindowClosedFirst` needs an earlier poll.** A window seen gone in the same poll as the exit does not count. Ambiguity therefore errs toward the conservative suspected-crash prompt, never toward a silent deliberate close.
- **J3 — Unopenable process.** When a process (for example an elevated one) cannot be opened, its exit is observed by its window disappearing, with exit code −1, which classifies as a suspected crash. When a handle answers "unknown", watching continues while the window lives.
- **J4 — Window owner mismatch.** A window that no longer belongs to the watched process at `WatchTarget` is an immediate unknown exit. A reused handle or process id is never watched.
- **J5 — Data roots.** Normal runs use the fixed development root. `--test-mode` always uses an isolated `TestDataRootPolicy` root (explicit, or a fresh temporary one), so integration tests never read or write Boss's development data.
- **J6 — Presentation maps notices.** Presentation references Orchestration so the adapter contract can map notices. Keys are `companion.<kind>[.<member>]` with family templates, and every member resolves without per-member strings. The Stage 13 adapter replaces the wording.
- **J7 — Conversation lineage.** `OrchestratorSnapshot.ConversationLineage` exposes the coordinator id, which is the one Conversation Thread lineage. It makes "the same thread across restarts" directly assertable in-process and from the real App.
- **J8 — Serialized App launches.** All App-launching test classes share one xUnit collection, so real launches never race the single-instance guard.
- **J9 — Provisional intervals.** Polling runs at 100 ms while watching, 500 ms idle, and 2 s for relaunch discovery, with a 1 s dispatcher tick. All are provisional until Stage 11.
- **J10 — Offline Braincase.** The App uses the offline Braincase shell with an in-memory credential store. Braincase reports "unavailable" once (WIRE-01 J5). Live use stays Task 12.

## Defects found by composition

None in accepted components. The candidate passed Windows CI on its first attempt.

## Deferred findings

- **D1 — Interactive controls.** The App has no interactive controls yet for camera actions, quiet-check answers, exit decisions, watch tasks, or Bnuy Repairs. Those surfaces belong to the presentation phase. The orchestrator commands exist and are tested.
- **D2 — Undiscoverable relaunches.** Relaunch detection sees only windows that title-free discovery accepts. A relaunched game that stays cloaked or tool-windowed is detected once it becomes eligible.
- **D3 — Temporary test roots.** Fresh temporary test-mode roots (when no `--test-data-root` is given) are left under the temp directory.
- **D4 — Lock and sleep on real hardware.** Lock and sleep come through `SystemEvents`. CI proves the subscription and forwarding logic, but not a real lock or suspend. That needs target-PC verification (Stage 11).
