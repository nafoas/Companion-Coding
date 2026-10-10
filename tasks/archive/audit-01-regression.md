# AUDIT-01 — Regression Audit of Every Accepted Task

Status: **accepted** — Paw Gate PASS; PR #43 squash-merged as `c1787e03070588e7a2d46af1dda95f42ccaff332`
Authorized: 2026-10-10 by Boss ("…Test every task previously and make sure it all works fine, bugfix, and then let's roundtable again").
Accepted remote base: `bf25765a816ad29a08b6a200ef64d6b8be078dec` (after WIRE-02 closure)
Working branch: `agent/audit-01-regression`
Builder: Claude

## Objective

Re-verify every accepted task against the current, fully wired `main`. The audit:

- re-runs every suite repeatedly under load to expose timing flakes;
- replays every stored mutation pass to prove no suite has lost strength;
- adds a first mutation pass over the Tasks 1–6 invariants;
- closes every test gap found. Product code changes only if a real defect appears.

## Method

1. **Stress.** The full suite ran 5 times over all 17 Linux-runnable test projects (85 project runs) with every core saturated by CPU load.
2. **Mutation replay.** Every stored mutation set was replayed against current `main`:
   - Task 7: 15 mutants;
   - Task 8: 23;
   - Task 9: 30;
   - ERPP-01: 18;
   - Task 10: 65;
   - Task 11: 64;
   - KEEP-01: 52;
   - KEEP-02: 37;
   - WIRE-01: 48;
   - WIRE-02: 34.

   Each surviving mutant was compared with its packet's recorded equivalents.
3. **First invariant pass for Tasks 1–6.** 34 new mutants:
   - append-only write gate and generation-bound admission;
   - operation idempotency and conflict;
   - proposal validation bounds;
   - data-root guards and the repair marker;
   - the privacy lease and generation;
   - single-target, consent, denial, and display preflight;
   - frame admission;
   - browser and sensitive-category denial;
   - stored-policy matching;
   - the RAM-only frame ring;
   - single runtime construction.

## Results

- **Stress:** 85/85 project runs green, 0 failing tests.
- **Mutation:** 420 mutants in total; **403 killed**, 16 equivalent, 1 unobservable.
- **Replayed sets:** every replayed set matched its accepted record exactly. Each survivor was one of that packet's recorded equivalents:
  - Task 10: 1;
  - Task 11: 1;
  - KEEP-01: 4;
  - KEEP-02: 3;
  - WIRE-01: 3 equivalent and 1 unobservable.

  There were two exceptions, both found and fixed by this audit (gaps below).
- **No product defect was found.** Every gap was a missing or nondeterministic test.

## Gaps found and closed (tests only)

- **A1 — Task 2, memory validation.** Not pinned by any test:
  - the per-operation record bound (128);
  - duplicate record ids within one operation;
  - a record linking to itself.

  New: `LocalWriteGateTests.StructuralLimits_AreEnforcedBeforeDurability`, which also proves exactly 128 records commit.
- **A2 — Task 4, target authorization.** Not pinned:
  - eight of the eleven sensitive-by-default categories;
  - the first-line browser-filename denial, which a validly checksummed stored entry for `chrome.exe` with an incorrect category would otherwise bypass;
  - stored entries applying only to the same executable name.

  New: `EverySensitiveCategory_IsDeniedByDefault` (11 cases), `OrdinaryCategories_AreAskedNotDenied`, `AStoredStandingEntryForABrowserFilename_IsNeverHonored`, and `AStoredEntry_AppliesOnlyToTheSameExecutableName`.
- **A3 — Task 5, capture.** The RAM-only `ByteBoundedFrameRing` had no direct test. Its oversize refusal, frame-count eviction, and byte-bound eviction all survived mutation. New: `ByteBoundedFrameRingTests` (4 tests).
- **A4 — Task 7, Braincase.** The raw-response credential-echo check survived. The decoded-text check covers summaries, labels, and proposal text, but not fields such as `sourceKind`. New: `ASecretEchoedInAFieldOutsideTheDecodedText_StillInvalidatesTheWholeResponse`.
- **A5 — WIRE-01, a nondeterministic kill.** The sheet-burst test killed the single-flight mutant in only 3 of 4 runs: a 20-sheet burst could collapse into the latest-sheet slot before the guard mattered. The test now holds the first call in flight and delivers each later sheet through the mailbox. It kills the mutant 5 of 5 times and stays stable across repeated runs.

## Equivalent mutants (recorded, not weakened)

These are defense-in-depth checks that a deterministic test cannot isolate without weakening the design.

- **Tasks 1–6:**
  - the development/production root-distinctness guard (the namespaces are fixed and distinct);
  - session-generation frame matching (always equal to the current privacy generation while authorized, which is checked first);
  - the frame admission lease (the policy check re-verifies the generation; the lease only closes a race window).
- **Task 7:** the post-response admission lease. The commit is fenced again by the write gate's generation-bound admission, and publication re-checks the generation. No sheet reaches the provider during a pause: the grant check and the per-attempt generation check both stop it.
- **Earlier packets:** the replayed equivalents in Task 10, Task 11, KEEP-01, KEEP-02, and WIRE-01 are unchanged from their records.

## Allowed change scope

- Tests in:
  - `tests/CompanionCore.Memory.Tests`;
  - `tests/CompanionCore.TargetAuth.Tests`;
  - `tests/CompanionCore.Capture.Worker.Tests`;
  - `tests/CompanionCore.Api.Tests`;
  - `tests/CompanionCore.Orchestration.Tests`.
- Control records.

There is no product change.

## Paw Gate

Gate result: **PASS** on 2026-10-10.

- **CI.** Head `8f02306`: push run `38084310013` and PR run `38084321751` each passed **929/929**.
- **Artifacts:**
  - test-results and attention-sheet archive digests verified;
  - attention-sheet PNG: 792×621, `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- **Merge.**
  - The merge ref had exact parents `bf25765` (main) and `8f02306` (head), and a tree equal to the head tree `74c6421`.
  - It was squash-merged through the expected-head fence as `c1787e0`.

Local:

- Locked restore and a `--no-incremental` Release build with `/warnaserror`: 0 warnings, 0 errors. 0 vulnerable packages.
- 914/914 on Linux.

## Personal Round Judgments

- **J1 — Test-only remedy.** Every gap was closed with a test, not by changing product code, because no product behavior was wrong.
- **J2 — The stored browser entry is forged through the store's own writer.** It models an older build or a hand edit that passes the checksum. The test proves the first-line filename denial is load-bearing.

## Deferred findings

None.
