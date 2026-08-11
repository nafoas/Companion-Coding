# Task 3 — Atomic Backup and Repair

Status: **Accepted and squash-merged to `main` as `f685dd2023a5844309c5b5fb7d0abd1bf54406b9`**
Authorized: 2026-08-10 under the accepted direct-build workflow and Boss-approved Stage 2 split
Accepted remote base: `44caa2fc6474b0952eaed5f086bfb3c49bf73c18`
Accepted base tree: `2423473e0a0fb915bf40c2554947f7166cd7d9f4`
Working branch: `agent/task-03-vault-repair`
Reviewed remote head: `d4d7bddb35ae1a2b8f3b2fdb47e28d74322ef83a`; exact tree `324874f5b7c2d274732ff82c91aa9631fa587b57`; Windows CI and the distinct code/invariant/diff Paw Gate passed. The unchanged gate-record descendant was squash-merged through PR #7 and independently rechecked after merge.

## Objective

Make the accepted append-only memory spine recoverable without weakening its live write boundary: create one validated atomic backup archive at an exact journal cut, rotate only the covered journal prefix, and restore a damaged development/test store while preserving the damaged source and replaying every valid post-cut append.

This remains neutral Builder infrastructure. It uses only synthetic development/test roots and a neutral internal archive filename. It does not create or open a production BunDex, expose repair to automated/API code, add personality presentation, or wire persistence into the WPF app.

## Entry evidence and authority resolution

- Task 2 passed its distinct Paw Gate with 94/94 Windows tests and was squash-merged to `main` as `44caa2fc6474b0952eaed5f086bfb3c49bf73c18`.
- The accepted architecture §5.2 requires SQLite's online backup mechanism, a momentary serialized cut, source/snapshot health validation, atomic promotion, and rotation only through the promoted cut.
- The master packet assigns one complete archive, checksummed manifest, damaged-source preservation, repair, and valid post-backup replay to Task 3.
- The core-only boundary forbids themed/final labels. The shipping presentation may later render the accepted user-facing Vault name, but this task's filesystem artifact is the neutral `memory-vault-v1.zip`.

## Required implementation

### Fixed non-production backup boundary

- Derive backup, staging, and damaged-source-preservation locations only from a validated development/test `MemoryStoreLocation`; ordinary callers receive no raw-path backup, restore, import, or production-root surface.
- Use one fixed promoted archive name, `memory-vault-v1.zip`, physically under the validated environment root. Temporary files use unique names in the same filesystem so promotion can be atomic.
- Archive construction and repair operate only on synthetic development/test data. No `Production` enum value, location factory, fallback, import, or open capability may be added.
- Staging cleanup may remove only task-owned unpromoted temporary files. It may never delete a promoted archive, committed database, live journal, or damaged-source preservation bundle.

### Exact backup cut and online snapshot

- Establish a `cutSequence` while holding the one serial writer only long enough to prove the journal has no unresolved live tail, read the highest committed SQLite/journal sequence, and establish a stable SQLite read snapshot at that cut.
- Release the writer before copying pages. Use `SqliteConnection.BackupDatabase` (the supported SQLite online backup path), never a raw copy of the live WAL database.
- Writes after the cut must remain available while the snapshot is built and must not enter the cut snapshot. They stay represented by journal append frames above `cutSequence`.
- Run a complete source health check before promotion: SQLite integrity/foreign-key checks, exact schema version/object/column checks, canonical operation envelopes, record/link checksums, and operation/record ownership.
- The archive contains exactly one database snapshot, one canonical versioned manifest, and one checksum entry. The manifest records format version, memory schema version, backup ID, UTC creation time, cut sequence, fixed database entry name, byte length, and SHA-256 digest.
- Validate archive entry names/counts, bounded lengths, manifest canonical form/checksum, database checksum/length, schema version, SQLite health, and that the snapshot's maximum sequence equals the manifest cut before promotion.

### Atomic promotion and journal rotation

- Build and validate a complete temporary archive before touching the current promoted archive.
- Atomically replace the current archive only after validation succeeds. Any cancellation, injected interruption, invalid source/snapshot, checksum failure, or exception before promotion leaves the prior valid archive byte-for-byte unchanged.
- Once promotion begins, ignore caller cancellation until the promotion outcome is known. A promoted valid archive remains authoritative even if later journal rotation fails.
- Rotate the journal only after the corresponding archive is promoted. A versioned checksummed rotation-base frame may cover sequences at or below the cut; every append above the cut and the latest applicable checkpoint remain intact.
- Journal rewrite uses a same-directory durable temporary file plus atomic replacement. Failure must leave either the old valid journal or the complete new valid journal, never a silently accepted partial rewrite.
- A write committed after the cut but before archive validation/promotion must remain recoverable from the rotated journal and must never be sacrificed to the backup.

### Guarded repair and damaged-source preservation

