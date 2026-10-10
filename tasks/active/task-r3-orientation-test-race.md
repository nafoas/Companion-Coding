# Task R3 — Reliable Orientation Delivery

Status: **Active**
Accepted base: `2b5ef836183dde261d52bfa2f6c68340aff2e9f3`, tree `fb6e016a585effede01cb42e2f368499f2546a76`
Roadmap slice: Stage 4 corrective work only; Task 7 remains unopened
Builder: Claude. Boss directed on 2026-10-10: "make delivery more reliable… tighten the screws until it reliably delivers the screenshot, with a backup failsafe of it taking it again only if this doesn't happen."

## Objective

After consent, the worker reliably produces exactly one high-fidelity orientation sheet per visual epoch, but that sheet did not reliably reach the consumer. Make orientation delivery reliable at every layer within the existing two-sheet ceiling, and add a bounded failsafe that asks the worker to retake the orientation only when delivery still fails.

## Entry evidence

- The post-merge `main` run `38026209974` failed `SyntheticWorker_ProducesBoundedOrientationAndManualRegionalSheets` with "Expected: Orientation, Actual: Regional". The same case failed identically in run `33027979425` (2026-08-27).
- `Restart_EmitsFreshOrientationAndRetainsOnlyFreshEpochSheets` timed out waiting for an orientation notice in runs `33030041052`, `33029729188`, and `38026568119`. `StopAndRestart_ClearQueuedSheetsAndCreateFreshOrientation` failed with "Expected: Orientation, Actual: Regional" in run `33027981777`.
- There were five recorded failures from one cause, two of them earlier recorded as transient. Neither the accepted code nor R2 introduced a nondeterministic product fault; the accepted design simply never guaranteed delivery.
- Source review found four independent loss paths and one unrecoverable case:
  1. Client retention was newest-preserving at two sheets, so the oldest sheet — always the orientation — was evicted first. The worker emits a regional sheet for every changed frame (every 10 ms with the synthetic source).
  2. Client notification dispatched only the newest retained metadata, so the orientation notice was coalesced away (Task 6 J9).
  3. The client deferred at most three start-time frames and dropped the oldest. The orientation's source frame is the first one, so a slow start handshake stranded the orientation.
  4. The controller held one sheet and kept the higher source sequence, so a newer regional sheet displaced an orientation that was awaiting privacy admission.
  5. A privacy-rejected orientation source frame can never be admitted. Only a fresh orientation can recover.
- Wire order: the worker hands a sheet to IPC inside the frame processor, before the frame's `FrameReady`. The orientation sheet therefore normally precedes its source frame.

## Required correction

1. **Client retention:** the orientation is pinned outside regional retention until it is taken. It counts toward the unchanged two-sheet ceiling, so regional retention shrinks to the newest one while it is pinned. `TakeLatestAttentionSheet` hands over an undelivered orientation before any regional sheet.
2. **Client notification:** the orientation notice is never coalesced. It is dispatched exactly once, only after its exact source frame has been dispatched, and before any newer regional notice. If its source frame is skipped (dropped or fenced) while a newer frame is dispatched, the stranded orientation is released rather than announced without its frame.
3. **Start deferral:** eviction from the three-frame start deferral never removes the pinned orientation's source frame.
4. **Controller:** the orientation has its own held slot and is released before the regional slot. Newer regional sheets keep flowing while an orientation awaits admission.
5. **Failsafe:** the controller owes an orientation whenever the worker enters `Running` from any other status or recalibrates after `SourceResized`. If eight admitted frames pass without an orientation becoming available, it asks the worker to retake one through a new grant-checked `RequestOrientation` command. It does this at most three times per outstanding orientation, then stops and counts an honest delivery failure. Privacy-rejected frames do not count. The request runs off the privacy admission lease.
6. **Worker:** `RequestOrientation` (IPC protocol version 3) re-arms the one orientation for the active grant of a running worker; the next usable frame, even a duplicate, becomes the orientation.

## Invariants

- No sheet bound changes: at most two retained client sheets, at most two worker pending sheets, three source frames, and 64 MiB.
- No change to pixels, sheet format, privacy correlation, grant authority, epoch fencing, or status lanes.
- The failsafe is bounded: at most three retakes per outstanding orientation, with no timer or unbounded loop, and no worker I/O inside a privacy admission lease.
- Task 6 scenario 1 (exactly one orientation per epoch) holds except for an explicitly requested retake, which the worker metric counts.

