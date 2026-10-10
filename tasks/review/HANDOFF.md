# Task Handoff

## Task

Task R2 — Stage 4 Recertification and Closure (`tasks/active/task-r2-stage4-recertification.md`). This corrective packet is based directly on accepted `main` `04af51b90ffbc44933a56c21a1c1223940ef671a` and is published as draft PR #15. Task 7 remains unopened.

Builder: Claude, which took over direct construction from Codex at Boss's instruction on 2026-10-10. The Paw Gate protocol is unchanged.

## Completed

- Recertified the accepted Stage 4 chain against the repository. Task 6 merged through PR #11, the attributable resource correction merged through PR #13, and PR #14 merged the docs-only acceptance as `04af51b`. The previous handoff still described PR #14 as pending, and this one replaces it.
- Reviewed Codex's interrupted R2 evidence commit `326afd4`. Its candidate fixes were never published and are unavailable. Its packet and two regressions were kept as reviewed evidence, not inherited work.
- Diagnosed the six-hour hang of run `33337966344`. `ProducerPressure_DoesNotAccumulateWakeSignalsForEvictedFrames` failed its assertion before releasing its blocked processor, so `await using` disposal waited on `ClearAsync`, which waited on the processor, which waited on the pipeline lifetime. The deadlock was reproduced locally with `--blame-hang-timeout`, which named exactly that test.
- Fixed the wake-signal debt in `CaptureFramePipeline`. Every non-consumer removal of a pending frame now goes through `DequeuePendingUnsafe`, which retires that entry's unreserved wake signal with `SemaphoreSlim.Wait(0)` under the pipeline gate. This covers oldest-pending eviction in `TryOffer`, byte/count eviction in `MakeRoomFor`, and `ClearAsync`. The consumer's own dequeue is unchanged, because it already consumed its signal.
- Fixed status-fence starvation in `OutOfProcessCaptureWorker`. Status changes now travel on a second bounded 64-entry drop-oldest lane with their own dispatcher. Frames and attention sheets keep the original lane and its single serialized dispatcher, so sheet-after-source-frame ordering is untouched.
- Preserved the ordering the single lane implied. `SetStatus` advances a visual fence under the state gate for every status other than an ordinary `Running` transition, which is exactly the set `TargetSessionController` treats as clearing visual admission. Frame events record the fence when they are created, and dispatch rejects them if it has moved.
- Made every regression fail promptly. The pressure regression releases its processor in `finally` and asserts the exact invariant of one unreserved signal per pending entry. A new case proves `ClearAsync` retires the signals of cleared entries before it waits for the processor. The blocked-observer case requires `Stopped` within 3 s, and then requires that releasing the observer dispatches none of the frames queued behind it.
- Bounded CI hangs: `--blame-hang-timeout 10m` on the Test step and `timeout-minutes: 60` on the job.

## Changed

Six paths relative to accepted `main`, all within the packet's allowlist (330 additions, 52 deletions):

- `src/CompanionCore.Capture.Worker/CaptureFramePipeline.cs`: wake-signal retirement helper; three removal sites routed through it; the test-visible `PendingWakeSignalCount` from `326afd4`.
- `src/CompanionCore.Capture.Client/OutOfProcessCaptureWorker.cs`: independent status lane and dispatcher, visual fence, shared lane factory, and disposal of both dispatchers.
- `tests/CompanionCore.Capture.Worker.Tests/CaptureFramePipelineTests.cs`: reworked pressure regression and a new clear regression.
- `tests/CompanionCore.Capture.Worker.Tests/OutOfProcessCaptureWorkerTests.cs`: blocked-observer `Stopped` and post-fence non-dispatch assertions.
- `.github/workflows/ci.yml`: hang and job time bounds only.
- `tasks/active/task-r2-stage4-recertification.md`: the revised packet.

No project, package, lock, solution, contract, memory, runtime, privacy-authority, target-discovery, WGC, app, production-data, or durable-image path changed.

## Verification

