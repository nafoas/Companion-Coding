# Task 10 — Memory Consolidation and Retrieval Mechanics

Status: **accepted — Paw Gate passed 2026-10-10**. Stage 8 is complete.
Authorized: 2026-10-10 under Boss's standing direction to continue autonomously through the pre-API tasks under the Paw Gate model
Accepted remote base: `6f27392840f9b2c38e7570848921944a8dbdca98`
Working branch: `agent/task-10-memory-consolidation`
Builder: Claude
Accepted gate head: `033eef6f79ed30572f1d3134729f13a52e8b55ae`, tree `c200f164ce1ac92b63b60fb26a5a2fcc0e412e29`
Pull request: #31, squash-merged as `e529eb679a17dcc6059d2f83d33d850dc6a80c2f`
Final CI: push run `38056657903`, PR run `38056660572`, each 621/621

## Objective

Build the personality-neutral PRINCE mechanics for Roadmap Stage 8: consolidation and recall over the accepted append-only memory store. It covers:

- scopes;
- summaries that reference originals without deletion;
- grouped evolving beliefs;
- lore provenance;
- adventure records;
- spoiler-aware save retrieval;
- local relevant-context selection;
- user-correction precedence;
- immutable local interest roots with validated generated-seed storage.

Every visible recollection stays placeholder-neutral. The Bun-written voice is Stage 13 work on Builder Prince.

## Entry evidence and authority

- Stage 7 is accepted and final. Post-merge `main` `6f27392` passed 563/563.
- Neutral-core packet Task 10 lists the mechanics above and explicitly puts the final autobiographical voice out of scope.
- The Design BunDex "PRINCE and the BunDex" and "Local authority" sections are the behavioral authority:
  - committed records are never automatically deleted;
  - consolidation adds summaries and links while originals remain;
  - decay affects priority, not existence;
  - later validated understanding and corrections outrank earlier conceptions, though recency alone is not truth;
  - duplicates become linked recurrences;
  - lore distinguishes observed, read, told, suspected, and confirmed;
  - old-save knowledge is spoiler-suppressed for new adventures;
  - authority order: user corrections, then integrations, then direct facts, then interpretations, then guesses;
  - only relevant memory reaches any API call.
- Roadmap Stage 8 Paw Gate scenarios, in neutral form:
  - complete a synthetic adventure and produce a record without deleting sources;
  - resume a save and retrieve its relevant history;
  - start a new save without spoiler contamination;
  - correct lore and retrieve the later validated understanding while the earlier theory remains;
  - preserve a shared joke closely while compressing routine progression.

  The "sounds like Prince" scenario belongs to Stage 13's voice gate, because the core uses neutral placeholders by mandate.

## Required implementation

### Read-only memory query (accepted Memory project)

- `MemoryQuery`, exposed through `MemoryRepository.RetrieveAsync`, filters by game, save, session, scope, subject prefix, or record IDs.
- At least one filter is required, and results are bounded.
- Every result passes the existing checksum verification, through a shared read path that subject retrieval also uses.
- No schema, write, or maintenance surface changes.

### New `CompanionCore.Recall` project (and tests)

It references only Memory and produces append proposals committed only through `LocalWriteGate`.

**Session consolidation.**
- One summary record whose `Source` links reference every consolidated original; nothing is deleted or modified.
- Highlights (personal, funny, important, lore-rich) are preserved verbatim as their own linked records.
- Routine entries are compressed into the summary's counts.

**Adventure records.**
- Active, paused, and finished statuses are append-only and each supersedes the previous one, so retrieval yields the current status while the history remains.
- New-save and ending hypotheses are recorded as guesses awaiting Boss confirmation; a confirmation is a user correction that supersedes the hypothesis.

**Lore provenance.**
- Statuses: observed, read, told, suspected, confirmed.
- A confirmation or correction appends a linked record, so the validated understanding ranks first and the earlier theory remains.

**Grouped evolving beliefs.** Opinions on a subject append and supersede, and the belief history reconstructs current-first with every earlier stance kept.

**User-correction precedence.** A correction is a `UserCorrection` record that `Corrects` its target and always ranks first.

**Spoiler-aware recall.** `RecallSelector` and `RecallService` select local relevant context for a game, save, and session, ranked and bounded:
- session records only for the same session;
- save records only for the same save;
- game records from another save only when not spoiler-flagged;
- general memories reached through focus subjects.

**Interest roots and generated seeds.**
- `InterestRootSet` is immutable, versioned, and checksummed, loaded locally.
- A generated seed is stored only after validation against an existing root, and can never alter a root.

### Records and decisions

- Retention: session transcripts and every committed record are retained, with no automatic deletion. This matches "generous" ledger retention and the never-delete rule.
- Placeholder-neutral visible text throughout.
- Deterministic record IDs derived from the operation, so replaying a consolidation is `AlreadyCommitted`.

## Explicitly forbidden

- Any update, delete, overwrite, or compaction of committed memory.
- Any change to the schema or to maintenance and repair.
- Personality voice, API calls, or app wiring.
- Durable photographs (Stage 10).

