# Task Handoff

## Task

DEF-02 — Full-Resolution Keepsake Photographs (`tasks/active/def-02-full-resolution-photographs.md`). These are the smaller deferred items, part 2 (the last).

- Branch: `agent/def-02-full-resolution-photographs`.
- Base: `main` `4854577`.
- Builder: Claude.

## Changed

- **Contracts:**
  - `AttentionSheetKind.Photograph`, with strict metadata;
  - `AttentionSheet.MaximumPhotographEdge`;
  - `CaptureIpcMessageKind.RequestPhotograph`;
  - `ICaptureWorker.RequestPhotographAsync`;
  - IPC protocol version 4.
- **Worker:**
  - `VisualObservationPipeline` makes the next usable frame a native-resolution photograph, halving it only to fit the bounds;
  - `PngEncoder.TryEncode`;
  - the engine's `RequestPhotographAsync`;
  - the IPC command.
- **Client.** `RequestPhotographAsync`, and a pinned photograph slot.
- **Controller.** `RequestPhotographAsync`, a held photograph slot, and delivery in the order orientation → photograph → regional.
- **Orchestrator:**
  - the camera action requests a photograph;
  - ordinary sheets keep flowing to attention;
  - a photograph from a frame older than the action asks again;
  - a refused request falls back to the sheet's resolution;
  - `SheetPhotographSource` converts whole-sheet photographs in place.
- **Bridge.** `ApiBridge` refuses photograph sheets (no full-resolution upload).
- **Fakes.** Synthetic, recording, and scripted workers.
- **Tests:**
  - `PhotographSheetTests` (12) and a Windows process test;
  - `TargetSessionPhotographTests` (4);
  - `OrchestrationPhotographTests` (7), plus the updated Scenario 6;
  - the bridge refusal test (1).

## Verification

- **Local:** full gate green: 985/985 on Linux, 0 warnings, 0 vulnerable packages.
- **Mutation:** 27 mutants: 24 killed and 3 equivalent (defense in depth).
- **Windows CI:** pending (expected about 1002).

## Remaining

- Gate, CI, merge, and closure.
- Then stop before Stage 12.

## Risks and assumptions

- The saved keepsake edge (1280) stays provisional, under photograph-storage tuning.
- The process-level photograph path is proven on Windows CI only.

## Personal Round Judgments

DEF-02 J1–J3, recorded in the packet.

## Review focus

- No capture before authorization.
- Only the selected target is photographed.
- Privacy admission applies to photographs.
- Every existing bound still holds.

## Next safe task

None before Stage 12. Stage 12 needs Boss's direction: real credentials and live API.