- SDK: pinned .NET 10.0.302, installed in the builder scratch directory.
- Locked restore: `dotnet restore CompanionCore.slnx --locked-mode -p:EnableWindowsTargeting=true` passed.
- Dependency audit: `dotnet package list --project CompanionCore.slnx --vulnerable --include-transitive --no-restore --format json --output-version 1` (with `EnableWindowsTargeting=true`) exited 0. It reported all 20 projects and 0 vulnerable direct or transitive packages.
- Strict Release build: `dotnet build CompanionCore.slnx -c Release --no-restore /m:1 /p:EnableWindowsTargeting=true /warnaserror --no-incremental` built all 20 projects with 0 warnings and 0 errors.
- Local red proof: with only the accepted `CaptureFramePipeline.cs` restored, both pipeline regressions failed promptly and for the intended reason. The pressure test expected 2 signals and found 4096 in 20 ms. The clear test expected 0 and found 2 in 14 ms. The filtered suite finished in 9 s with no hang.
- Local green: the focused pipeline suite passed 9/9 three consecutive times. The full locally executable suite passed 298/298: Capture 14, Capture Worker 63, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 64. The Windows-only branches of the Capture Worker suite return early on Linux, and App Integration needs `Microsoft.WindowsDesktop.App`, so Windows CI is authoritative for those cases.
- Windows red evidence: head `bf0113cdd86e2c51110643d7181ef51cbabac54b` (tests only, accepted product code), push run `38025402946`, job `114135078349`, 2 min 9 s.
  - Restore, audit, and the 20-project Release build passed.
  - Tests: 311 executed, 308 passed, and exactly the 3 intended failures, with no timeouts or skips:
    - `Clear_RetiresWakeSignalsOfClearedPendingFrames`: 0 vs 2, 94 ms.
    - `ProducerPressure_DoesNotAccumulateWakeSignalsForEvictedFrames`: 2 vs 4096, 130 ms.
    - `BlockingFrameObserver_CannotBlockProtocolResponsesOrWorkerStop`: `TimeoutException` waiting for `Stopped`, 3 s.
  - Artifacts: test results `11659674170` (`sha256:5cf62c2e6b87d3eb4564da4f3b4aef41668f3ff16a77d6371547b55c5bebdd7e`) and sheet `11659474458` (`sha256:e0398105401a571dc491cceb1a3a74a6d229ca37252bf7900cc635c023c717ac`).
- Windows candidate evidence: head `82a70df099a53a877b0d2443a8b39b02ece846a6`, tree `f9b13752f4d54a19208fec3a933590a9d73b23e1`.
  - Push run `38025519542`, job `114135429187`, and PR run `38025588908`, job `114135636155`, each passed on the first attempt. Each ran locked restore, the clean 20-project audit, all 20 Release builds with 0 warnings and 0 errors, 311/311 tests (App Integration 13, Capture 14, Capture Worker 63, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 64), and both artifact uploads.
  - Key durations on the push run: blocked observer 1.03 s, including its deliberate 0.5 s post-release observation window; 24 restarts with exact owned handles 13.39 s; accelerated visual soak 1.71 s; pressure regression 0.09 s; clear regression 0.10 s.
- Artifact inspection: every archive was downloaded and hashed, and each digest matched GitHub's.
  - Push: test results `11659403212` (`sha256:2bdbed783d0565dc2f67a44d3bbafd53bbc04bf9fb4799d175adc8cd0a8bd543`) and sheet `11659143398` (`sha256:c9ffbea6e7171a0062a67451ab9da1c9dbdefd784ade45ef6103832955bd9cdd`).
  - PR: test results `11659043533` (`sha256:db8e2ba8eaf13940830e0e0e5e0539ef8065931eff426c9282a9d25babc52904`) and sheet `11659423284` (`sha256:296647b48ab99b137abd3b908976d93d44c0d3ab613c1266ae6c6ed89112a17f`).
  - Every TRX set totals 311 executed. Every extracted PNG, including the red run's, is 792×621, 8-bit RGBA, non-interlaced, with the accepted pixel digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`. Visual inspection confirmed the `FULL CONTEXT`, `CENTER`, and `LOWER / DIALOGUE` labels and synchronized crops.
- Static gates: `git diff --check origin/main...HEAD` passed; exactly one packet is active; the changed-path set equals six allowlisted paths; local and remote heads and trees are identical.

## Remaining

- This evidence descendant must pass both exact Windows event paths. PR #15 then merges with an expected-head fence.
- After that merge, a docs-only reconciliation must archive the packet, update `BUILD_LEDGER.md` and `README.md`, and pass its own exact gates before Stage 4 is recorded as closed.
- Task 7 stays unopened until that reconciliation merges.

## Risks and assumptions

- A frame already inside a frame observer when a fence advances completes normally and can finish after the status lane delivers the fence. The single lane had the same in-flight behaviour. No queued frame can be dispatched after its fence.
- The status lane drops its oldest entry only if 64 status changes queue behind a blocked status observer. Newest status is preserved, which is the state consumers act on.
- The synthetic capture source cannot emit `SourceResized`, so the fence is exercised through `Stopped`. The resize path uses the same code and was reviewed by reading it.
- Minimized and exclusive-fullscreen WGC remain unsupported without target-PC evidence. Watchdog growth thresholds remain provisional until Stage 11.

## Personal Round Judgments

J1–J6 are recorded in the packet: R2 as a corrective label, one-for-one wake signals, an independent status lane, a visual fence, CI hang bounds, and builder continuity. Every one is reversible without changing identity, memory, privacy authority, or later-stage semantics.

## Review focus

- Signal accounting across consumer reservation, eviction, clear, pause/resume, and disposal: available ≤ pending and available + reserved ≥ pending.
- Fence placement relative to `TargetSessionController`'s clearing statuses, and the absence of status-triggered sheet dispatch.
- Both GitHub event paths at 311/311 on the exact final head, with both artifacts.

## Repository state

- Branch `agent/task-r2-stage4-recertification`, draft PR #15 against `main`.
- Candidate implementation head `82a70df`; this handoff is its evidence descendant. Worktree clean after commit.

## Next safe task

Merge PR #15 after its final-head gates pass, then publish the docs-only Stage 4 closure reconciliation. Do not open Task 7 within this packet.
