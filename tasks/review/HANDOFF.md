# Task Handoff

## Task

CAL-02 — Calibration Recorder, Report, and Soak Protocol (`tasks/active/cal-02-calibration-recorder.md`), Stage 11 part 2.

- Branch: `agent/cal-02-calibration-recorder`.
- Base: `main` `50b4dd2`.
- Builder: Claude.

## Changed

- **New `CompanionCore.Calibration`:** sample, bounded recorder, and analyzer, with 11 tests.
- **App:**
  - `CalibrationSampler`;
  - `--calibration-log` and `--calibration-interval-ms`;
  - `--test-mode=calibrate`.
- **New `tools/CompanionCore.CalibrationReport`.**
- **App integration:** `CalibrationModeTests` (2 real-process tests).
- **`docs/Calibration-Protocol.md`.**
- Solution, lock files, and the packet.

## Verification

- **Local:**
  - 0 warnings, 0 vulnerable packages;
  - 939/939 on Linux;
  - the report tool was smoke-run on a synthetic 2-hour log.
- **Mutation:** 16 mutants; 15 killed, 1 equivalent (double ownership filter).
- **Windows CI:** pending (expected 956).

## Remaining

- CI, merge, and closure.
- Then CAL-03 and the smaller deferred items.
- Stop before Stage 12.

## Risks and assumptions

- The final thresholds need Boss's measured report (D1).

## Personal Round Judgments

CAL-02 J1–J4, recorded in the packet.

## Review focus

- Recorded fields are numbers and state names only.
- The log bounds.
- Opt-in recording.

## Next safe task

CAL-03.
