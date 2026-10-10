# Task R2 — Stage 4 Recertification and Closure

Status: **Accepted. PR #15 squash-merged to `main` as `11cc752eae1e9457a12c9d847aacdeb073463b13` after the red evidence failed exactly the three intended cases and the final head passed both exact Windows event paths at 311/311.**
Accepted base: `04af51b90ffbc44933a56c21a1c1223940ef671a`, tree `ad027224f73e4fb3401c2d48d9591226395b4bf4`
Roadmap slice: Stage 4 corrective verification only; Task 7 remains unopened
Builder: Claude, which took over direct construction from Codex at Boss's instruction on 2026-10-10

## Objective

Independently recertify the completed Stage 4 implementation and publication chain, correct only defects that violate an already accepted Task 5/6 invariant, and reconcile the standing control records to the final accepted repository state. This packet adds no new companion capability.

## Entry evidence

- Task 6 product and evidence changes were accepted through PRs #11 and #13; PR #14 merged the docs-only acceptance descendant as `04af51b90ffbc44933a56c21a1c1223940ef671a`.
- The accepted Task 6 tree passed both final implementation event paths at 309/309 and both PR #14 event paths at 309/309 after one transparently recorded transient restart-orientation timeout on the first push attempt.
- Codex's interrupted R2 session published one regression-evidence commit, `326afd4ff9afccafe666b15c990081d54c810f2a`, directly on the accepted base. Its candidate fixes existed only in an unpublished workspace, were never compiled or verified, and are unavailable. That commit is reviewed evidence, not accepted work.
- `326afd4` push run `33337966344` did not fail: its Test step hung for six hours until GitHub cancelled the job. All non-Windows projects had passed in under twenty seconds.
- Builder reproduction on 2026-10-10 identified the hang with `--blame-hang-timeout`: `ProducerPressure_DoesNotAccumulateWakeSignalsForEvictedFrames` failed its assertion before releasing its blocked processor, so `await using` disposal waited for a processor that was waiting for the pipeline lifetime. The red evidence deadlocked instead of failing.
- Independent source review confirms two bounded Stage 4 gaps:
  1. `CaptureFramePipeline` releases one wake signal per accepted pending frame, but evicting or clearing a pending frame never retires it. Under sustained pressure the signal count grows with runtime (4,096 surplus signals after 4,096 pressured offers).
  2. `OutOfProcessCaptureWorker` dispatches status, frame, and attention events through one 64-entry drop-oldest lane on one dispatcher. A blocked frame observer delays the `Stopped`/`Faulted`/resize fence used by target authorization, and continued frame traffic can evict that fence entirely.
- The current handoff still describes PR #14 as pending even though it merged, so the standing control state is internally stale.

## Required correction

