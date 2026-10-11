# CAL-01 — Agreed Calibration Numbers and Cadence Enforcement (Stage 11, part 1)

Status: **accepted** — Paw Gate PASS; PR #45 squash-merged as `a330a3ca5fcbcfbf67d63f51910b2233e0daf708`
Authorized: 2026-10-11 by Boss ("Stage 11 should start now, and then stop at Stage 12. I think we already discussed the calibration in the PDF somewhere, see if you can find the numbers we agreed on").
Accepted remote base: `ee15d2a79caa21e7c9049571a13ce70912695b4a`
Working branch: `agent/cal-01-agreed-cadence`
Builder: Claude

## What the design conversation agreed

Source: Boss's design conversation PDF and the Design BunDex.

| Agreement | Source | Status before this packet |
|---|---|---|
| 64 MB fixed screenshot ring | Question 9 ("Provisional memory protections") | Enforced (`CaptureWorkerMetrics.ScreenshotBudgetBytes`) |
| At most three full-resolution source frames | Question 9 | Enforced (`MaximumSourceFrames` = 3; the real source keeps 2) |
| One active vision request normally; no unbounded retry queue | Question 9 | Enforced (orchestrator single flight, bounded bridge retries) |
| Watchbun: a check after an hour of no input and no meaningful change, then sleepy consolidation after another hour | Watchbun bedtime, Boss's own words | Enforced (`QuietThreshold` and `SecondQuietThreshold` = 1 h) |
| BIC clock: a separate 30-second timer, offset from the game-observation clock | Expression clocks, Boss's own words | Enforced (`InitiatedClockInterval` = 30 s, offset 15 s) |
| Local capture/comparison: Noticing every 1–2 s, Investigating every 0.5–1 s | "Provisional capture timing" | **Not enforced:** frames were compared at the source rate |
| Semantic vision: Noticing about every 10–15 s or triggered; Investigating about every 4–8 s; Bnuy Mode event-driven with bounded frequency; Watchbun paused or heavily reduced; Suspended none | "Provisional capture timing" | **Partly enforced:** the Watchbun pause and suspension held, but no cadence limited interpretation |
| Final resource thresholds come from measured baselines and an 8–12-hour soak on the 32 GB system | Question 9, "Baseline calibration" | Deferred by design to measurement (CAL-02) |
| The Bun Budget is a locally configured limit expressed as sleepiness | Question 11D | Number intentionally unset; live API is Stage 12 |

Illustrative, not agreed: the adaptive next-check intervals (20–30 s static, 2–4 s dialogue), the 20–60 s encouragement cooldown, and the 10–15-minute Resume Capsule stability window. Those were marked "illustrative" or "might be". They remain calibration inputs, and the stability window belongs to live API (Stage 12).

## Required implementation

- **Worker local spacing.** `CaptureWorkerEngine.AgreedLocalFrameSpacing` = 1 s, the single spacing inside both agreed local ranges, used by the real capture source. Earlier frames are released at arrival. The first frame after a start or any reset (resize, stall, fault) always flows. The synthetic test source stays unthrottled.
- **Semantic cadence governor.** `OrchestratorOptions.SemanticCadence`:
  - Noticing: 12 s (agreed 10–15 s);
  - Engaged and Afterglow: 6 s (agreed 4–8 s);
  - Bnuy Mode and triggered looks: an event floor of 3 s.

  A sheet whose change score reaches the meaningful-change threshold (0.25) triggers an early look at the event floor. Every other sheet inside the interval is released at once (RAM-only). A new target session looks immediately. The governor is validated: the floor ≤ Engaged ≤ Noticing ≤ 5 min.
- **Conformance test.** It pins every agreed number as the running default.

## Personal Round Judgments

- **J1 — The 1 s local spacing.** The worker does not know the attention state (that would need a new IPC command), and 1 s satisfies both agreed ranges exactly.
- **J2 — Midpoints of the agreed ranges.** Defaults sit at the middle of each agreed range: 12 s in 10–15 s, 6 s in 4–8 s, and 3 s in the 2–4 s dialogue range for the event floor. All are configurable and remain calibration inputs.
- **J3 — "Or triggered" is a meaningful visual change.** The same 0.25 threshold already marks a Watchbun meaningful event. A trigger never goes below the event floor.
- **J4 — Scenario harness unthrottled.** The orchestration harness keeps an unthrottled cadence because scenario tests drive many sheets at one instant. Cadence has its own end-to-end tests with the real defaults. The App runs the agreed defaults.

## Paw Gate

Gate result: **PASS** on 2026-10-11.

- **CI.** Head `3c731c4`: push run `38097858502` and PR run `38097870359` each passed **943/943** on the first attempt. This includes the real Windows capture worker tests with the 1 s spacing.
- **Artifacts:**
  - archive digests verified;
  - attention-sheet PNG: 792×621, `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- **Merge.**
  - The merge ref had exact parents `ee15d2a` and `3c731c4`, and a tree equal to the head tree `c990426`.
  - It was squash-merged through the expected-head fence as `a330a3c`.

Local:

- Release build with `/warnaserror`: 0 warnings, 0 errors.
- Capture Worker 73/73, Orchestration 61/61.
- Mutation: 12 mutants, 12 killed.

## Deferred findings

- **D1 — State-aware local cadence.** Noticing at 2 s and Investigating at 0.5 s would need an attention-state IPC command to the worker. Revisit after the measured CPU baseline.
