# Task Handoff

## Task

R5 — App Integration Cold-Start Bounds (`tasks/active/task-r5-app-cold-start-bounds.md`). Branch `agent/task-r5-app-cold-start-bounds`, based on accepted `main` `ed5f9a88685f38c84ccd07841cbf163f339863d9`.

Builder: Claude.

## Completed

- `AppProcess.Run` now measures two bounded phases:
  - **Startup:** launch until the scenario's first stdout marker, bounded by the new 90-second cold-start liveness bound (J1).
  - **Exit:** marker until process exit, bounded by the unchanged 30-second `ExitTimeout`.
- A timeout in either phase kills the process tree and names its phase.
- Standard error is drained from launch, and draining output after exit is now bounded (it previously blocked without bound).
- `AppProcessTests` logs both phase durations for every launch. The shutdown test also asserts that its exit phase was inside the exit bound.
- The second-process test uses the startup bound for the held process's first marker and the exit bound after release.

## Changed

- `tests/CompanionCore.App.IntegrationTests/AppProcess.cs`
- `tests/CompanionCore.App.IntegrationTests/AppProcessTests.cs`
- this handoff and the active packet

## Verification

- `dotnet build CompanionCore.slnx -c Release --no-restore /m:1 /p:EnableWindowsTargeting=true /warnaserror --no-incremental`: 0 warnings, 0 errors.
- `git diff --check` is clean.
- Candidate `a507a4f`: push run `38033166320` and PR run `38033174631` each passed 453/453. Both artifact digests verified on each path, and the attention-sheet PNG keeps the accepted digest `5eb11c96…b046dd`.
- Per-phase evidence from the TRX output:

  | Run | Scenario | Startup | Exit |
  |---|---|---|---|
  | push `38033166320` | shutdown | 28.983 s | 2.026 s |
  | push `38033166320` | multiwindow | 0.500 s | 0.095 s |
  | push `38033166320` | ready (×2) | 0.371 s, 0.104 s | 0.072 s, 0.010 s |
  | PR `38033174631` | shutdown | 12.118 s | 2.038 s |
  | PR `38033174631` | multiwindow | 0.970 s | 0.147 s |
  | PR `38033174631` | ready (×2) | 0.873 s, 0.237 s | 0.164 s, 0.024 s |

  The push run's 28.98 s cold start plus its 2.03 s exit totals 31 s. That would have failed the old single 30-second bound, and it now passes with shutdown measured precisely. This confirms the diagnosis.

## Remaining

- CI on this evidence descendant, the merge-ref check, merge, closure records, and post-merge `main` CI.

## Risks and assumptions

- The 90-second startup bound is sized at three times the worst cold start observed (≈30 s, J1).
- The shutdown exit phase takes about 2 s (deferred finding 1, Stage 11 profiling).

## Personal Round Judgments

J1–J2 are recorded in the packet.

## Review focus

- Phase boundaries match each test mode's single marker.
- No bound is unbounded.
- The 30-second exit bound is unchanged.

## Repository state

- Branch `agent/task-r5-app-cold-start-bounds`.

## Next safe task

Complete this Paw Gate. Then open Task 8 (attention engine, Stage 6).