1. Keep capture-pipeline wake signals attributable to actual bounded pending entries. Evicting or clearing a pending frame must retire its available signal when that signal has not already been reserved by the single consumer.
2. Prove pressure cannot accumulate wake-signal debt with runtime duration while preserving the newest two pending entries, the one processing entry, the three-source-frame/64 MiB ceiling, and deterministic disposal.
3. Dispatch worker status changes on a bounded lane independent from frame/attention observers so a blocked frame observer cannot delay or evict the stop/resize/fault fence used by target authorization.
4. Preserve the ordering guarantee the single lane provided: a frame enqueued before a visual-invalidating status must not be dispatched after that status, so independent lanes cannot re-admit stale-geometry frames.
5. Preserve nonblocking IPC/control operations, event exception isolation, epoch fencing, exact-source sheet ordering, and deterministic shutdown.
6. Make every regression fail promptly instead of hanging when its invariant is violated.
7. Reconcile `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, and `README.md` to the actual final Stage 4 chain, this corrective gate, and the exact post-merge state.

## Invariants

1. One runtime, one exact authorized target, and revocation-first privacy behavior remain unchanged.
2. No source pixels or final attention payload gains new lifetime, authority, persistence, or network exposure.
3. Source ownership remains at most three frames/64 MiB; pending processing entries remain at most two; attention sheets remain at most two per worker/client owner.
4. Status fencing may not depend on progress by frame or attention observers.
5. Every queue, signal, and observer lane remains explicitly bounded and newest-preserving where loss is permitted.
6. `FrameProduced` and `AttentionSheetProduced` remain serialized on one dispatcher in source order; a sheet is never dispatched before its source frame.
7. Task 7, semantic interpretation, API/provider work, conversation, ERPP implementation, personality, and production data remain out of scope.

## Acceptance scenarios

1. A blocked processor plus 4,096 pressured offers leaves exactly one unreserved wake signal per bounded pending entry, retains bounded newest work, and disposes every evicted/cleared frame exactly once.
2. Clearing removes pending entries and their unreserved wake signals before waiting for the processor, so no empty wakes remain after the clear.
3. A blocked frame observer cannot prevent a current `Stopped` status notification, protocol metrics response, or worker stop/cleanup.
4. After the `Stopped` fence is delivered, releasing the blocked observer dispatches none of the frames queued behind it.
5. Existing restart/resize/fault/manual-region/privacy tests remain green, including fresh-orientation/fresh-epoch evidence.
6. Locked restore and direct/transitive vulnerability audit pass on the exact candidate dependency graph.
7. All 20 Release projects build with zero warnings/errors and all Windows suites pass with no unexplained failure, skip, or hang.
8. The deterministic attention-sheet artifact is still emitted and retains the accepted decoded structure and pixel digest unless an explicitly reviewed test-artifact-only change explains otherwise.
9. Actual-diff review finds only the bounded corrective implementation, focused tests, CI hang bounds, and directly necessary Stage 4 control records.
10. The active packet is archived, the handoff describes the merged result rather than a pending branch, no active task remains, and Task 7 is still unopened.

## Allowed paths

- `src/CompanionCore.Capture.Worker/CaptureFramePipeline.cs`
- `src/CompanionCore.Capture.Client/OutOfProcessCaptureWorker.cs`
- `tests/CompanionCore.Capture.Worker.Tests/CaptureFramePipelineTests.cs`
- `tests/CompanionCore.Capture.Worker.Tests/OutOfProcessCaptureWorkerTests.cs`
- `.github/workflows/ci.yml` — hang and job time bounds only
- `README.md`
- `BUILD_LEDGER.md`
- `tasks/active/task-r2-stage4-recertification.md`
- `tasks/archive/task-r2-stage4-recertification.md`
- `tasks/review/HANDOFF.md`

No project, package, lock, solution, memory, runtime, privacy-authority, target-discovery, WGC, app, production-data, or durable-image path is authorized.

## Paw Gate

R2 passes only when:

- the focused regressions fail against the accepted base for the intended reason, promptly and without a hang, and pass on the candidate;
- source review confirms signal accounting under enqueue, eviction, clear, cancellation, and consumer-reservation races;
- source review confirms status delivery is bounded and independent from frame/attention observer progress, and that the visual fence prevents stale frame dispatch after an invalidating status;
- `git diff --check`, the one-active-task rule, and the allowed-path diff pass;
- exact Windows push and pull-request paths pass locked restore, dependency audit, all 20 Release builds, all tests, and both artifact uploads on the same candidate tree;
- retained TRX and PNG artifacts are independently inspected;
- exact head/tree/remote equality and expected-head merge are verified;
- the packet is archived and every standing record describes the final merged state without opening Task 7.

## Personal Round Judgments

- **J1 — R2 is corrective, not a new roadmap task.** The defects violate already accepted bounded-queue and status-fence requirements. The `R2` label avoids reusing the unrelated historical R1 draft while keeping Task 7 closed. Reversal: revert this packet and its bounded corrections without affecting later-stage design.
- **J2 — Signals remain one-for-one with pending frames.** A fixed-capacity semaphore remains the smallest correction to the existing single-consumer queue. Every non-consumer removal goes through one helper that retires an unreserved signal with `Wait(0)`; a signal already reserved by the consumer drains as at most one empty wake. Under the pipeline gate this keeps `available ≤ pending` and `available + reserved ≥ pending`, so there is neither wake debt nor lost work. Reversal: replace queue plus semaphore with an equivalently disposing bounded channel in a separately reviewed change.
- **J3 — Status receives its own bounded dispatch lane.** Status uses a second 64-entry drop-oldest lane with its own dispatcher; frames and attention sheets keep the existing lane, so their mutual order and single-threaded delivery are unchanged. The status lane no longer triggers attention-sheet dispatch: a sheet can only be dispatched after its source frame, and the frame and sheet events already perform that check. Reversal: replace both dispatchers with one custom bounded priority dispatcher that proves the same non-loss and nonblocking properties.
- **J4 — A visual fence replaces the ordering the single lane implied.** Every status other than an ordinary `Running` transition (exactly the statuses `TargetSessionController` treats as clearing visual admission) increments a fence under the state gate before the status is enqueued. Frame events record the fence at creation and are rejected at dispatch if it has moved. The one frame already inside an observer when a fence occurs completes normally, as it did before. Reversal: remove the fence together with J3.
- **J5 — CI bounds hangs.** The R2 red-evidence run consumed six hours because the workflow had no hang bound. The Test step now uses `--blame-hang-timeout 10m`, which fails the hung test with a named dump, and the job has a 60-minute `timeout-minutes`. Both only strengthen the gate; neither skips or weakens a test. Reversal: remove the two bounds from `ci.yml`.
- **J6 — Builder continuity.** Boss transferred direct construction from Codex to Claude on 2026-10-10 with unchanged Paw Gate authority. Renaming the builder role throughout standing documents is a separate control task after R2, so this corrective gate stays bounded. Reversal: none required; the rename task can choose any wording Boss prefers.

## Candidate evidence

- Red evidence: head `bf0113cdd86e2c51110643d7181ef51cbabac54b` (regressions only, accepted product code), push run `38025402946`, job `114135078349`, 2 min 9 s. 311 executed and 308 passed. Exactly the three intended cases failed, with no timeout or skip: clear left 2 signals where 0 were expected (94 ms), pressure left 4096 signals where 2 were expected (130 ms), and `Stopped` timed out behind a blocked observer (3 s).
- Candidate implementation: head `82a70df099a53a877b0d2443a8b39b02ece846a6`, tree `f9b13752f4d54a19208fec3a933590a9d73b23e1`. Push run `38025519542` (job `114135429187`) and PR run `38025588908` (job `114135636155`) each passed 311/311 on the first attempt.
- Final evidence descendant: head `a54330178764bfddb18f15d0801a3f24f9df1ad9`, tree `5483a2d7ccd577447013c3844a2be22beeb55a44`. Push run `38025762113` (job `114136151131`) and PR run `38025763952` (job `114136156632`) each passed locked restore, the clean 20-project audit, all 20 Release builds with 0 warnings and 0 errors, 311/311 tests, and both artifact uploads, on the first attempt.
- Artifacts: every archive was downloaded and matched GitHub's digest. Every TRX set totals 311 executed. Every attention-sheet PNG is 792×621 8-bit RGBA, non-interlaced, with the accepted pixel digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`, and it was visually inspected.
- Local gate: locked restore; a clean audit of 20 projects; all 20 Release projects built with `/warnaserror`, 0 warnings and 0 errors; 298/298 locally executable tests passed.
- Merge: the PR #15 merge ref had parents `04af51b` and `a543301` and a tree equal to the head tree. It was squash-merged with the expected-head fence as `11cc752eae1e9457a12c9d847aacdeb073463b13`, and fetched `main` retained tree `5483a2d7ccd577447013c3844a2be22beeb55a44`.

## Review focus

- Semaphore count versus pending queue count across consumer reservation, oldest eviction, clear, pause/resume, and disposal.
- Status-lane capacity, event ordering, handler isolation, fence placement, and shutdown without an unbounded task or queue.
- No change to pixels, IPC framing, capture authority, WGC behavior, privacy generation, manual-region semantics, or attention-sheet format.
- Exact reconciliation of accepted hashes, run/artifact evidence, active-task count, and next safe task.

## Deferred findings

- Standing documents still name Codex as the builder and include the retired Claude-collaboration workflow; open draft PR #8 is stale and conflicted. A bounded control task after R2 should rename the builder role, finish or supersede PR #8, and retire the obsolete `claude/multi-ai-code-collab-o5qhj1`, `foreman/task-01-control`, and `agent/task-r1-retired-collaboration-cleanup` branches.
- Roadmap Stage 10 (keepsakes and complete memory recovery) has no corresponding task in the core task packet; a packet must be written before that stage begins.
- The synthetic capture source cannot emit `SourceResized`, so the visual fence is exercised through the `Stopped` fence. A synthetic resize capability would allow a direct resize-ordering test in a later visual task.
