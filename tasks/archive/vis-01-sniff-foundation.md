# VIS-01 — The Sniff: fast local noticing within 100 ms (Vision upgrade, part 1)

Status: **accepted** — Paw Gate PASS; PR #65 squash-merged as `2defb17a1ccda22cac3318098faf6890ab178d56`.
Authorized: 2026-10-11 by Boss:

- "he needs to see, notice, and respond, without an immersion killing delay … Glance ideally needs to be 100 ms or less, and happen frequently to catch even quick stuff … I want this done now, with greatly reduced latency. I have a strong PC, so allocate as much RAM is needed".
- The local vision AI is "just part of the app, not a separate download".
- A Glance is "sorta like his peripheral vision … it doesn't overwrite his main focus (only add to it) … Don't make him scatterbrained, but equally don't give him tunnel vision".

Accepted remote base: `3797652ccf0f566bb5e2fb5b17a841954582f314`
Working branch: `agent/vis-01-sniff`
Builder: Claude

## Why

Today Prince's attention only moves after a cloud interpretation returns, which takes seconds. Local frames are compared once per second. No cloud model can answer in 100 ms, so the ≤100 ms tier has to run on Boss's PC.

The Vision upgrade has three speeds:

- **Sniff:** local, every frame, ≤100 ms. This packet builds it.
- **Glance:** Haiku, peripheral vision (VIS-02).
- **Inspect:** the detailed model, main focus (VIS-02).

Local text reading (VIS-03) and the bundled local vision model (VIS-04) later add to the Sniff.

## Scope

1. **Attention-aware capture cadence** (`CaptureCadence`). The orchestrator sets it whenever his attention state changes.

   | Attention state | Frame spacing | Sheet spacing |
   |---|---|---|
   | Noticing | 250 ms (4 per second) | 1 s |
   | Engaged and Afterglow | 150 ms | 500 ms |
   | Bnuy Mode | 100 ms (10 per second) | 250 ms |

   - A sniff trigger always produces a sheet at once.
   - Sheets still coalesce to the newest; an orientation or photograph sheet is never displaced.
2. **The Sniff, in the capture worker, on every non-duplicate frame, before any sheet work.**
   - The 64×36 signature is split into an 8×6 grid of cells.
   - Each sniff reports:
     - the global change and the hottest cell's change;
     - the changed-cell fraction (motion);
     - a scene cut;
     - the hot region and its nearest named region kind (dialogue, corners, sides, center);
     - its timing.
   - It holds no pixels, ever.
3. **Protocol Version 5.** Adds `SniffProduced` (worker to client) and `SetCadence` (client to worker), both strictly validated.
   - The client publishes a sniff only for the current worker epoch, the current grant, and an increasing sequence.
   - The controller publishes a sniff only after its source frame passed the same privacy admission that sheets use.
4. **The reflex lane in the orchestrator.**
   - A sniff updates the thread-safe attention engine and publishes its Attention notices immediately, without waiting behind the mailbox.
   - Conversation and cadence consequences are posted to the mailbox.
   - Local evidence carries a score ceiling below Bnuy Mode (`InterestEvent.ScoreCeiling`). Only understanding, or decisive evidence, can enter Bnuy Mode, so a flashing screen cannot make him frantic.
   - Each hot cell is its own evidence source and topic. Sustained or spread change corroborates; a looping animation in one cell habituates.
   - Forwarding is rate-limited per cell.
5. **RAM.** The screenshot budget goes from 64 MB to 512 MB, and the visual working budget from 64 MB to 256 MB. A 4K frame (33.2 MB) currently exceeds the 32 MB per-frame copy limit and is dropped; 4K and larger now work. At most 3 raw frames are still kept, so frames stay fresh, not queued.
6. **Latency evidence and diagnostics.** The worker metrics report sniff counts and sniff compute time (last and maximum). The orchestrator diagnostics report sniff arrival latency (last, maximum, and the 95th percentile over a bounded window).

## Acceptance

