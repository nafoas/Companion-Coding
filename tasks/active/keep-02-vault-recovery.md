# KEEP-02 — Da Bun Vault Inclusion and Complete Recovery

Status: **active — Paw Gate review**
Authorized: 2026-10-10 under Boss's standing direction, as the second of two Stage 10 packets (after KEEP-01 passed)
Accepted remote base: `53d72d63ca4230ab28cc53ee026a9c04c4d5c582`
Working branch: `agent/keep-02-vault-recovery`
Builder: Claude

## Objective

Complete Roadmap Stage 10. Da Bun Vault must carry photographs, settings, and the active checkpoint alongside the accepted BunDex memory backup. One recovery path restores everything and reports honestly when recovery is incomplete. Neither backup nor restore may ever delete a live photograph or a BunDex record.

## Entry evidence and authority

- KEEP-01 is accepted (PR #35; closure PR #36; `main` `53d72d6`).
- Design BunDex, "Crash safety and Da Bun Vault":
  - each completed session atomically produces one validated compressed Vault, holding the complete BunDex, Prince identity and state, settings, game recognition, photographs, and a checksum manifest;
  - a valid old Vault is never overwritten by an invalid new backup;
  - Bnuy Repairs validates, restores, replays, and reports honestly if recovery is incomplete.
- Roadmap Stage 10 deliverables and scenarios:
  - photographs included in Da Bun Vault;
  - a recovery test covering BunDex, photos, settings, and the active checkpoint;
  - disk-growth reporting without automatic memory deletion;
  - restoring the Vault restores photograph links and files;
  - resource cleanup cannot delete a photograph or a BunDex record.
- Accepted Task 3 design: backup and repair are guarded maintenance authorities, separate from the live append path. That separation is preserved here.

## Design

The accepted memory archive (`memory-vault-v1.zip`) and its repair protocol are **unchanged**.

### Companion archive

The Vault becomes the backup set: the memory archive plus a companion archive (`companion-vault-v1.zip`) built in the same backup operation.

- The companion holds every canonical keepsake file and every verified state entry.
- It carries a canonical manifest that names the paired backup ID, plus an exact checksum list.
- It is built and independently validated before either promotion.
- If the companion fails, the whole backup fails, and the previous memory archive and companion stay untouched.
- Promotion order is companion first, then memory. A crash between the two leaves a newer companion beside the older memory archive. That is safe, because restoration verifies every file against the restored records, never against archive pairing.

### Memory change

The memory change is minimal and internal:

- an optional companion hook in the backup service, before promotion;
- the companion archive path;
- a read-only, keyset-paged subject-prefix query. This resolves KEEP-01 D1, so restore can see every photograph.

### New `CompanionCore.Vault` project

It references Memory and Keepsakes, and is the single composition point of backup and repair authority. Memory and Keepsakes grant it a production friend line for their internal maintenance surfaces.

- **`VaultStateStore`.** Settings and the active checkpoint are stored as named, checksummed, atomically written state entries in a validated sibling `State` directory. Names, sizes, and counts are bounded.
- **`CompanionArchive`.** A strict writer and validator: canonical manifest and checksum, an exact entry set, bounded lengths, no traversal, and digest-verified extraction.
- **`DaBunVault`.**
  - `CreateAsync` runs the memory backup with the companion.
  - `RestoreAsync` runs:
    1. companion validation;
    2. the accepted memory repair;
    3. photograph restoration against the restored live records, restoring a missing or damaged file only from a digest-matching Vault copy and preserving any damaged copy;
    4. state restoration for missing or damaged entries, keeping valid newer ones and preserving damaged copies;
    5. an honest report: verified, restored, and missing photographs; kept, restored, and missing states; and whether recovery is complete.

## Explicitly forbidden

- Any change to the memory archive format, its validator, or the repair protocol.
- Deleting or overwriting a live photograph file that already matches its record.
- Restoring a photograph whose record was deleted at Boss's request.
- Deleting any BunDex record.
- Discarding a damaged copy without preserving it.
- Production roots.
- Live API.
- Personality.

## Acceptance scenarios

1. **Complete recovery.** Commit memories, photographs, settings, and the active checkpoint, then create the Vault. Corrupt the live BunDex, delete or damage photographs, and damage the state. Restore: memory, post-cut appends, photographs, settings, and checkpoint all return, and the report says complete.
2. **Photograph links.** Restored photograph records and files verify. A photograph deleted at Boss's request is not resurrected. A photograph committed after the backup cut keeps its live file and is verified.
3. **Honest incompleteness.** A photograph missing from both the disk and the Vault is reported missing by ID, and the report says incomplete. Nothing is fabricated.
4. **Old Vault preserved.** A failing companion (invalid state, interruption before promotion) leaves the previous memory archive and companion byte-identical, and the journal is not rotated.
5. **Strict companion validation.** Tampered, extra, missing, oversized, traversal, non-canonical, or mismatched entries are refused.
6. **No deletion.** Neither backup nor restore deletes a live photograph or a record. Damaged copies are preserved under a damaged directory.
7. **State store.** Writes are atomic and checksummed. Damage is detected. Names, sizes, and counts are bounded.
8. **Paging.** Keyset paging enumerates every photograph beyond the 1000-record query bound, in a stable order, without duplicates.
9. **Compatibility.** A memory-only Vault (no companion) still restores the BunDex, and the report states that the companion is missing.
10. **Regression.** Every accepted memory backup and repair test passes unchanged, and all 710 accepted tests pass on Windows CI.

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Memory/`: `MemoryBackupService.cs`, `MemoryRepository.cs`, `MemoryStoreLocation.cs`, `MemoryStore.Retrieval.cs`, `BackupTestHooks.cs`, `MemoryBackupResult.cs`, `AssemblyInfo.cs`, and a new `IVaultCompanion.cs`
- `src/CompanionCore.Keepsakes/AssemblyInfo.cs` (friend line)
- `tests/CompanionCore.Memory.Tests/**` (new paging tests only; existing tests unchanged)
- `src/CompanionCore.Vault/**` and `tests/CompanionCore.Vault.Tests/**`
- Test-only friend lines for `CompanionCore.Vault.Tests` in Capture.Contracts, Privacy, Memory, and Keepsakes
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Candidate evidence (pending the evidence-descendant CI, merge-ref check, and acceptance):

- First CI on `91fbcb8` (runs `38062246267` and `38062259517`) failed one test. It was a test-only Windows assumption (hashing the exclusively held live journal), corrected in `25d5785` by asserting the journal's rotation state, with no product change.
- Fix head `25d5785`:
  - push run `38062566372` and PR run `38062568915` each passed **744/744** (16 test projects, Vault 32, Memory 87, App Integration 13);
  - test-results and attention-sheet artifacts: archive digests verified;
  - attention-sheet PNG: 792×621, `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- Local gate: 731/731, 0 warnings, 0 vulnerable packages across 36 projects. Every accepted memory backup and repair test is unchanged.
- Mutation pass: 37 mutants; 34 killed, 3 equivalent (defense in depth).
- Acceptance scenarios 1–9 are covered by `VaultRecoveryTests`, `CompanionArchiveTests`, `VaultStateStoreTests`, and `MemoryPagingTests`. Scenario 10 is covered by the full CI suite.

## Personal Round Judgments

- **J1 — Companion archive beside the accepted memory archive.** The Vault is the backup set: the unchanged `memory-vault-v1.zip` plus `companion-vault-v1.zip`, built and validated in one backup operation. This keeps the most safety-critical code (archive format, validator, repair protocol) byte-for-byte accepted, and every accepted memory backup and repair test passes unchanged. The design's "one Vault.zip" is honored as one atomic backup set; a single-file export can wrap both later without touching recovery.
- **J2 — One composition point for authority.**
  - `CompanionCore.Vault` receives production friend lines from Memory (`IVaultCompanion`, backup and repair) and Keepsakes (record and metadata internals).
  - No public backup or repair surface is added; app wiring will expose one deliberate command later.
  - This is reversible: remove the friend lines and the project.
- **J3 — Promotion order.**
  - The companion is promoted first, then the memory archive.
  - A failing or interrupted companion before promotion aborts the whole backup and leaves the previous Vault byte-identical, with no journal rotation.
  - A crash between the two promotions leaves a newer companion beside the older memory archive. Restoration tolerates this because every photograph is verified against the restored records, not against archive pairing. A dedicated test proves it.
- **J4 — A damaged state entry fails the backup.** The previous Vault keeps the good copy rather than being replaced by a Vault missing that state.
- **J5 — Restoration policy.**
  - A photograph file is restored only when its live record exists, it is not deleted at Boss's request, and the on-disk file is missing or fails its digest, and only from a Vault copy whose digest matches the record.
  - State entries are restored only when missing or damaged; valid newer state is kept.
  - Any damaged copy is moved to `damaged-v1/`, never deleted.
  - A photograph that cannot be verified or restored is reported by ID, and `Complete` is false.
  - The memory repair runs even when the companion is missing or invalid, and the report says so.
- **J6 — Keyset paging.** The read-only subject-prefix keyset paging is ordered by record ID, resolving KEEP-01 D1. It shares the checksum-verified read path, and restoration enumerates every photograph through it.
- **J7 — State envelopes.** Each state file is `companion-state-v1\n<sha256>\n<payload>`, written by atomic replace. Any flipped byte, truncation, or header change is detected (an exhaustive byte-flip test). There are at most 64 entries of 1 MiB each, which is provisional.

## Deferred findings

- **D1 — Single-file export.** A single-file `Da Bun Vault.zip` export (wrapping both archives) and a public, deliberate backup/repair command belong to app wiring.
