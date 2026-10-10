# Task Handoff

## Task

Task R3 — Reliable Orientation Delivery. Accepted and archived (`tasks/archive/task-r3-orientation-test-race.md`). PR #17 merged the correction and its evidence as `755b11f2304ed8567011958f2de6e16448ef15ec` with an expected-head fence and unchanged tree `81523c004c6b2b791ceb67aa90a67071a4c11712`. This docs-only reconciliation records the closure. Stage 4 is complete, no task is active, and Task 7 remains unopened.

Builder: Claude. Boss directed reliable delivery plus a retake failsafe on 2026-10-10.

## Completed

- Diagnosed the failure of post-merge `main` run `38026209974` as one cause behind five recorded failures since 2026-08-27. The worker produces exactly one orientation per epoch, but it could be lost before the consumer saw it, in four places:
  - newest-preserving client retention;
  - newest-only notice coalescing;
  - start deferral that dropped the orientation's source frame;
  - a controller slot that let a newer regional sheet displace it.

  A privacy-rejected source frame made it unrecoverable.
- An interim test-only relaxation (`c491525`) was superseded by Boss's decision to fix delivery in the product. Its PR-path run `38026568119` also failed `Restart_EmitsFreshOrientation…`, which confirmed the coalescing cause.
- Client:
  - Pinned retention: the orientation counts toward the unchanged two-sheet ceiling and is handed over first.
  - Notice: dispatched exactly once, after its exact source frame, before newer regional notices. A stranded orientation is released.
  - Start deferral: never evicts the orientation's source frame.
  - Added a grant-checked `RequestOrientationAsync`.
- Controller: a separate orientation slot, released first. The failsafe owes an orientation from every entry into `Running` and every `SourceResized`. It asks for a retake after eight admitted frames, at most three times, then counts an honest delivery failure. Privacy-rejected frames do not count. Requests run on the thread pool, outside the privacy admission lease.
- Worker:
  - `RequestOrientation` command (IPC protocol v3), shape-validated;
  - engine method gated on a running worker and the active grant;
  - pipeline re-arm that makes the next frame, even a duplicate, the orientation.
- Fake worker and test harness implement the new interface member. The harness follows the orientation-first take contract and builds valid regional sheets.

## Changed

Sixteen paths relative to accepted `main` `2b5ef83`, all within the packet's allowlist: 8 product files, 5 modified and 1 new test file, the packet, and the handoff. The handoff on the merged head miscounted these as seventeen; this record is the correction.

## Verification

- Local (pinned .NET 10.0.302, Linux cross-build):
  - all 20 Release projects built with `/warnaserror`, 0 warnings and 0 errors;
  - 312/312 locally executable tests passed: Capture 14, Capture Worker 68, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 73;
  - the Target Authorization suite passed three consecutive times;
  - `git diff --check` passed.
- Local red: with the accepted `TargetSessionController.cs` restored, `UnadmittedOrientation_IsNotDisplacedByANewerRegionalSheet` failed. It returned the Regional sheet with sequence 3 where the Orientation with sequence 2 was expected.
- Windows red evidence: head `8aaac5a0c2bb53bb26796035f7e443fc0970c7f7` (tests only, accepted product). Push run `38027555649` and PR run `38027558031` each executed 312 tests. Exactly the two intended cases failed, with no others failing and no hang:
  - `SyntheticWorker_ProducesBoundedOrientationAndManualRegionalSheets`: "Expected: Orientation, Actual: Regional", 0.87 s and 0.80 s.
  - `SlowFrameObserver_StillReceivesEveryEpochOrientationFirst`: orientation-notice timeout, 10.4 s.
- Windows candidate: head `ff2add8f260c9fedd768eb0bf4eaa0bc94cdb55d`, tree `4504522d27cf9a16c2d6f183a880d7ebc76d25aa`. Push run `38027635962` (job `114141766276`) and PR run `38027637986` (job `114141772681`) each passed restore, audit, all 20 Release builds with 0 warnings and 0 errors, 325/325 tests, and both artifact uploads.
  - Totals: App Integration 13, Capture 14, Capture Worker 68, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 73.
  - Key durations (push): slow observer across 4 epochs 2.06 s; retake through the worker process 0.35 s; strict orientation-first 0.66 s; restart orientation 1.02 s; 24-restart owned handles 8.01 s.
