# Task Handoff

## Task

DEF-01 — Long-Session Consolidation and Single-File Vault Export (`tasks/active/def-01-consolidation-and-export.md`). These are the smaller deferred items, part 1.

- Branch: `agent/def-01-deferred-items`.
- Base: `main` `63c38cf`.
- Builder: Claude.

## Changed

- **Memory.** Added `MemoryRepository.RetrieveSessionPageAsync`, a read-only keyset page over one session in record-id order, with 1 test.
- **Orchestration.** `ConsolidateAsync` now:
  - streams every original of the session;
  - commits the consolidation in parts that each fit one atomic append;
  - gives each part a stable operation id, where part 0 keeps the intent's id.

  Added `PlannedRecords` and `PartOperationId`.
- **Vault.** New `VaultExport`, which:
  - validates both archives;
  - wraps them unchanged with a checksummed manifest in one `.zip`;
  - writes atomically and never overwrites;
  - verifies the result.
- **Host.**
  - `CompanionHost.ExportVaultAsync(path)` takes a fresh backup and then exports.
  - `CompanionHost.VerifyVaultExportAsync(path)`.
- **Tests:**
  - `OrchestrationLongSessionTests` (4);
  - `OrchestrationVaultExportTests` (5);
  - `MemoryPagingTests` (+1).

## Verification

- **Local:**
  - 0 warnings, 0 vulnerable packages;
  - full gate green on Linux;
  - Orchestration 81/81;
  - Memory 89/89.
- **Mutation:** 24 mutants: 19 killed and 5 equivalent (defense in depth), recorded in the packet.
- **Windows CI:** pending (expected about 977).

## Remaining

- CI, merge, and closure.
- Then DEF-02 (full-resolution photographs).
- Stop before Stage 12.

## Risks and assumptions

- An export older than the live journal is refused by Bnuy Repairs. Importing it would accept losing later memories, and that is Boss's decision (D1).

## Personal Round Judgments

DEF-01 J1–J2, recorded in the packet.

## Review focus

- A replay of the parts appends nothing.
- Every original is consolidated exactly once.
- The export never overwrites, and never writes inside the data root.

## Next safe task

DEF-02 — Full-resolution photographs.