- Repair is implemented behind an assembly-internal, sealed maintenance capability/service with no reference from `LocalWriteGate`, `IAutomatedWriteProposal`, the app, runtime, capture, presentation, or any public API surface.
- Repair requires the live `MemoryRepository` to be closed and proves exclusive local ownership before mutation. An open runtime/repository causes a typed fail-closed rejection.
- Validate the promoted archive fully before changing live files. Arbitrary archive paths, duplicate/extra ZIP entries, traversal names, unsupported versions, and checksum/health failures are rejected.
- Before the first live mutation, preserve the damaged database, WAL/SHM companions if present, and full journal in a unique fixed preservation directory with a canonical manifest, byte lengths, and SHA-256 checksums. Preservation is never overwritten by a later repair.
- Read the live journal through the production frame validator. Retain every valid append above the archive cut, reject non-trailing corruption, and tolerate only the same single torn/checksum-invalid trailing frame allowed by normal recovery.
- Construct a recovery journal anchored at the archive cut with every retained post-cut append and no stale post-cut checkpoint that could hide replay.
- Replace the live database with the validated snapshot and install the recovery journal as one guarded maintenance operation. Once mutation begins, caller cancellation cannot strand an ambiguous half-repair; complete or roll back to the preserved source.
- Reopen through the ordinary Task 2 recovery path so unique operation IDs replay post-cut appends idempotently, then run full health validation. No committed memory may be edited, merged, or selectively discarded.

### Failure bounds and test seams

- All manifest and journal structures are versioned, checksummed, length-bounded, and parsed fail-closed. ZIP extraction is entry-by-entry to task-owned staging; no general-purpose extract-to-directory path is used.
- Deterministic internal test-only fault points may stop backup before promotion or repair before/after replacement. They must exercise the production protocol and expose no public/shipping command-line surface.
- Backup and repair retries are idempotent with unique staging/preservation identifiers. Orphan staging from an interrupted pre-promotion attempt cannot become authoritative.
- No new dependency is added unless the BCL and existing locked SQLite package cannot satisfy a named acceptance requirement; any dependency change requires exact locks and a clean vulnerability audit.

## Explicitly forbidden

Do not implement or introduce:

- a production BunDex opener, production backup/import, Builder-memory transfer, or Companion Awakening behavior;
- a public/general maintenance store, arbitrary SQL/path repair API, automated/API-visible restore capability, record-level update/delete, or bypass around `LocalWriteGate`;
- raw copying of a live WAL database, rotation before archive promotion, time/size-only journal deletion, or dropping any frame above the promoted cut;
- unvalidated ZIP extraction, overwrite of the only valid archive with an invalid candidate, silent recovery from non-trailing corruption, or deletion of damaged evidence;
- API bridges, network calls, credentials, encryption-key handling, model output, capture, authorization, privacy hotkeys, conversation, attention, or semantic retrieval;
- personality voice, themed filesystem/UI labels, final presentation, animation, audio, photographs, or real gameplay/private content;
- schema version 2 or a speculative migration with no accepted target schema; Task 3 provides the guarded maintenance boundary but does not invent a migration.

## Required tests and evidence

The handoff records exact commands/results. Tests use unique temporary roots and synthetic neutral fixtures only.

1. Clean locked restore, clean direct/transitive dependency audit, and Release build on Windows with zero warnings/errors.
2. A healthy store creates one archive with exact entries, canonical/checksummed manifest, schema version, cut, online SQLite snapshot, and independent health validation.
3. A deterministic write after the established cut continues while snapshot validation is paused; it is absent from the cut snapshot, remains above the cut after rotation, and survives restore through journal replay.
4. Corrupt/invalid source or candidate archive cannot replace a previously valid archive; its bytes and digest remain unchanged.
5. Cancellation or injected interruption before promotion leaves the prior archive valid and the committed store/journal untouched.
6. Journal rotation occurs only after promotion, records the promoted cut, removes no frame above it, and reopens with contiguous global sequences.
7. Restore while a repository is open is rejected before preservation/mutation; public API reflection finds no maintenance, restore, raw-path, or production capability.
8. A corrupted live database restores from the promoted snapshot, preserves the damaged source bundle/checksums, replays valid post-cut appends exactly once, and passes full health validation.
9. A tampered manifest/checksum, duplicate/extra/traversal ZIP entry, unsupported format/schema, or non-trailing corrupt journal fails closed without changing live data.
10. Repair cancellation before mutation changes nothing; cancellation/fault after mutation begins completes or restores the preserved live source rather than leaving an ambiguous mix.
11. Repeating backup/repair does not duplicate committed records, overwrite earlier damaged-source evidence, or allow cleanup to touch committed memory.
12. All 94 accepted Task 1/2 tests pass alongside the focused Task 3 suite.

## Allowed change scope

- `src/CompanionCore.Memory/**`;
- `tests/CompanionCore.Memory.Tests/**`;
- `CompanionCore.slnx` and the two memory project/lock files only if a named Task 3 requirement genuinely needs dependency/build integration;
- `BUILD_LEDGER.md`, `README.md`, this task/archived packet, and `tasks/review/HANDOFF.md` for control/gate records.

