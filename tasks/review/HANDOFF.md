# Task Handoff

## Task

Task 6 — Regions and Attention Sheets, the final Stage 4 packet. Local implementation and actual-diff review passed; publication, Windows CI, remote equality, merge, and accepted-main reconciliation remain blocking Paw Gate evidence.

## Completed

- Extended the exact-target worker with bounded BGRA32 readback, a tiny local luminance signature, near-duplicate rejection, normalized region remapping, deterministic staggering, and a current-grant manual override.
- Added one orientation sheet after authorization/restart/resize and ordinary one-moment sheets containing a labeled full-context panel plus at most two labeled focus crops.
- Added a deterministic lossless PNG encoder and strict metadata carrying exact target session, privacy generation, target identity, source sequence/time/dimensions, sheet geometry, region geometry, kind, and change score.
- Upgraded capture IPC to version 2 while retaining the 64 KiB control-JSON ceiling. Only `AttentionSheetProduced` may carry one separately framed payload, capped at 8 MiB and verified with SHA-256 before atomic ownership transfer.
- Preserved sealed-grant isolation: neither client nor worker can issue capture grants. Worker readback/composition has no runtime, memory, discovery, presentation, network, semantic, conversation, or persistence authority.
- Bounded source ownership at Task 5's three frames/64 MiB, processing at capacity two, temporary visual work at 64 MiB, and final worker/client sheets at two. All raw, signature, canvas, encoder, payload, evicted, stale, and disposed buffers have deterministic zero/release paths.
- Added a provisional sampled watchdog for invalid hard bounds or sustained private-memory/native-handle growth; a trip revokes visual admission, stops the source, clears work, and reports `ResourceBudgetExceeded`.
- Correlated sheets through Task 4's exact source-frame Privacy Guard. A newer pulled sheet is held until its own frame is admitted; rejection/revocation/stop/manual reset clears held work.
- Closed ordering races found during separate review: deferred start-time frames, synchronous resize/fault status fences, coalesced newest-sheet events, and a pause/drain/response/resume fence around manual-region changes.
- Added neutral WPF controls for applying/clearing a normalized manual region and displaying only privacy-safe sheet metadata. No final character wording, artwork, semantic output, or image display was added.
- Added 32 capture-worker and 4 target-authorization tests over the accepted Task 5 baseline, including strict payload negatives, geometry/labels, manual barriers, privacy correlation, watchdog behavior, ownership cleanup, deterministic artifact decoding, and a 216,000-frame accelerated six-hour visual soak.

## Changed

- 41 paths relative to accepted Task 5 `main`: 4,149 additions and 106 deletions.
- Capture Contracts, Client, Worker, Fake, Target Authorization controller, and the minimal neutral App controls.
- Capture Worker and Target Authorization tests.
- CI synthetic-artifact upload plus Task/README/Ledger/Handoff control records.
- No package, lock, solution, memory, runtime, semantic/API, conversation, ERPP implementation, personality, production-root, or durable product-image path changed.

## Verification

- SDK: exact pinned .NET 10.0.302 installed locally for the gate.
- Locked restore: `dotnet restore CompanionCore.slnx --locked-mode --disable-parallel /m:1 /p:EnableWindowsTargeting=true`; passed. Serialization avoided an SDK project-graph race that otherwise returned failure with zero diagnostics.
- Dependency audit: `EnableWindowsTargeting=true dotnet package list --project CompanionCore.slnx --vulnerable --include-transitive --no-restore --format json --output-version 1`; passed across all 20 projects with no vulnerable package entries.
- Strict Release build: `dotnet build CompanionCore.slnx -c Release --no-restore /m:1 /p:EnableWindowsTargeting=true /warnaserror`; all 20 projects passed with 0 warnings and 0 errors.
- Focused suites: Capture Worker 60/60 and Target Authorization 61/61 passed after the final ordering/resource corrections.
- Full locally executable suite: 292/292 invocations passed — Capture 14, Capture Worker 60, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 61.
- Accelerated visual soak: 216,000 generated frames (six hours at 10 fps), over 200,000 duplicate rejections, bounded maxima, and zero current sheet/working ownership at completion.
- Synthetic artifact: decoded PNG, 792×621, 8-bit RGBA, non-interlaced; visual inspection confirmed `FULL CONTEXT`, `CENTER`, and `LOWER / DIALOGUE` labels and synchronized source crops. Digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- Structural/static review: `git diff --check` passed; worker still references only Capture Contracts; sealed-grant isolation test passed; no added network/API, runtime/memory authority, target enumeration/fallback, production image write, semantic/conversation/ERPP/personality, or unrelated data surface was found. The sole added image write is the generated test artifact beneath `TestResults`.
- Auxiliary formatter: `dotnet format --verify-no-changes` could not launch its Roslyn build-host pipe in this restricted Linux environment (`SocketException: Permission denied`). It is not a Task 6 Paw Gate command; strict compiler/analyzer and diff-whitespace gates passed.