- Artifacts: every archive was downloaded and matched GitHub's digest.
  - Push: test results `11660652791` (`sha256:80760275787d7213cee8f131c2f6d9d6fc0640d39380ba1152b0aa771a089705`) and sheet `11660807846` (`sha256:91c6a14432d73c09e831ec244e5a184df8116b9a93389d3842e0f3fda5d7d64e`).
  - PR: test results `11660353253` (`sha256:0eac3ca08700cbed159a22d34908c8a41d23dc8bbad01a604932e0f26eab90b2`) and sheet `11660238334` (`sha256:6a2a67743a027b110af17965cafbb580573d22df3159e7ebb8b3da7f8729583b`).
  - Every PNG is 792×621 RGBA with the accepted pixel digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- Source review:
  - Sheet ceilings are unchanged.
  - The orientation notice cannot precede or outlive its exact source frame.
  - Regional notices are held back only until the orientation's source frame resolves.
  - The deferral protects at most one frame.
  - The failsafe is bounded and never performs worker I/O inside an admission lease.
  - `RequestOrientation` is checked against the grant at the client, the IPC shape check, and the engine.
- Final event paths on evidence head `975a7ce2a19956041996dc47037d07bca7f1e650`: push run `38027884572` (job `114142508091`) and PR run `38027886917` (job `114142515094`) each passed 325/325 with both artifacts on the first attempt. Push artifacts are test results `11660498445` and sheet `11660323524`; PR artifacts are test results `11661321188` and sheet `11660543477`. All hashes matched GitHub, and every PNG retained the accepted pixel digest.
- Merge: the PR #17 merge ref had exact parents `2b5ef83` and `975a7ce` and a tree equal to the head tree. It was squash-merged with expected head `975a7ce` as `755b11f2304ed8567011958f2de6e16448ef15ec`; fetched `main` retained exact tree `81523c004c6b2b791ceb67aa90a67071a4c11712`.

## Remaining

- This docs-only reconciliation must pass both exact Windows event paths and merge with an expected-head fence, followed by green post-merge `main` CI.
- No Stage 4 work remains. Task 7 stays unopened; later work begins through its own bounded active packet.
- Next in order, from the deferred findings:
  - a bounded control task to rename the builder role in standing documents, retire the obsolete Claude-collaboration workflow, close stale draft PR #8, and retire the abandoned branches;
  - surfacing the orientation failsafe counters in later diagnostics;
  - a Stage 10 keepsakes packet before that stage begins.

## Risks and assumptions

- An orientation whose exact source frame is skipped is released rather than announced. Recovery depends on the controller failsafe, which is bounded at three retakes.
- The eight-frame budget and three retakes are provisional until Stage 11 calibration.
- The synthetic source cannot emit `SourceResized`, so resize-owed orientations are proven at the controller through status events.

## Personal Round Judgments

J1–J7 are recorded in the packet:
- J1: fix delivery in the product rather than relax the tests;
- J2: keep the `TakeLatest` name with orientation-first semantics;
- J3: a frame budget instead of a timer;
- J4: IPC protocol version 3;
- J5: release a stranded orientation;
- J6: make `ValidateCommandShape` internal;
- J7: update two protocol-version literals in accepted tests.

## Review focus

- Confirm this reconciliation changes only `BUILD_LEDGER.md`, `README.md`, this handoff, and the R3 active-to-archive rename and content update.
- Require both GitHub event paths to execute 325/325 and generate both artifacts on the exact reconciliation head.

## Repository state

- Accepted `main`: `755b11f2304ed8567011958f2de6e16448ef15ec`, tree `81523c004c6b2b791ceb67aa90a67071a4c11712` (PR #17).
- Reconciliation branch `agent/task-r3-stage4-closure`, based directly on that `main`. Worktree clean after commit.

## Next safe task

Merge this reconciliation after its exact gates pass, and confirm post-merge `main` CI is green. Then open the bounded control task named under Remaining, or Task 7 through its own packet, one active task at a time.
