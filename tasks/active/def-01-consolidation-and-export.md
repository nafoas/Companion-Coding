# DEF-01 — Long-Session Consolidation and Single-File Vault Export

Status: **active — gate review**
Authorized: 2026-10-11 by Boss ("Once that is done, do the smaller deferred items, and then stop before stage 12").
Accepted remote base: `63c38cfa30300a81279f0de319223b98c0d551bf`
Working branch: `agent/def-01-deferred-items`
Builder: Claude

## Items

### WIRE-01 D1 — Long sessions

Before this packet, a session's consolidation read only its first 1000 originals. One hidden failure was worse: a session whose highlights pushed one consolidation past the planner's 128-record atomic bound made the planner throw on every attempt. That consolidation could never complete, and replayed and failed on every start.

Now:

- **Paging.** A new read-only keyset page, `MemoryRepository.RetrieveSessionPageAsync`, enumerates every record of one session exactly once, in record-id order.
- **Parts.** The orchestrator streams the session page by page and commits consolidation in parts, each within one atomic append (a summary per 256 sources plus each verbatim highlight, at most 128 records).
- **Idempotent replay.** Part 0 keeps the intent's operation id, so earlier intents and records stay valid. Later parts derive stable ids from it. The partition is a pure function of the session's originals, so a replay reproduces every part exactly and appends nothing.

### KEEP-02 D1 — Single-file Vault export

`CompanionHost.ExportVaultAsync(path)` produces the export, serialized with every other Vault operation through the orchestrator:

1. It takes a fresh Vault backup.
2. It re-validates both archives exactly as a repair would read them.
3. It wraps them unchanged, with a checksummed manifest, into one `.zip` at an absolute path outside the data root.
4. It writes atomically, refuses to overwrite an existing file, and verifies the result.

`CompanionHost.VerifyVaultExportAsync(path)` verifies an export without opening any data root.

## Evidence

- **Memory:** `SessionPaging_EnumeratesEveryRecordOfOneSessionOnce_AndNothingElse`.
- **Orchestration — `OrchestrationLongSessionTests`:**
  - 1500 originals, about 193 of them highlights: every original is summarized exactly once and every highlight is kept verbatim, across several parts;
  - a replay appends nothing;
  - a refused part stops the consolidation and leaves the intent queued (nothing past it is committed);
  - the part arithmetic and part ids.
- **Orchestration — `OrchestrationVaultExportTests`:**
  - an export is one verified file whose inner archives are byte-identical to the Vault's;
  - an export restores this Prince's BunDex and photographs after his local Vault is lost;
  - an export older than the latest backup is refused by Bnuy Repairs rather than dropping later memories;
  - unsafe destinations (relative, non-zip, inside the data root, existing) are refused, and an existing file is never overwritten;
  - tampered, extra-entry, unknown-version, foreign, garbage, and missing exports fail verification with a validation error.
- **Memory paging** uses varied confidence, so its relevance order differs from its record-id order.

## Mutation pass

24 mutants across the orchestrator, `VaultExport`, and session paging:

- **19 killed.** Four of these first survived. Each got a test (a refused part, extra entries, the manifest version, page ordering) and was then killed on rerun.
- **5 equivalent under every reachable input (defense in depth):**
  - **The existing-destination check and `overwrite: false`.** Each guard covers the other, and `overwrite: false` also covers a check-then-move race.
  - **The verify-before-move pass.** It catches corruption between the write and the move.
  - **Temp cleanup.** It runs only on failure paths after the temp file exists.
  - **The pre-export memory-archive validation.** A fresh backup is always taken just before it.

## Personal Round Judgments

- **J1 — Record-id partition.** Paging and partitioning in record-id order keeps memory bounded to one page plus one part, and makes replays exact.
- **J2 — Export only; import is Boss's decision.** The export is a verified point-in-time copy of this Prince's Vault, not a second Prince. Restoring an export older than the live journal's cut is refused by the accepted Task 3 repair rule, because it would drop later memories. Whether Prince should ever accept such a loss (a disaster with no local data left) is a policy decision for Boss, under the deferred export/migration item (Paw Pile 7).

## Paw Gate

Pending CI. Local: full gate green (0 warnings, 0 vulnerable packages); Orchestration 81/81, Memory 89/89.

## Deferred findings

- **D1 — Importing an older export.** Disaster recovery from an export older than the live journal (accepting the loss of later memories) needs Boss's decision (Paw Pile 7).
- **D2 — Full-resolution photographs (WIRE-01 D2).** These are DEF-02, a capture-protocol change.
