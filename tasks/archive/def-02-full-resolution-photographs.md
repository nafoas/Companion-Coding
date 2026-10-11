# DEF-02 — Full-Resolution Keepsake Photographs

Status: **accepted** — Paw Gate PASS; PR #53 squash-merged as `98ae435ff71ddb75d4a9e847d7520cd82966146f`
Authorized: 2026-10-11 by Boss ("Once that is done, do the smaller deferred items, and then stop before stage 12").
Accepted remote base: `48545775b664e5094117c1561a0d3927f813dc1d`
Working branch: `agent/def-02-full-resolution-photographs`
Builder: Claude

## Objective

WIRE-01 D2 noted that keepsake photographs were cropped from the next attention sheet's full-context region. That region is the worker's downscaled view: 768×432 on a regional sheet, and at most 1280×720 on an orientation sheet. A full-resolution photograph needs a worker capture command and an IPC contract.

The design says a photograph "is saved as a compressed keepsake rather than a raw full-resolution frame". The keepsake camera already does this: it downscales to its saved edge (1280, provisional) and PNG-compresses. What changes is the photograph's **source**. It is now the authorized target's native frame instead of an attention-sheet thumbnail, so the saved keepsake is downscaled once, from full resolution.

## Design

- **Contract.**
  - New `AttentionSheetKind.Photograph`, with strict metadata:
    - exactly one full-context region covering the whole sheet;
    - the sheet is never larger than its source;
    - the longest edge is at most `AttentionSheet.MaximumPhotographEdge` (8192, the camera's source bound).
  - New `CaptureIpcMessageKind.RequestPhotograph` and `ICaptureWorker.RequestPhotographAsync(grant)`.
  - Capture IPC protocol version 3 → 4, following the R3 precedent for a new command.
- **Worker.**
  - `RequestPhotograph` arms one photograph for the active grant only. Any reset (stop, resize, fault, new grant, disposal) disarms it.
  - The next usable frame, even a duplicate, becomes a photograph sheet at native resolution. It consumes no visual change, so orientation and regional attention are untouched.
  - If the image exceeds the edge bound, or its PNG exceeds the existing 8 MiB sheet bound, it is halved (2×2 average) until it fits. Every image fits by 1280 pixels, so this always ends.
  - Every existing bound still holds:
    - the 64 MiB visual working budget is checked before encoding;
    - the 8 MiB payload;
    - at most two retained sheets.
- **Client and controller.**
  - A photograph sheet has its own pinned slot (client) and held slot (controller). Newer regional sheets never displace it.
  - It is delivered after an undelivered orientation and before any regional sheet.
  - It passes the same privacy frame admission as every sheet.
  - The controller requests a photograph only for the current authorized target, never while privacy-paused.
- **Orchestrator.**
  - The camera action requests a photograph. While it waits, ordinary sheets keep flowing to attention and never become the photograph.
  - A photograph from a frame captured before the action asks again, within the action window.
  - A photograph sheet never reaches the semantic bridge. Without a camera action it is released at once.
- **Bridge (defense in depth).** `ApiBridge` itself refuses a photograph sheet before any provider call. The design says "no full-resolution upload merely because the source display supports it".
  - If the worker refuses the request, the photograph comes from the next sheet's full-context region, as before.
- **Copies.** `SheetPhotographSource` converts a whole-sheet photograph in place, so a large photograph never holds a second full-size copy in the core.

## Evidence

- **Worker — `PhotographSheetTests`, 12 tests:**
  - the native-resolution photograph is pixel-exact, comes even from a duplicate frame, and consumes no visual change;
  - the next changed frame is still regional;
  - a source wider than the camera edge is halved even when it would encode;
  - a photograph that cannot be produced stays armed;
  - orientation still follows;
  - a reset or disposal disarms the request;
  - incompressible 2048×1536 noise is halved to 1024×768 within every bound;
  - the 2×2 average, rounded to the nearest value;
  - `TryEncode` reports an image that cannot fit;
  - engine grant checks and one photograph produced;
  - IPC command shape;
  - strict photograph metadata.
- **Windows process test.** `RequestPhotograph_ReturnsOneNativeResolutionPhotographOfTheActiveGrantOnly`: a native-resolution photograph comes through the real worker process for the active grant only.
- **Controller — `TargetSessionPhotographTests`, 4 tests:**
  - delivery order: orientation, then photograph, then regional;
  - privacy-rejected frames are never delivered;
  - a manual-region change releases a held photograph;
  - a photograph is requested only for the current authorized target.
- **Orchestration — `OrchestrationPhotographTests`, 7 tests, plus the updated Scenario 6 (`Scenario6_CameraActionTakesAVerifiedNativeResolutionPhotograph`):**
  - ordinary sheets never become the photograph;
  - an older frame asks again;
  - a refused request, or a failed retake, falls back to the sheet resolution;
  - a whole-sheet photograph is converted in place, pixel-exact;
  - a stray photograph is released and never interpreted;
  - a privacy stop takes nothing.
- **Api.** `AKeepsakePhotograph_IsNeverUploaded`.

## Mutation pass

27 mutants across the worker pipeline, encoder, contract, engine, IPC, controller, orchestrator, in-place conversion, and the bridge guard:

- **24 killed.** Four of these first survived. Each got a test and was then killed on rerun:
  - failure re-arm;
  - the edge bound;
  - halving rounding;
  - no visual change consumed.
- **3 equivalent (defense in depth):**
  - **The photograph's stale-state check.** It is a concurrency guard mirroring the regional path; no await separates it from its lock.
  - **The encoder's early stop.** It only saves work; the final bound check still refuses.
  - **The controller's phase and current-grant check.** A privacy pause clears the grant, so the null-grant check already refuses. This is the same pattern as `SetManualRegionAsync`.

The client's pinned slot is exercised only by the Windows process test.

## Personal Round Judgments

- **J1 — Source resolution, not storage resolution.** "Full-resolution photographs" means the source frame. The saved keepsake remains the design's compressed copy under the camera's provisional 1280 edge ("photograph-storage tuning" stays deferred). This needs no change to a design rule.
- **J2 — Halve to fit, never raise a bound.** The 8 MiB payload and 64 MiB working budgets are accepted bounds. A photograph that cannot fit them is halved rather than widening them.
- **J3 — Graceful fallback.** A worker that cannot take a photograph degrades to the sheet's full-context region instead of failing the camera action.

## Paw Gate

**PASS.**

- **CI.** Head `d58807c499c1d4f213ff8ad5152ee1b37cb2c4bc` passed push run `38104142116` and PR run `38104151642` at 1002/1002, with verified artifacts.
- **Real worker process on Windows.** `RequestPhotograph_ReturnsOneNativeResolutionPhotographOfTheActiveGrantOnly` executed and passed in both runs.
- **Local.** The full gate is green: 985/985 on Linux, 0 warnings, 0 vulnerable packages.
- **Merge ref.** Parents were exactly `4854577` and `d58807c`, and the tree equalled the head tree (`1c9f13a`).
- **Merge.** Squash-merged through the expected-head fence.

## Deferred findings

- None new. The saved keepsake edge (1280) remains provisional photograph-storage tuning (Design BunDex deferred list).
