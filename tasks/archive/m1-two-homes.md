# M1 — Two homes: the BunDex and the BrainCase

Status: **in progress**
Authorized: 2026-10-12 by Boss ("Let us begin, then."). This is step M1 of the approved plan, `tasks/proposed/pre-stage-13-changes.md`.
Accepted remote base: `1af6d3c0d79a8bd5c37961b936e2004ab96e4a31`
Working branch: `agent/m1-two-homes`
Builder: Claude

## Objective

Give Prince two separate homes:

- **the BunDex**: his own notebook, a directory of folders and pages he makes himself, in his own words, organically and messily;
- **the BrainCase**: his subconscious, which keeps the authoritative ledger of everything written in the BunDex plus the invisible index (roles, tags, registrations, moves) he never sees.

M1 builds the storage, the safety rails, and the "keep this" pull, and proves them with a plain stand-in writer. Prince's real voice arrives in Stage 13. This step covers items 2, 3, 5, 10, and 11 of the plan.

## Scope

1. **Locations.**
   - `BunDex/` is a sibling directory of the validated development or test memory location (like `Keepsakes/`). The subconscious ledger lives in `Braincase/bundex-ledger-v1/`, inside the existing Braincase directory. On Windows, "BrainCase" and "Braincase" are the same name.
   - There is no raw-path constructor, so neither can ever resolve the production root.
2. **The ledger (BrainCase).** Segmented, append-only JSONL with a SHA-256 hash chain; each segment is at most 4 MiB.
   - A line is committed only when it is complete and flushed to disk.
   - A torn final line, which was never committed, is dropped on open.
   - A break anywhere else fails closed: the BunDex opens read-only and reports Damaged.
   - It records: folder created, page created, text added to a page, folder moved or renamed, page moved or renamed, role registered, and keep declined.
3. **Pages are append-only.**
   - The ledger is written first, then the files are materialized: a new page through a temporary file and a no-overwrite move; added text by appending and flushing.
   - On open, every page is reconciled against the ledger:
     - a missing page or folder is rewritten;
     - a page that is a prefix of its expected text gets the missing tail appended;
     - a pending move is completed;
     - any other difference is reported as **altered**, and the page is left untouched and refuses new text until resolved.
   - No API exists to rewrite, truncate, or delete a page or folder.
4. **Safe names, mess allowed.**
   - Folder and page names are cleaned: path separators, reserved characters, and control characters are removed; spaces are collapsed; leading and trailing dots and spaces are trimmed; length is bounded. Reserved device names and `..` are made safe. Name collisions get " (2)".
   - Names that look like secrets are refused.
   - Folders nest at most 4 deep. Spelling, grammar, and neatness are never checked.
5. **Roles, registration, and the pull (subconscious).**
   - An invisible role catalog: things he was thinking about, short-term, medium-term, long-term, cherished, lore, opinions, adventures, scary or cool stuffs, relationship, and little requests.
   - `BunDexKeeper.KeepAsync(role, tags, moment)` lets the writer respond:
     - when the role has a registered home, that home "comes to mind";
     - when it has none, he only has the feeling that something is worth keeping.
   - The writer decides: write in an existing place, make a new place, or not now.
   - A new place made for a role is registered for it. A decline is recorded, and the next moment for that role carries a slightly stronger feeling, capped. Nothing is ever repeated in words.
6. **The curtain at the writer boundary.** What the writer sees (`KeepMoment`, `BunDexView`) carries only his own notebook (folder and page names) and the moment's feeling and content. It never carries roles, IDs, tags, scores, or registrations.
7. **Page index (BrainCase).** Each page carries tags (role, layer, game, save, subjects, created time) and a stable page ID that survives moves and reorganizing. Pages can be found by tags whatever folder they sit in.
8. **Da Bun Vault.**
   - Ledger segments join the companion archive as `bundex-ledger-v1-NNNNNN.jsonl` entries.
   - Bnuy Repairs restores missing segments, and preserves damaged ones before restoring the Vault copy.
   - The BunDex is then rebuilt from the ledger on open.
9. **Host.** `CompanionHost` opens the BunDex (reconciling it) on every start and after repairs, and exposes it.
10. **The one-time Builder Prince reset** (`tools/CompanionCore.BuilderReset`).
    - A dry run by default lists exactly what would be removed and what is kept.
    - It acts only with `--confirm=<exact development Memory path>`.
    - It is development root only, and refuses test and production. It keeps `braincase-settings.json` and target authorization. It is idempotent.

## Non-goals

