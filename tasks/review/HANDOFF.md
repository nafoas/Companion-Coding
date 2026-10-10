# Task Handoff

## Task

Task 10 — Memory Consolidation and Retrieval Mechanics (`tasks/active/task-10-memory-consolidation.md`), Roadmap Stage 8.

Builder: Claude.

## Completed

- **Read-only `MemoryQuery`.** Exposed through `MemoryRepository.RetrieveAsync`:
  - filters by game, save, session, literal subject prefix, scope, or record IDs;
  - requires at least one filter, and is bounded (limit ≤1000, ≤256 IDs);
  - shares one checksum-verified read path with subject retrieval;
  - adds no schema, write, or maintenance change.
- **New `CompanionCore.Recall` project** (references only Memory):
  - session consolidation: summaries `Source`-link every original, highlights are copied verbatim, routine entries are compressed into counts;
  - append-only adventure statuses and hypotheses, plus Boss confirmation;
  - lore provenance with linked corrections;
  - grouped evolving opinions (`BeliefHistory`);
  - user-correction precedence;
  - spoiler-aware, bounded local recall (`RecallSelector`, `RecallService`);
  - immutable checksummed interest roots, with validated generated-seed storage.
- Packet judgments J1–J9, including the retention decision (retain everything; no deletion).

## Changed

- `CompanionCore.slnx`
- `src/CompanionCore.Memory/MemoryQuery.cs` (new), `MemoryStore.Retrieval.cs`, `MemoryRepository.cs`
- `src/CompanionCore.Recall/**` (new)
- `tests/CompanionCore.Memory.Tests/MemoryQueryTests.cs` (new)
- `tests/CompanionCore.Recall.Tests/**` (new)
- This packet and this handoff

## Verification

- Local gate (Linux, cross-targeted build), with the 13 App integration tests deferred to Windows CI:
  - locked restore;
  - Release build with `--no-incremental /warnaserror`: 0 warnings, 0 errors;
  - vulnerability audit: 0;
  - tests: **608/608**: Api 128, Attention 49, Capture 14, Capture Worker 68, Conversation 44, Memory 85 (+17 query tests), Presentation 50, Privacy 13, Recall 41 (new), Runtime 26, TargetAuth 73, Transcript 17.
- Expected on Windows CI: 621 (608 plus 13 App integration tests).
- Recall tests repeated 5× with no flake, after record-ID ordering was compared as sets.
- Mutation pass: 63 mutants across Recall, `MemoryQuery`, and the retrieval SQL.
  - The first run killed 59.
  - Three survivors were real test gaps, closed with new tests: focus bonus, service session query, and belief-current selection.
  - One is equivalent: removing the duplicate-root check still refuses through the dictionary build with the same `ArgumentException`.
- CI: pending.

## Bug hunt findings fixed before the gate

These came from review and from writing the tests:

- A consolidation larger than one store operation would have been rejected opaquely; it is now refused up front.
- Duplicate originals would have produced duplicate links, which the store rejects; they are now deduplicated.
- Spoiler lore without a save would have reached new saves; spoiler lore now requires a save, and the selector suppresses unattributed spoilers.
- A full-confidence Integration record could tie or beat a low-confidence user correction; corrections now have their own tier.
- The roots fingerprint was ambiguous under tab/newline descriptions; it now uses JSON framing.
- A missing roots fingerprint was accepted; it is now required.
- Null recall metadata threw; it now parses as an entry.

## Remaining

- Finish the gate (CI, evidence, merge, closure).
- Then Task 11 (Stage 9 Watchbun continuity).

## Risks and assumptions

- Recall scoring weights and budgets are provisional until Stage 11.
- Consolidation idempotency needs the caller to persist the operation time (J3); wiring is deferred.

## Review focus

- No update, delete, or compaction path exists anywhere in Recall.
- Spoiler suppression and the scope rules.
- The `MemoryQuery` SQL builds only from fixed fragments plus bound parameters.

## Repository state

- Branch `agent/task-10-memory-consolidation`, based on `main` `6f27392`.

## Next safe task

After closure: Task 11, Watchbun continuity (Stage 9).
