# Task R2 — Stage 4 Recertification and Closure

Status: **Active**
Accepted base: `04af51b90ffbc44933a56c21a1c1223940ef671a`, tree `ad027224f73e4fb3401c2d48d9591226395b4bf4`
Roadmap slice: Stage 4 corrective verification only; Task 7 remains unopened

## Objective

Independently recertify the completed Stage 4 implementation and publication chain, correct only defects that violate an already accepted Task 5/6 invariant, and reconcile the standing control records to the final accepted repository state. This packet adds no new companion capability.

## Entry evidence

- Task 6 product and evidence changes were accepted through PRs #11 and #13; PR #14 merged the docs-only acceptance descendant as `04af51b90ffbc44933a56c21a1c1223940ef671a`.
- The accepted Task 6 tree passed both final implementation event paths at 309/309 and both PR #14 event paths at 309/309 after one transparently recorded transient restart-orientation timeout on the first push attempt.
- Independent artifact inspection reproduced the recorded ZIP hashes, 309/309 TRX totals, and the deterministic 792×621 RGBA synthetic attention sheet.
- The current handoff still describes PR #14 as pending even though it merged, so the standing control state is internally stale.
- Independent source review found two bounded Stage 4 gaps: evicted capture frames leave surplus wake signals in `CaptureFramePipeline`, and a blocked frame observer can prevent the controller-facing status fence from being dispatched.

## Required correction

1. Keep capture-pipeline wake signals attributable to actual bounded pending entries. Evicting or clearing a pending frame must retire its available signal when that signal has not already been reserved by the single consumer.
2. Prove pressure cannot accumulate wake-signal debt with runtime duration while preserving the newest two pending entries, the one processing entry, the three-source-frame/64 MiB ceiling, and deterministic disposal.
3. Dispatch worker status changes on a bounded lane independent from frame/attention observers so a blocked frame observer cannot suppress the stop/resize/fault fence used by Target Authorization.
4. Preserve nonblocking IPC/control operations, event exception isolation, epoch fencing, exact-source sheet ordering, and deterministic shutdown.
5. Reconcile `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, and `README.md` to the actual final Stage 4 chain, this corrective gate, and the exact post-merge state.

## Invariants

1. One runtime, one exact authorized target, and revocation-first privacy behavior remain unchanged.
2. No source pixels or final attention payload gains new lifetime, authority, persistence, or network exposure.
3. Source ownership remains at most three frames/64 MiB; pending processing entries remain at most two; attention sheets remain at most two per worker/client owner.
4. Status fencing may not depend on progress by frame or attention observers.
5. Every queue, signal, and observer lane remains explicitly bounded and newest-preserving where loss is permitted.
6. Task 7, semantic interpretation, API/provider work, conversation, ERPP implementation, personality, and production data remain out of scope.

## Acceptance scenarios

1. A blocked processor plus sustained producer pressure leaves at most two pending wake signals, retains bounded newest work, and disposes all evicted/cleared frames exactly once.
2. A blocked frame observer cannot prevent a current `Stopped` status notification, protocol metrics response, or worker stop/cleanup.
3. Existing restart/resize/fault/manual-region/privacy tests remain green, including fresh-orientation/fresh-epoch evidence.
4. Locked restore and direct/transitive vulnerability audit pass on the exact candidate dependency graph.
5. All 20 Release projects build with zero warnings/errors and all Windows suites pass with no unexplained failure or skip.
6. The deterministic attention-sheet artifact is still emitted and retains the accepted decoded structure and pixel digest unless an explicitly reviewed test-artifact-only change explains otherwise.
7. Actual-diff review finds only the bounded corrective implementation, focused tests, and directly necessary Stage 4 control records.
8. The active packet is archived, the handoff describes the merged result rather than a pending branch, no active task remains, and Task 7 is still unopened.

## Allowed paths

- `src/CompanionCore.Capture.Worker/CaptureFramePipeline.cs`
- `src/CompanionCore.Capture.Client/OutOfProcessCaptureWorker.cs`
- `tests/CompanionCore.Capture.Worker.Tests/CaptureFramePipelineTests.cs`
- `tests/CompanionCore.Capture.Worker.Tests/OutOfProcessCaptureWorkerTests.cs`
- `README.md`
- `BUILD_LEDGER.md`
- `tasks/active/task-r2-stage4-recertification.md`
- `tasks/archive/task-r2-stage4-recertification.md`
- `tasks/review/HANDOFF.md`

No project, package, lock, solution, workflow, memory, runtime, privacy-authority, target-discovery, WGC, app, production-data, or durable-image path is authorized.

## Paw Gate

R2 passes only when:

- the two focused regressions fail against the accepted base for the intended reason and pass on the candidate;
- source review confirms signal accounting under enqueue, eviction, clear, cancellation, and consumer-reservation races;
- source review confirms status delivery is bounded and independent from frame/attention observer progress;
- `git diff --check`, the one-active-task rule, and the allowed-path diff pass;
- exact Windows push and pull-request paths pass locked restore, dependency audit, all 20 Release builds, all tests, and both artifact uploads on the same candidate tree;
- retained TRX and PNG artifacts are independently inspected;
- exact head/tree/remote equality and expected-head merge are verified;
- the packet is archived and every standing record describes the final merged state without opening Task 7.

## Personal Round Judgments

- **J1 — R2 is corrective, not a new roadmap task.** The defects are violations of already accepted bounded-queue and status-fence requirements. The `R2` label avoids reusing the unrelated historical R1 draft while keeping Task 7 closed. Reversal: revert this packet and its bounded corrections without affecting later-stage design.
- **J2 — Signals remain one-for-one with pending frames.** A fixed-capacity semaphore remains the smallest correction to the existing single-consumer queue. Removing pending work opportunistically retires its unreserved signal; a signal already reserved by the consumer safely drains as an empty wake after a concurrent clear. Reversal: replace queue plus semaphore with an equivalently disposing bounded channel in a separately reviewed change.
- **J3 — Status receives its own bounded dispatch lane.** Frame and attention delivery keep their current bounded coalescing policy, while state fences no longer wait behind a frame observer. The lane is still finite and observer exceptions remain isolated. Reversal: replace both dispatchers with one custom bounded priority dispatcher that proves the same non-loss and nonblocking properties.
- **J4 — The restart timeout is investigated through evidence, not hidden.** The same exact PR #14 head passed the case in the simultaneous PR run and push rerun near one second, while all successful artifacts were independently verified. R2 adds no timeout inflation; fresh exact-head Windows event paths must execute it again. Reversal: add a separately justified deterministic synchronization correction if new evidence reproduces a product race.

## Review focus

- Semaphore count versus pending queue count across consumer reservation, oldest eviction, clear, pause/resume, and disposal.
- Status-lane capacity, event ordering, handler isolation, and shutdown without an unbounded task or queue.
- No change to pixels, IPC framing, capture authority, WGC behavior, privacy generation, manual-region semantics, or attention-sheet format.
- Exact reconciliation of accepted hashes, run/artifact evidence, active-task count, and next safe task.