- **Budget (Windows CI process test).** With the real out-of-process worker and a synthetic source, from frame arrival to the client's `SniffProduced`: 95th percentile ≤100 ms, every one ≤250 ms.
- **Compute.** A 3840×2160 sniff takes ≤50 ms (median of repeated runs), on Linux and Windows.
- **Reflex.** While the mailbox is blocked by a slow work item, a sniff still produces its Attention notice. Local evidence alone never enters Bnuy Mode. A one-cell loop habituates. Spread change reaches Engaged.
- **Privacy.**
  - No sniff without an admitted frame.
  - None after a privacy stop, or for a stale grant or epoch.
  - A malformed sniff tears down the worker, like any other protocol violation.
- **Cadence.** Each attention state sets its cadence, and a sniff trigger produces an immediate sheet.
- **4K.** A 3840×2160 frame produces sheets and sniffs.
- **No regressions.** The whole suite passes, and the agreed-number pins are updated only for the values Boss changed.

## Personal Round Judgments

- **J1 — Sniffs carry their own frame description.** The worker announces a frame only after its sheet work, so a sniff instead carries its source frame's size and accounting, and goes through the controller's same privacy admission gate on its own. The privacy rule is identical, and there is no wait on composition.
- **J2 — Sniffs get their own event lane.** In the client, a small bounded lane: a sniff can never evict a frame or sheet, and a slow frame observer can never delay noticing.
- **J3 — The reflex lane runs outside the mailbox.** The attention engine is already thread-safe. Notices are serialized by one lock, and the App hands them to the UI with `BeginInvoke`, so there is no deadlock. Conversation and cadence consequences go through the mailbox.
- **J4 — Per-cell habituation, not region topics.** A looping cell fades in a few exposures and recovers with a 20 s half-life. The engine's topic habituation would have muted a whole region for about 10 minutes, so sniff evidence carries no topic. Weak evidence corroborates per region kind.
- **J5 — A trigger-level change is trusted on first sight** (confidence 0.8). A single strong change, such as a pop-up in one cell, perks his ears at once; weaker change needs a second cell.
- **J6 — Cell-aware duplicates.** A frame is a duplicate only when the whole frame is still and no single cell moved (cell change < 0.06). A small icon now counts even when the global change is tiny.
- **J7 — A bounded, zeroing pixel pool.** At 4K, fresh 33 MB arrays every frame made the collector stall one copy from about 20 ms to about 180 ms. Pooled buffers (at most 4 per size, at most 256 MB retained) bring a 4K sniff back to about 10–30 ms in this container. Every buffer is zeroed before reuse, and fresh ones start zeroed.
- **J8 — The cadence is re-asserted.** It is sent again at once on a resume or re-grant, every 10 s from the tick (a restarted worker may have lost it), and after a refused send.
- **J9 — Re-armed orientation frames are sniffed too.** Only a first frame, which has no change baseline, is not sniffed.

## Deferred findings

- **VIS-02:** Haiku Glance (peripheral) and Inspect (focus), a pre-event history ring of frames from before and during an event, and the model-recommended next look.
- **VIS-03:** local text reading.
- **VIS-04:** the bundled local scene model.

## Mutation pass

- **33 mutants.** 31 were killed; 2 survive as deliberate defense in depth:
  - the controller's "current grant" check before a cadence (worker status already gates it);
  - the orchestrator's sniff grant check (the controller already admits only the current grant).
- **The first round found three test gaps:** scene cut versus half screen, a trigger breaking through spacing, and the exact aimed region. Tests were added for all three.
- **It also found two design issues,** both fixed: region-wide habituation, and resume not re-sending the cadence.

## Paw Gate

**PASS.**

- **CI.** Head `2c5baecee576e86ecb9351cdb7bcc922d24043d9` passed push run `38161132869` and PR run `38161148925` at 1076/1076 with verified artifacts.
  - `SniffsCrossTheProcessBoundary_WithinTheHundredMillisecondBudget` passed on both runs: the real out-of-process worker on Windows, p95 ≤ 100 ms and max ≤ 250 ms.
  - So did `ACadence_ReachesTheWorkerProcess_AndPacesItsLooking`.
- **Local.** The full gate passed: 1058/1058, 0 warnings, no vulnerable packages.
- **Merge ref.** Parents were exactly `3797652` and `2c5baec`, and the tree was equal.
- **Merge.** Squash-merged through the expected-head fence.
