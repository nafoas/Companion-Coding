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
- These tests need Windows, so execution evidence comes from Windows CI on both event paths. It is pending and will be recorded here with the per-phase durations.

## Remaining

- Both Windows CI paths, phase evidence, merge-ref check, merge, closure records, and post-merge `main` CI.

## Risks and assumptions

- The 90-second startup bound is sized at three times the worst cold start observed (≈30 s, J1).

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