Do not modify Task 1 source/tests, non-memory projects, CI semantics, the Design BunDex, roadmap, neutral-core master packet, accepted architecture, direct workflow, prior archived/paused packets, or unrelated files.

## Paw Gate

Task 3 passes only when:

- the implementation proves the exact cut → online snapshot → validate/promote → rotate protocol and preserves every post-cut append;
- repair is structurally maintenance-only, requires quiescence, preserves damaged evidence, and routes replay through the accepted Task 2 recovery path;
- archive/journal replacement and cancellation behavior are atomic, bounded, and fail-closed under every named interruption/tamper test;
- exact diff review confirms no production opener, automated maintenance path, Task 4+ behavior, personality, or private fixture;
- fresh exact-candidate Windows CI passes locked restore, audit, Release build, all 94 regressions, and the complete Task 3 suite;
- limitations and Personal Round Judgments are recorded before Task 4 becomes active.

## Personal Round Judgment log

### J1 — Tree-identical local base after Git transport denial

Remote Task 2 was squash-merged to `main` as `44caa2fc6474b0952eaed5f086bfb3c49bf73c18`. While Git transport was unavailable, local work began from Task 2 acceptance commit `bc3c34f`, whose tree `2423473e0a0fb915bf40c2554947f7166cd7d9f4` exactly matched accepted remote `main`. Once transport returned, the Task 3 commits were replayed onto the actual merged SHA; code checkpoint tree `e8bd0811f2b6ccd93892a7eec97778fa9e9fcaca` remained identical. The surrogate branch stays as non-authoritative recovery history.

### J2 — Neutral internal archive filename during core construction

The roadmap's eventual user-facing name is personality presentation, while `AGENTS.md` and the neutral-core packet prohibit themed/final labels during core construction. Task 3 therefore uses the fixed internal filename `memory-vault-v1.zip`; a later accepted personality/presentation adapter may render the intended Vault wording without renaming or changing the recovery protocol. Reversing the displayed label later requires no data migration.

### J3 — Explicit synthetic archive bounds

The canonical manifest is bounded to 16 KiB and both the SQLite snapshot and complete archive are bounded to 8 GiB. Task 3 needs fail-closed lengths now but has no accepted production sizing evidence, so these are conservative non-production safety caps rather than a final retention policy. They are centralized version-one format constants and can be changed through a later migration/compatibility decision without weakening committed-memory invariants.

### J4 — One cooperative ownership lease for runtime and maintenance

Ordinary repositories and offline repair acquire the same fixed-root, cross-process exclusive file lease. The file may remain after a crash; authority comes from the open exclusive handle, so a stale filename never blocks recovery by itself. Repair borrows the already-held lease only for its ordinary recovery validation reopen, preventing a second identity/store owner while avoiding a maintenance bypass in public APIs.

### J5 — Checksummed repair marker plus immutable preservation bundle

Before live mutation, repair writes a versioned checksummed marker that names one unique, independently checksummed damaged-source bundle. Ordinary startup refuses the marker. A later repair attempt completes byte-exact rollback from that bundle before honoring new cancellation or beginning another repair, so every crash point has one bounded recovery instruction and prior evidence is never overwritten.

### J6 — Validate a journal copy so evidence remains complete

The accepted production journal parser deliberately truncates the one tolerated torn/checksum-invalid trailing frame. Repair therefore durably copies the closed live journal into task-owned validation staging and runs that same parser on the copy. This preserves the original damaged journal byte-for-byte while keeping one parser and one corruption policy.

### J7 — Serialize complete backup attempts

The serial writer is released after the pinned cut so ordinary post-cut writes can continue, but complete backup attempts are separately serialized per repository. Without that narrow backup-attempt fence, a slow older cut could promote after a newer cut and make the already-rotated journal unable to cover the regressed archive. The fence does not block ordinary writes and is reversible only if promotion gains an equivalent monotonic compare-and-swap protocol.

### J8 — Cleanup may leave only uniquely named non-authoritative orphans

Staging/candidate cleanup is path-guarded and best-effort. A cleanup I/O failure may leave a uniquely named file or directory that no live opener recognizes, but it cannot turn backup/repair success into evidence deletion, release the wrong lock, or touch a promoted archive, live database/journal, or completed preservation bundle. Later bounded maintenance may remove such orphans.

## Deferred Findings

- There is no accepted schema version 2, so Task 3 validates/restores schema v1 and establishes the guarded maintenance boundary without inventing a migration.
- Production roots, production backup scheduling, user repair UI/intent ceremony, installer/upgrade behavior, and no-Builder-transfer validation remain at their named later stages.
- Backup confidentiality/encryption-key policy belongs with credential/security hardening before any production BunDex exists; Task 3 archives synthetic non-production fixtures only.
- Multi-generation archives, cloud/off-device copies, user-selected import/export, and retention policy are not required by the one-current-archive Task 3 contract.
