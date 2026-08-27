# Task 6 — Regions and Attention Sheets

Status: **Active; authorized by Boss on 2026-08-26 as the remainder of Stage 4.**
Accepted base: `a257d5a1d70d03a77f27582b9f0bbecd0194e67d`
Working branch: `agent/task-06-regions-attention-sheets`
Roadmap slice: Stage 4, final packet — bounded local visual composition only

## Objective

Turn exact-target source frames already owned by the isolated Task 5 worker into bounded, privacy-fenced visual observations: an initial high-fidelity orientation image, normalized default and manual focus regions, staggered synchronized crops, labeled attention sheets, local change scoring and duplicate rejection, and resolution/scaling remapping. Final attention sheets may cross the same-user worker boundary only as size-bounded, checksummed, RAM-only payloads whose target session and privacy generation remain inseparable from their metadata.

This packet completes Stage 4. It does not interpret images, choose focus semantically, call a provider, create conversations or ERPP events, write memory, install personality, retain photographs, or touch production data.

## Entry evidence

- Accepted `main` is Task 5 merge `a257d5a1d70d03a77f27582b9f0bbecd0194e67d`, exact tree `b3a84d9a0a5be6cc81b4f517ac1b1fdb8537a23b`.
- Task 5 Windows run `31477853767`, job `93735671804`, passed locked restore, dependency audit, all 20 Release builds with zero warnings/errors, and 269/269 tests.
- The exact-target WGC worker, 64 MiB/three-source-frame accounting, queue capacity two, deterministic cleanup, terminal IPC validation, and restart behavior are accepted dependencies and must not be weakened.
- Boss explicitly authorized completing and pushing the full remainder of Stage 4 on 2026-08-26.

## Required implementation

### Pixel ownership and privacy fence

- Keep WGC surface readback, crop extraction, change detection, composition, and encoding inside the dedicated capture process.
- Extend a source-frame lease with a bounded BGRA32 readback operation. Synthetic private-safe sources use the identical pixel contract.
- Every visual result carries the exact target-session ID, privacy generation, target identity, source sequence, source timestamp, and source dimensions.
- Recheck active authorization after asynchronous visual work. Stop, fault, resize, restart, or generation revocation disposes stale work before publication.
- Raw source pixels, downsample signatures, crop buffers, canvases, encoder buffers, and final sheets remain RAM-only and are zeroed/released deterministically. No production path writes an image to disk.

### Orientation, regions, and geometry

- Emit exactly one initial high-fidelity orientation sheet from the first usable frame after each authorization, worker restart, or source-geometry recalibration.
- Define a small immutable normalized default layout covering full context, center environment, lower dialogue/inventory, upper corners, and side regions.
- Map normalized regions to clamped pixel rectangles with deterministic rounding. Window movement does not affect content-relative mapping; resolution, DPI/content scaling, and aspect-ratio changes remap from normalized coordinates rather than reusing stale pixels.
- Add one bounded manual focus override contract corresponding to `Look here, little Prince`. Setting or clearing it requires the current sealed grant and cannot retarget capture.
- A resize clears incompatible retained frames, change history, outstanding sheets, and stagger state; the next usable frame is a new orientation.

### Local visual processing

- Compute a small local luminance signature and a normalized change score without semantic interpretation.
- Reject exact and near-duplicate frames after orientation; retain only the bounded signature needed for the next comparison.
- Stagger focus regions deterministically across changed source frames. A manual override receives priority without allowing an unbounded region count.
- Build each ordinary attention sheet from one source moment: moderate full context plus at most two labeled focus crops from that same source frame.
- Encode sheets in a deterministic, lossless, widely inspectable image format with bounded dimensions and payload size. Labels must be present both visibly and as strict structured metadata.

### Bounded transport and lifetime

- Preserve the 64 KiB ceiling for control JSON. Pixel data may travel only as a separately framed attention-sheet payload with an independent hard byte ceiling and SHA-256 integrity field.
- Only the `AttentionSheetProduced` notification shape may carry a payload. Malformed shape, invalid metadata, oversize, checksum mismatch, stale/mismatched authorization, duplicate/out-of-order sheet sequence, or unexpected payload tears down that disposable worker epoch.
- Bound worker-side pending sheets and main-process retained sheets to at most two, newest-preserving. Dropped, superseded, stopped, faulted, restarted, and disposed sheets are zeroed/released.
- Expose a transfer-of-ownership `TakeLatestAttentionSheet` operation; taking the newest sheet disposes any older queued result. No unbounded event or consumer backlog is permitted.

### Watchdog and metrics

- Extend privacy-safe metrics with orientation/change/duplicate/sheet counts, pending and maximum sheet bytes/count, visual working bytes, transport drops, and watchdog trips. Metrics contain no pixels, hashes, titles, paths, labels, or unrelated-process data.
- Add a conservative provisional watchdog for impossible hard-bound violations and sustained private-memory/native-handle growth after warmup. A trip revokes visual admission, clears disposable state, and reports an honest terminal resource fault; it never touches BunDex data.
- Keep thresholds explicit, deterministic, testable, and provisional pending Stage 11 physical profiling.

## Invariants