## Acceptance scenarios

1. A synthetic adventure completes: a finished status and a summary are committed, every original stays retrievable and unchanged, and replaying the consolidation is `AlreadyCommitted`.
2. Resuming a save retrieves its relevant history, with the summary and current status first.
3. A new save of the same game receives no save-scoped or spoiler-flagged knowledge from the old save, but does receive non-spoiler game knowledge and general memories.
4. Correcting lore ranks the confirmed understanding first while the suspected theory remains retrievable as non-current.
5. A shared joke is preserved verbatim; routine progression is compressed into counts.
6. A user correction outranks every other source kind; evolving opinions reconstruct current-first.
7. Adventure hypotheses await confirmation, and a confirmation supersedes the hypothesis.
8. Interest roots are immutable and checksummed; invalid generated seeds (unknown root, bad text) are refused; valid seeds append.
9. `MemoryQuery` is bounded, validated, verified, and filter-required. Every accepted memory test still passes.
10. All 563 accepted tests still pass on Windows CI.

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Memory/MemoryQuery.cs`, `src/CompanionCore.Memory/MemoryStore.Retrieval.cs`, and `src/CompanionCore.Memory/MemoryRepository.cs` (the read-only query only)
- `tests/CompanionCore.Memory.Tests/**` (query tests only)
- `src/CompanionCore.Recall/**` and `tests/CompanionCore.Recall.Tests/**`
- `src/CompanionCore.Memory/AssemblyInfo.cs` (one test-only friend line)
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Gate result: **PASS** on 2026-10-10.

**Actual-diff review.**
- The 21-path change is allowlisted.
- Recall exposes no update, delete, or compaction path, which a reflection test guards.
- Every write is an append proposal through `LocalWriteGate`.
- `MemoryQuery` SQL is built only from fixed fragments plus bound parameters.
- No schema or maintenance change.

**Merge.**
- PR #31's merge ref had exact parents `6f27392` (main) and `033eef6` (head), and a tree equal to the head tree `c200f16`.
- It was marked ready and squash-merged through the expected-head fence as `e529eb6`, with the tree unchanged.

**Evidence.**

- Gate head `755f8d3`:
  - push run `38056481599` and PR run `38056494760` each passed **621/621** (13 projects, Recall 41, Memory 85, App Integration 13);
  - test-results and attention-sheet artifacts: archive digests verified;
  - attention-sheet PNG: 792×621, `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- Local gate: 608/608, 0 warnings, 0 vulnerable packages.
- Mutation pass: 63 mutants; 62 killed, 1 equivalent.
- Acceptance scenarios 1–9 are covered by `RecallScenarioTests`, `RecallMechanicsTests`, and `MemoryQueryTests`. Scenario 10 is covered by the full CI suite.

## Personal Round Judgments

- **J1 — Retention.** Session transcripts and every committed record are retained; there is no deletion or compaction path. This follows Boss's "generous" ledger retention and the never-delete invariant, and closes the deferred transcript-retention item.
- **J2 — Consolidation is one atomic append.** A session summary and its highlight copies form one `AppendMemoryProposal`, bounded by the store's 128 records per operation. An oversized session is refused (`ArgumentException`) rather than silently truncated, so the caller splits it explicitly. Summaries chunk at 256 `Source` links, the store's per-record link bound.
- **J3 — Idempotency needs a persisted operation time.** Record IDs derive from the operation ID and index, but the operation checksum also covers `CreatedAtUtc`. Replaying the same operation and time is `AlreadyCommitted`; a drifted re-plan under the same operation is a store `Conflict`, so it can never double-commit. Future wiring must persist `(operation, now)` with the consolidation intent. The tests prove both outcomes.
- **J4 — Spoilers are attributed to a save.** Spoiler-flagged lore requires the save where it was learned. The selector offers spoiler knowledge only inside that save, and suppresses unattributed spoiler records from other paths (the conservative choice).
- **J5 — Strict authority order in recall.** A current user correction forms its own tier above every other source. Confidence is weighted below the smallest authority gap, so it only breaks ties within one source kind. Relevance bonuses (focus, current summary or adventure, highlight) order records within a tier. Non-current history always ranks after current understanding.
- **J6 — Highlights stay close across saves.** A highlight copy is game-scoped (general when there is no game) and keeps its spoiler flag. A non-spoiler shared joke can therefore surface in a later save of the same game; a spoiler highlight cannot.
- **J7 — The roots fingerprint is required and unambiguous.** `InterestRootSet.Load` requires and verifies the fingerprint. The canonical form uses JSON framing, after the bug hunt showed that tab/newline framing let two different root sets collide.
- **J8 — Defensive planner inputs.** Duplicate originals are deduplicated. Originals from another game or save are refused, as are null originals. Recall metadata from other write paths parses as a plain entry.
- **J9 — No Memory friend line needed.** `TestDataRootPolicy` is public, so the Recall tests need no new `InternalsVisibleTo` in Memory; the allowed `AssemblyInfo.cs` change is unused.

## Deferred findings

(None yet.)
