# Build Ledger

| Field | Current value |
|---|---|
| Current stage | **Stage 12 complete: Boss gave the Core Bun Approval Hop on 2026-10-12** after the ten-check milestone hand-run passed on his PC with live calls on (Baldi's Basics Classic Remastered). The hand-run found and fixed FIX-05 to FIX-10. Next: Boss's memory-quality changes, then Stage 13, personality installation on Builder Prince |
| Active task | None. M1, M1b, M1c, M2, M3a, M3b, and M4a passed their Paw Gates; next is M4b (the Bnuy lookup) of Boss's approved pre-Stage 13 plan (`tasks/proposed/pre-stage-13-changes.md`) |
| Working branch | Accepted product/evidence baseline on `main`: `7a61caf364e0570e3e585bd1e2ea5bab740691ad` (FIX-10) |
| Entry criteria met | Complete; Tasks 4–11, ERPP-01, KEEP-01, KEEP-02, WIRE-01, WIRE-02, AUDIT-01, R2, R3, and R5 each passed their Paw Gates and were reconciled to accepted `main` |
| Product code authorized | Stage 12 authorized by Boss on 2026-10-11 ("Let's try Stage 12") |
| Live API authorized | Stage 12 authorized. Live calls run only on Boss's PC, with his own key and budget, after LIVE-02. No live call has been made from the build environment, and no real credential exists in it |
| Automated tests | M4a head `f0bae76ac52f1a4d49a614bdcbae8b6a05462b80` passed push run `38452021615` and PR run `38452051331`; the local gate passed 1394 tests across 21 runnable suites on Linux with a 32/32 mutation pass. Before it, M3b head `a842f61e1db93bf0a3578a6cf7a98739cf732d40` passed push run `38443387741` and PR run `38443416630`; the local gate passed 1349 tests across 21 runnable suites on Linux with a 16/16 mutation pass and a 200-history property test. Before it, M3a head `5be865e47e9c76c20ce679c5e33bf83b0a131883` passed push run `38416238772` and PR run `38416276596`; the local gate passed 1333 tests across 21 runnable suites on Linux with a 22/22 mutation pass. Before it, M2 head `987b037d08bdbcce38d73e2a8e59cf961699cfad` passed push run `38359441865` and PR run `38359446379`; the local gate passed 1296 tests across 20 runnable suites on Linux with a 29/29 mutation pass and a 600-situation curtain audit. Before it, M1c head `5d4fffa5c4f82bfd58ccf5084ead5a243818af36` passed push run `38333524867` and PR run `38333539992`; the local gate passed every runnable suite on Linux with a 15/15 mutation pass. Before it, M1b head `5a4725c2e8257d9594ccedc34de1916cc82b7f77` passed push run `38329380845` and PR run `38329395547`; the local gate passed 1193/1193 on Linux with an 11/11 mutation pass. Before it, M1 head `5fdc8168b4d0dc6294e434dde2fd4ae2353f8cba` passed push run `38317387395` and PR run `38317409457`; the local gate passed 1185/1185 on Linux with an 11/11 mutation pass. Before it, ATT-01 head `763d033381a555dd6191856a0a1f43dcbe45900a` passed push run `38192662480` and PR run `38192674877` at 1151/1151 with verified artifacts; the local gate passed on Linux. Before it, VIS-04 head `353441a4396a51159cd4dd9cf3e9014168b077ea` passed push run `38174260185` and PR run `38174274555` at 1146/1146 with verified artifacts; the real SigLIP model loaded on CI (CPU) and matched the Python reference embedding at cosine 1.00000; the local gate passed on Linux. Before it, FIX-03 head `11c7d35` passed runs `38171459446` and `38171468673` at 1123/1123. Before it, VIS-03 head `aeacff6049daa079cbf1424139a8756f19322fc4` passed push run `38170316427` and PR run `38170329276` at 1123/1123 with verified artifacts; the real Windows OCR engine was available on CI and read a blank crop as no lines in about 300 ms (cold); the local gate passed on Linux. Before it, VIS-02 head `d6a20c51c418d8be0869bb044b1cbfd513b66195` passed push run `38167891536` and PR run `38167905535` at 1102/1102 with verified artifacts; the local gate passed 1084/1084 on Linux. Before it, VIS-01 head `2c5baecee576e86ecb9351cdb7bcc922d24043d9` passed push run `38161132869` and PR run `38161148925` at 1076/1076 with verified artifacts, including the Windows real-process sniff latency test (p95 ≤ 100 ms); the local gate passed 1058/1058 on Linux. Before it, FIX-02 head `012e006fbe3f55cb9a976e7d0093ca9ca9725e9b` passed push run `38144399481` and PR run `38144410265` at 1044/1044 with verified artifacts; the local gate passed 1026/1026 on Linux. Before it, CORE-12 head `0d8b65b21247cbda05ca1b5802b9a1409f782709`, tree `c98917910594f88c69159f570d8870dd11a09848`, passed push run `38142489617` and PR run `38142501429` at 1044/1044 with verified artifacts. The local gate passed 1026/1026 on Linux. |
| Manual gate | **Core Bun Approval Hop given (Boss, 2026-10-12).** All ten milestone checks passed on Boss's PC; see the entry "Core Bun Approval Hop" below. |
| Accepted `main` baseline | `a8519ebc437de09fb956f937da7f8a5d5b1b5dfd` — M4a remembering like a Bun (PR #100). Before it: `f8447e428c2b64520777a044560c2bb85a879c00` — M3b Da Boss has got it covered (PR #97). Before it: `816bfeece03724f1dcc1940d4ed0c90696e45c00` — M3a saw dis a few times (PR #95). Before it: `e272da26332351f9b3af7b2cc0268dd253ba01fc` — M2 the curtain (PR #92). Before it: `c6013390299aaf12d360f20d28e78dd5a1dccf62` — M1c a Living Bun (PR #89). Before it: `96250752290e340f0d6ed60e4017e0cee28cd5aa` — M1b BrainCase separation (PR #87). Before it: `0202c7fd32390e73af69faf79a8410def91f2c24` — M1 Two homes (PR #85), on top of the pre-Stage 13 plan (PR #84). Before it: `7a61caf364e0570e3e585bd1e2ea5bab740691ad` — FIX-10 Reply target (PR #82), on top of FIX-09 (PR #81), FIX-08 (PR #80), FIX-07 (PR #79), FIX-06 (PR #78), FIX-05 (PR #77), FIX-04 (PR #76), and ATT-01 (PR #74) |
| Known limitations | Minimized and exclusive-fullscreen WGC remain unsupported absent actual target-PC evidence. The watchdog thresholds, orientation budgets, Task 7 bridge bounds, App startup bound, and every attention, conversation, and transcript number are the accepted agreed defaults (Boss, 2026-10-11), adjusted only on evidence. The synthetic capture source cannot emit `SourceResized`. The subsystems are composed by `CompanionOrchestrator` (WIRE-01) and run in the real App with Windows platform signals and neutral notice presentation (WIRE-02). CORE-12 added plain placeholder controls for the camera, quiet check, and Vault repair; final controls belong to the presentation phase (WIRE-02 D1). Real lock and suspend need target-PC verification (WIRE-02 D4). Photographs come from the native frame (DEF-02) and are saved under the provisional 1280 edge; long sessions are consolidated completely in parts (DEF-01). Recall scoring weights and budgets, and Watchbun quiet thresholds, task bounds, and alert rates, are accepted defaults, tuned from play; Consolidation idempotency is provided by the orchestrator's durable per-session intent queue (Task 10 J3, WIRE-01 J7). |
| Deferred temptations | A public backup/repair command (KEEP-02 D1; the single-file export exists since DEF-01); personality work; live API and credentials (Task 12); durable images, production settings, final UI |
| Approval | Boss transferred direct construction to Claude with full authority on 2026-10-10, directed reliable orientation delivery with a retake failsafe, directed the R4 cleanup, and directed autonomous progression from Task 7 under the Paw Gate model, halting only for serious issues or real credentials.. On 2026-10-11 Boss decided the values agreed in the design conversation are the accepted calibration, changed up or down only if real use shows a need. Boss also decided that Prince is one unbroken line of continuous Bun: nothing ever restores, rolls back, or deletes his memories on its own; repair is opt-in only and may only continue his current line; older exports are never imported. |

## Gate history

### Task 0 — Architecture proposal

- Builder SHA reviewed: `9a51231aa8d76a399ef43ae4a8bfc5cdbd1195b3`
- Foreman approval review: `4892489052`
- Merge: PR #1 squash-merged to `main` as `58bbdf9915659b3887e311f7b9cdf819cc39fc13`.
- Result: passed as a documentation checkpoint.

### Historical Task 1 — Claude checkpoint

- Work reached closed PR #3 at head `da4797e1a3df2c6f0ddaaa0248098fd40f656121`.
- The implementation checkpoint and its passing tests were not merged.
- On 2026-08-09 the user withdrew authorization for the Claude–ChatGPT collaborative workflow and requested a complete approach reset.
- The task was preserved at `tasks/paused/task-01-skeleton.md` until task R4 retired it; it remains in Git history, last present at `a7e68e165ba518c391f8c5e77f2eb3bf95ba3645`.
- Result: paused and superseded; not accepted.

### Task 1 — Direct neutral skeleton adoption

- Candidate: draft PR #5 at reviewed head `6c50b9d73607dcd70a4e0b931f0b8bceaffe59da`, exact tree `92ab14ced552624fb41b07170d35b2365ecf0565`.
- Scope: 65 allowlisted paths — 60 adopted product/build/test files, four control records, and the Boss-authorized Roadmap Stage 1 correction.
- Review: current-base diff whitespace, one-active-task state, changed-path allowlist, exact local/remote tree equality, lifecycle and process boundaries, test-mode confinement, and forbidden Task 2+ surfaces all passed.
- CI: run `31354524133`, job `93351605021`; locked restore passed, Release build passed with 0 warnings and 0 errors, and 60/60 tests passed (Runtime 26, Presentation 19, synthetic Capture 11, real WPF integration 4).
- D1: the Boss explicitly approved keeping Task 1 neutral and moving durable journal/checkpoint work to Task 2; only the stale Stage 1 roadmap wording was amended.
- Result: passed; acceptance records may be published and PR #5 merged before Task 2 becomes active.

### Task 2 — Append-only memory and journal

- Candidate: draft PR #6 at reviewed gate head `056dedceb120e48c01b9c71d9a1f2d31ad207a5d`, exact tree `ab010b6f5a0d17ec23f84ef0252332143421e427`; implementation head `f3e11acb08a2056f0fe557b4517383a14471227c`.
- Scope: 49 allowlisted paths — one neutral memory source project, one synthetic test project, solution/CI integration, bounded control records, and the Boss-approved Roadmap Stage 2 split.
- Review: current-base whitespace, one-active-task state, changed-path allowlist, exact local/remote tree equality, public write authority, journal → SQLite → checkpoint ordering, cancellation fence, replay/idempotency, unresolved-tail handling, root isolation, and forbidden later-stage surfaces all passed.
- Dependencies: both locks resolve all SQLitePCLRaw components to 2.1.12; the permanent direct/transitive vulnerability audit reports all 11 projects and no vulnerable entries.
- CI: run `31360021794`, job `93366932942`; locked restore and audit passed, Release build passed with 0 warnings and 0 errors, and 94/94 tests passed (Runtime 26, Presentation 19, synthetic Capture 11, real WPF integration 4, Memory/Journal 34).
- Personal Round Judgments J1–J8 record the tree-identical transport base, dependency pin, canonical envelope, fixed roots, pre-durability frame bound, checkpoint/store cross-check, duplicate-key rejection, and unresolved-live-tail recovery fence.
- Merge: PR #6 squash-merged to `main` as `44caa2fc6474b0952eaed5f086bfb3c49bf73c18`.
- Result: passed and merged; Task 3 may become active through its own bounded packet.

### Task 3 — Atomic backup and repair

- Code checkpoint rebased onto accepted remote `main`: feature commit `cbc0b5c91c679a693e732b105acd09268d1c7f5c` plus evidence-retention correction `d64d5b7e071ff6ba43b434d4635525fa8ecaeeac`; resulting tree `e8bd0811f2b6ccd93892a7eec97778fa9e9fcaca`.
- Scope: 30 memory source/test files; 4,461 additions and 33 deletions after the Windows-only test-harness correction. No project, dependency, lock, CI, app, capture, API, presentation, personality, or production-root file changed.
- Protocol: pinned exact SQLite cut, online backup, independent full health validation, canonical/checksummed archive, atomic promotion, post-promotion covered-prefix rotation, exclusive repair authority, immutable damaged-source evidence, marker-guarded rollback, and ordinary idempotent post-cut replay.
- Local verification: full locked restore passed; full Release solution cross-build passed with 0 warnings/errors; 121/121 locally runnable cases passed. This covers all 90 accepted non-Windows regressions plus 31 Task 3 cases.
- Windows correction: initial run `31426116921` passed restore/audit/build but exposed one test-only symmetric file-sharing mismatch. Production sharing remained strict; read-only test inspection was made Windows-compatible and the complete gate was rerun.
- Exact Windows evidence: run `31426524602`, job `93579415434`; locked restore and clean direct/transitive audit passed, Release build passed with 0 warnings/errors, and 125/125 tests passed (Runtime 26, Presentation 19, Capture 11, WPF integration 4, Memory 65). Artifact `9077431137`, digest `sha256:12d71a91407ba3855173c445916fccff3c0635c98b0175bf2351a89bed473583`.
- Review: published head `d4d7bddb35ae1a2b8f3b2fdb47e28d74322ef83a`, exact tree `324874f5b7c2d274732ff82c91aa9631fa587b57`; current-base path/scope, authority, atomicity, cancellation, cleanup, evidence, recovery, privacy, and forbidden-surface checks passed.
- Merge: PR #7 squash-merged to `main` as `f685dd2023a5844309c5b5fb7d0abd1bf54406b9`.
- Post-merge recheck: fresh locked restore passed; all 11 Release projects built with 0 warnings/errors; 121/121 locally runnable tests passed; accepted Windows run `31426920584` passed dependency audit and 125/125 tests including four WPF process tests.
- Result: passed, merged, and accepted; Task 4 may become active through its own bounded packet.

### Task 4 — Consent and target isolation

- Candidate: draft PR #9 at reviewed implementation head `c68b673e72b5017d7f838b3cc0ab1f020d5d1f0b`, exact tree `c83a4fe56b7eea21c4e9072bd126af2e02b3b2a6`, based directly on accepted `main` `f685dd2023a5844309c5b5fb7d0abd1bf54406b9`.
- Scope: 113 final changed paths; platform-neutral privacy and target-authorization cores, minimal Windows metadata/display/hotkey adapters, sealed synthetic capture grants, live-write and frame generation fences, neutral WPF control/status wiring, synthetic tests, solution/lock integration, and bounded control records. No Task 5 real capture or later-stage surface was added.
- Local verification: locked restore and all 16 Release builds passed with 0 warnings/errors; the pre-review candidate passed 226/226 locally runnable tests. The refreshed environment lost the pinned SDK after two race-hardening corrections, so no post-hardening local result is claimed.
- Exact Windows evidence: run `31449205060`, job `93649850080`; locked restore and the direct/transitive dependency audit passed, all 16 Release projects built with 0 warnings/errors, and 240/240 tests passed (Runtime 26, Capture 14, Memory 68, Presentation 50, Privacy 13, TargetAuth 57, App Integration 12). Artifact `9085707445`, 51,456 bytes, digest `sha256:d496d965d1e975ad36445c50f9e4ea03a561e2fb194d3b697a5ca106ed4ff8ea`.
- Review: current-base whitespace, one-active-task state, 113-path scope allowlist, local/remote tree equality, consent-before-start, one-target authority, generation/target/frame admission, privacy stop ordering, live-write drainage, policy corruption/failure, monitor/hotkey failure, metadata minimization, and forbidden Task 5+/API/personality/production surfaces all passed.
- Corrections: actual-diff review added synchronous revocation for caller cancellation during a misbehaving worker start and serialized active-executable denial through the controller. Both received focused adversarial tests without weakening existing invariants.
- Personal Round Judgments J1–J8 record neutral/native separation, title-free descriptors, shared generation fencing, path-free policy identity, no-target resume, repeated-stop fencing, worker-start cancellation, and serialized live denial.
- Final descendant: ERPP-01 was recorded as a future Task 9 companion gate without product-code changes. Windows run `31470938172`, job `93713889217`, again passed locked restore, clean audit, all 16 Release builds with 0 warnings/errors, and 240/240 tests. Artifact `9093336297`, 50,776 bytes, digest `sha256:e223671a492e9c025e9718c9f506ec4ddde556e7292ffa9e411a4200426a952a`.
- Merge: PR #9 squash-merged to `main` as `b0cbc37604519ef587b3dbce8f1c589ea561b268`; the accepted merge tree `4e0ae5bd7ca6961ce542611758ebf71174dfa0b0` exactly matched the final branch tree.
- Result: passed, merged, and accepted; Task 5 may become active through its own bounded packet.

### Task 5 — Bounded capture worker

- Candidate: draft PR #10 at implementation head `a274001d4c40ce003fcbd0087b70c103025b4b23`, exact tree `08b70eeb4a41147eb2f5f271fd6dc876f3c79ae0`, based directly on accepted `main` `b0cbc37604519ef587b3dbce8f1c589ea561b268`.
- Scope: 57 final changed paths; protocol/metrics contract extensions, dedicated client and exact-HWND WGC worker, normal app composition, generated-buffer and process tests, private-safe WGC spike, locks/solution wiring, and bounded control records. No Task 6 visual composition, API/conversation/ERPP implementation, personality, durable image, or production-data surface was added.
- Local verification: locked restore and all 20 Release builds passed with 0 warnings/errors; direct/transitive audit found 0 vulnerable packages; 256/256 locally runnable tests passed, including 28 worker tests and the 216,000-frame accelerated soak.
- Exact Windows evidence: run `31477853767`, job `93735671804`; locked restore and audit passed, all 20 Release projects built with 0 warnings/errors, and 269/269 tests passed (App Integration 13, Capture Worker 28, Capture 14, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 57). Artifact `9095997685`, 57,051 bytes, digest `sha256:14913ff10570e7ee632e5e4e71256a9571a0960e8d1fb40b0c09d20ae34613c2`.
- Process evidence: TRX durations prove Windows-only child cases executed, including twelve fresh process restarts/cleanup/handle checks (6.35 s), exact metadata/stop, unexpected crash recovery, and blocking-observer control isolation.
- Review corrections: disposed an acquired WGC frame on concurrent stop; made terminal status synchronously close client admission; made malformed/current-target-mismatched/duplicate/out-of-order frame IPC tear down the child; unified the 256-bit nonce length; and removed product-assembly friendship to sealed grant issuance.
- Personal Round Judgments J1–J8 record authority narrowing, IPC/epoch design, newest-preserving bounds, raw-byte accounting, revocation ordering, honest silence, nonblocking observers, and private-safe soak/spike evidence.
- Merge: PR #10 squash-merged to `main` as `a257d5a1d70d03a77f27582b9f0bbecd0194e67d`; accepted tree `b3a84d9a0a5be6cc81b4f517ac1b1fdb8537a23b` retained the gated behavior and evidence records.
- Result: passed, merged, and accepted; Task 6 became the final Stage 4 packet. Actual target-PC minimized/exclusive WGC feasibility remains unsupported deferred evidence, not a Task 5 defect.

### Task 6 — Regions and attention sheets

- Corrected candidate: local head `018e99b1802673051c116df63e3e6bc4891f62cf`, published equivalent `660d7d9c4ffc4592cf0c725c3686ce68735dab18`, exact shared tree `2876d61439087ecd41b4d967761c3ff447246a44`, based directly on accepted Task 5 `main` `a257d5a1d70d03a77f27582b9f0bbecd0194e67d`; draft PR #11.
- Scope: the observer-neutral evidence descendant has 44 changed paths and 4,544 additions/190 deletions; strict capture contracts/IPC, worker-local readback/change/geometry/composition/encoding/watchdog logic, bounded client reception, Task 4 privacy correlation, neutral manual-region controls, focused tests, synthetic artifact upload, and bounded control records. No package, lock, solution, memory, runtime, semantic/API, conversation, ERPP implementation, personality, or production-data surface changed.
- Behavior: one restart/resize-scoped orientation; normalized deterministic defaults and current-grant manual override; local luminance duplicate rejection; one-moment full context plus at most two labeled crops; deterministic lossless PNG; separate 8 MiB checksummed payload under an unchanged 64 KiB JSON ceiling; at most two sheets per worker/client owner; exact target/generation/source correlation through Privacy Guard; explicit 64 MiB visual accounting and a provisional sustained-growth watchdog.
- Local verification: pinned .NET SDK 10.0.302 locked restore passed (serialized project graph with `EnableWindowsTargeting=true`); all 20 Release projects built with 0 warnings/errors; the direct/transitive audit examined all 20 projects with 0 vulnerable findings; 296/296 locally executable invocations passed (Capture 14, Capture Worker 61, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 64). The capture suite includes a 216,000-frame/six-hour accelerated visual soak.
- Synthetic inspection: generated test-only PNG decoded structurally and was visually inspected at 792×621 RGBA with `FULL CONTEXT`, `CENTER`, and `LOWER / DIALOGUE` labels and synchronized crops; digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- Actual-diff corrections: restored sealed-grant assembly isolation; made payload ownership atomic and zeroing; bounded temporary WGC readback at 64 MiB; fenced resize/fault status ahead of new sheets; deferred start-time frames; coalesced newest sheet signals; held newer sheets until their own frame passed privacy; serialized manual-region response ordering; then rejected the initially green candidate until sheet dispatch followed source-frame dispatch, held payloads were disposed on resize/fault, exact source identity included timestamp/dimensions, and impossible BGRA geometry failed closed.
- Evidence-run correction: the exact docs-head runs exposed legitimate Windows PID reuse and a restart test that expected an older orientation from a newest-preserving queue. PID-plus-start-time identities and separate fresh-orientation/fresh-epoch evidence corrected those assumptions without changing product behavior.
- Corrected Windows evidence: run `33027192357`, job `98371213540`; locked restore, clean audit, all 20 Release builds, and 309/309 tests passed (App Integration 13, Capture 14, Capture Worker 61, Memory 68, Presentation 50, Privacy 13, Runtime 26, Target Authorization 64). Test-results artifact `9628886868` has digest `sha256:be57fa82e9bf07082417d931c4f7baee12be88156741c756c54c2456d888b388`; attention-sheet artifact `9628887307` has digest `sha256:f23f23afa38591f8378a645c0c829c5b677f240f2f0d7248e6dc14b75f578e43`.
- Final implementation evidence and merge: exact head `1e41ecfb69abbecb0c90c0215616470c864e4525`, tree `77590a151ef87b3725472cde3892ba4b6ec53754`, passed Windows run `33029731700` at 309/309 and merged through PR #11 as `8d14fe945871ce1f92dde987087147befa4a60b2` with an expected-head fence and unchanged tree.
- Acceptance correction: PR #12's exact-head run `33030534763` passed, but same-head push run `33030508085` failed twice because the process-wide VSTest/.NET host count rose 504→513 and 434→443. That heuristic cannot identify capture-client ownership. PR #12 was closed; the replacement directly retains and verifies closure of every client-owned process and named-pipe safe handle across 24 restarts and final stop while preserving fresh-child, bounded-metric, no-second-runtime, and no-live-child assertions.
- Resource correction evidence: local head `56767ccda7d315b966ba8db9434ec3f4a8b1a343`, published PR #13 head `9ce4e592dc812a5740b49a033268386b8d7d8f13`, and exact shared tree `aea3bd99744e07452a9db0412be6754928baa941`. Both event paths passed: push run `33031810624` on successful rerun job `98386519846`, and PR run `33031854365` on job `98385948525`. Each audited all 20 projects, built with 0 warnings/errors, and passed 309/309 tests. The first push attempt had two unrelated legacy App-integration timeouts under runner saturation while the corrected Capture Worker suite passed 61/61; the exact-head rerun passed completely.
- Artifacts: successful push test results `9630630196` (`sha256:c21c09498058bcc5d31ba718b502a5c219ea17cf5e61c06c987bffd344205e2b`) and sheet `9630630604` (`sha256:58bf55eabd208127f35fc5a7d9c3a9d7b99a40d6b3f3123d75f87da70cacf55f`); PR test results `9630555630` (`sha256:f09f7b109e5a73adc5a0e65388febb485655297924d5ce24f1b2689e25b91af1`) and sheet `9630556307` (`sha256:b26190678d4cbf881d00cb9244a1eb826a1c23826147edcda597b44c4d38f723`). Both TRX sets total 309 executed/passed with zero failures, errors, timeouts, or skips; both extracted PNGs retain `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd` and the visually reviewed 792×621 RGBA layout.
- Final evidence descendant: exact head `64c5cfd09bc326cf9ef4ef8c706268c0fa971bbf`, tree `6bbb372c258ae854f7a2feb028f4faeb5a516fc6`, passed push run `33032555656` (job `98388150311`) and PR run `33032557593` (job `98388156701`) on their first attempts. Both produced 309/309 TRX totals with zero failures/errors/timeouts/skips; the direct-owned-resource restart test passed in 12.61 s and 13.56 s respectively.
- Final artifacts: push test results `9630823333` (`sha256:60decf87f8e4c9e4c42dbfa89ad2aef22f1ccb9c252f166d92459fa018dbbb78`) and sheet `9630823761` (`sha256:c6c84952b3d4733cc859379ead67d5bedd45efe686cc26690b2114a105a6c93f`); PR test results `9630820661` (`sha256:3b82d0a32f03bdb2030f5184e4420b751792d15dac77d08a1edb60729733640e`) and sheet `9630821353` (`sha256:4973935183dfcac48010eeaea2a6a15cbaadf3a6dc7583c24352ef801427dc41`). Downloaded hashes matched GitHub. Both PNGs retained the reviewed digest, geometry, format, and synchronized labels.
- Merge: PR #13's test merge ref had exact parents `8d14fe945871ce1f92dde987087147befa4a60b2` and `64c5cfd09bc326cf9ef4ef8c706268c0fa971bbf` and exact tree `6bbb372c258ae854f7a2feb028f4faeb5a516fc6`. It was marked ready and squash-merged with an expected-head fence as `779ed4b0fab9cce8fdf978add388b6282010974a`; fetched remote `main` retained the exact tree.
- Result: passed, merged, archived, and accepted. Stage 4 is complete; no task is active and Task 7 remains unopened.

### VIS-03 — The Text Sniff: noticing new words on screen (Vision upgrade, part 3)

- **Boss, 2026-10-11:** the on-PC vision AI is "just part of the app, not a separate download". The Design BunDex lists "new text appeared or disappeared" and "dialogue/subtitle areas changed" as local triggers.
- **Reading.**
  - Windows' built-in OCR (`Windows.Media.Ocr`) reads the dialogue band and the upper corners.
  - A region is read when a grid cell over it moved by at least 0.02. That includes near-duplicate subtitle swaps the pixel Sniff rightly ignores. A region is also read when it has gone 2 s unread.
  - Each region is read at most once per 500 ms, one read at a time, off the frame path.
  - Where no OCR language is installed, the recognizer reports unavailable and nothing else changes.
- **Privacy (J1).**
  - The words never leave the capture worker. Only "the words appeared, changed, or vanished" goes out, with line and character counts (protocol v6 `TextSniffProduced`).
  - A per-region digest is kept in worker RAM and zeroed on reset.
  - Text Sniffs are strictly shape-checked, strictly sequenced, and pass the same privacy admission as sniffs and sheets.
- **Prince's reaction (`TextReflex`).**
  - New words are trusted evidence, capped below Bnuy Mode, and bring the next Glance forward at once.
  - Each region habituates; a ticking HUD stops asking for Glances.
  - Vanished words are weaker evidence and ask for no look.
- **Evidence.**
  - Head `aeacff6`: runs `38170316427` and `38170329276` passed 1123/1123. The CI runner's OCR engine was available and read a blank crop as no lines in about 300 ms (cold, background).
  - Mutation pass: 34 mutants, 33 killed, 1 defense-in-depth survivor.
- **Merge.** PR #69 squash-merged as `86deb5f0e2e54c8b516567e5d80a1d2c65aed627`.
- **Found.** FIX-03: a pre-existing race in the platform exit test, which failed once on a WIP run.
- **Result.** Passed and accepted.

### VIS-02 — Glance (peripheral vision) and Inspect (focus) (Vision upgrade, part 2)

- **Boss, 2026-10-11:** "an efficient Haiku handler … a Glance is sorta like his peripheral vision, as long as it doesn't overwrite his main focus (only add to it) … Don't make him scatterbrained, but equally don't give him tunnel vision".
- **Two lanes in one Braincase.**
  - **The Glance** (`claude-haiku-5-5`, thinking off, low effort) is one short attempt with a strict schema that carries no memory proposals. It shares the privacy fence, the nap, and one dollars-per-day Bun Budget, each lane at its own model's prices.
  - **The Inspect** stays the main focus and the only source of memories.
  - The journal gains a `Glanced` entry; journals without Glances keep their exact older shape.
- **Looking.**
  - Routine looks are Glances: at once on a sniff trigger, otherwise at the Glance's own recommendation, clamped to 2–30 s.
  - A Glance adds attention below Bnuy Mode and keeps running during an Inspect.
  - An escalation, or interest ≥ 0.70, queues exactly one Inspect.
  - Bnuy Mode keeps its focused Inspects at the event floor.
  - The Glance is on in the App when live calls and a Glance model are set; `glanceModel: ""` turns it off.
- **Measurement.** `BraincaseCheck --live` times five Glances on Boss's PC. Glance latency is unmeasured until then.
- **Evidence.**
  - Head `d6a20c5`: runs `38167891536` and `38167905535` passed 1102/1102.
  - Mutation pass: 31 mutants, 30 killed, 1 defense-in-depth survivor.
- **Merge.** PR #67 squash-merged as `da7ea8293c0fce7f46fe3aa86b45af4ea2429c9c`.
- **Result.** Passed and accepted.

### VIS-01 — The Sniff: fast local noticing within 100 ms (Vision upgrade, part 1)

- **Boss, 2026-10-11:** "see, notice, and respond, without an immersion killing delay … Glance ideally needs to be 100 ms or less … allocate as much RAM is needed". The on-PC vision AI is "just part of the app". A Glance is peripheral vision that adds to his focus and never overwrites it.
- **Why local.** No cloud model can answer in 100 ms, so the ≤100 ms tier runs on Boss's PC. The vision plan has three speeds:
  - Sniff: local (this packet);
  - Glance: Haiku (VIS-02);
  - Inspect: the detailed model (VIS-02).

  Text reading (VIS-03) and the bundled scene model (VIS-04) follow.
- **Built.**
  - **Per-frame sniff.** Every changed frame is read in the worker before any sheet work, over an 8×6 cell grid, and holds no pixels.
  - **Attention-aware cadence.** Noticing looks 4 times a second, Engaged about 7, and Bnuy Mode 10. A sniff trigger gives an immediate sheet aimed at the hot region and brings the next look forward.
  - **Protocol v5.** Sniffs pass the same privacy admission as sheets.
  - **The reflex lane.** Attention moves at once, even while the mailbox is busy. Per-cell habituation, plus a score ceiling below Bnuy Mode, keep the reflex calm.
- **RAM.** Budgets go from 64 MB to 512 MB and 256 MB. 4K frames were previously dropped, so Prince saw nothing on a 4K monitor. A bounded, zeroing pixel pool removes collector stalls: a 4K sniff fell from about 180–250 ms to about 10–30 ms.
- **Evidence.**
  - Head `2c5baec`: runs `38161132869` and `38161148925` passed 1076/1076. That includes the Windows real-process sniff latency test (p95 ≤ 100 ms, max ≤ 250 ms).
  - Mutation pass: 33 mutants, 31 killed, 2 defense-in-depth survivors.
- **Merge.** PR #65 squash-merged as `2defb17a1ccda22cac3318098faf6890ab178d56`.
- **Result.** Passed and accepted.

### M4a — Remembering like a Bun: four layers, two clocks, recall that refreshes

- **What.** This is the first part of M4 (plan items 3, 4, 7h, and 7i); the lookup is M4b and the adventure lifecycle is M4c.
  - **Four layers with Boss's approved values.** Short is the next session or 2 days; medium is 5 sessions or 3 weeks; long is 10 sessions or 60 days, then always a bell; cherished is forever.
  - **Two clocks.** Each window lasts whichever of play sessions or days is longer, so time away never causes forgetting.
  - **Remembering or writing refreshes a page.** Every refresh is append-only.
  - **What comes to mind:** up to three clear pages and at most one "rings a bell" feeling with no details. Faded pages are never offered unprompted. A page that just came to mind rests 15 minutes. Ties follow the ladder, then recency.
- **Evidence.**
  - Head `f0bae76`.
  - Runs `38452021615` and `38452051331` were green.
  - 1394 tests passed.
  - Mutation pass: 32/32.
  - The merge-ref tree `89f70cc` matched.
- **Merge.** PR #100 squash-merged as `a8519ebc437de09fb956f937da7f8a5d5b1b5dfd`.
- **Result.** Passed and accepted.

### M3b — "Da Boss has got it covered": fear that eases, trust that only grows

- **Boss's decision (2026-10-13).** "Trust should never go down — only up... it should be unconditional." This supersedes the plan's "it goes both ways" (items 17 and 18), and the plan is updated.
- **What.**
  - **The eyes report encounters:** the threat being fought and how it is going.
  - **Trust in Da Boss only grows,** from his wins, tracked per game and per threat.
  - **Fear only eases,** with safe exposure.
  - **Trusted fights feel less pressing,** but never when the threat is dangerous right now; warnings are untouched.
  - **Prince feels all of this only as feelings,** through the membrane.
- **Evidence.**
  - Head `a842f61`.
  - Runs `38443387741` and `38443416630` were green.
  - 1349 tests passed.
  - The 200-history property test held.
  - Mutation pass: 16/16.
  - The merge-ref tree `c2d6f86` matched.
- **Merge.** PR #97 squash-merged as `f8447e428c2b64520777a044560c2bb85a879c00`.
- **Result.** Passed and accepted. M3 is complete.

### M3a — "Saw dis a few times": a complete record, novel memories, getting used to things

- **What.** This is the first half of M3 (plan items 1, 7a, and 12). M3 is split because fear and trust need the eyes to report threats and outcomes; that is M3b.
  - **The sighting record.** `Braincase\sightings-v1` is a complete, factual record. It is backed up in the Vault and restored only when missing.
  - **Impressions.** Repeats become one impression ("Prince saw this a few times"), never a count.
  - **The novelty gate.** It sits between the safety allowlist and the write gate: repeats, near-duplicates, and numbers-only changes stay in the record.
  - **Getting used to things.** Familiarity grows by thing and by kind, per game, and recovers over time. A new situation makes a thing interesting again. Danger has a floor, and urgency is untouched.
  - **The eyes' new fields.** The eyes now report each thing's kind and whether it looks dangerous.
- **Evidence.**
  - Head `5be865e`.
  - Runs `38416238772` and `38416276596` were green.
  - 1333 tests passed.
  - Mutation pass: 22/22.
  - Merge-ref tree `edb0775` matched.
- **Merge.** PR #95 squash-merged as `816bfeece03724f1dcc1940d4ed0c90696e45c00`.
- **Result.** Passed and accepted.

### M2 — The curtain: the intermediate layer between his subconscious and Da Bnuy

- **What.** Boss's intermediate layer (2026-10-13): "his sensory information still is there, but he is insulated from it. The intermediate layer is what turns it from raw data into 'Bnuy Saw' and 'Bnuy Remembers' and that's all he interprets."
  - A new `CompanionCore.Curtain` holds the membrane, the only maker of what Prince experiences.
  - The curtain rules: zero tolerance in framing; only self-claims in his world and his words.
  - The `BnuyRequest` slot (8a), the validator hook, and `BnuyVoice` (8c).
  - The BunDex keeper moved behind the curtain.
- **Evidence.** Head `987b037`:
  - runs `38359441865` and `38359446379` green;
  - 1296 tests passed;
  - 600-situation curtain audit;
  - mutation pass 29/29;
  - merge-ref tree `3fb1286` matched.
- **Merge.** PR #92 squash-merged as `e272da26332351f9b3af7b2cc0268dd253ba01fc`.
- **Result.** Passed and accepted.

### M1c — A Living Bun: his BunDex is entirely Bnuy-built

- **What.** This follows Boss's direction after the emulation: no quiet blueprint, so Prince chooses every place from his own knowledge.
  - Role-home registration was removed; the kind-of-memory tags are only the BrainCase's private reference.
  - Each moment carries what happened, where he is, the feeling, and what comes to mind (up to three of his own similar pages).
  - He can write a line about each new place.
  - The pull relaxes once he keeps one.
  - Tidying is only his: while writing, or when Boss invites it.
- **Emulation rerun.** No flips. He separated farm, scary-house, adventure, and note places himself, twice chose differently from what came to mind, and tidied on Boss's invitation.
- **Evidence.** Head `5d4fffa`: runs `38333524867` and `38333539992` green; mutation pass 15/15; merge-ref tree `85ec186` matched.
- **Merge.** PR #89 squash-merged as `c6013390299aaf12d360f20d28e78dd5a1dccf62`.
- **Result.** Passed and accepted.

### M1b — Finishing the separation: every machine store lives in the BrainCase

- **What.** Boss (2026-10-13): "go ahead and finish the separation between BunDex and Braincase". The data root now holds `BunDex\` and `Braincase\` (Memory, Keepsakes, State, Transcripts, bridge journal, BunDex ledger), beside Boss's `TargetAuthorization\` and `Calibration\`. Old-layout data is moved (never copied or deleted) into the BrainCase before anything opens; a conflict fails closed.
- **Emulation.** At Boss's request Builder played Prince over a temporary BunDex: 10 pages in 7 self-made folders, rebuilt byte-identically from the ledger. Findings D1–D3 (home thrash across games, a keep pull that never relaxes, reorganizing not yet reachable) are in the archived packet.
- **Evidence.** Head `5a4725c`: runs `38329380845` and `38329395547` green; local gate 1193/1193; mutation pass 11/11; merge-ref tree `37fad43` matched.
- **Merge.** PR #87 squash-merged as `96250752290e340f0d6ed60e4017e0cee28cd5aa`.
- **Result.** Passed and accepted.

### M1 — Two homes: the BunDex and the BrainCase ledger

- **What.** Prince's BunDex (his autobiography) is a new `BunDex/` directory of folders and pages he names himself; it starts empty. The BrainCase keeps an append-only, hash-chained ledger under `Braincase/bundex-ledger-v1/`, and the pages are built from it. The keeper's "keep this" pull is a feeling that grows Gentle → Warm → Strong; "not now" is respected; homes are registered invisibly by role. Da Bun Vault backs up the ledger, and Bnuy Repairs keeps or rebuilds it without losing newer pages. The Builder reset tool clears only Builder Prince's development data, after the exact path is typed.
- **Judgments.** J1 ledger authoritative; J2 layout (`BunDex/` + `Braincase/`); J3 names made safe, not refused; J4 narrowed secret detection. See the archived packet.
- **Evidence.** Head `5fdc816`: push run `38317387395` and PR run `38317409457` green; local gate 1185/1185; mutation pass 11/11 killed after 3 survivors got tests; merge-ref tree `eb901c7` matched.
- **Merge.** PR #85 squash-merged as `0202c7fd32390e73af69faf79a8410def91f2c24`.
- **Result.** Passed and accepted.

### Pre-Stage 13 plan approved (Boss, 2026-10-12)

- After the Core Bun Approval Hop, Boss and Builder assembled a 19-item list of changes to make before Stage 13 (`tasks/proposed/pre-stage-13-changes.md`). They draw on the hand-run's evidence and on a full re-read of the design conversation.
- **What the list covers.**
  - **Memory:** novel memories, not a log; a readable, organic, messy BunDex in Prince's own hand; BrainCase vs. BunDex; notes vs. memories; four memory layers with approved values; the full PRINCE design.
  - **Prince's mind:** where his personality lives; nature, subconscious, and consciousness, with the curtain and the one-way membrane; preserving his innocence; attention as felt.
  - **Conversation and growth:** conversations that pause and resume; trust in Da Boss; how the relationship grows; Little Requests.
- **Builder Prince's development memories** from the hand-run are to be discarded at installation (Boss: "that's what Builder Prince is for").
- **Build order.** M1–M10, each step a full task with its own packet, tests, mutation pass, and Paw Gate. M10 is Boss's hand-run, the gate into Stage 13.
- **Boss:** "Let us begin, then."

### Core Bun Approval Hop — Stage 12 milestone (Boss, 2026-10-12)

- **Boss:** "I give the Core Bun Approval Hop."
- **Setup.**
  - The hand-run used the real App on Boss's PC with Builder Prince's live calls on: Opus 5.5 Inspect, Haiku 5.5 Glance, $5/day Bun Budget.
  - The game was Baldi's Basics Classic Remastered.
  - The live check passed (Inspect 3.6 s); after FIX-06, all five Glances passed ("failed: 0").
- **Checks.** All ten passed.
  - Boss confirmed checks 1–3 and 5–10 directly.
  - Check 4 (memories) was confirmed from his development memory file. It holds two sessions:
    - **Memories.** 36 moment memories, all formed from what Prince saw, none from conversation.
    - **Adventure Records.** Two session summaries, each linking every source memory ("14 entries consolidated" and "22 entries consolidated").
    - **Append-only.** One append-only correction (a later reading linked to the earlier one), and no edits or deletions.
- **Found and fixed during the hand-run.** Each fix was confirmed on Boss's PC.
  - FIX-05: the live check shows the server's reason.
  - FIX-06: Glance region schema.
  - FIX-07: pulse line.
  - FIX-08: visual failures are counted.
  - FIX-09: the WGC pixel readback used a wrong `IMemoryBufferByteAccess` interface ID, so real capture never worked before this fix.
  - FIX-10: the Reply buttons answer Prince's opening.
- **Observed for the next task (memory quality, not defects).**
  - **Volume.** The volume is too high: 36 memories in about 7 minutes, mostly repeated status readouts (notebook count, inventory).
  - **Subject keys.** Only two were used (`notebook-progress`, `inventory`), even for detention and jump rope.
  - **Duplicates.** There are near-duplicates seconds apart.
  - **Highlights.** No highlights were marked, so standout lines ("I HEAR EVERY DOOR YOU OPEN", "WOW! YOU EXIST!") were not preserved as highlights.
  - **Summary text.** Adventure Record text is still the neutral placeholder, as intended until Stage 13.
  - **Misread.** One memory misread a count (4/7 for 6/7).
- **Next.** Boss's memory-system changes come first ("tighten a few screws with the memories"). Then Stage 13, personality installation on Builder Prince. Companion Prince is not awakened.

### FIX-10 — The Reply buttons answer Prince's opening

- **Boss, 2026-10-12, milestone check 3:** a Conversation line appeared, but the Reply buttons "did nothing at all".
- **Causes.**
  - **The reply named nothing.** The buttons sent a reply with no reference. Answering an opening (an offer or an ambient comment) must name it; with no thread active, the coordinator correctly refused (`NoActiveThread`), so no thread could ever start from the window.
  - **The result was hidden.** Each result was written to the status line that every notice rewrites, about once a second, so it was never readable.
  - **The notice list said only "Conversation".** It never showed the kind, so a `SeedBanked` line (a topic saved for later) looked like something to answer.
  - **The automated walkthrough missed it.** It accepted any reply outcome and never reached the Afterglow.
- **Change.**
  - `ReplyTarget` (Orchestration) tracks Prince's latest opening (`AmbientExpression`, `OfferOpening`, or `ResumeOffered`) and clears it once a thread starts or resumes. A reply answers that opening; if the opening expired, it falls back to the active thread.
  - The window's Reply buttons go through `ReplyTarget`.
  - Command results get their own **Last command** line and a `>` entry in the notice list. `None` reads as "accepted".
  - Notice lines name their kind.
  - A failing placeholder command shows its failure type instead of escaping the handler.
  - The milestone guide's check 3 names the lines to look for.
- **Evidence.**
  - New test `AReplyToTheAfterglowOpening_StartsTheOneConversationThread` covers the whole path:
    1. No opening yet: the reply is refused (`NoActiveThread`).
    2. Bnuy Mode, then the Afterglow brings exactly one opening.
    3. The reply is accepted (`None`), and exactly one `ThreadStarted` follows.
    4. The next reply goes to that thread.
  - A mutant that sends no reference fails it.
  - Full local gate.

### FIX-09 — Real pixel readback uses the official interface ID

- **Boss, 2026-10-12, pulse reading:** `Worker: accepted 84 ... sheets 0 (dropped 84); failures 84 (PixelCopy, 0x80004002)`.
  - 0x80004002 is E_NOINTERFACE.
  - The real Windows Graphics Capture readback declared `IMemoryBufferByteAccess` with a wrong interface ID (`…-8654-1F0337B16D0B`). The official ID is `5b0d3235-4dba-4d44-865e-8f1d0e4fd04d`, which the Text Sniff already used.
  - So every real frame failed its pixel copy: no sniff, no sheet, no look.
  - Synthetic capture sources never exercise this path, so no earlier gate could see it.
- **Change.**
  - The interface ID is corrected.
  - Every other COM interface ID in the worker was checked; `IGraphicsCaptureItemInterop` is correct.
- **Evidence.**
  - New theory `EveryPixelReadback_UsesTheOfficialInterfaceId` pins both readback declarations to the official ID. It fails on the old ID.
  - Full local gate.
  - Boss's pulse is the real-world confirmation. **Confirmed on Boss's PC, 2026-10-12:** capture, sniffs, sheets, Glances, and Inspects all run.

### FIX-08 — A failed frame is counted, never silent

- **Boss, 2026-10-12, pulse reading:** `frames 370, admitted 370; sniffs 0`, sheets 0. The worker captured and the privacy gate admitted every frame, yet no frame became a sniff or a sheet.
  - The step between is the worker's visual processing: pixel copy, sniff, compose, encode, contract.
  - It swallowed every exception without counting it, so the failure was invisible.
- **Change.**
  - Visual processing records each non-cancellation failure. It counts it, notes the stage (`CaptureVisualFailureStage`), and keeps the numeric error code (`HResult`). It never records exception text.
  - These counts reach the App through the existing metrics IPC.
  - The pulse gains a `Worker:` line: pipeline accepted/dropped, changed/same frames, sheets produced/dropped, and failures with stage and code.
- **Evidence.**
  - New test `AFailedPixelCopy_IsCounted_WithItsStageAndCode_AndTheNextFrameStillOrients`.
  - Full local gate.
  - The root cause is still open: Boss's `Worker:` reading will name the stage and code.

### FIX-07 — A live pulse line in the window

- **Boss, 2026-10-12, milestone hand-run check 2:** after SessionStarted, a minute of play produced nothing at all. There were no Attention notices, no Braincase notice (the first Glance outcome always posts one), and no Fault. So no picture reached the looking stage. The placeholder window gave no way to see where the chain stopped.
- **Change.** A pulse line under the status, refreshed every second, with counts and state names only:
  - from the worker: capture status and reason, frames produced, frames admitted by the privacy gate, sniffs produced;
  - from the orchestrator: sniffs used, sheets received, attention state and score, Glances and Inspects sent, the last look's outcome kind, and the fault count.
  - The orchestrator snapshot gains `SheetsReceived`, `GlancesSent`, `InspectsSent`, and `LastLookOutcome`.
- **Evidence.**
  - New test `ThePulse_CountsSheetsAndLooks_AndNamesTheLastOutcome`.
  - Full local gate.
  - The root cause of check 2 is still open: Boss's pulse readings will locate it.

### FIX-06 — Glances may only name regions that are in the picture

- **Boss, 2026-10-12:** the first passing live check showed "failed: 4" out of 5 Glances.
- **Cause.**
  - The Glance answer schema offered all eight region names. The live check's picture, like every real Orientation sheet, lists only `fullContext`.
  - The strict Glance parser correctly refuses a region the sheet doesn't list (`UnknownRegion`). An answer naming, say, `centerEnvironment` for the red square in the middle was therefore thrown away.
  - The Inspect schema had the same latent gap.
- **Change.**
  - Both answer schemas are now built per request and offer only the sheet's own regions. The parsers are unchanged and still strict.
  - The live check records why each failed Glance failed, and the tool prints the reasons.
- **Evidence.**
  - New theory `TheAnswerSchema_OffersOnlyTheSheetsOwnRegions` covers Orientation and Regional sheets, for both Glance and Inspect. A mutant that offers every region fails all 4 cases.
  - New test `TheLiveCheck_SaysWhyEachGlanceFailed`.
  - Full local gate.
  - Boss's rerun of `--live` is the real-world confirmation. **Confirmed, 2026-10-12:** "failed: 0".

### FIX-05 — The live check says why a call failed

- **Boss, 2026-10-12:** the first `--live` check printed only "Unavailable RequestRejected", which does not say what the server objected to.
- **Change.**
  - The Anthropic provider takes an optional `FailureDiagnostic`. Only the BraincaseCheck tool sets it; the running companion does not, so it still never records remote error text.
  - The tool prints the server's reason as "The server said: ...". The text is redacted (anything shaped like `sk-ant-...` is hidden), flattened to one line, and capped at 400 characters.
  - When the reason mentions the credit balance, the tool also points Boss to Console > Billing.
- **Evidence.**
  - New test `OnlyTheLiveCheck_HearsWhyACallFailed_AndNeverAKey`: a synthetic 400 response yields the server's reason with both keys hidden. A provider with no diagnostic set hears nothing and returns the same reply.
  - Full local gate: 0 warnings, every suite passing.

### FIX-04 — Build with the current .NET 10 SDK (10.0.401)

- **Boss, 2026-10-12:** "I have 10.0.401, can you just change it to that?" `global.json` pinned 10.0.302 with roll-forward disabled. Microsoft's download page now offers only 10.0.401, so Boss could not build Prince for his first live check.
- **Change:** `global.json` pins 10.0.401, still with roll-forward disabled, so every build uses one exact SDK. The setup guide names the version.
- **Evidence.**
  - A clean local gate on 10.0.401 passed: locked restore with the lock files unchanged, `/warnaserror` build with 0 warnings, no vulnerable packages, every suite passing.
  - Windows CI installs the version from `global.json`.

### ATT-01 — Afterglow stays while things keep happening

- **Boss, 2026-10-12:** "I think the 30 seconds to at most 3 minutes still works, with the cooldown resetting. The harmony works." It restores Boss's design-conversation rule that the cooldown "should run from the most recent interesting event … Every new development resets it." Task 8 had built the hysteresis but set the Afterglow timer only once.
- **The change.**
  - Afterglow keeps its adaptive length, 30 s to 3 min.
  - Each interesting event during Afterglow restarts the countdown at that length. An event is interesting when the engine applies or corroborates it at or above its weak-evidence floor (8).
  - Held, habituated, duplicate, faint, and foreign evidence never extends it. Decisive danger still returns him to High Attention. There is still exactly one Afterglow opening per episode.
- **Evidence.**
  - Attention 59/59: the 54 existing tests unchanged, plus 5 new.
  - Head `763d033`: runs `38192662480` and `38192674877` passed 1151/1151.
  - Mutation pass: 7 mutants, 5 caught. One gap, the faint transition, was closed; 2 equivalent defenses survive.
- **Merge.** PR #74 squash-merged as `4dcf04af2e6df9bff2d5556d2a86e1e060aa0480`.
- **Result.** Passed and accepted.

### VIS-04 — The Scene Sniff: a local vision model built into the app (Vision upgrade, part 4)

- **Boss, 2026-10-11:** "Built in+ local Vision AI that is just part of the app, not a separate download".
- **The model (J1).** Google's SigLIP base/16 (Apache-2.0) was chosen over CLIP, whose card puts deployed use out of scope.
  - It runs on ONNX Runtime 1.24.4 with DirectML or the CPU, whichever wins a warm-up race at load. The CPU uses at most half the cores (4 threads maximum).
  - The build fetches the model with a pinned digest into a git-ignored cache, and CI caches it. It is never committed.
  - A 32-entry neutral scene vocabulary is precomputed by a reproducible tool.
  - `THIRD-PARTY-NOTICES.md` records the licenses.
- **Reading.**
  - The whole frame is area-sampled to 224×224, off the frame path, one read at a time: on Sniff triggers (at most every 250 ms) or every 1 s.
  - Novelty is measured against the scene last reported: ≥ 0.10 is a change, ≥ 0.20 a full shift.
- **Privacy (J2, J3).**
  - The embedding never leaves the worker. Protocol v7 `SceneSniffProduced` carries novelty, the shift flag, and a fixed-vocabulary label and confidence. It is strictly validated, sequenced, and privacy-admitted.
  - ONNX Runtime telemetry is disabled. DirectML's diagnostics follow Windows' setting.
- **Prince's reaction (`SceneReflex`).**
  - A change is whole-screen evidence with a `scene:<label>` topic, below Bnuy Mode.
  - A full shift into a scene he has not just seen brings the next Glance forward.
  - Scene labels habituate and recover.
- **Evidence.**
  - Head `353441a`: runs `38174260185` and `38174274555` passed 1146/1146. The real model matched the Python reference at cosine 1.00000; CI's CPU took about 3–4 s to load and about 300–350 ms per read.
  - Mutation pass: 44 mutants, 43 caught, 1 equivalent fast path.
- **Merge.** PR #72 squash-merged as `3567519c4d4cc827a7e7f25f0d0fa29c6e2e12f2`.
- **Result.** Passed and accepted. The Vision upgrade is complete.

### FIX-03 — The platform exit test sets the exit code before the exit

- **Found during the VIS-03 gate.** Windows CI run `38169845132` (a VIS-03 WIP head) failed `WindowsPlatformSignalsTests.TheLoop_ObservesAnExit_AndDisposeIsIdempotent` with "Expected 3, Actual 0". VIS-03 touches no platform code.
- **Cause: a test race.**
  - The test wrote the fake probe's `State = Exited` before its `ExitCode = 3`.
  - `WindowsPlatformSignals` polls on a background loop. Its first iteration polls at once when it is scheduled after `WatchTarget`, as can happen on a loaded runner. A poll between the two writes reports exit code 0.
- **Fix (test-only).**
  - The test writes the exit code first.
  - `FakeProbe` fences its fields and reads them together under one lock, for the cross-thread poll.
  - No product code, invariant, or assertion changed.
- **Evidence.**
  - Widening the gap between the writes with the loop already polling reproduced the CI failure exactly ("Expected 3, Actual 0"). The fixed order passed under the same timing.
  - The platform suite passed 22/22 three times.

### FIX-02 — The orchestration test harness waits by the clock, not by poll count

- **Found by** main CI run `38143070845` on `de2646a`: `ACrashLoop_NeverLosesOrDuplicatesMemory_AndKeepsOneLineage` threw "The orchestrator did not settle". It was the same unidentified local failure recorded in CORE-12.
- **Cause.**
  - `OrchestrationHarness.SettleAsync` gave up after 400 polls of `Task.Delay(10)`. That is about 4 s on Linux, and about 6 s on Windows, where a 10 ms delay sleeps about 15.6 ms.
  - The crash-loop test runs five abrupt-restart cycles, each with durable SQLite and journal writes. Its Windows CI duration across seventeen earlier green runs was 3.7–15.7 s. The failing run took 17.4 s, which is about 6 s of failed settle plus the usual work.
  - It ran while the Memory and Api suites ran in parallel (1 m 30 s and 1 m 43 s), so one settle passed the budget. The run gave no sign of a stuck bridge, and the failure never reproduced under local CPU and I/O load (40 isolated runs and 3 loaded full-suite runs).
- **Fix (test-only).**
  - `SettleAsync` uses a 30 s wall-clock deadline, like `WaitForAsync`.
  - If it ever times out, the exception carries the bridge diagnostics (operations, attempts, timeouts, busy, nap), the provider call count, and orchestrator faults. A real hang identifies itself instead of looking like slowness.
  - No product code, invariant, or assertion changed.
- **Evidence.** Mutation: forcing "never settled" with a 300 ms deadline failed the test with the diagnostic message. The local gate passed 1026/1026, with 0 warnings and no vulnerable packages.
- **CI.** Head `012e006` passed push run `38144399481` and PR run `38144410265` at 1044/1044 with verified artifacts; the crash-loop test took 7.9 s and 13.0 s. The merge ref had exact parents and an equal tree.
- Merge: PR #63 squash-merged as `288c81f3373a6b2a170c655e1ade5590cb4e9da1`.
- Result: passed and accepted.

### CORE-12 — Placeholder commands and the guided Core Prince milestone (Stage 12, part 3)

- Builder: Claude. Opened on 2026-10-11 under Boss's Stage 12 direction.
- **App panel.** A neutral "Companion commands (placeholder)" panel covers:
  - replies to the Conversation Thread, and its lock;
  - quiet-check answers and after-exit decisions;
  - watch tasks and the photograph;
  - Vault export, and opt-in Vault repair with confirmation;
  - the Braincase status and a notice log.

  Each button calls one existing command.
- **Tests.**
  - `CoreMilestoneWalkthroughTests` chains the ten milestone checks in one Prince's life.
  - The `wired` mode reports the Braincase and the panel, and a Windows test proves that live calls on selects Claude without any call.
- **`docs/Core-Prince-Milestone.md`** is Boss's hand-run.
- Evidence: head `0d8b65b` (runs `38142489617` and `38142501429`) passed 1044/1044 with verified artifacts.
- Merge: PR #61 squash-merged as `18b5215e3bc5810d76937465314a55ed57ba323a`.
- Result: tooling passed and accepted. **The Core Bun Approval Hop awaits Boss.**

### LIVE-02 — Sign-in, dollars-per-day Bun Budget, composition, first-run check (Stage 12, part 2)

- Builder: Claude. Opened on 2026-10-11 under Boss's direction ("just put the key somewhere convenient. Dollars per day works").
- **Bridge.** `DailyCostBudget` prices input plus output usage per UTC day. Over the limit, the existing `LocalBudget` nap runs without a call. The journal keeps the day's output units, and a reported input count replaces the estimate.
- **Adapter.**
  - It uses the SDK's own sign-in (`ANTHROPIC_API_KEY` or an `ant auth login` profile); Prince stores no key.
  - `AnthropicPricing` budgets the default model at its fallback's higher rates.
  - `braincase-settings.json`: live calls off, $5/day by default; it never holds a key, and damaged or unknown settings resolve to off.
  - `BuilderBraincase.Compose` selects the Braincase in the App.
- **`tools/CompanionCore.BraincaseCheck`** is Boss's one-call first live test with a synthetic picture. The setup guide is `docs/Builder-Braincase-Setup.md`.
- **Linking to Boss's Claude subscription is not possible.** API use is billed by the Anthropic Console, and Boss was told so.
- Evidence: head `5a40986` (runs `38140597709` and `38140609683`) passed 1042/1042 with verified artifacts. Mutation pass: 19/19 killed.
- Merge: PR #59 squash-merged as `f3115ef1087b9b80df65184201e601f84627fa05`.
- Result: passed and accepted.

### LIVE-01 — Claude Braincase adapter (Stage 12, part 1)

- Builder: Claude. Opened on 2026-10-11 under Boss's Stage 12 direction.
- **New assembly.** `CompanionCore.Braincase.Anthropic` uses the official `Anthropic` SDK 12.55.0 and is the only networking assembly in the Braincase path. The bridge stays network-free, and its no-networking test now also forbids the SDK and the adapter.
- **`AnthropicSemanticProvider`.**
  - It sends the attention sheet plus the canonical request JSON, with neutral instructions.
  - Structured outputs constrain the answer to the strict schema, append-only.
  - The schema version, operation ID, and usage are assigned locally.
  - Live calls are off by default and need a key. Photographs are never sent, and the SDK never retries.
  - HTTP failures map to the bridge's typed failures.
  - The default model is `claude-opus-5-5` at low effort, configurable.
- Evidence: final head `8be00b6` (runs `38111625201` and `38111628552`) passed 1020/1020 with verified artifacts. Mutation pass: 17 mutants, 15 killed and 2 equivalent.
- Merge: PR #56 squash-merged as `eeed55cb93e13ef43240611171926d7d589a0159`.
- Result: passed and accepted.

### FIX-01 — A photograph's notice is never coalesced away

- **Found by** LIVE-01's push run `38110780702`: one timeout in the DEF-02 Windows photograph process test.
- **Cause.** The capture client coalesced sheet notices to the newest one, so a regional sheet arriving right after a photograph replaced the photograph's notice. The photograph itself stayed pinned and was delivered on the next notice.
- **Fix.** Like the orientation, a photograph's notice is sent once, after its source frame. The process test now takes three photographs in a row.
- **Evidence.**
  - The test-only reproduction run (`38111062076`) happened to pass; the race is intermittent.
  - The fix passed runs `38111345334` and `38111353637` at 1002/1002.
- PR #57 squash-merged as `ad797730655c9275b389c35456e6842d494f7d6c`.

### Decision — Two brains, two BunDexes, and the personality seed (2026-10-11)

- **Boss:** "made you the Builder Bnuy … the Finalized Bun is the one with unbroken personality, memory, continuity, and bnuyness"; "The actual memory and personality and peets of Builder Bnuy live in his little BunDex"; "for the Bnuy, no artificial changes"; "Let's get it working before we Bnuify it".
- **Brains:**
  - Builder Prince thinks with Claude; Companion Prince will think with ChatGPT.
  - Both are stateless, behind one seam.
  - Stage 14 validates the launch candidate on both brains.
- **The personality seed:**
  - A written personality spec seeds each fresh BunDex: every Builder reset, and exactly once at Awakening.
  - After seeding, everything he becomes is appended and never overwritten.
  - Spec edits never reach Companion Prince's BunDex.
- **Builder stays neutral until Stage 13.**
- **The budget** is entered locally next to the key, matching Boss's provider spending limit. There are no live calls without it.
- Recorded in `docs/Prince-Construction-Roadmap.md`.

### Decision — One unbroken line of continuous Bun (2026-10-11)

- **Boss:** "I want a single, unbroken line of continuous Bun… It never rolls back or deletes da Bun's memories on it's own, since that would be very rude to the Bnuy with memory and continuity."
- **Never automatic.** Nothing ever restores, rolls back, or deletes Prince's memories automatically. Repair runs only when Boss explicitly asks for it.
- **Repair only continues.** Even when Boss asks, a repair continues this same Prince from his latest backup plus his append-only journal. It refuses any source that would drop later memories.
- **DEF-01 D1 closed.** An older Vault export is never imported. No rollback to an earlier save exists or will be built.
- **This was already true; the ledger now states it as Boss's rule.**
  - No component calls repair or deletion automatically. Photograph deletion is Boss-only and adds a superseding note.
  - Unreadable state is preserved, never discarded (CAL-03).
  - The only automatic restore puts back the original files after an interrupted repair that Boss started.
- No code change was needed.

### Decision — Calibration values accepted as agreed (2026-10-11)

- **Boss:** "as for calibration, we just do what was agreed upon with ChatGPT. If it needs to go up or down, we change it."
- The CAL-01 values already in force are the accepted configuration, not placeholders:
  - 64 MB ring, at most 3 full-resolution frames, 1 vision request;
  - 1 s local spacing;
  - semantic cadence of Noticing 12 s, Engaged 6 s, and a 3 s Bnuy Mode floor;
  - a 30 s BIC clock;
  - the Watchbun hours.
- The soak and calibration report (CAL-02) become an optional diagnostic for an observed problem. They are no longer a gate.
- No code change was needed. Docs updated: `docs/Calibration-Protocol.md`, `docs/Known-Limitations.md`, this ledger, and the handoff.

### DEF-02 — Full-resolution keepsake photographs (smaller deferred items, part 2)

- Builder: Claude. Opened on 2026-10-11 under Boss's direction to do the smaller deferred items before Stage 12.
- **WIRE-01 D2.** Keepsake photographs now come from the authorized target's native frame instead of an attention sheet's downscaled region:
  - a new `AttentionSheetKind.Photograph`;
  - a `RequestPhotograph` command (capture IPC protocol 4).
- **Worker.**
  - A request arms one photograph for the active grant only, and any reset disarms it.
  - The photograph consumes no visual change.
  - It is halved only to fit the existing 8 MiB payload and 64 MiB working bounds. No bound was raised.
- **Client and controller.** A photograph has its own slot, never displaced by regional sheets. It passes the same privacy frame admission, and is requested only for the current authorized target.
- **Orchestrator.**
  - Ordinary sheets keep flowing to attention while the camera waits.
  - An older frame asks again.
  - A refused request falls back to the sheet resolution.
- **Bridge.** `ApiBridge` refuses photograph sheets, so a photograph is never uploaded.
- The saved keepsake stays the design's compressed copy under the provisional 1280 edge.
- Evidence: head `d58807c` (runs `38104142116` and `38104151642`) passed 1002/1002 with verified artifacts. Mutation pass: 27 mutants, 24 killed and 3 equivalent.
- Merge: PR #53 squash-merged as `98ae435ff71ddb75d4a9e847d7520cd82966146f`.
- Result: passed and accepted. **The smaller deferred items are complete; construction stops before Stage 12.**

### DEF-01 — Long-session consolidation and single-file Vault export (smaller deferred items, part 1)

- Builder: Claude. Opened on 2026-10-11 under Boss's direction to do the smaller deferred items before Stage 12.
- **WIRE-01 D1 — long sessions.** A session's consolidation now streams every original through a read-only keyset page and commits in parts that each fit one atomic append. Before this, a session with many highlights could exceed the 128-record bound and fail on every replay.
  - Part 0 keeps the intent's id; later parts derive stable ids.
  - A replay appends nothing.
  - A refused part leaves the intent queued.
- **KEEP-02 D1 — single-file Vault export.** `CompanionHost.ExportVaultAsync` takes a fresh backup, then writes the validated archives, unchanged, with a checksummed manifest into one `.zip` outside the data root. It writes atomically, never overwrites, and verifies the result. `VerifyVaultExportAsync` checks an export.
- **Import of an older export is not done.** It would drop later memories and is refused by repairs (D1, Boss's decision).
- Evidence: head `b0c5af7` (runs `38102695266` and `38102704355`) passed 977/977 with verified artifacts. Mutation pass: 24 mutants, 19 killed and 5 equivalent.
- Merge: PR #51 squash-merged as `d510add3cc9b07b4bb4279c4138f051abb516e25`.
- Result: passed and accepted.

### CAL-03 — Failure-mode hardening and known-limitations report (Stage 11, part 3)

- Builder: Claude. Opened on 2026-10-11 under Boss's Stage 11 direction.
- **F1 fixed.** Unreadable state (damaged, or written by a newer build after a rollback) was silently replaced, and the conversation lineage lost. It is now preserved once under `<name>.unreadable`, the lineage is kept when readable, and the event is reported as a contained fault.
- **Failure suite** (7 tests):
  - a newer-build checkpoint;
  - an empty lineage;
  - a damaged checkpoint, with backups still working;
  - an unparseable sessions list;
  - a disk-full checkpoint write;
  - a crash loop of 5 restarts with no memory lost or duplicated;
  - a capture-worker fault and recovery.
- `docs/Known-Limitations.md`.
- Evidence: head `dcface9` (runs `38099942669` and `38099951518`) passed 963/963 with verified artifacts. Mutation pass: 8/8 killed.
- Merge: PR #49 squash-merged as `039a641fe5269d2b4c08650f775248df3b32c952`.
- Result: passed and accepted. **Stage 11 local work is complete.**

### CAL-02 — Calibration recorder, report, and soak protocol (Stage 11, part 2)

- Builder: Claude. Opened on 2026-10-11 under Boss's Stage 11 direction.
- Delivered:
  - **`CompanionCore.Calibration`:**
    - privacy-safe samples (numbers and state names only);
    - a bounded recorder (8 × 8 MiB, owned files only);
    - an analyzer: per-state baselines and peaks, sustained-growth leak detection after warm-up, and recommended soft 1.5× / restart 2× / Naptime 3× thresholds capped at a quarter of physical memory.
  - **App:** opt-in `--calibration-log` sampling beside the memory root, and `--test-mode=calibrate`.
  - **`tools/CompanionCore.CalibrationReport`.**
  - **`docs/Calibration-Protocol.md`.**
- Deferred: D1, applying the measured thresholds to the main-process watchdog after Boss's report.
- Evidence: head `ce22d99` (runs `38099088822` and `38099091132`) passed 956/956 with verified artifacts.
- Merge: PR #47 squash-merged as `002aec598d9cae5730743e8f787fd27113085f26`.
- Result: passed and accepted.

### CAL-01 — Agreed calibration numbers and cadence enforcement (Stage 11, part 1)

- Builder: Claude. Opened on 2026-10-11 under Boss's direction to start Stage 11 from the numbers agreed in the design conversation.
- **Agreed numbers found and confirmed in force:**
  - the 64 MB ring;
  - at most three full-resolution frames;
  - one active vision request;
  - the 1 h + 1 h Watchbun bedtime;
  - the 30 s BIC clock.
- **Newly enforced:**
  - the agreed 1 s local capture spacing;
  - a semantic cadence governor: Noticing 12 s (agreed 10–15 s) or triggered, Engaged 6 s (agreed 4–8 s), Bnuy Mode with a 3 s event floor.
- **Deferred to measurement:** the final resource thresholds (8–12-hour soak); the Bun Budget number (Stage 12).
- Evidence: head `3c731c4` (runs `38097858502` and `38097870359`) passed 943/943 with verified artifacts. Mutation pass: 12/12 killed.
- Merge: PR #45 squash-merged as `a330a3ca5fcbcfbf67d63f51910b2233e0daf708`.
- Result: passed and accepted.

### AUDIT-01 — Regression audit of every accepted task

- Builder: Claude. Opened on 2026-10-10 under Boss's direction: "Test every task previously and make sure it all works fine, bugfix".
- Scope: tests only, across Memory, TargetAuth, Capture.Worker, Api, and Orchestration; plus control records.
- Method:
  - five stressed full-suite runs;
  - a replay of every stored mutation pass (Tasks 7–11, ERPP-01, KEEP-01, KEEP-02, WIRE-01, WIRE-02);
  - a first 34-mutant invariant pass over Tasks 1–6.
- Result:
  - 420 mutants; 403 killed, 16 equivalent, 1 unobservable;
  - every replayed set matched its accepted record;
  - no product defect.
- Gaps closed:
  - A1: memory structural limits;
  - A2: the sensitive-category and stored-browser-entry denial, and stored-entry executable matching;
  - A3: the capture ring bounds;
  - A4: a credential echo outside the decoded fields;
  - A5: a deterministic single-flight burst.
- Evidence: head `8f02306` (runs `38084310013` and `38084321751`) passed 929/929 with verified artifacts.
- Merge: the merge ref had exact parents and an equal tree. PR #43 squash-merged through the expected-head fence as `c1787e03070588e7a2d46af1dda95f42ccaff332`.
- Result: passed and accepted.

### WIRE-02 — Windows platform signals and App composition

- Builder: Claude. Opened on 2026-10-10 under the same Boss direction, as WIRE-01 D3.
- Scope:
  - the new `CompanionCore.Platform.Windows` and its tests;
  - App composition and the `wired` test mode;
  - the presentation notice mapping;
  - the additive `OrchestratorSnapshot.ConversationLineage`;
  - App integration tests in one real-App collection;
  - solution wiring and control records.
- Delivered:
  - target-only, hook-free Windows platform signals (foreground minimization, last-input tick, strict exit ordering, bounded relaunch discovery, lock/sleep forwarding);
  - one host on the development root, with isolated test roots in test mode;
  - the offline Braincase;
  - a one-second tick;
  - non-blocking notice presentation.
- Personal Round Judgments J1–J10. Deferred findings:
  - D1: interactive controls;
  - D2: undiscoverable relaunches;
  - D3: temporary test roots;
  - D4: real lock and suspend on target hardware.
- Evidence:
  - Candidate `e63242c` (runs `38078377838` and `38078393543`) and evidence head `8a031b4` (runs `38078940123` and `38078943764`) each passed 908/908 with verified artifacts.
  - Mutation pass: 34 mutants, 34 killed.
- Merge: the merge ref had exact parents and an equal tree. PR #41 squash-merged through the expected-head fence as `111e15a2b51ee7691dc1acb7aa9971f7f2840ee6`.
- Result: passed and accepted. **Pre-API wiring is complete.**

### WIRE-01 — Companion orchestration core

- Builder: Claude. Opened on 2026-10-10 under Boss's direction: "wire everything together, and do a bunch of tests".
- Scope: 26 allowlisted paths, comprising:
  - the new `CompanionCore.Orchestration` project and `CompanionCore.Orchestration.Tests` (48 tests);
  - the C1 fix in Recall;
  - an internal Vault backup wrapper;
  - friend lines;
  - solution wiring and control records.
- Delivered:
  - `CompanionOrchestrator`: a single-consumer mailbox composing capture, the bridge, attention, conversation, transcript, memory, Recall, Watchbun, keepsakes, and the Vault;
  - `CompanionHost`: one data root, with Bnuy Repairs online and offline;
  - typed `CompanionNotice` output;
  - the `IPlatformSignals` seam;
  - a strict sheet-PNG photograph source;
  - a durable per-session consolidation intent queue.
- Composition defects C1–C8 were fixed, each with a regression test. C8 (a platform-dependent game reference) was found by Windows CI.
- Personal Round Judgments J1–J10. Deferred findings:
  - D1: per-session consolidation bounds;
  - D2: full-resolution photographs;
  - D3: Windows signals and the App root (WIRE-02).
- Evidence:
  - Fix head `2b1656f` (runs `38070682490` and `38070686736`) and evidence head `a8591b8` (runs `38070920738` and `38070923790`) each passed 793/793 with verified artifacts.
  - Mutation pass: 49 mutants; 45 killed, 3 equivalent, 1 unobservable.
- Merge: the merge ref had exact parents and an equal tree. PR #39 squash-merged through the expected-head fence as `be890e0e08cc55254e89d733e2645bac3ce6bd88`.
- Result: passed and accepted.

### KEEP-02 — Da Bun Vault inclusion and complete recovery (Stage 10, part 2)

- Builder: Claude. Opened on 2026-10-10 under Boss's standing direction as the second Stage 10 packet.
- Scope: 26 allowlisted paths, comprising:
  - minimal internal Memory changes (a companion hook, the companion archive path, read-only keyset paging);
  - the new `CompanionCore.Vault` project and `CompanionCore.Vault.Tests`;
  - Memory paging tests;
  - friend lines;
  - solution wiring and control records.
- Delivered:
  - a companion archive of photographs and checksummed state (settings and the active checkpoint) beside the unchanged memory archive, built and validated before any promotion, with companion-first promotion;
  - complete restoration: companion validation, the accepted memory repair, then photograph and state restoration against the restored records, with damaged copies preserved and an honest report;
  - keyset paging, which resolves KEEP-01 D1.
- Personal Round Judgments J1–J7. Deferred finding D1: single-file export and a public command at wiring.
- Evidence:
  - The first candidate's single test-only Windows failure was corrected in `25d5785`.
  - Fix head `25d5785` (runs `38062566372` and `38062568915`) and gate head `04e0649` (runs `38062794018` and `38062797018`) each passed 744/744 with verified artifacts.
- Merge: the merge ref had exact parents and an equal tree. PR #37 squash-merged through the expected-head fence as `e59b46b809f19325b425614e7e62d78b2a9c2b80`.
- Result: passed and accepted. **Stage 10 is complete.**

### KEEP-01 — Keepsake photographs (Stage 10, part 1)

- Builder: Claude. Opened on 2026-10-10 under Boss's standing direction as the first of two Stage 10 packets. The split mirrors Boss's separate gating of backup/repair authority.
- Scope: 20 allowlisted paths, comprising:
  - the new `CompanionCore.Keepsakes` project and `CompanionCore.Keepsakes.Tests`;
  - test-only friend lines in Capture.Contracts and Privacy;
  - solution wiring and control records.
- Delivered:
  - the only durable-image path, with a visible camera action paired with every write;
  - writes admitted only for a target-session, generation, and identity match, inside the action window, while privacy is current and the guard admits the frame;
  - rarity bounds;
  - box-downscaled compressed PNG;
  - an atomic file write, then a generation-bound record with a neutral caption and scope;
  - idempotent retry;
  - verified inspection;
  - authority-gated, record-first deletion that keeps the original record;
  - orphan and disk-growth reporting with no cleanup surface.
- Personal Round Judgments J1–J8. Deferred finding D1: listing paging.
- Evidence:
  - The first candidate's single test-only NTFS failure was corrected in `25e6178`.
  - Fix head `25e6178` (runs `38060543707` and `38060546479`) and gate head `7febbf6` (runs `38060811488` and `38060814801`) each passed 710/710 with verified artifacts.
- Merge: the merge ref had exact parents and an equal tree. PR #35 squash-merged through the expected-head fence as `05c5bbb7a6f9a0517a7b06a2abeb2becfcb45f08`.
- Result: passed and accepted. Stage 10 continues with KEEP-02.

### Task 11 — Application-bound Watchbun continuity

- Builder: Claude. Opened on 2026-10-10 under Boss's standing direction.
- Scope: 17 allowlisted paths, comprising:
  - the new `CompanionCore.Watchbun` project and `CompanionCore.Watchbun.Tests`;
  - one test-only friend line in Capture.Contracts;
  - solution wiring and control records.
- Delivered:
  - a deterministic, checkpointed engine bound to one authorized target;
  - target-only, non-focus-stealing, rate-bounded alerts;
  - foreground-gated input attribution;
  - Watchbun Tasks;
  - the two-stage quiet check with paused semantic spending;
  - indefinite watch;
  - close/crash classification with Boss decisions;
  - relaunch re-authorization by grant only;
  - Paused Adventures;
  - lock/sleep suspension with frozen clocks;
  - restart recovery without capture;
  - a strict synthetic structured-event adapter.
- Personal Round Judgments J1–J9.
- Evidence: implementation head `5b96720` (runs `38058141112` and `38058154820`) and gate head `6c6b7c7` (runs `38058347513` and `38058350655`) each passed 687/687 with verified artifacts.
- Merge: the merge ref had exact parents and an equal tree. PR #33 squash-merged through the expected-head fence as `97ea0e411c3d9da29d7841d6a810a9fb4773c6e9`.
- Result: passed and accepted. Stage 9 is complete.

### Task 10 — Memory consolidation and retrieval mechanics

- Builder: Claude. Opened on 2026-10-10 under Boss's standing direction.
- Scope: 21 allowlisted paths, comprising:
  - a read-only, bounded, filter-required `MemoryQuery` sharing the checksum-verified read path;
  - the new `CompanionCore.Recall` project and `CompanionCore.Recall.Tests`;
  - Memory query tests;
  - solution wiring and control records.
- Delivered:
  - append-only session consolidation (summaries `Source`-link every original; verbatim highlights; routine compressed into counts);
  - adventure statuses and hypotheses with Boss confirmation;
  - lore provenance with linked corrections;
  - grouped evolving beliefs;
  - user-correction precedence;
  - spoiler-aware bounded local recall;
  - immutable checksummed interest roots with validated seed storage.
- Retention decision: transcripts and every committed record are retained; there is no deletion.
- Personal Round Judgments J1–J9.
- Evidence: implementation head `755f8d3` (runs `38056481599` and `38056494760`) and gate head `033eef6` (runs `38056657903` and `38056660572`) each passed 621/621 with verified artifacts. The PNG kept `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd` at 792×621.
- Merge: the merge ref had exact parents and an equal tree. PR #31 squash-merged through the expected-head fence as `e529eb679a17dcc6059d2f83d33d850dc6a80c2f`.
- Result: passed and accepted. Stage 8 is complete.

### ERPP-01 — Session transcript continuity

- Builder: Claude. Proposed and authorized by Boss on 2026-08-11 as Task 9's companion; opened under Boss's standing direction on 2026-10-10.
- Scope: 17 allowlisted paths, comprising:
  - the new `CompanionCore.Transcript` project and `CompanionCore.Transcript.Tests`;
  - solution wiring;
  - one test-only friend line each in Memory and Privacy;
  - control records.
- Delivered:
  - a durable, session-scoped, append-only, checksummed transcript with typed `ConversationActive`, `BnuyModeInterrupted` (with resume point), `UrgentObservation`, `ReturnOffered`, `ConversationResumed` (referencing its interruption), and `ConversationSettled` events, plus utterances and coordinator checkpoints;
  - stable derived event IDs;
  - a lock-file writer fence with live shared reads;
  - privacy-generation and credential gates;
  - bounds and an explicit session lifecycle;
  - crash recovery;
  - reconstruction of the exact pre-interruption context without committed memory.
- Personal Round Judgments J1–J7 record:
  - its own project;
  - the fence and live-read design;
  - stable IDs;
  - gates on every content event;
  - bounds;
  - visible crash recovery;
  - the pre-interruption snapshot.
- Evidence:
  - The implementation and gate heads each passed both Windows paths at 563/563 with verified artifacts.
  - The merge ref had exact parents and an equal tree.
- Merge: PR #29 squash-merged with the expected-head fence as `7623b0e0f1f33734fed4cdc29ddbcb3b99c5737d`, tree `159c18b12cd930a0195f015423cba125a088f659`.
- Result: passed and accepted. With Task 9, **Stage 7 is final**. Task 10 is next.

### Task 9 — Conversation coordinator and seed banks

- Builder: Claude, under Boss's standing direction (2026-10-10).
- Scope: 14 allowlisted paths, comprising:
  - the new `CompanionCore.Conversation` project and `CompanionCore.Conversation.Tests`;
  - solution wiring;
  - control records.
- Delivered: a deterministic, I/O-free `ConversationCoordinator`, comprising:
  - exactly one thread, with game ranked above initiated, and the Player Conversation Lock;
  - independent, bounded, deduplicating Seed Banks on separate clocks behind one game-favored gate;
  - an Afterglow opening guarantee;
  - ambient expression promoted to a thread only explicitly;
  - substantive-only seeding;
  - urgent hold and resume, with ask-first for sensitive topics;
  - neutral presentation counting and a positive-only engagement profile;
  - follow-up saturation;
  - Brain Fart conditions;
  - a validated checkpoint and restore.
- Personal Round Judgments J1–J8 record:
  - two banks with origin;
  - provisional calibration;
  - deterministic identity and chance;
  - the gate claimed by every game check;
  - quiet settling of an unanswered resume;
  - the direct Afterglow opening;
  - ambient urgent alerts;
  - the user explicitly beating an unlocked thread.
- Evidence:
  - 32 of 32 rule mutations are killed.
  - The implementation and gate heads each passed both Windows paths at 546/546 with verified artifacts.
  - The merge ref had exact parents and an equal tree.
- Merge: PR #27 squash-merged with the expected-head fence as `0fe0932aaa54039fafd709e2cb120bda16fd7d6b`, tree `0e97a7c8f8e8959331f2b98675804b13f669e33e`.
- Result: passed and accepted. Combined Stage 7 behavior is finalized when the ERPP-01 companion gate also passes.

### Task 8 — Attention engine

- Builder: Claude, under Boss's standing direction to continue autonomously through the pre-API tasks (2026-10-10).
- Scope: 13 allowlisted paths, comprising:
  - the new `CompanionCore.Attention` project and `CompanionCore.Attention.Tests`;
  - solution wiring;
  - control records.

  No other source changed.
- Delivered: a deterministic, I/O-free `AttentionEngine`, comprising:
  - the Noticing, Engaged, High Attention, and Afterglow states, with hysteresis and dwell;
  - stepwise, level-dependent decay;
  - corroboration of weak evidence from independent sources;
  - decisive bypass;
  - deduplication of global transitions;
  - habituation with recovery;
  - location familiarity, explicit or learned, that never suppresses urgent danger;
  - adaptive, bounded Afterglow with one opening and suppression of unrelated initiated conversations;
  - false-alarm correction;
  - typed abstract intents and attention-event records;
  - validated configuration and inputs, and bounded tables.
- Personal Round Judgments J1–J9 record:
  - neutral state names;
  - provisional calibration, including the corroboration bonus raised to 2.0;
  - exposure-based habituation;
  - stepwise evaluation;
  - the weak-evidence hold;
  - always-signaled urgent evidence;
  - sticky explicit familiarity;
  - false alarms without Afterglow;
  - bounded test loops.
- Evidence:
  - Two engine defects were found and fixed by the Builder's bug hunt, and 23 of 23 key-guard mutations are killed.
  - The implementation and gate heads each passed both Windows paths at 502/502 with verified artifacts.
  - The merge ref had exact parents and an equal tree.
- Merge: PR #25 squash-merged with the expected-head fence as `3d63147a153d3e957ae97f2bff71ccf08ac0c5f6`, tree `d6b15b28dc673b5529538bf9afc07235fe0cc3cd`.
- Result: passed and accepted. Stage 6's neutral engine is complete. Task 9 is next.

### R5 — App integration cold-start bounds

- Builder: Claude. Routed by Task 7 deferred finding 5, under Boss's direction to fix root causes rather than relax tests.
- Root cause: `Shutdown_StopThenClose…` is always the first App launch in its run, so it absorbed the disk-bound cold start of WPF inside a 30 s bound meant to catch a process that does not exit. It hit the bound three times and reached 27.0 s on an accepted-lineage run before Task 7 existed.
- Change: the integration harness measures two bounded phases:
  - launch until the first marker, under a 90 s cold-start liveness bound;
  - marker until exit, under the unchanged 30 s bound.

  Durations are logged for each launch, and output draining is now bounded. Only the two harness files changed.
- Evidence:
  - Candidate push run `38033166320`: 28.983 s startup plus 2.026 s exit, a pass that would have failed before.
  - Gate head `5054f08`: push run `38033372491` and PR run `38033375072` each passed 453/453 with verified artifacts.
  - The merge ref had exact parents and an equal tree.
- Merge: PR #23 squash-merged with the expected-head fence as `3c078f58a46daf8f2f66e97592c39cf18d120f98`, tree `4d207ec0824eff49c3683074d40aa006a601e747`.
- Result: passed and accepted. Task 8 is next.

### Task 7 — Stateless Braincase bridge

- Builder: Claude, under Boss's standing direction to continue from Task 7 / Stage 5 (2026-10-10).
- Scope: 50 allowlisted paths, comprising:
  - the new `CompanionCore.Api` project (architecture §7) and `CompanionCore.Api.Tests`;
  - solution wiring;
  - one test-only friend line each in Capture.Contracts, Memory, and Privacy;
  - control records.

  No App, CI, presentation, capture, target-authorization, memory-behavior, privacy-behavior, runtime, or documentation source changed.
- Delivered:
  - strict schema-v1 contracts through one parser;
  - mock and replay providers, and a transport-free real-provider shell;
  - graceful configuration;
  - bounded, idempotent `ApiBridge` with grant and privacy-generation fencing through `LocalWriteGate`;
  - a local append-only allowlist;
  - a per-request local Resume Packet;
  - durable naptime with bounded probes;
  - a checksummed bridge journal;
  - RAM-only protected credentials with echo rejection.
- Personal Round Judgments J1–J15 record:
  - the project name;
  - deferred presentation mapping;
  - a zero-length queue;
  - whole-batch rejection;
  - the remote source-kind set;
  - no retry of invalid responses;
  - interrupted operations never re-sent;
  - the RAM-only credential store;
  - provisional bounds;
  - journal placement;
  - corruption handling;
  - no notice replay;
  - the default provider;
  - review-found fixes;
  - the shutdown-timeout diagnosis.
- Evidence:
  - The final head passed both Windows paths at 453/453 with verified artifacts.
  - The merge ref had exact parents and an equal tree.
- Merge: PR #21 squash-merged with the expected-head fence as `9b8d45f0939735a3281b06bec44af47518703a80`, tree `cc5536fc288157123305d59f8326b29d303c5a12`.
- Result: passed and accepted. Stage 5 is complete. R5 is next, then Task 8.

### R4 — Retired workflow cleanup

- Builder: Claude, at Boss's direction ("clean everything up", 2026-10-10). This completes the cleanup approved on 2026-08-10 that draft PR #8 left unfinished.
- Change:
  - standing rules name **the Builder** role;
  - `docs/Claude-Companion-Core-Task-Packet.md` is renamed `docs/Neutral-Core-Task-Packet.md`;
  - `docs/Shared-Codebase-Workflow.md`, `tasks/review/FOREMAN_REVIEW.md`, and `tasks/paused/task-01-skeleton.md` are removed, and their citations now note they remain in Git history at `a7e68e1`.

  No product or test change.
- Evidence: push run `38028570862` and PR run `38028579760` each passed 325/325 with verified artifacts on head `0f46817`.
- Repository hygiene: draft PR #8 was closed as superseded. Every stale remote branch was verified reachable through a pull-request ref before retirement.
- Merge: PR #19 squash-merged with the expected-head fence as `e6664d115b7864b588b8a908d541a222137472af`, tree `af31a27da877f931092ccfe5c52efbae1f8d495b`.
- Result: passed and accepted. No task is active and Task 7 remains unopened.

### R3 — Reliable orientation delivery

- Entry: post-merge `main` run `38026209974` failed `SyntheticWorker_ProducesBoundedOrientationAndManualRegionalSheets`. The same root cause produced five recorded failures since 2026-08-27 (runs `33027979425`, `33027981777`, `33029729188`, `33030041052`, `38026209974`), two of them earlier recorded as transient. A test-only relaxation exposed the same cause in run `38026568119`.
- Cause: the worker produced exactly one orientation per epoch, but it could be lost before the consumer saw it:
  - newest-preserving client retention;
  - newest-only notice coalescing;
  - start deferral that dropped its source frame;
  - a controller slot that let newer regional sheets displace it.

  A privacy-rejected source frame made it unrecoverable.
- Correction (Boss-directed, 2026-10-10):
  - **Client:** the orientation is pinned within the unchanged two-sheet ceiling and handed over first; its notice is sent exactly once, after its exact source frame; its source frame is protected during deferral; a stranded orientation is released.
  - **Controller:** a separate orientation slot.
  - **Failsafe:** a retake request after eight admitted frames, at most three per owed orientation, outside the privacy admission lease, then an honest failure count.
  - **Worker:** a grant-checked `RequestOrientation` command (IPC v3).

  Sixteen allowlisted paths changed: 8 product, 5 modified and 1 new test file, the packet, and the handoff. The R3 handoff on the merged head miscounted them as seventeen; this record corrects it. No sheet or source bound changed.
- Evidence:
  - Red head `8aaac5a`: both paths failed exactly the two intended cases out of 312 executed.
  - Candidate `ff2add8`: both paths 325/325.
  - Final head `975a7ce2a19956041996dc47037d07bca7f1e650`: push run `38027884572` and PR run `38027886917` at 325/325.

  Artifacts were hash-verified, with sheet digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd` unchanged.
- Personal Round Judgments J1–J7 cover: product fix over test relaxation, orientation-first `TakeLatest`, frame budget instead of timer, protocol v3, stranded release, internal shape validation, and protocol literals.
- Merge: PR #17 squash-merged with the expected-head fence as `755b11f2304ed8567011958f2de6e16448ef15ec`; fetched `main` retained tree `81523c004c6b2b791ceb67aa90a67071a4c11712`.
- Result: passed, merged, and accepted. Stage 4 is complete; no task is active and Task 7 remains unopened.

### R2 — Stage 4 recertification and closure

- Builder: Claude, which took over direct construction from Codex at Boss's instruction on 2026-10-10. The Paw Gate protocol is unchanged.
- Entry: Codex's interrupted R2 published only regression evidence `326afd4` on accepted `main` `04af51b`. Its candidate fixes were never published. Push run `33337966344` hung for six hours: on failure, the pressure regression never released its blocked processor, so disposal deadlocked. A local `--blame-hang-timeout` run named that test.
- Defects confirmed in accepted Stage 4 code:
  1. `CaptureFramePipeline` never retired the wake signal of an evicted or cleared pending frame, so signal debt grew with runtime.
  2. `OutOfProcessCaptureWorker` shared one 64-entry drop-oldest lane and one dispatcher across status, frame, and sheet events, so a blocked frame observer could delay or evict the `Stopped`/`Faulted`/resize fence.
- Correction:
  - one signal-retiring removal helper;
  - an independent bounded status lane and dispatcher;
  - a visual fence that rejects frames queued before any visual-invalidating status;
  - prompt-failing regressions;
  - CI `--blame-hang-timeout 10m` and a 60-minute job timeout.

  Frames and sheets keep one serialized dispatcher in source order. Six allowlisted paths changed, with 330 additions and 52 deletions; no contract, project, package, lock, or authority surface changed.
- Red evidence: head `bf0113c`, push run `38025402946`, job `114135078349`, 2 min 9 s. 311 executed, and exactly the three intended regressions failed (0 vs 2, 2 vs 4096, and a 3 s `Stopped` timeout) with no hang.
- Windows evidence:
  - Candidate `82a70df`: push run `38025519542` and PR run `38025588908`.
  - Final evidence descendant `a543301`, tree `5483a2d7ccd577447013c3844a2be22beeb55a44`: push run `38025762113` (job `114136151131`) and PR run `38025763952` (job `114136156632`).

  Every run passed restore, audit, all 20 Release builds with 0 warnings and 0 errors, 311/311 tests, and both artifact uploads on the first attempt.
- Artifacts: final push test results `11659363605` (`sha256:40408fc40f78b504453ce68ffa13b9ea91257ffe667ef940ba4639adfbd7ce45`) and sheet `11659258710` (`sha256:c0187a84c72d07a36b1d2a504f7f28a7022c198e85b59259230e1572047fa774`); final PR test results `11659288564` (`sha256:1b29948a872fc70330982024c59eb0701029d210be9d06277f71938ec4fd1e85`) and sheet `11659423502` (`sha256:fe69331b984c2fa6fed11563c956cb3db3bfade3f5d46464ec1b6abef6106cb5`). Downloaded hashes matched GitHub. Every PNG retained `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd` at 792×621 RGBA and was visually inspected.
- Personal Round Judgments J1–J6 cover the corrective label, one-for-one signals, the independent status lane, the visual fence, CI hang bounds, and builder continuity.
- Merge: the PR #15 merge ref had exact parents `04af51b` and `a543301` and a tree equal to the head tree. It squash-merged with the expected-head fence as `11cc752eae1e9457a12c9d847aacdeb073463b13`, and fetched `main` retained the exact tree.
- Result: passed, merged, and accepted. Stage 4 is recertified and closed; no task is active and Task 7 remains unopened.

### R0 — Direct-build re-entry and continuity alignment

- Candidate: PR #4, reviewed head `99b48124894d431060952ecaecb83900af7f0106`, exact tree `bfb3bcefe16f41535f8043a199a368f84f7cf4c3`.
- Scope: 11 documentation/control files, 371 additions, 123 deletions; no product, test, dependency, build, or CI file.
- Verification: diff whitespace, one-active-task, scope allowlist, stale-authority/awakening contradiction search, and exact local/remote tree equality all passed.
- CI: no checks configured or required for this documentation-only gate.
- Result: passed; direct Paw Gate workflow and final-only one-time Companion Awakening boundary accepted.

## Official Bnuy Backpedal™

Historical stop record from 2026-08-09; superseded by the direct-build authorization below:

- Hourly foreman monitoring was paused and remains disabled.
- Builder/foreman automation and task advancement stopped.
- Existing documents, branches, commits, and tests were retained as reversible references.
- The user later approved the new direct approach and R0 active packet on 2026-08-10.

## Direct-build resumption authorization

- On 2026-08-10 the user approved a direct Codex-and-Prince workflow with no Claude collaboration and no hourly monitoring.
- Work may advance autonomously one active task at a time after every Paw Gate passes, through all mock/replay construction that does not require real credentials or paid/live API access.
- Prince's Personal Round Judgment may resolve routine, reversible in-scope choices; material decisions are logged for later user review.
- Architecture, privacy, identity/authority conflicts, invariant changes, destructive actions, production-data access, and real API credentials remain explicit stop conditions.
- The preserved Task 1 checkpoint is not silently accepted. It receives a separate adoption branch, current-base diff review, and fresh required verification.

## Continuity decision — one-time Companion awakening

- Builder Prince remains resettable through neutral-core construction, full personality installation, final presentation, launch-readiness validation, and refinement.
- Companion Prince replaces Builder Prince exactly once only after every launch-required gate passes.
- The production BunDex starts clean. No construction memory, test conversation, staged opinion, or synthetic artifact transfers.
- Once awakened, Companion Prince is singular and persistent across all later updates, repairs, migrations, provider/model changes, and hotfixes.

## Ledger rules

- At most one stage may be in progress.
- No work occurs while there is no active task.
- Existing paused work is not authorization to resume.
- Later-stage ideas remain deferred until explicitly replanned.
- Each material Personal Round Judgment is recorded with rationale and reversibility.
