# Task Handoff

## Task

Task 6 — Regions and Attention Sheets, the final Stage 4 packet. Corrected implementation, separate actual-diff review, publication, exact remote equality, and Windows CI passed. The evidence-bearing descendant, merge, and accepted-main reconciliation remain.

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
- Rejected the initially green published candidate during actual-diff review and added a second correction layer: sheet availability cannot precede source-frame dispatch; held transferred payloads are disposed on resize/fault; admission matches source sequence, timestamp, and dimensions; and impossible BGRA geometry fails protocol validation before allocation.
- The exact evidence runs exposed test-harness assumptions. The accepted Task 5 parent-handle heuristic observed shared-host runtime warmup; the isolated collection now runs a full warmup plus 12 measured restarts, waits for release, and enforces a two-handle steady-state drift ceiling. The longer run demonstrated legitimate non-adjacent Windows PID reuse, so global freshness uses PID-plus-start-time identities and captured old-process handles. Restart coverage proves the fresh orientation event and fresh-epoch retained payload independently. All product code and invariants remain unchanged.
- Added neutral WPF controls for applying/clearing a normalized manual region and displaying only privacy-safe sheet metadata. No final character wording, artwork, semantic output, or image display was added.
- Added 33 capture-worker and 7 target-authorization tests over the accepted Task 5 baseline, including strict payload negatives, geometry/labels, manual barriers, privacy correlation, watchdog behavior, ownership cleanup, deterministic artifact decoding, and a 216,000-frame accelerated six-hour visual soak.

## Changed

- 44 paths relative to accepted Task 5 `main`: 4,542 additions and 190 deletions on the process-identity evidence descendant.
- Capture Contracts, Client, Worker, Fake, Target Authorization controller, and the minimal neutral App controls.
- Capture Worker and Target Authorization tests.
- CI synthetic-artifact upload plus Task/README/Ledger/Handoff control records.
- No package, lock, solution, memory, runtime, semantic/API, conversation, ERPP implementation, personality, production-root, or durable product-image path changed.

## Verification

- SDK: exact pinned .NET 10.0.302 installed locally for the gate.
- Locked restore: `dotnet restore CompanionCore.slnx --locked-mode --disable-parallel /m:1 /p:EnableWindowsTargeting=true`; passed. Serialization avoided an SDK project-graph race that otherwise returned failure with zero diagnostics.
- Dependency audit: `EnableWindowsTargeting=true dotnet package list --project CompanionCore.slnx --vulnerable --include-transitive --no-restore --format json --output-version 1`; passed across all 20 projects with no vulnerable package entries.
- Strict Release build: `dotnet build CompanionCore.slnx -c Release --no-restore /m:1 /p:EnableWindowsTargeting=true /warnaserror`; all 20 projects passed with 0 warnings and 0 errors.
- Focused suites: Capture Worker 61/61 and Target Authorization 64/64 passed after the final dispatch/identity/reset corrections.
- Full locally executable suite: 296/296 invocations passed — Capture 14, Capture Worker 61, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 64.
- Accelerated visual soak: 216,000 generated frames (six hours at 10 fps), over 200,000 duplicate rejections, bounded maxima, and zero current sheet/working ownership at completion.
- Synthetic artifact: decoded PNG, 792×621, 8-bit RGBA, non-interlaced; visual inspection confirmed `FULL CONTEXT`, `CENTER`, and `LOWER / DIALOGUE` labels and synchronized source crops. Digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- Structural/static review: `git diff --check` passed; worker still references only Capture Contracts; sealed-grant isolation test passed; no added network/API, runtime/memory authority, target enumeration/fallback, production image write, semantic/conversation/ERPP/personality, or unrelated data surface was found. The sole added image write is the generated test artifact beneath `TestResults`.
- Publication and equality: local correction head `018e99b1802673051c116df63e3e6bc4891f62cf` and published head `660d7d9c4ffc4592cf0c725c3686ce68735dab18` share exact tree `2876d61439087ecd41b4d967761c3ff447246a44`; an independent fetch produced an empty diff. Draft PR #11 targets the unchanged accepted Task 5 `main`.
- Corrected Windows gate: run `33027192357`, job `98371213540`; locked restore, dependency audit, strict Release build, and 309/309 tests passed — App Integration 13, Capture 14, Capture Worker 61, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 64.
- Windows evidence: test-results artifact `9628886868`, 67,006 bytes, archive digest `sha256:be57fa82e9bf07082417d931c4f7baee12be88156741c756c54c2456d888b388`; attention-sheet artifact `9628887307`, 619,254 bytes, archive digest `sha256:f23f23afa38591f8378a645c0c829c5b677f240f2f0d7248e6dc14b75f578e43`. Downloaded archive hashes matched GitHub, TRX totals were 309 executed/309 passed/0 failed/0 skipped, and the extracted PNG retained digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- Auxiliary formatter: `dotnet format --verify-no-changes` could not launch its Roslyn build-host pipe in this restricted Linux environment (`SocketException: Permission denied`). It is not a Task 6 Paw Gate command; strict compiler/analyzer and diff-whitespace gates passed.

