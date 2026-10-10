# Task Handoff

## Task

KEEP-01 — Keepsake Photographs (`tasks/active/keep-01-keepsake-photographs.md`), the first of two Roadmap Stage 10 packets.

Builder: Claude.

## Completed

- **New `CompanionCore.Keepsakes`** (references Memory, Privacy, and Capture.Contracts):
  - `KeepsakeLocation`: a validated test or development sibling directory.
  - `KeepsakeCamera`:
    - a visible camera action precedes every durable write, and the two are paired by action ID;
    - writes accept only a frame for the authorized target and current privacy generation, inside the action window, and admitted by the privacy guard;
    - camera actions are rare by construction;
    - frames are box-downscaled to a bounded edge and saved as compressed PNG;
    - the file is written atomically first, then the record is committed through the generation-bound gate;
    - retries are idempotent.
  - `KeepsakeStore`:
    - verified inspection;
    - authority-gated deletion that is record-first and keeps the original record;
    - orphan reporting;
    - read-only disk-growth figures, with no cleanup surface.
- Judgments J1–J8 and deferred finding D1 (listing paging) are recorded in the packet.

## Changed

- `CompanionCore.slnx`
- `src/CompanionCore.Keepsakes/**` (new)
- `tests/CompanionCore.Keepsakes.Tests/**` (new)
- Test-only friend lines in `src/CompanionCore.Capture.Contracts/AssemblyInfo.cs` and `src/CompanionCore.Privacy/AssemblyInfo.cs`
- This packet and this handoff

## Verification

- Local gate (Linux, cross-targeted build), with the 13 App integration tests deferred to Windows CI:
  - locked restore;
  - Release build with `--no-incremental /warnaserror`: 0 warnings, 0 errors;
  - vulnerability audit: 0 across 34 projects;
  - tests: **697/697** (Keepsakes 23, new).
- Expected on Windows CI: 710.
- Mutation pass: 52 mutants; 48 killed, 4 equivalent.
  - The equivalents are:
    - the generation-bound submit, which matters only in a race between the privacy check and the commit;
    - the open-action removal, since the saved cache answers first;
    - the edge clamp, which rounding never exceeds;
    - the length pre-check, since the digest catches the same files.
  - The first run's three real gaps were closed with new tests: the encoded-size bound, a rejected record, and the saved-edge lower bound.
- The bug hunt also made orphan detection strict about canonical file names.
- CI: pending.

## Remaining

- Gate CI, the evidence descendant, merge, and closure.
- Then KEEP-02: Vault inclusion and complete recovery.

## Risks and assumptions

- Rarity, size, and compression bounds are provisional.
- Rarity counts reset when the runtime restarts (J5).
- Listing is bounded at 1000 records (D1).

## Review focus

- No durable byte is written without a preceding action and every admission check.
- There is no cleanup or public delete surface.
- Committed records are never removed.

## Repository state

- Branch `agent/keep-01-keepsake-photographs`, based on `main` `531fa5c`.

## Next safe task

After closure: KEEP-02.
