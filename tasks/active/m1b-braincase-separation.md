# M1b — Finish the separation: everything machine lives in the BrainCase

Status: **in progress**
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

_(none yet)_
