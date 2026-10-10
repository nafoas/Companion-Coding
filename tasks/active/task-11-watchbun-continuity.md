# Task 11 — Application-Bound Watchbun Continuity

Status: **active — Paw Gate review**
Authorized: 2026-10-10 under Boss's standing direction to continue autonomously through the pre-API tasks under the Paw Gate model
Accepted remote base: `08063ceda8287cb945d63f4788d1c9f3c794d99b`
Working branch: `agent/task-11-watchbun-continuity`
Builder: Claude

## Objective

Build the personality-neutral Watchbun mechanics for Roadmap Stage 9, so background companionship stays reliable across tab-aways, idle periods, crashes, and returns. The session belongs to one authorized application and stays attached to it.

## Entry evidence and authority

- Stage 8 is accepted (Task 10, PR #31; closure PR #32; `main` `08063ce`).
- The neutral-core packet's Task 11 lists:
  - background target monitoring;
  - target-specific input and activity tracking;
  - temporary watch tasks;
  - a first quiet-hour check with paused semantic spending;
  - a second quiet-hour safe closure;
  - an indefinite-watch override;
  - a close/crash distinction with user confirmation;
  - paused-session and relaunch recovery;
  - lock/sleep/wake suspension;
  - non-focus-stealing neutral alerts.

  It uses a synthetic structured-event adapter, not a game mod.
- Behavioral authority is the Design BunDex section "Watchbun Mode and session lifecycle":
  - tabbing away never ends a session or changes the target;
  - quiet means no target-game input, no meaningful game change, and no pending Watchbun Task;
  - after one quiet hour, Prince asks; semantic screenshots and API calls pause while a cheap local wake detector remains;
  - the options are keep watching, naptime, and "I'll be back eventually"; the last disables abandonment timers for that Watchbun period;
  - another unanswered quiet hour leads to checkpoint, consolidate, close, and nap;
  - game closure and a suspected crash raise different prompts, and Boss decides: consolidate, wait for relaunch, or preserve a Paused Adventure;
  - sleep, lock, restart, or crash suspends capture and restores from the local record;
  - sessions end because Boss says so.
- Roadmap Stage 9 Paw Gate scenarios are listed below in neutral form.

## Required implementation

### New `CompanionCore.Watchbun` project (and tests)

It references only `CompanionCore.Capture.Contracts`, for the target identity.

**`WatchbunEngine`.** A deterministic, clock-driven state machine for one authorized target session. It emits typed intents; wording belongs to the presentation adapter.

- **Target binding.** The engine is bound to one `CaptureTargetIdentity` and never retargets on foreground changes. Capture permission names only the bound target, or nothing.
- **Activity attribution.**
  - Input counts as target activity only while the bound target's window and process are foreground.
  - Input in any other application, including a Nap-listed one, is never attributed to the target and never causes its capture.
- **Structured game events** (synthetic adapter):
  - events for another target are rejected and counted;
  - meaningful events reset quiet time;
  - urgent events and Watchbun Task matches raise non-focus-stealing, target-only alerts;
  - alerts are rate-bounded.
- **Watchbun Tasks.** Temporary watch tasks:
  - a bounded count and bounded lifetime;
  - a pending task holds off quiet time;
  - completion raises an alert;
  - expiry is visible.
- **Quiet timers.**
  - After one continuous quiet hour of non-suspended time:
    - the engine asks the check once;
    - semantic spending pauses;
    - capture drops to local wake detection.
  - If the check goes unanswered for another quiet hour, the engine checkpoints, requests consolidation, closes, and naps.
  - Non-response produces no preference or relationship signal.
- **Answers.**
  - *Keep watching* resumes the session.
  - *Naptime* closes it the same safe way.
  - *Back eventually* enables indefinite watch: no abandonment timers until Boss returns. Spending stays paused until real activity, so costs remain bounded.
- **Exit classification.**
  - A clean exit (window closed before the process exited with code 0) gets the naptime prompt.
  - Anything else (nonzero code, no clean window close, or a hung window) gets the adventure-over prompt.
  - Capture and spending stop immediately, and the session is preserved until Boss decides.
- **Decisions.** Consolidate (close), wait for relaunch, or preserve a Paused Adventure.
  - A relaunch of the same executable (file name and path fingerprint) requests re-authorization only.
  - Reattaching requires an explicitly authorized identity of the same executable; there is no capture before authorization.
- **Lock/sleep/wake.**
  - Suspension stops capture and spending and freezes the quiet and task clocks.
  - Resume restores the prior state.
- **Checkpoint/restore.** A complete, validated checkpoint.
  - A restored engine starts in recovery: no capture or spending until the target is re-authorized and reattached, or its exit is reported.

**`StructuredGameEventAdapter`.** A synthetic, strict parser for structured event lines: a bounded line length, typed fields, unknown fields rejected, and a target-bound session.

## Explicitly forbidden

- Capturing or naming any non-target application.
- Focus stealing.
- Retargeting on tab-away.
- Process injection.
- Real OS hooks or app wiring (deferred to the wiring task).
- Live API.
- Personality wording.
- Any committed-memory write. The engine only requests consolidation; Recall and memory writes stay in their accepted projects.

## Acceptance scenarios

1. **Tab-away alert.** Tab away (another application foreground) while the synthetic game changes: the correct target-only, non-focus-stealing alert is raised. Events for another target raise nothing.
2. **Nap-listed foreground.** With a Nap-listed application foreground, its input is never attributed. Capture permission names only the bound target throughout, and the target itself is never retargeted.
3. **Two-stage quiet.** One quiet hour asks once and pauses spending (local wake only). A second unanswered hour checkpoints, requests consolidation, closes, and naps. Activity before either stage resets the clock, and a pending Watchbun Task holds it off.
4. **Indefinite watch.** "Back eventually" leaves no abandonment timer across many quiet hours, spending stays paused (bounded), and Boss's return restores normal timers.
5. **Crash/relaunch.**
   - Close and crash are distinguished.
   - The session is preserved until Boss decides.
   - Waiting for relaunch requests re-authorization for the same executable only and refuses a different one.
   - Reattach resumes, and a Paused Adventure stays recoverable.
6. **Lock/sleep/wake.** Suspension stops capture and spending, freezes the clocks, and resumes the exact prior state.
7. **Watchbun Tasks.** Count and lifetime are bounded; completion alerts and expiry are visible.
8. **Checkpoint/restore.** A restore round-trips exactly, invalid checkpoints are refused, and a restored engine stays in recovery without capture until reattached.
9. **Adapter.** Malformed, oversized, unknown-field, or wrong-target lines are refused.
10. **Regression.** All 621 accepted tests still pass on Windows CI.

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Watchbun/**` and `tests/CompanionCore.Watchbun.Tests/**`
- `src/CompanionCore.Capture.Contracts/AssemblyInfo.cs` (one test-only friend line, so tests can mint synthetic grants exactly like the accepted Api and TargetAuth tests)
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Candidate evidence (pending the evidence-descendant CI, merge-ref check, and acceptance):

- Implementation head `5b96720`:
  - push run `38058141112` and PR run `38058154820` each passed **687/687** (14 test projects, Watchbun 66, App Integration 13);
  - test-results and attention-sheet artifacts: archive digests verified;
  - attention-sheet PNG: 792×621, `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- Local gate: 674/674, 0 warnings, 0 vulnerable packages across 32 projects.
- Mutation pass: 64 mutants; 63 killed, 1 equivalent.
- Acceptance scenarios 1–9 are covered by `WatchbunScenarioTests`, `WatchbunMechanicsTests`, and `StructuredGameEventAdapterTests`. Scenario 10 is covered by the full CI suite.

## Personal Round Judgments

- **J1 — Reattach takes a grant.** Reattach accepts only a `CaptureAuthorizationGrant`, which only TargetAuth can mint. "No capture before authorization" is therefore enforced by type, and a relaunch merely requests re-authorization. Matching uses the executable file name (case-insensitive, as on Windows) plus the path fingerprint, so PID or window reuse cannot impersonate the target.
- **J2 — Quiet and task clocks use active time.** Both clocks count only time that is attached and not suspended. Lock, sleep, exit prompts, relaunch waits, paused adventures, and recovery all freeze them, so a long sleep never produces a false check or close. Long jumps advance deadline by deadline, so stages happen in order.
- **J3 — Indefinite watch is bounded.** "Back eventually" removes both abandonment stages for that Watchbun period, but semantic spending stays paused until real activity. A meaningful game event wakes attention without re-asking. Only Boss's own target input (or a watch-task instruction) ends the period.
- **J4 — Activity attribution.**
  - Raw local input counts only while the bound target's window *and* process are foreground. Matching only one of the two is not enough.
  - Instructions Boss gives Prince (adding a watch task, answering the check) count as presence.
  - A pending watch task already holds quiet time, so a completion or cancellation needs no separate activity signal. The mutation pass showed that the earlier explicit signals were redundant, and they were removed.
- **J5 — Exit classification is conservative.** Only a zero exit after the window closed, and not hung, is a deliberate close. Everything else is a suspected crash. Either way, capture stops and the session waits for Boss indefinitely; no timer ends an exit prompt.
- **J6 — Events during suspension are refused, not dropped silently.** The engine refuses events with `Suspended` and changes nothing, so the adapter can redeliver them after resume.
- **J7 — Restart recovery.** A restored attached session starts in `Recovering`, with no capture or spending until it is reattached by grant or its exit is reported. Suspension flags are not restored, because the restarted runtime observes the system afresh. Non-attached phases restore exactly.
- **J8 — Alerts are rate-bounded.** At most 6 per minute (provisional). Excess alerts are counted, not queued, so a burst cannot create unbounded presentation work. A watch-task completion is still recorded even when its alert is suppressed.
- **J9 — Real OS hooks are deferred.** Foreground and input monitors, process-exit observers, and lock/sleep notifications are deferred to the wiring task, consistent with the accepted Tasks 7–10. This task proves the semantics through typed inputs and the synthetic adapter.

## Deferred findings

(None yet.)
