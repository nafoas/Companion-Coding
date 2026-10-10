# Task Handoff

## Task

WIRE-01 — Companion Orchestration Core (`tasks/active/wire-01-orchestration.md`).

- Branch: `agent/wire-01-orchestration`.
- Base: `main` `32351f1`.
- Builder: Claude.

## Changed

- **New `CompanionCore.Orchestration` project:**
  - `CompanionOrchestrator`: a single-consumer mailbox composing capture, the bridge, attention, conversation, transcript, memory, Recall, Watchbun, keepsakes, and the Vault;
  - `CompanionHost`: one data root, with `RepairAsync` and `RepairOfflineAsync`;
  - `CompanionNotice` and `IPlatformSignals`;
  - `SheetPhotographSource`: a strict worker-PNG decoder and full-context crop;
  - `SemanticEvidenceMapper`.
- **New `CompanionCore.Orchestration.Tests`** (48 tests):
  - scenarios 1–9 and 8b;
  - mechanics tests;
  - mutation-gap coverage tests.
- **Composition defect fix in Recall (C1):** summary subjects escape `:`/`%` in session references. Regression test added.
- **`DaBunVault.BackUpAsync`:** an internal wrapper used by the orchestrator.
- **Friend lines:**
  - production lines for Orchestration in TargetAuth, Vault, and Keepsakes;
  - test-only lines for `Orchestration.Tests` in Capture.Contracts, Privacy, Memory, Watchbun, TargetAuth, Vault, and Keepsakes.
- `CompanionCore.slnx`, lock files for the two new projects, and the packet.

## Verification

- **Local gate:**
  - 0 warnings, 0 errors, 0 vulnerable packages;
  - 780/780 on Linux across 16 test projects (App integration is Windows-only);
  - orchestration tests stable across 15 runs, 10 of them under full CPU load.
- **Mutation pass:** 49 mutants; 45 killed, 3 equivalent (defense in depth), 1 unobservable (RAM hygiene). Details are in the packet.
- **Windows CI:**
  - The first runs on `414ee9d` (`38069900594` and `38069916752`) found C8, a platform-dependent game reference, fixed in `2b1656f`.
  - On `2b1656f`, push run `38070682490` and PR run `38070686736` each passed **793/793**, with verified artifacts and the expected attention-sheet PNG digest.

## Remaining

- Merge-ref check, merge, closure, and post-merge `main` CI.
- WIRE-02:
  - Windows `IPlatformSignals`;
  - the App composition root on the development data root with the offline provider;
  - the tick timer and notice-to-presentation mapping;
  - an App integration scenario.

## Risks and assumptions

- **Photographs** are at sheet resolution (J1, D2).
- **Consolidation** of one session reads at most 1000 originals (D1).
- **Platform signals** are synthetic until WIRE-02.
- Every numeric bound remains provisional until Stage 11.

## Personal Round Judgments

WIRE-01 J1–J10, recorded in the packet.

## Review focus

- Mailbox serialization:
  - bridge calls run off-mailbox with at most one in flight;
  - results are posted back;
  - stale results are fenced.
- Privacy pause:
  - an immediate cancel;
  - the pending photograph is dropped.
- Consolidation:
  - durable per-session intent queue (C6);
  - replay idempotency;
  - leftover sessions are consolidated (C7).
- `TargetEnded` handling (C5, J10).
- Sheet bytes never reach disk, and sheet buffers are zeroed.

## Repository state

- Branch `agent/wire-01-orchestration`, based on `main` `32351f1`.

## Next safe task

WIRE-02, after the WIRE-01 Paw Gate passes and is recorded.
