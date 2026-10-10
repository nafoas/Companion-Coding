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

Pending.

## Personal Round Judgments

(Recorded during implementation.)

## Defects found by composition

(Recorded during implementation.)

## Deferred findings

(None yet.)
