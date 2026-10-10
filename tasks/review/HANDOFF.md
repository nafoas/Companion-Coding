# Task Handoff

## Task

Task 11 — Application-Bound Watchbun Continuity (`tasks/active/task-11-watchbun-continuity.md`), Roadmap Stage 9.

Builder: Claude.

## Completed

- **New `CompanionCore.Watchbun`** (references only Capture.Contracts):
  - `WatchbunEngine`: a deterministic, checkpointed state machine bound to one authorized target. It covers:
    - target-only, non-focus-stealing, rate-bounded alerts;
    - foreground-gated input attribution;
    - Watchbun Tasks;
    - the two-stage quiet check with paused semantic spending;
    - indefinite watch;
    - close/crash classification with Boss decisions;
    - relaunch re-authorization by grant only;
    - Paused Adventures;
    - lock/sleep suspension with frozen clocks;
    - restart recovery without capture.
  - `StructuredGameEventAdapter`: the strict synthetic event-line adapter.
- Judgments J1–J9 are recorded in the packet.

## Changed

- `CompanionCore.slnx`
- `src/CompanionCore.Watchbun/**` (new)
- `tests/CompanionCore.Watchbun.Tests/**` (new)
- `src/CompanionCore.Capture.Contracts/AssemblyInfo.cs` (one test-only friend line)
- This packet and this handoff

## Verification

- Local gate (Linux, cross-targeted build), with the 13 App integration tests deferred to Windows CI:
  - locked restore;
  - Release build with `--no-incremental /warnaserror`: 0 warnings, 0 errors;
  - vulnerability audit: 0 across 32 projects;
  - tests: **674/674** (Watchbun 66, new).
- Expected on Windows CI: 687.
- Mutation pass: 64 mutants; 63 killed, 1 equivalent.
  - The equivalent is the defensive `checkPending` guard on the Dozing invariant.
  - The first run left six survivors:
    - three real test gaps, now closed: process-only foreground match, minor-change wake, and answering during an indefinite watch;
    - two redundant activity signals, now removed: completion and cancellation;
    - the equivalent above.
- CI: pending.

## Remaining

- Gate CI, the evidence descendant, merge, and closure.
- Then a Stage 10 keepsakes packet.

## Risks and assumptions

- Quiet thresholds, task bounds, and alert rates are provisional until Stage 11.
- Real OS foreground, input, process, and lock/sleep hooks are deferred to wiring (J9).

## Review focus

- Capture never names a non-target application.
- Reattach requires a grant for the same executable.
- Clocks freeze outside attached, unsuspended time.
- Restore recovers without capture.

## Repository state

- Branch `agent/task-11-watchbun-continuity`, based on `main` `08063ce`.

## Next safe task

After closure: open a Stage 10 keepsakes packet.
