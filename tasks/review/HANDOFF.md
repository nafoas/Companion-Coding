# Task Handoff

## Task

No task is active. The last accepted packet is CORE-12 (`tasks/archive/core-12-milestone.md`), merged through PR #61 as `18b5215e3bc5810d76937465314a55ed57ba323a`.

Builder: Claude.

**Stage 12 is built and waits for Boss.**

## VIS-04 (in review)

The Scene Sniff, part 4 of Boss's Vision upgrade. Packet: `tasks/active/vis-04-scene-sniff.md`. Branch: `agent/vis-04-scene-sniff`.

- **The model.** A bundled local vision model, Google's SigLIP base/16 (Apache-2.0), chosen over CLIP for its shipping license (J1). It runs on ONNX Runtime, on DirectML or the CPU, whichever wins a warm-up race.
- **Shipping.** The build fetches the model with a pinned digest into a git-ignored cache; CI caches it. It is never committed.
- **Reading.** The whole frame is read off the frame path, one read at a time, on Sniff triggers (at most every 250 ms) or every 1 s.
- **What leaves the worker.** Only novelty against the last reported scene, a shift flag, and a fixed-vocabulary label (protocol v7). The embedding never leaves.
- **Prince's reaction.** A shift perks his ears below Bnuy Mode and brings the next Glance forward; scene labels habituate. ONNX Runtime telemetry is off (J3).
- **Evidence.**
  - Windows CI ran the real model: cosine 1.00000 to the Python reference, CPU, 388 ms median on the runner.
  - Mutation pass: 43 of 44 mutants caught.
- **For the reviewer:** J1–J5; the DirectML diagnostics note in `THIRD-PARTY-NOTICES.md`; GPU speed is measured on Boss's PC.

## VIS-03 (accepted)

The Text Sniff, part 3 of Boss's Vision upgrade, merged through PR #69 as `86deb5f`. It passed 1123/1123 on Windows CI, where the real OCR engine was available. The packet is `tasks/archive/vis-03-text-sniff.md`.

**FIX-03** (test-only race in the platform exit test) was merged through PR #71 as `4959bf0`.

## VIS-02 (accepted)

Glance (Haiku, peripheral) and Inspect (focus), merged through PR #67 as `da7ea82`. It passed 1102/1102 on Windows CI. The packet is `tasks/archive/vis-02-glance-inspect.md`.

## VIS-01 (accepted)

The Sniff, part 1 of Boss's Vision upgrade, merged through PR #65 as `2defb17`. It passed 1076/1076 on Windows CI, including the real-process sniff latency test (p95 ≤ 100 ms). The packet is `tasks/archive/vis-01-sniff-foundation.md`.

## FIX-02 (accepted)

Main CI on `de2646a` failed one crash-loop test whose settle wait gave up by poll count on a slow runner. The test-only fix (PR #63, `288c81f`) waits by wall clock and reports bridge diagnostics on timeout. It passed 1044/1044 on both CI runs. Ledger entry: FIX-02.

## Changed (CORE-12 closure)

This docs-only reconciliation:

- archives CORE-12;
- updates `BUILD_LEDGER.md` and `README.md`;
- resets this handoff.

## Verification

- CORE-12 head `0d8b65b`, tree `c989179`: push run `38142489617` and PR run `38142501429` passed 1044/1044 with verified artifacts.
- The merge ref had exact parents and an equal tree.

## Remaining (Boss)

1. **The first live check.** Follow `docs/Builder-Braincase-Setup.md`: a Console account, then `setx ANTHROPIC_API_KEY` or `ant auth login`, then run `tools/CompanionCore.BraincaseCheck` with `--live` (and `--turn-on`).
2. **The milestone hand-run** (`docs/Core-Prince-Milestone.md`), and the **Core Bun Approval Hop**.

## Next safe task

None until the Core Bun Approval Hop. Stage 13, personality installation on Builder Prince, follows it.
