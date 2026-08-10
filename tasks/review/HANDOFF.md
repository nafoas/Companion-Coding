# Task Handoff

## Task

Task 3 — Atomic Backup and Repair. Implementation is complete and rebased onto accepted remote `main`; exact-candidate publication, Windows CI, and the distinct Paw Gate remain pending.

## Completed

- Recovered the exact credit-stop state and confirmed no Task 3 product edit had begun or been omitted before the stop.
- Implemented a pinned exact journal/SQLite cut, released the serial writer before `SqliteConnection.BackupDatabase`, and independently health-validates both the snapshot and current source.
- Implemented the fixed `backups-v1/memory-vault-v1.zip` archive with exactly one snapshot, one canonical versioned manifest, and one manifest-checksum entry; database length/SHA-256 live inside the manifest.
- Implemented validate-before-promote atomic replacement and post-promotion journal rotation with a versioned checksummed rotation-base frame, retained post-cut appends, latest checkpoint, and old-or-complete-new replacement outcomes.
- Serialized complete backup attempts so a delayed older cut cannot regress a newer archive while ordinary post-cut writes remain unblocked.
- Implemented an assembly-internal repair authority/service behind the repository's cross-process exclusive lease; no public, automated, app, raw-path, or production maintenance surface exists.
- Implemented exact damaged DB/WAL/SHM/journal preservation in unique immutable bundles, a checksummed repair-state marker, byte-exact rollback after any post-marker fault, and interrupted-repair rollback before new cancellation is honored.
- Repair validates the archive and a durable copy of the live journal before mutation, retains every canonical contiguous post-cut append, tolerates only the production parser's one trailing tear/checksum failure, rejects non-trailing corruption, and reopens through ordinary Task 2 idempotent recovery.
- Added 31 focused Task 3 cases covering cut exclusion, concurrent backup ordering, source/candidate/archive health, canonical ZIP structure, promotion/rotation fault sides, cancellation fences, open-repository rejection, immutable completed damaged evidence, post-cut replay, tamper/version/path attacks, interrupted repair, rollback, and repeated idempotency.
- Completed the distinct local code/invariant/diff review. It found and corrected one preservation-boundary mismatch, then rechecked the final code checkpoint; the formal Paw Gate remains held for exact-head Windows evidence.

## Changed

- `src/CompanionCore.Memory/**` — backup format/writer/validator/service, pinned snapshot, full health checks, rotation-base journal support, repository/maintenance lease, preservation/marker/repair transaction, bounded internal test seams, and cleanup guards.
- `tests/CompanionCore.Memory.Tests/MemoryBackupTests.cs` — 10 focused backup cases.
- `tests/CompanionCore.Memory.Tests/MemoryRepairTests.cs` — 21 repair/tamper cases, counting nine theory rows.
- `tasks/active/task-03-vault-repair.md` — local checkpoint and Personal Round Judgments J3–J8.
- `BUILD_LEDGER.md`, `README.md`, and this handoff — evidence and still-pending gate state.

No project, package, lock, CI, app, capture, API, conversation, presentation, personality, architecture, roadmap, Design BunDex, prior task, screenshot, credential, production-data, or production-opener file changed.

## Verification

