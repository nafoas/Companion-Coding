# KEEP-01 — Keepsake Photographs

Status: **active — Paw Gate review**
Authorized: 2026-10-10 under Boss's standing direction to continue autonomously through the pre-API roadmap under the Paw Gate model. It is the first of two Stage 10 packets.
Accepted remote base: `531fa5c316b6ed2b5d205e709401d8e19811135b`
Working branch: `agent/keep-01-keepsake-photographs`
Builder: Claude

## Objective

Implement Roadmap Stage 10's explicitly disclosed durable-image exception, personality-neutral:

- a rare photograph action taken from the authorized target only;
- a compressed local keepsake with a neutral placeholder caption and scope metadata, plus its memory record;
- local inspection, and deletion only on Boss's explicit request;
- disk-growth reporting that never deletes anything.

## Split

Stage 10 is delivered through two sequential packets with separate Paw Gates. This mirrors Boss's 2026-08-10 decision to gate backup/repair authority separately from the live append path.

- **KEEP-01 (this packet):** keepsake photographs, without changing the accepted Vault format.
- **KEEP-02:** a Da Bun Vault that carries photographs, settings, and the active checkpoint, plus a complete recovery test (BunDex, photos, settings, checkpoint).

## Entry evidence and authority

- Stage 9 is accepted (Task 11, PR #33; closure PR #34; `main` `531fa5c`).
- Design BunDex, "Photographs":
  - ordinary screenshots stay in bounded RAM and are discarded;
  - rare durable Prince photographs are the deliberate exception;
  - the visible camera action discloses the write;
  - only the authorized game target is captured;
  - the image is compressed, captioned by Prince, and locally inspectable and deletable.
- Every visible BunDex item is autobiographical in Prince's voice, so the core uses neutral placeholders until Stage 13.
- Roadmap Stage 10 deliverables and Paw Gate scenarios: the camera action and durable write are visibly paired; ordinary screenshots remain RAM-only; resource cleanup cannot delete a photograph or BunDex record.

## Required implementation

New `CompanionCore.Keepsakes` project, referencing Memory, Privacy, and Capture.Contracts.

- **`KeepsakeLocation`.** A validated sibling `Keepsakes` directory of a development or test memory location, the same pattern as `TranscriptLocation`. There is no raw-path constructor, so it can never resolve a production root.
- **`KeepsakeCamera`.**
  - The visible camera action comes first. `BeginCameraAction` takes the current authorization grant and privacy generation and returns a single-use, short-lived `CameraAction` plus a visible `CameraShown` intent. Actions are rare: a minimum interval and a daily bound apply.
  - `TakeAsync` accepts only a frame whose metadata names the action's target session and current privacy generation, timestamped within the action window, with bounded dimensions. Otherwise nothing is written.
  - The frame is downscaled to a bounded edge and PNG-compressed. It is written content-addressed and atomically (temp, flush, rename), then recorded through `LocalWriteGate` with the generation-bound overload.
  - The record carries a neutral placeholder caption, scope (game, save, session), and keepsake metadata (digest, length, dimensions, action ID).
  - The result carries a `PhotographSaved` intent paired with the action ID.
  - Record IDs are deterministic from the action, so a retry is idempotent.
- **`KeepsakeStore`.**
  - Lists photographs from committed records through the bounded `MemoryQuery`.
  - Inspection verifies the file's digest against its record.
  - Deletion requires an explicit-user-intent authority. It removes only that photograph's file and appends a superseding "deleted at Boss's request" record; the original record is never removed.
  - Orphan files (written but never recorded) are reported, never deleted automatically.
- **`DiskGrowthReport`.** Read-only byte and file counts for the keepsake directory and the memory root (database, journal, backups). There is no cleanup surface.

## Explicitly forbidden

- Writing any image without a preceding visible camera action, or for a non-target or stale-generation frame.
- Durable writes of ordinary screenshots or attention sheets.
- Deletion other than Boss's explicit request.
- Any update or delete of a committed record.
- Changes to the Vault format (KEEP-02).
- Personality captions.
- Live API.

## Acceptance scenarios

1. **Pairing.** A camera action and its durable write are visibly paired: every saved file has a `CameraShown` and a `PhotographSaved` intent with one action ID. A take without a valid action (none, expired, reused, or from a different engine) writes nothing.
2. **Target only.** A frame from another target session, a stale privacy generation, a paused privacy state, or outside the action window writes nothing, and the keepsake directory stays unchanged.
3. **RAM-only screenshots.** Ordinary capture and attention-sheet paths never write to disk. The keepsake directory receives files only through `TakeAsync`, which a reflection and path guard prove.
4. **Compression and metadata.** The saved image is a valid PNG within the edge bound, its digest matches the record, and the record holds the neutral caption, scope, and metadata.
5. **Rarity.** The minimum interval and daily bound refuse extra camera actions.
6. **Inspection.** Inspection returns verified bytes. A tampered or missing file is reported, never silently served.
7. **Explicit deletion.** It requires the authority, removes only that file, and appends a superseding record; the original record remains retrievable. Repeating the deletion is idempotent.
8. **No cleanup deletion.** No public API other than authority-gated deletion can remove a keepsake file or record. Orphans are reported, not removed. The disk report is read-only.
9. **Idempotent retry.** Retrying the same action after a recorded write is idempotent. A retry after the file was written but before the record commits completes the record without duplicating the file.
10. **Regression.** All 687 accepted tests still pass on Windows CI.

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Keepsakes/**` and `tests/CompanionCore.Keepsakes.Tests/**`
- Test-only friend lines for `CompanionCore.Keepsakes.Tests` in `src/CompanionCore.Capture.Contracts/AssemblyInfo.cs` (to mint synthetic grants) and in `src/CompanionCore.Privacy/AssemblyInfo.cs` (to exercise privacy pause)
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Pending.

## Personal Round Judgments

- **J1 — Stage 10 is split into two packets.** KEEP-01 handles photographs; KEEP-02 handles the Vault and complete recovery. This follows Boss's earlier choice to gate backup/repair authority separately. KEEP-01 leaves the accepted Vault format untouched.
- **J2 — One file per camera action.** Files are named `<actionId>.png`, with the SHA-256 kept in the record, rather than content-addressed. Each photograph therefore owns exactly one file, explicit deletion can never affect another photograph, and a different file under an action's name is never overwritten (`StoredFileMismatch`).
- **J3 — File first, then record.** The atomic temp-flush-rename write happens before the generation-bound record commit, so a committed record never points at a missing file. An interrupted attempt leaves a reported orphan, and a retry inside the action window completes the record without rewriting the file.
- **J4 — Defense in depth before any durable byte.** The camera checks the action's target session, generation, and target identity; the frame window; the current privacy generation; and a local privacy-guard evaluation (trusted-game bypass or a clear assessment). Any failure writes nothing.
- **J5 — Rarity is counted per camera instance.** A 5-minute minimum interval and 12 actions per day (provisional), counted on begun actions, whether or not a frame is saved. A runtime restart resets the in-memory count. Persisted rarity is deferred to wiring and calibration.
- **J6 — Compression.** Box downscale to a 1280-pixel longest edge, then an RGBA PNG at optimal zlib compression, which is lossless and needs no external codec. The bounds are provisional. Design note 9 defers compression targets and disk policy to measurements.
- **J7 — Deletion is authority-gated and record-first.** It needs an internal explicit-user-intent capability, like the accepted repair authority; there is no public delete surface until app wiring. The superseding note is committed before the file is removed. A privacy pause refuses the note and keeps the file, and a repeat is idempotent.
- **J8 — Orphans and disk growth are reported, never cleaned.** Orphans are files without a live record, deleted-record leftovers, temporaries, and non-canonical names. Only an exact `<actionId>.png` name counts as canonical; the bug hunt made that check strict. There is no cleanup API, which a reflection guard proves.

## Deferred findings

- **D1 — Listing is bounded.** `ListAsync` returns at most 1000 photograph records, the `MemoryQuery` maximum. At the provisional rarity, that limit is reached after about 80 days. KEEP-02 or wiring should add cursor paging to `MemoryQuery`. The Vault must enumerate keepsakes directly, not through this listing.