1. One authoritative runtime and one exact authorized target remain unchanged.
2. Foreground changes never retarget capture; visual geometry is target-content-relative.
3. No pixels exist before Task 4 authorization or survive privacy stop, target end, terminal fault, or worker teardown.
4. Source-frame ownership remains at most three frames and 64 MiB. Visual work and final-sheet queues have separate explicit hard bounds and cannot grow with runtime duration.
5. One sheet uses one source sequence and timestamp. Staggering selects regions across sheets, never mixes source moments within a sheet.
6. Local motion is only a change/duplicate signal. It does not infer meaning, urgency, interest, game state, or personality.
7. The worker exposes no runtime construction, memory write, semantic-provider, conversation, or identity authority.
8. No image is durably written by product code; committed synthetic inspection artifacts may exist only in test output.

## Explicit non-goals

- semantic interpretation, API/provider contracts, credentials, network or paid calls;
- attention states, interest/urgency scoring, learned layouts, meaning-based focus, game-specific adapters;
- conversation, ERPP-01 implementation, BunDex proposals, session consolidation, personality, Prince wording, animation, or audio;
- permanent photographs, image galleries, screenshot history, disk caches, telemetry upload, or production data;
- desktop/display capture, foreground fallback, browser-tab capture, multi-target capture, OCR, or exclusive-fullscreen workarounds;
- claiming minimized or exclusive-fullscreen WGC support without target-PC evidence.

## Acceptance scenarios

1. First usable authorized frame yields exactly one orientation sheet with matching target/generation/source metadata; later duplicates do not yield another.
2. Default normalized regions map inside 720p, 1080p, scaled, ultrawide, and resized content while retaining proportional alignment.
3. A manual override is accepted only for the current grant, is clamped/validated, appears in the next changed sheet, and can be explicitly cleared.
4. Ordinary sheets contain a labeled global view and no more than two focus crops, all extracted from the same synthetic source moment.
5. Deterministic changed pixels produce a bounded score; identical/near-identical frames are rejected; reset/resize does not compare incompatible geometry.
6. Stop, privacy revocation, resize, fault, restart, and disposal clear signatures, raw/intermediate buffers, queued sheets, and client-held sheets without late publication.
7. Control messages remain at most 64 KiB. A valid sheet payload round-trips; oversize, truncated, corrupted, unexpected, stale, target-mismatched, duplicate, and out-of-order payloads fail closed.
8. Repeated production fills preserve at most three source frames/64 MiB, two processing entries, two worker pending sheets, and two client sheets; newest work survives pressure.
9. A multi-hour accelerated synthetic run has constant bounds and zero outstanding pixel/sheet ownership after clear/dispose.
10. Resize/minimize/render-stall and worker crash/restart regressions remain honest and do not duplicate the runtime or orientation state.
11. The provisional watchdog ignores ordinary fluctuation, detects injected sustained growth/hard-bound violations, clears visual state, and reports one resource fault.
12. A deterministic synthetic attention sheet is decoded and inspected in tests and published as a CI artifact; it contains the expected visible labels and synchronized regions.
13. Every accepted Task 1–5 regression passes unchanged, including Windows/WPF child-process cases, with zero build warnings/errors and a clean locked dependency audit.
14. Actual-diff review finds no Stage 5+, API, attention-engine semantics, conversation, ERPP implementation, personality, durable-image, or production-data surface.

## Allowed paths

- `src/CompanionCore.Capture.Contracts/**` for strict region/sheet contracts, ownership, metrics, and versioned IPC;
- `src/CompanionCore.Capture.Worker/**` for pixel readback, geometry, local change detection, composition/encoding, watchdog, queues, and cleanup;
- `src/CompanionCore.Capture.Client/**` for bounded verified sheet reception, stale admission, ownership transfer, and cleanup;
- `src/CompanionCore.Capture.Fake/**` only to satisfy the expanded capture contract with deterministic bounded behavior;
- `src/CompanionCore.TargetAuth/**` and `src/CompanionCore.App/**` only if minimal current-grant/manual-region plumbing is required; no final UI or wording;
- `tests/CompanionCore.Capture.Worker.Tests/**`, `tests/CompanionCore.Capture.Tests/**`, and `tests/CompanionCore.App.IntegrationTests/**` for focused and regression coverage;
- `.github/workflows/ci.yml` only to retain the synthetic inspection artifact;
- solution/project/lock files only if a bounded Task 6 project split is demonstrably necessary;
- `README.md`, `BUILD_LEDGER.md`, `tasks/**`, and directly necessary Stage 4 control-document corrections.

## Paw Gate

Task 6 and Stage 4 pass only when:

- every acceptance scenario above has current evidence;
- locked restore, dependency audit, all Release builds, focused tests, full local regressions, and Windows CI pass without unexplained warnings, failures, or skips;
- the candidate diff is reviewed separately against accepted `main` for privacy generation fencing, exact-target isolation, allocation arithmetic, payload framing/validation, queue ownership, cancellation, disposal, watchdog behavior, and forbidden later-stage surface;
- a synthetic attention-sheet artifact is visually/structurally inspectable and contains no private source data;
- exact commands/results, limitations, Personal Round Judgments, branch/head/tree, remote equality, and worktree state are recorded;
- the task is archived, Stage 4 is marked accepted, and Task 7 remains unopened until the accepted merge is verified.

## Personal Round Judgments

Record routine reversible Task 6 choices here before the gate. No judgment may weaken the invariants or cross a stop condition.

## Deferred findings

- Actual target-PC minimized and exclusive-fullscreen behavior remains unsupported evidence from Task 5, not a Task 6 defect to paper over.
- Meaning-based focus, learned layouts, and animated-region suppression belong to Task 8 after Task 7's semantic bridge contracts exist.
- ERPP-01 remains affiliated with Task 9 and has no Task 6 implementation surface.