- Code checkpoint rebased onto accepted remote `main` — feature commit `cbc0b5c91c679a693e732b105acd09268d1c7f5c` plus evidence-retention correction `d64d5b7e071ff6ba43b434d4635525fa8ecaeeac`; exact resulting tree `e8bd0811f2b6ccd93892a7eec97778fa9e9fcaca`; 30 source/test paths, 4,445 additions, 31 deletions.
- Locked restore — passed locally with exact SDK 10.0.302: `dotnet restore CompanionCore.slnx --locked-mode -p:EnableWindowsTargeting=true -m:1`.
- Release build — passed for all 11 projects with 0 warnings and 0 errors: `dotnet build CompanionCore.slnx --no-restore --configuration Release -p:EnableWindowsTargeting=true -m:1`.
- Runnable regression/focused suite — 121/121 passed through a temporary in-process reflection runner because this sandbox denies VSTest's local communication socket. Breakdown: 90 accepted non-Windows regressions plus 31 Task 3 cases.
- Windows-only remainder — the four accepted real WPF app/process integration cases cross-compile here but cannot execute on Linux. With them, exact-candidate Windows CI should report 125/125.
- Dependency graph — no project or lock file changed. Locked restore resolves the accepted versions, including Microsoft.Data.Sqlite 10.0.10 and every SQLitePCLRaw component at 2.1.12. The permanent `dotnet package list --vulnerable --include-transitive` query was blocked by this sandbox's network policy and remains mandatory in Windows CI; no clean fresh audit is claimed locally.
- Local review — clean: the complete diff passes `git diff --check`, all product/test changes are confined to the two authorized memory trees, project/dependency/lock/CI files are unchanged, exactly one task is active, and synthetic fixtures contain no credentials, network/API, capture, personality, or private content.
- Authority surface — clean: reflection verifies no exported backup/repair/maintenance type, no public backup/repair/raw-path/production capability, and no app/runtime/capture/presentation reference to repair.
- Formatting tool — `dotnet format --verify-no-changes` could not connect to its sandbox-blocked named pipe. Normal and full-solution compilers report zero warnings; no formatter result is claimed.

## Remaining

- Publish the Task 3 descendant to `agent/task-03-vault-repair`, then open/update the draft PR from the actual accepted `main` parent.
- Run the exact Windows workflow: locked restore, permanent direct/transitive vulnerability audit, Release build with zero warnings/errors, and all expected 125 tests.
- Confirm the published exact head/tree matches this locally reviewed descendant, combine that confirmation with Windows CI, and record the formal Paw Gate decision. Do not mark Task 3 accepted or activate Task 4 before that evidence passes.
- If CI exposes a Windows-specific replacement/durability difference, change only Task 3 and rerun the complete gate.

## Risks and assumptions

- `File.Replace`, file-share exclusion, WPF process behavior, and the permanent vulnerability audit still require the named Windows execution oracle even though the complete solution cross-builds locally.
- The 8 GiB database/archive caps are explicit synthetic non-production safety bounds, not final production retention sizing.
- Staging cleanup is deliberately best-effort: a uniquely named non-authoritative orphan may remain after cleanup I/O failure, but no cleanup path can target the promoted archive, committed DB/journal, or completed damaged-source bundle.
- Production roots, session-end scheduling, user repair UI/ceremony, backup encryption, schema migration, identity/photograph expansion, and multi-generation/off-device retention remain deferred.

## Review focus

- Re-prove cut pinning and `BackupDatabase` page copy happen before promotion, with post-cut writes excluded from the snapshot but retained through rotation and repair.
- Re-prove concurrent attempts cannot regress the archive and every failure leaves an old or new complete journal consistent with the authoritative promoted cut.
- Re-prove archive/parser bounds, canonical manifest/checksums, exact entry set, SQLite/schema/content health, and tamper/path/version rejection before mutation.
- Re-prove repair quiescence, capability isolation, immutable complete evidence, marker recovery, cancellation boundary, byte-exact rollback, and ordinary idempotent replay.
- Re-run public-surface and changed-path checks for forbidden production, automated maintenance, Task 4+, personality, private fixture, or dependency drift.

## Repository state

- Accepted remote base: `44caa2fc6474b0952eaed5f086bfb3c49bf73c18`; accepted tree `2423473e0a0fb915bf40c2554947f7166cd7d9f4`.
- Current branch/publication target: `agent/task-03-vault-repair`; preserved surrogate branch: `agent/task-03-vault-repair-local`.
- Candidate code checkpoint: `d64d5b7e071ff6ba43b434d4635525fa8ecaeeac`; this handoff is its control-record descendant.
- Existing obsolete Claude foreman automations remain paused under the accepted direct-build workflow; they were not resumed.

## Next safe task

Stop at the remote/Windows gate. When transport is restored, publish this descendant, run exact-head Windows CI, and close the formal Paw Gate only from passing evidence. Do not begin Task 4.

## Credit status

Credits and Git transport are restored. The candidate is safely rebased and ready to publish; no remote branch update, PR head, vulnerability-audit pass, Windows test pass, Paw Gate approval, or Task 3 acceptance is claimed until those actions actually complete.
