# Task Handoff

## Task

CAL-01 — Agreed Calibration Numbers and Cadence Enforcement (`tasks/active/cal-01-agreed-cadence.md`), Stage 11 part 1.

- Branch: `agent/cal-01-agreed-cadence`.
- Base: `main` `ee15d2a`.
- Builder: Claude.

## Changed

- **Worker:** `CaptureWorkerEngine` gains the agreed 1 s local frame spacing, used by the real source.
- **Orchestration:** `SemanticCadence` on `OrchestratorOptions`, enforced in the sheet path.
- **Tests:**
  - an engine spacing test;
  - `OrchestrationCadenceTests` (cadence end to end, the state mapping, validation, and agreed-number conformance);
  - the harness defaults to an unthrottled cadence.
- The packet.

## Verification

- **Local:**
  - 0 warnings, 0 vulnerable packages;
  - 928/928 on Linux.
- **Mutation:** 12/12 killed.
- **Windows CI:** pending (expected 943).

## Remaining

- CI, merge, and closure.
- Then CAL-02 (calibration recorder and soak protocol) and CAL-03 (failure-mode hardening).

## Risks and assumptions

- The cadence values sit at the midpoints of the agreed ranges and stay calibration inputs.
- Real-hardware CPU and RAM baselines are not yet measured.

## Personal Round Judgments

CAL-01 J1–J4, recorded in the packet.

## Review focus

- The cadence gate releases sheets (RAM-only).
- The first frame after a reset is never spaced out.

## Repository state

- Branch `agent/cal-01-agreed-cadence`, based on `main` `ee15d2a`.

## Next safe task

CAL-02.