## Remaining

- Obtain exact-destination confirmation required by the environment's external-write reviewer, then publish `agent/task-06-regions-attention-sheets` to `https://github.com/nafoas/Companion-Coding`.
- Run the exact candidate on Windows CI. Expected complete count from the accepted 269-test baseline plus 36 Task 6 tests is 305; the result must be reported, not assumed.
- Inspect the uploaded synthetic PNG and Windows TRX to prove the child-process/WPF branches actually executed.
- Correct any Windows-only defect without weakening an invariant; rerun the entire gate after any code correction.
- Reconcile exact local/remote tree equality, record the Windows run/job/artifact evidence, archive Task 6, mark Stage 4 accepted, merge, and verify accepted `main` before opening Task 7.

## Risks and assumptions

- Linux cross-builds Windows projects but cannot launch `Microsoft.WindowsDesktop.App`; real WPF integration and Windows child/WGC execution remain unverified until CI.
- Minimized and exclusive-fullscreen WGC remain explicitly unsupported without actual target-PC evidence. CI cannot change that claim.
- The watchdog's growth thresholds are intentionally conservative and provisional until Stage 11 physical profiling; hard byte/count limits are not provisional.
- Product code retains no images. A caller that takes ownership of an `AttentionSheet` must dispose it; controller/client-owned sheets are zeroed automatically on revocation and teardown.
- The configured `git push` was attempted under Boss's explicit authorization but rejected before execution by the external-write reviewer because it required exact destination confirmation. No alternate or credential-bypassing route was attempted.

## Personal Round Judgments

J1–J9 are recorded in the active packet. They cover worker-only nonsemantic pixels, deterministic normalized defaults, PNG/size bounds, separate payload framing, exact-frame privacy correlation, manual response fencing, provisional watchdog thresholds, synthetic-only durable evidence, and bounded coalesced event behavior. Every choice is reversible without changing identity, memory, privacy authority, or later-stage semantics.

## Review focus

- Recheck WGC `SoftwareBitmap` stride/capacity arithmetic and the conservative two-copy visual accounting on Windows.
- Verify start/resize/fault/manual response ordering from TRX and ensure the first post-start/post-resize orientation has a published exact source frame.
- Verify valid/truncated/corrupt/oversize/unexpected/stale/mismatched/duplicate/out-of-order payload cases terminate or reject the disposable worker epoch as specified.
- Verify stop, privacy stop, manual reset, resize, fault, crash, restart, and disposal return source/signature/canvas/encoder/worker-sheet/client-sheet ownership to zero.
- Recheck the 41-path scope against accepted Task 5 and scan again for Task 7+, API, semantic attention, conversation, ERPP implementation, personality, durable product images, and production data.

## Repository state

- Branch: `agent/task-06-regions-attention-sheets`.
- Accepted base: `a257d5a1d70d03a77f27582b9f0bbecd0194e67d`, tree `b3a84d9a0a5be6cc81b4f517ac1b1fdb8537a23b`.
- Local implementation head: `550e81304ff5e3cc3a722eb02e2a4b7ffc984f53`, tree `44e9fcd2dab03f563a09158076751e650f349483`.
- Documentation/evidence descendant: pending this update.
- Remote branch: not created; the attempted push was rejected before execution by the environment gate.
- Accepted remote `main`: unchanged at Task 5.

## Next safe task

Publish this exact Stage 4 candidate once the external destination gate is satisfied, then require a clean Windows Paw Gate. Do not archive Task 6, mark Stage 4 accepted, merge, or open Task 7 before that evidence exists.

## Credit status

No credit-related stop.
