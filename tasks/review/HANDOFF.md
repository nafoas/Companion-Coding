# Task Handoff

## Task

Task 6 — Regions and Attention Sheets, the final Stage 4 packet. Accepted and archived. The implementation merged through PR #11; the attributable resource correction and final evidence descendant passed both exact Windows event paths and merged through PR #13 with an expected-head fence. Stage 4 is complete.

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
- The exact evidence runs exposed test-harness assumptions. Legitimate non-adjacent Windows PID reuse is handled with PID-plus-start-time identities and old-identity absence checks without test-side async wait registrations. Restart coverage proves the fresh orientation event and fresh-epoch retained payload independently.
- Rejected a post-merge acceptance claim when same-head push run `33030508085` failed twice while PR run `33030534763` passed. Both failures showed exactly nine process-wide VSTest/.NET host handles above baseline, a measurement that cannot identify resources owned by `OutOfProcessCaptureWorker`.
- Replaced that heuristic with attributable evidence: the 24-restart test now retains non-owning observations of each exact client process and named-pipe safe handle, proves the old pair closes on every epoch change, and proves the final pair closes with no owned resources after stop. Fresh child identity, bounded worker metrics, no second runtime, and no surviving child assertions remain.
- Added neutral WPF controls for applying/clearing a normalized manual region and displaying only privacy-safe sheet metadata. No final character wording, artwork, semantic output, or image display was added.
- Added 33 capture-worker and 7 target-authorization tests over the accepted Task 5 baseline, including strict payload negatives, geometry/labels, manual barriers, privacy correlation, watchdog behavior, ownership cleanup, deterministic artifact decoding, and a 216,000-frame accelerated six-hour visual soak.

## Changed