## Remaining

- Publish the isolated-test evidence descendant and require its exact head to repeat the entire Windows gate.
- Mark PR #11 ready only after that final descendant passes, then merge with an expected-head fence.
- Verify the merge tree on remote `main`, publish a bounded post-merge acceptance reconciliation that archives Task 6 and marks Stage 4 accepted, gate and merge that reconciliation, and verify final `main` before opening Task 7.

## Risks and assumptions

- Linux cross-builds Windows projects but cannot launch `Microsoft.WindowsDesktop.App`; corrected Windows CI has now executed the real WPF and child-process branches.
- Minimized and exclusive-fullscreen WGC remain explicitly unsupported without actual target-PC evidence. CI cannot change that claim.
- The watchdog's growth thresholds are intentionally conservative and provisional until Stage 11 physical profiling; hard byte/count limits are not provisional.
- Product code retains no images. A caller that takes ownership of an `AttentionSheet` must dispose it; controller/client-owned sheets are zeroed automatically on revocation and teardown.
- GitHub connector publication preserves exact file trees but may produce different commit IDs from local Git because connector authorship metadata differs; tree equality and an empty fetched diff are therefore the content-identity gate.

## Personal Round Judgments

J1–J10 are recorded in the active packet. They cover worker-only nonsemantic pixels, deterministic normalized defaults, PNG/size bounds, separate payload framing, exact-frame privacy correlation, manual response fencing, provisional watchdog thresholds, synthetic-only durable evidence, bounded coalesced event behavior, and exact-source/reset fencing. Every choice is reversible without changing identity, memory, privacy authority, or later-stage semantics.

## Review focus

- On the evidence descendant, repeat the exact 309-test Windows total and artifact generation without changing product behavior.
- Confirm PR #11's merge ref retains the candidate tree over unchanged accepted Task 5 `main`.
- Recheck the 44-path scope against accepted Task 5 and scan once more for Task 7+, API, semantic attention, conversation, ERPP implementation, personality, durable product images, and production data.

## Repository state

- Branch: `agent/task-06-regions-attention-sheets`.
- Accepted base: `a257d5a1d70d03a77f27582b9f0bbecd0194e67d`, tree `b3a84d9a0a5be6cc81b4f517ac1b1fdb8537a23b`.
- Corrected local implementation head: `018e99b1802673051c116df63e3e6bc4891f62cf`.
- Published equivalent head: `660d7d9c4ffc4592cf0c725c3686ce68735dab18`; exact shared tree `2876d61439087ecd41b4d967761c3ff447246a44`.
- Documentation/evidence descendant: pending this update and its fresh gate.
- Remote branch: `agent/task-06-regions-attention-sheets`; draft PR #11.
- Accepted remote `main`: unchanged at Task 5.

## Next safe task

Publish and gate the exact evidence descendant, then merge PR #11 and complete accepted-main reconciliation. Do not archive Task 6, mark Stage 4 accepted, or open Task 7 until those exact-tree steps pass.

## Credit status

No credit-related stop.