## Acceptance scenarios

1. Against the accepted client, the strict orientation-first and slow-observer multi-epoch process tests fail deterministically. With this correction, they pass.
2. Against the accepted controller, `UnadmittedOrientation_IsNotDisplacedByANewerRegionalSheet` fails. With this correction, it passes.
3. The failsafe asks after exactly the budget, at most three times, stops honestly, ignores privacy-rejected frames, resets on resize, contains request failures, and never fires when the orientation was delivered or the worker is not running.
4. `RequestOrientation` is grant- and status-checked in the engine, shape-validated over IPC, produces a fresh orientation from a duplicate frame, and works end to end through the worker process.
5. All 20 Release projects build with zero warnings and errors; both exact Windows event paths pass every test with both artifacts; the attention-sheet digest is unchanged.

## Allowed paths

- `src/CompanionCore.Capture.Contracts/CaptureIpcProtocol.cs`, `src/CompanionCore.Capture.Contracts/ICaptureWorker.cs`
- `src/CompanionCore.Capture.Worker/WorkerIpcHost.cs`, `src/CompanionCore.Capture.Worker/CaptureWorkerEngine.cs`, `src/CompanionCore.Capture.Worker/VisualObservationPipeline.cs`
- `src/CompanionCore.Capture.Client/OutOfProcessCaptureWorker.cs`
- `src/CompanionCore.Capture.Fake/FakeCaptureWorker.cs`
- `src/CompanionCore.TargetAuth/TargetSessionController.cs`
- `tests/CompanionCore.Capture.Worker.Tests/{CaptureIpcProtocolTests,CaptureWorkerEngineTests,OutOfProcessCaptureWorkerTests,VisualObservationPipelineTests}.cs`
- `tests/CompanionCore.TargetAuth.Tests/{TargetAuthTestHarness,TargetSessionOrientationDeliveryTests}.cs`
- `README.md`, `BUILD_LEDGER.md`, `tasks/review/HANDOFF.md`, `tasks/active/task-r3-orientation-test-race.md`, `tasks/archive/task-r3-orientation-test-race.md`

## Paw Gate

R3 passes when:

- the red evidence fails exactly the intended cases against the accepted product;
- the actual diff touches only allowed paths;
- source review confirms bounds, ordering, lease safety, and grant checks;
- both exact Windows event paths on the final head pass every test with verified artifacts;
- the merge uses the expected-head fence;
- post-merge `main` CI is green.

## Personal Round Judgments

- **J1 — Delivery is fixed in the product, not relaxed in the tests.** Boss chose reliable delivery over relaxed assertions. This refines Task 6 J9: regional notices and retention still coalesce to the newest, but the single orientation is exempt within the same ceiling. Reversal: restore newest-only retention and notices, and relax the assertions.
- **J2 — `TakeLatestAttentionSheet` keeps its name with orientation-first semantics**, documented on the interface. A rename would ripple through accepted callers for no behavioral gain. Reversal: rename to `TakeNextAttentionSheet` in a later interface pass.
- **J3 — The failsafe budget is eight admitted frames, not a timer.** Admitted frames scale with the capture cadence, need no clock, and are deterministic in tests. Normal delivery completes within one or two frames. Three retakes bound the cost. Reversal: change `OrientationDeliveryFrameBudget` and `MaximumOrientationRetakes`, both provisional until Stage 11 calibration.
- **J4 — Protocol version 3.** A new command kind is a protocol change. Client and worker ship together, so a mismatched pair fails closed. Reversal: revert the enum member and version together.
- **J5 — Stranded orientations are released, not announced.** An orientation whose exact source frame was skipped can never pass privacy admission. Releasing it frees the pinned slot, and the controller failsafe retakes. Reversal: hold it until the next epoch.
- **J6 — `ValidateCommandShape` becomes internal** so the new command's accepted and rejected shapes are tested directly. Reversal: test through the host instead.
- **J7 — Two hard-coded protocol-version literals in accepted tests are updated.** The unknown-member case now pins version 3, and the unsupported-version case derives `Version + 1`, so both keep testing what they were written to test.

## Deferred findings

- The failsafe counters (`OrientationRetakeRequests`, `OrientationDeliveryFailures`) are internal. Stage 11 diagnostics or a later metrics pass should surface them through the hidden technical view.
- The synthetic capture source cannot emit `SourceResized`, so resize-triggered orientation ownership is proven at the controller through status events, not through the worker process.