- 44 paths relative to accepted Task 5 `main`: 4,544 additions and 190 deletions on the observer-neutral evidence descendant.
- Capture Contracts, Client, Worker, Fake, Target Authorization controller, and the minimal neutral App controls.
- Capture Worker and Target Authorization tests.
- CI synthetic-artifact upload plus Task/README/Ledger/Handoff control records.
- The active correction changes only Capture Client internal resource observation, the Capture Worker restart regression, and bounded Task/README/Ledger/Handoff records relative to implementation `main`.
- No package, lock, solution, workflow, memory, runtime, semantic/API, conversation, ERPP implementation, personality, production-root, durable product-image, or product behavior path changed in the correction.

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
- Publication and equality: local correction head `018e99b1802673051c116df63e3e6bc4891f62cf` and published head `660d7d9c4ffc4592cf0c725c3686ce68735dab18` share exact tree `2876d61439087ecd41b4d967761c3ff447246a44`; an independent fetch produced an empty diff. PR #11 originally targeted the unchanged accepted Task 5 `main`.
- Corrected Windows gate: run `33027192357`, job `98371213540`; locked restore, dependency audit, strict Release build, and 309/309 tests passed — App Integration 13, Capture 14, Capture Worker 61, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 64.
- Windows evidence: test-results artifact `9628886868`, 67,006 bytes, archive digest `sha256:be57fa82e9bf07082417d931c4f7baee12be88156741c756c54c2456d888b388`; attention-sheet artifact `9628887307`, 619,254 bytes, archive digest `sha256:f23f23afa38591f8378a645c0c829c5b677f240f2f0d7248e6dc14b75f578e43`. Downloaded archive hashes matched GitHub, TRX totals were 309 executed/309 passed/0 failed/0 skipped, and the extracted PNG retained digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- Final implementation evidence: head `1e41ecfb69abbecb0c90c0215616470c864e4525`, tree `77590a151ef87b3725472cde3892ba4b6ec53754`, Windows run `33029731700`, job `98379224949`, passed restore, clean audit, all 20 Release builds with 0 warnings/errors, and 309/309 tests. PR #11 squash-merged it with an expected-head fence as `8d14fe945871ce1f92dde987087147befa4a60b2`; fetched `main` retained the exact tree.
- Blocked acceptance evidence: PR #12 exact-head run `33030534763` passed; same-head push run `33030508085` failed the host-global handle heuristic twice at 504→513 and 434→443. PR #12 was documented and closed without merge.
- Active correction local evidence: locked restore remained unchanged; all 20 serialized Release projects passed with warnings-as-errors at 0 warnings/0 errors; 296/296 locally executable tests passed (Capture 14, Capture Worker 61, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 64); `git diff --check` passed. The local vulnerability command was network-blocked, so no new local audit is claimed; the unchanged locked graph was clean in prior exact evidence and the Windows gate must audit it again.
- Active correction actual-diff review: six allowed paths only; the observation exposes booleans while keeping both safe handles private, cannot close/transfer a handle, and is callable only through existing friend assemblies. No product flow calls it. No behavior, workflow, package/lock, solution, API/network, semantic, conversation, ERPP implementation, personality, production-data, or durable-image surface changed.
- Published correction identity: local head `56767ccda7d315b966ba8db9434ec3f4a8b1a343`, PR #13 head `9ce4e592dc812a5740b49a033268386b8d7d8f13`, exact shared tree `aea3bd99744e07452a9db0412be6754928baa941`; independent fetched-tree comparison was empty.
- Exact correction gates: push run `33031810624`, successful rerun job `98386519846`, and PR run `33031854365`, job `98385948525`, each passed locked restore, the clean 20-project dependency audit, all 20 Release builds with 0 warnings/errors, and 309/309 tests. The first push attempt timed out only two legacy App-integration cases under runner saturation while Capture Worker passed 61/61; its exact-head rerun passed completely.
- Successful evidence artifacts were downloaded and hash-verified. Push: test results `9630630196` (`sha256:c21c09498058bcc5d31ba718b502a5c219ea17cf5e61c06c987bffd344205e2b`) and sheet `9630630604` (`sha256:58bf55eabd208127f35fc5a7d9c3a9d7b99a40d6b3f3123d75f87da70cacf55f`). PR: test results `9630555630` (`sha256:f09f7b109e5a73adc5a0e65388febb485655297924d5ce24f1b2689e25b91af1`) and sheet `9630556307` (`sha256:b26190678d4cbf881d00cb9244a1eb826a1c23826147edcda597b44c4d38f723`). Both TRX sets total 309 passed with no failures/errors/timeouts/skips; both PNGs retain the reviewed digest and 792×621 RGBA geometry.
- Final evidence identity and gates: head `64c5cfd09bc326cf9ef4ef8c706268c0fa971bbf`, tree `6bbb372c258ae854f7a2feb028f4faeb5a516fc6`; push run `33032555656`, job `98388150311`, and PR run `33032557593`, job `98388156701`, each passed locked restore, the clean audit, all 20 Release builds with 0 warnings/errors, 309/309 tests, and both artifact uploads on the first attempt.
- Final evidence artifacts were downloaded and hash-verified. Push: test results `9630823333` (`sha256:60decf87f8e4c9e4c42dbfa89ad2aef22f1ccb9c252f166d92459fa018dbbb78`) and sheet `9630823761` (`sha256:c6c84952b3d4733cc859379ead67d5bedd45efe686cc26690b2114a105a6c93f`). PR: test results `9630820661` (`sha256:3b82d0a32f03bdb2030f5184e4420b751792d15dac77d08a1edb60729733640e`) and sheet `9630821353` (`sha256:4973935183dfcac48010eeaea2a6a15cbaadf3a6dc7583c24352ef801427dc41`). Both TRX sets total 309 passed with no failures/errors/timeouts/skips; both PNGs retain digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`, 792×621 RGBA geometry, and the visually inspected synchronized labels.
- PR #13 merge-ref review proved exact parents, head, and tree. The PR was marked ready and squash-merged with expected head `64c5cfd09bc326cf9ef4ef8c706268c0fa971bbf` as `779ed4b0fab9cce8fdf978add388b6282010974a`; fetched remote `main` retained exact tree `6bbb372c258ae854f7a2feb028f4faeb5a516fc6`.
- Auxiliary formatter: `dotnet format --verify-no-changes` could not launch its Roslyn build-host pipe in this restricted Linux environment (`SocketException: Permission denied`). It is not a Task 6 Paw Gate command; strict compiler/analyzer and diff-whitespace gates passed.

## Remaining

- No Stage 4 implementation work remains. This docs-only acceptance reconciliation must pass its exact event gates and expected-head merge before the next packet is opened.
- Keep Task 7 unopened; any later work begins through its own bounded active packet.

## Risks and assumptions

- Linux cross-builds Windows projects but cannot launch `Microsoft.WindowsDesktop.App`; corrected Windows CI has now executed the real WPF and child-process branches.
- Minimized and exclusive-fullscreen WGC remain explicitly unsupported without actual target-PC evidence. CI cannot change that claim.
- The watchdog's growth thresholds are intentionally conservative and provisional until Stage 11 physical profiling; hard byte/count limits are not provisional.
- Product code retains no images. A caller that takes ownership of an `AttentionSheet` must dispose it; controller/client-owned sheets are zeroed automatically on revocation and teardown.
- GitHub connector publication preserves exact file trees but may produce different commit IDs from local Git because connector authorship metadata differs; tree equality and an empty fetched diff are therefore the content-identity gate.

## Personal Round Judgments

J1–J11 are recorded in the archived packet. J11 replaces a whole-testhost resource heuristic with direct observation of the process and pipe handles the capture client actually owns. Every choice is reversible without changing identity, memory, privacy authority, or later-stage semantics.

## Review focus

- Confirm this reconciliation changes only the three standing control records plus the Task 6 active-to-archive rename/content update.
- Require both GitHub event paths to execute 309/309 and generate both artifacts on the exact reconciliation head.
- Confirm `tasks/active/` is empty, Task 7 is absent, and remote `main` retains the exact accepted tree after the expected-head merge.

## Repository state

- Stage 4 acceptance branch: `agent/task-06-stage4-acceptance-final`, a docs-only descendant of accepted product/evidence `main`.
- Accepted base: `a257d5a1d70d03a77f27582b9f0bbecd0194e67d`, tree `b3a84d9a0a5be6cc81b4f517ac1b1fdb8537a23b`.
- Corrected local implementation head: `018e99b1802673051c116df63e3e6bc4891f62cf`.
- Published equivalent head: `660d7d9c4ffc4592cf0c725c3686ce68735dab18`; exact shared tree `2876d61439087ecd41b4d967761c3ff447246a44`.
- Final implementation head/tree: `1e41ecfb69abbecb0c90c0215616470c864e4525` / `77590a151ef87b3725472cde3892ba4b6ec53754`.
- Implementation merge on remote `main`: `8d14fe945871ce1f92dde987087147befa4a60b2`, exact tree retained.
- Premature acceptance PR #12: closed, unmerged.
- Final correction/evidence merge: PR #13 as `779ed4b0fab9cce8fdf978add388b6282010974a`, exact tree `6bbb372c258ae854f7a2feb028f4faeb5a516fc6`.
- Task 6 packet: archived. Active-task count: zero. Task 7: unopened.

## Next safe task

Gate and merge this docs-only Stage 4 acceptance reconciliation, then verify exact remote `main`. Do not open Task 7 within this packet.

## Credit status

No credit-related stop.
