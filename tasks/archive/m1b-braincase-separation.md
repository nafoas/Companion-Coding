# M1b — Finish the separation: everything machine lives in the BrainCase

Status: **accepted**
Authorized: 2026-10-13 by Boss ("go ahead and finish the separation between BunDex and Braincase"). This completes M1's deferred finding D2 before M2.
Accepted remote base: `6b92daf5132e8257cf19367739346657d88fabc4`
Working branch: `agent/m1b-braincase-separation`
Builder: Claude

## Objective

The data root holds Prince's two homes and nothing else of his:

```
CompanionCore.Dev\
  BunDex\                 his autobiography (M1)
  Braincase\              his subconscious
    Memory\               memory database, backups, Builder Braincase settings
    Keepsakes\            photographs
    State\                settings and checkpoints
    Transcripts\          session transcripts
    braincase-journal-v1.jsonl, corrupt-journal-v1\   bridge journal
    bundex-ledger-v1\, bundex-ledger-damaged-v1\      BunDex ledger (M1)
  TargetAuthorization\    Boss's consent (not Prince's mind)
  Calibration\            measurements (not Prince's mind)
```

## Scope

1. `MemoryStoreLocation` exposes the data root and the BrainCase root. The development, production, and test policies place `Memory` under `Braincase`.
2. Every sibling location is derived explicitly:
   - Keepsakes, State, and Transcripts derive from the BrainCase root;
   - the bridge journal is the BrainCase root itself;
   - the BunDex and Calibration derive from the data root;
   - the ledger derives from the BrainCase root.
3. **Layout migration** for Boss's existing development data. The legacy `Memory`, `Keepsakes`, `State`, and `Transcripts` directories are moved (never copied or deleted) into `Braincase\`.
   - The old `Braincase\` already sits in the right place.
   - Each move is one atomic rename, and the migration is idempotent and resumable.
   - It refuses, changing nothing more, when both a legacy and a non-empty new directory exist.
   - It runs before anything opens. That means `CompanionHost.OpenAsync`, `RepairOfflineAsync`, the Braincase check (only while the App is not running), and the Builder reset.
   - The host fails closed if a legacy directory remains.
4. The Builder reset follows the new layout.
5. Docs: paths updated.

## Non-goals

- No change to what is stored, only where.
- Production remains unopened. Its recognized path moves with the layout so the guards stay exact.

## Acceptance

- The development, production, and test layouts match the tree above. Development and test never resolve production.
- All Vault, repair, export, keepsake, transcript, journal, and BunDex suites pass unchanged in behavior.
- The migration:
  - moves legacy data with byte-identical contents;
  - is idempotent;
  - resumes after a partial move;
  - refuses a conflict without changing anything further;
  - treats an empty new directory as no conflict;
  - leaves `TargetAuthorization`, `Calibration`, and `BunDex` where they are.
- A host opened over a legacy layout sees the same memories, photographs, and BunDex pages as before.
- The Builder reset removes everything in the BrainCase except the Braincase settings, plus the BunDex. It keeps TargetAuthorization and Calibration, and refuses to plan over an unmigrated layout.

## Personal Round Judgments

- **J1 — TargetAuthorization and Calibration stay at the data root.** They are Boss's consent and measurements, not part of Prince's mind.
- **J2 — Migration moves, never copies.** A rename is atomic on one volume, keeps one copy of every memory, and is safe to resume. A conflict fails closed rather than choosing a side.

## Deferred findings

From Boss's requested emulation (2026-10-13): Builder played Prince as the writer over a temporary scratch BunDex through the real keeper. 13 moments across three games; 10 pages in 7 folders he made himself; the BrainCase rebuilt all 10 pages byte-identically from its 27-line ledger.

- **D1 — One home per kind of thing thrashes across games.** His first lore page was about a scary house, so a cozy farm fact brought "Scawy Stuffs / hollow house secrets" to mind; after he made "Farm Fings", scary-house lore brought "Farm Fings" to mind. Proposal: remember homes per kind *and* game (falling back to kind), and let a place he keeps choosing become the home. Needs Boss's view; natural fit for M5.
- **D2 — The keep pull never relaxes.** After two "not now"s the pull for that kind stays Strong permanently, even after he keeps one. That reads as insisting, against "inclinations, not instructions". Proposal: count only declines since the last time he kept that kind, so the pull returns to Gentle once he keeps one. Small fix; proposed for M2's packet.
- **D3 — Reorganizing is not yet reachable by Prince.** The store supports moves and renames (ledger-recorded), but the writer response cannot ask for one. Belongs with M5/M6.

## Paw Gate

- **Candidate.** Head `5a4725c2e8257d9594ccedc34de1916cc82b7f77`, PR #87, against accepted `main` `6b92daf5132e8257cf19367739346657d88fabc4`.
- **Local gate (Linux).** Locked restore; Release `/warnaserror`: 0 warnings, 0 errors; no vulnerable packages; 1193/1193 tests passed (Memory 95, +6 migration tests; Orchestration 119, +1 host-over-old-layout; BunDex 38, reset tests rewritten for the new layout plus old-layout refusal). Layout-pinning assertions were updated to the layout Boss directed.
- **Mutation pass.** 11 mutants across the migration, reset, host, and locations; 11/11 killed.
- **Windows CI.** Push run `38329380845` and PR run `38329395547`, both green, including the App test that places settings in the old layout and proves the App migrates before reading them.
- **Merge-ref check.** Parents `6b92daf` + `5a4725c`, tree `37fad4392383addee3d8f27b7e576bb77a078e4c`, equal to the local merge-tree.
- **Merge.** Squash-merged with an expected-head fence as `96250752290e340f0d6ed60e4017e0cee28cd5aa`.
- **Result.** Passed and accepted.
