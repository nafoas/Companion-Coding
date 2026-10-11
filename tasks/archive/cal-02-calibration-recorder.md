# CAL-02 — Calibration Recorder, Report, and Soak Protocol (Stage 11, part 2)

Status: **accepted** — Paw Gate PASS; PR #47 squash-merged as `002aec598d9cae5730743e8f787fd27113085f26`
Authorized: 2026-10-11 by Boss ("Stage 11 should start now, and then stop at Stage 12").
Accepted remote base: `50b4dd260c99dc45f87902d07811013cf89a926b`
Working branch: `agent/cal-02-calibration-recorder`
Builder: Claude

## Objective

The design conversation (Question 9, "Baseline calibration") settled that final resource limits come from measuring the real prototype on Boss's 32 GB PC: each state separately, failure modes, and an 8–12-hour soak. Diagnostic records must contain "resource numbers and state transitions—not screenshots or private game content".

This packet builds the instrument. The measurements themselves need Boss's hardware.

## Required implementation

- **New `CompanionCore.Calibration` (platform-neutral).**
  - **`CalibrationSample`:** numbers, typed state names, and usage counters only, with no field able to hold a title, path, pixel, recollection, or provider text. Its state label follows the design's measurement list:
    - Resting;
    - Noticing;
    - Engaged;
    - Bnuy Mode;
    - Afterglow;
    - Watchbun phases.
  - **`CalibrationRecorder`:** JSON lines in its own folder, bounded so the log can never fill the disk:
    - 8 MiB per file, 4 KiB per line;
    - only the newest 8 recorder-owned files kept;
    - files it does not own are never touched;
    - failures are counted, never thrown.
  - **`CalibrationAnalyzer`:**
    - per-state median, p95, p99, and maximum;
    - least-squares growth after a 10-minute warm-up, needing at least 1 h of settled data. The verdict is Stable, SustainedGrowth, or InsufficientData, with a tolerance of 16 MiB/h or 2% of the median per hour;
    - recommended thresholds: soft 1.5×, worker restart 2×, emergency Naptime 3× of the sustained (p99) peak, each capped at a quarter of physical memory;
    - a Markdown report.
- **App.**
  - `--calibration-log` starts `CalibrationSampler`. Every 10 s (bounded 100 ms – 10 min) it records:
    - main-process working set, private memory, managed heap, handles, threads, and CPU;
    - capture worker metrics;
    - orchestrator state;
    - Braincase usage counters.

    It writes to a `Calibration` folder beside the memory root (the development root, or the isolated test root).
  - Without the flag nothing is recorded.
  - `--test-mode=calibrate` proves the path in the real process.
- **`tools/CompanionCore.CalibrationReport`:** a console report over a calibration folder (`--physical-memory-gb`).
- **`docs/Calibration-Protocol.md`:** Boss's step-by-step measurement of each design state, the soak, and how the report sets the final thresholds.

## Personal Round Judgments

- **J1 — Threshold multipliers.** The design asks for limits "comfortably above legitimate peaks while remaining far below anything that threatens your 32 GB system". Soft 1.5×, restart 2×, and Naptime 3× of the p99 sustained peak, capped at 25% of physical memory, implement that rule directly. They are recommendations for Boss's review, not auto-applied limits.
- **J2 — A leak is sustained growth.** The design: "A leak is indicated not merely by high usage, but by resources continuing to rise after work has settled." Hence the warm-up exclusion, the minimum 1 h window, and the slope tolerance.
- **J3 — Opt-in recording.** Recording is off unless Boss asks for it; diagnostics stay "hidden unless explicitly requested".
- **J4 — Measurement is Boss's to run.** The final thresholds wait for the report from Boss's PC. The provisional bounds stay in force meanwhile.

## Paw Gate

Gate result: **PASS** on 2026-10-11.

- **First CI (`17803b7`):** push run `38098832724` and PR run `38098841564` passed 955/956. One test-only Windows assumption failed: a default-share read of a log still open for writing. It was fixed in `ce22d99` by reading after the writer closes, and by asserting that a live log stays readable with shared access, which is how the report reads. There was no product change.
- **Head `ce22d99`:** push run `38099088822` and PR run `38099091132` each passed **956/956**. The real App recorded `CALIBRATION SAMPLES:3 FAILURES:0`.
- **Artifacts:**
  - archive digests verified;
  - attention-sheet PNG: 792×621, `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- **Merge.**
  - The merge ref had exact parents `50b4dd2` and `ce22d99`, and a tree equal to the head tree `b5e1033`.
  - It was squash-merged through the expected-head fence as `002aec5`.
- **Local:** 0 warnings, 0 vulnerable packages, 939/939 on Linux. Mutation: 16 mutants; 15 killed, 1 equivalent.

## Deferred findings

- **D1 — Applying the measured thresholds.** Wiring them into the main-process watchdog actions (soft cleanup, worker restart, Naptime) is the follow-up once Boss's report exists. The worker's provisional growth watchdog remains in force.
