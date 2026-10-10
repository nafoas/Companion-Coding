# Task R3 — Stage 4 Orientation Regression Race

Status: **Active**
Accepted base: `2b5ef836183dde261d52bfa2f6c68340aff2e9f3`, tree `fb6e016a585effede01cb42e2f368499f2546a76`
Roadmap slice: Stage 4 corrective verification only; Task 7 remains unopened
Builder: Claude

## Objective

Remove a timing assumption from one accepted Stage 4 regression so that `main` is reliably green, without changing product behavior or any acceptance scenario.

## Entry evidence

- Post-merge `main` run `38026209974` on the docs-only R2 closure head `2b5ef83` failed exactly one case, `SyntheticWorker_ProducesBoundedOrientationAndManualRegionalSheets`: `Assert.Equal() Failure — Expected: Orientation, Actual: Regional` at line 247. The other 310 tests passed, and the attention-sheet digest was unchanged.
- The same case failed with the identical message on 2026-08-27 in run `33027979425`, on Task 6 code that predates R2. Its sibling `StopAndRestart_ClearQueuedSheetsAndCreateFreshOrientation` failed the same way in run `33027981777`. The Task 6 packet records correcting that sibling assumption ("a restart test that expected an older orientation from a newest-preserving queue"). This case was missed and never recorded in the Ledger.
- Mechanism: the worker emits exactly one orientation sheet, then a regional sheet for every changed frame (every 10 ms with the synthetic source). `TakeLatestAttentionSheet` disposes every older retained sheet and returns only the newest, and sheet notifications coalesce to the newest (Task 6 J9). After the first notification, whether the newest retained sheet is still the orientation depends only on timing. R2's diff does not touch sheet retention, which the reader thread performs.

## Required correction

1. Replace the assertion that the newest retained sheet is the orientation with assertions of guaranteed behavior: every delivered notification matches the grant and follows its source frame; the retained sheet matches the grant, is no older than the first notification, and decodes to its own metadata geometry.
2. Keep the exact worker-side proof of Task 6 acceptance scenario 1 (`ProducedOrientationSheets == 1`) and every manual-region, bound, and clear assertion unchanged.

## Invariants

- No product, contract, workflow, project, package, or lock change.
- No acceptance scenario is weakened. Scenario 1 (exactly one orientation with matching metadata) is proven by the exact worker metric. Orientation pixel layout is proven deterministically in-process (`Orientation_IsLosslessInspectablyLabeledAndBounded`) and by the CI artifact. Scenario 8 (newest work survives) is the contract the old assertion contradicted.

## Acceptance scenarios

1. The corrected case cannot fail on the order in which orientation and regional sheets are retained, and still fails on a foreign-grant sheet, on a sheet dispatched before its source frame, on a sheet older than the first notification, on undecodable payload geometry, on a missing manual-focus regional sheet, or on more or fewer than one orientation.
2. All 20 Release projects build with zero warnings and errors; both exact Windows event paths pass 311/311 with both artifacts; the attention-sheet digest is unchanged.

## Allowed paths

- `tests/CompanionCore.Capture.Worker.Tests/OutOfProcessCaptureWorkerTests.cs`
- `README.md`, `BUILD_LEDGER.md`, `tasks/review/HANDOFF.md`
- `tasks/active/task-r3-orientation-test-race.md`, `tasks/archive/task-r3-orientation-test-race.md`

## Paw Gate

R3 passes when the actual diff touches only the allowed paths, the corrected assertions are reviewed against Task 6 scenarios 1 and 8, both exact Windows event paths on the final head pass 311/311 with verified artifacts, the merge uses the expected-head fence, and post-merge `main` CI is green.

## Personal Round Judgments

- **J1 — Test assumption, not product defect.** Newest-preserving retention and notification coalescing are accepted Task 6 behavior (scenario 8, J9). The correction follows the accepted Task 6 precedent for the sibling restart case. Reversal: if Boss decides observers must always receive the orientation, restore the strict assertion together with the product change described under Deferred findings.

## Deferred findings

- **Orientation delivery is best-effort.** Because client retention and notifications coalesce to the newest sheet, the single orientation sheet can be superseded by a regional sheet before any observer takes it. Stage 4 requires production, not delivery, so this does not violate Stage 4. Stage 6 ("after consent… one high-fidelity orientation frame… proposes normalized focus regions") will need the orientation reliably. Before Stage 6 depends on it, a bounded packet should either retain the newest orientation separately from regional sheets or have consumers request a fresh orientation. That changes accepted judgment J9 and needs its own gate.
- `Restart_EmitsFreshOrientationAndRetainsOnlyFreshEpochSheets` waits for an orientation notification, which the same coalescing could in principle skip. It passed on every recorded run, so it is left unchanged and is to be revisited with the orientation-delivery packet.
