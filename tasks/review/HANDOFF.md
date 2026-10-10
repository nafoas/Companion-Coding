# Task Handoff

## Task

KEEP-02 — Da Bun Vault Inclusion and Complete Recovery (`tasks/active/keep-02-vault-recovery.md`), the second and final Roadmap Stage 10 packet.

Builder: Claude.

## Completed

- **Memory, minimal and internal.**
  - An optional `IVaultCompanion` hook in the backup service builds and validates the companion before any promotion. The companion is promoted first, then the memory archive.
  - A companion archive path.
  - A read-only keyset-paged subject-prefix query, which resolves KEEP-01 D1.
  - The accepted memory archive format, its validator, and the repair protocol are unchanged.
- **New `CompanionCore.Vault`**, the single composition point of backup and repair authority:
  - `VaultStateStore`: checksummed, atomically written, bounded state entries for settings and the active checkpoint.
  - `CompanionArchive`: a strict writer and validator.
  - `DaBunVault`: create, plus the complete restore. The restore runs the companion validation, then the accepted memory repair, then photograph restoration against restored live records, then state restoration. Damaged copies are preserved, and the report is honest.
- Judgments J1–J7 are recorded in the packet.

## Changed

- `CompanionCore.slnx`
- Memory: `IVaultCompanion.cs` (new), `MemoryBackupService.cs`, `MemoryRepository.cs`, `MemoryStoreLocation.cs`, `MemoryStore.Retrieval.cs`, `BackupTestHooks.cs`, `MemoryBackupResult.cs`, `AssemblyInfo.cs`
- `src/CompanionCore.Keepsakes/AssemblyInfo.cs`
- `src/CompanionCore.Vault/**` (new) and `tests/CompanionCore.Vault.Tests/**` (new)
- `tests/CompanionCore.Memory.Tests/MemoryPagingTests.cs` (new; existing memory tests unchanged)
- Test-only friend lines in Capture.Contracts and Privacy
- This packet and this handoff

## Verification

- Local gate (Linux, cross-targeted build), with the 13 App integration tests deferred to Windows CI:
  - locked restore;
  - Release build with `--no-incremental /warnaserror`: 0 warnings, 0 errors;
  - vulnerability audit: 0 across 36 projects;
  - tests: **731/731**. Vault 32 is new; Memory is 87 (85 accepted and unchanged, plus 2 paging tests).
- Expected on Windows CI: 744.
- Mutation pass: 37 mutants across the Vault, the backup hook, and paging; 34 killed, 3 equivalent.
  - The equivalents are defense-in-depth checks a later stage repeats:
    - the damaged-state pre-check, which validation's envelope check repeats;
    - the traversal check, which the exact declared-set check repeats;
    - the built-versus-validated comparison.
  - The three real gaps from the first run were closed with tests: a same-length tamper, a Vault copy that mismatches its record, and a silent companion.
- The bug hunt also wrapped corrupt-zip `InvalidDataException` as an invalid companion, and bounded entry size before reading.
- First CI (`91fbcb8`): push run `38062246267` and PR run `38062259517` each passed 743/744.
  - Both failed only `Scenario4_ADamagedStateFailsTheBackup_AndThePreviousVaultIsUntouched`, with an `IOException`: the test hashed the live journal while the repository held it exclusively, which Windows enforces.
  - Product behaviour is correct.
  - Fix: the test now asserts the journal's rotation base and highest append sequence are unchanged, instead of hashing the locked file.
- Fix head `25d5785`:
  - push run `38062566372` and PR run `38062568915` each passed **744/744** (16 test projects, Vault 32, Memory 87, App Integration 13);
  - test-results and attention-sheet artifacts: archive digests verified;
  - attention-sheet PNG: 792×621, `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.

## Remaining

- Gate CI, the evidence descendant, merge, and closure. That completes Stage 10.
- Then stop and report to Boss. Stage 11 needs target-PC calibration evidence, and Stage 12 (Task 12) needs credentials and live API, both of which are stop conditions. Pre-API wiring could be proposed as its own packet.

## Risks and assumptions

- The Vault is a two-archive backup set (J1).
- The production friend lines into Vault (J2) are deliberate and reversible.
- State bounds are provisional.

## Review focus

- Accepted memory backup and repair code paths are unchanged apart from the companion hook.
- Promotion order and failure atomicity.
- Restoration never deletes, never resurrects deleted photographs, and never restores a non-matching copy.

## Repository state

- Branch `agent/keep-02-vault-recovery`, based on `main` `53d72d6`.

## Next safe task

After closure: report to Boss at the Stage 11/12 boundary.