- Prince's voice; the real writer (Stage 13).
- The membrane and curtain checks beyond the writer boundary (M2).
- Memory layers, recall, and aging (M4).
- What gets kept and when (M5).
- Seeds (M6).
- A BunDex viewer in the App (M10 at the latest).
- Moving the existing Memory, State, Transcripts, and Keepsakes directories under the BrainCase (see J2).

## Acceptance

Per the approved test plan, M1:

- The BunDex and the BrainCase ledger are separate directories under the development root (and the test root in tests), never the production root.
- The stand-in writer creates a folder only when the pull fires and no home exists for that role. The folder is registered for the role, and the next item of that role goes there without a new pull.
- Pages are append-only:
  - every rewrite, delete, or overwrite path is absent or refused, and the page stays byte-identical;
  - a crash between the ledger commit and the file write is repaired on reopen, with no partial page;
  - an altered page is reported and never overwritten.
- Reorganizing (folder or page move or rename) is recorded. Every page is still found by its tags, its old location is in the ledger, and a crash mid-move completes on reopen.
- Unsafe names are made safe; secret-like names are refused; depth is bounded; collisions are suffixed.
- Da Bun Vault backs up and restores the ledger, and the BunDex is fully rebuilt after the BunDex directory is lost.
- The Builder reset:
  - refuses without the exact confirmation;
  - touches only the development root;
  - keeps the settings and authorization;
  - is idempotent.
- The writer boundary carries no roles, IDs, tags, or registrations (reflection test).
- Every existing suite passes unchanged.

## Personal Round Judgments

- **J1 — The ledger is authoritative; pages are his notebook built from it.** That makes crash safety, Vault backup, and rebuild simple, and pages stay plain readable text. Removal of a page by Boss (case by case) will need its own explicit ledger entry when it is first wanted, so that a rebuild does not bring the page back. Recorded as a deferred finding.
- **J2 — Directory layout.** The BunDex is a new `BunDex/` directory. The subconscious ledger goes into the existing `Braincase/` directory, which already holds the Braincase's raw outputs (the bridge journal), so the two homes Boss named are exactly `BunDex/` and `Braincase/`. The other machine directories (`Memory/`, `State/`, `Transcripts/`, `Keepsakes/`) stay where they are in M1. Moving them under `Braincase/` would touch accepted recovery and Vault paths; it is offered to Boss as a later option.
- **J3 — Names are made safe rather than refused**, except secret-like names, so his organic, messy names ("wat is dis?") survive in readable form.

- **J4 — Secret detection is narrowed.** The first draft refused any run of 40+ token characters, which also refused ordinary long words and repeated letters ("oooooh nooo"). A long token now counts as secret-like only when it mixes upper case, lower case, and digits, alongside the `sk-` and "api key:" patterns. Tested both ways.

## Deferred findings

- **D1 — Boss's case-by-case page removal** needs its own ledger entry kind when it is first wanted, so a rebuild never brings a removed page back (J1).
- **D2 — Directory layout option (J2):** moving `Memory/`, `State/`, `Transcripts/`, `Keepsakes/` under `Braincase/` is offered to Boss; not done.
- **D3 — The keeper is not yet called by the orchestrator.** The host opens the BunDex; what gets kept and when is M5.

## Paw Gate

- **Candidate.** Head `5fdc8168b4d0dc6294e434dde2fd4ae2353f8cba` on `agent/m1-two-homes`, PR #85, against accepted `main` `1af6d3c0d79a8bd5c37961b936e2004ab96e4a31`.
- **Local gate (Linux).** Locked restore; Release build with `/warnaserror`: 0 warnings, 0 errors; no vulnerable packages; 1185/1185 tests passed (BunDex 37 new; Vault 35, +3; Orchestration 118, +1; every other suite unchanged).
- **Mutation pass.** 11 mutants across the ledger, names, keeper, repair, store, and Builder reset. The first run left 3 survivors (a forged line that re-hashes itself, a longer healthy ledger that does not hold the Vault's history, a same-length page alteration); a test was added for each. Final: 11/11 killed.
- **Windows CI.** Push run `38317387395` and PR run `38317409457`, both green.
- **Merge-ref check.** `refs/pull/85/merge` parents `1af6d3c` + `5fdc816`, tree `eb901c737250717d79133ff1f364040a770ec607`, equal to `git merge-tree --write-tree origin/main HEAD`.
- **Merge.** Marked ready and squash-merged with an expected-head fence as `0202c7fd32390e73af69faf79a8410def91f2c24`.
- **Result.** Passed and accepted.
