# Task Handoff

## Task

Task R3 — Reliable Orientation Delivery (`tasks/active/task-r3-orientation-test-race.md`). This corrective Stage 4 packet is based directly on accepted `main` `2b5ef836183dde261d52bfa2f6c68340aff2e9f3` and is published as draft PR #17. Task 7 remains unopened.

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

Seventeen paths relative to accepted `main`, all within the packet's allowlist: 8 product files, 6 test files plus 1 new test file, the packet, and this handoff.

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

## Remaining

- This evidence descendant must pass both exact Windows event paths. PR #17 then merges with an expected-head fence.
- After that merge, a docs-only reconciliation archives R3 and updates the Ledger and README. Post-merge `main` CI must be green before Stage 4 is recorded as closed.

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

- Notice ordering against frame dispatch, and the stranded-orientation release.
- The controller's owed-orientation state transitions and the lease-free retake.
- Both GitHub event paths at 325/325 on the exact final head.

## Repository state

- Branch `agent/task-r3-orientation-test-race`, draft PR #17 against `main`.

## Next safe task

Merge PR #17 after its final-head gates pass, then publish the docs-only Stage 4 closure reconciliation.
