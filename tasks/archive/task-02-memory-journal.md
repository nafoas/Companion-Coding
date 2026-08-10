# Task 2 — Append-Only Memory and Journal

Status: **accepted — Paw Gate passed 2026-08-10**
Authorized: 2026-08-10 under the accepted direct-build workflow and Boss-approved Stage 2 split
Accepted remote base: `29d0a24e05b58f8ef053c4ebe0b6cfeea7b1ea99`
Accepted base tree: `69eb847631d381360d8ececa3d580912e4a5ad18`
Working branch: `agent/task-02-bundex`
Accepted implementation head: `f3e11acb08a2056f0fe557b4517383a14471227c`
Reviewed gate head: `056dedceb120e48c01b9c71d9a1f2d31ad207a5d`
Exact reviewed gate tree: `ab010b6f5a0d17ec23f84ef0252332143421e427`
Pull request: #6
Final pre-acceptance CI: run `31360021794`, job `93366932942`

## Objective

Build the smallest trustworthy local BunDex spine: a versioned SQLite committed store, an append-only automated write gate, a checksummed recovery journal with transactional checkpoints, linked correction/recurrence/supersession records, deterministic retrieval, and physically distinct development/test data roots.

This remains neutral Builder infrastructure. It stores only synthetic fixtures and neutral visible recollection payloads. It does not create, open, migrate, or import a production BunDex.

## Entry evidence and authority resolution

- Task 1 passed its separate Paw Gate and was squash-merged to `main` as `29d0a24e05b58f8ef053c4ebe0b6cfeea7b1ea99` after exact-tree review and 60/60 Windows tests.
- The accepted architecture §5.1 makes SQLite the single committed authority and `SessionJournal` a checksummed recovery tail, not a peer memory store.
- The master packet's binding order assigns append-only memory/journal to Task 2 and atomic backup/repair to Task 3.
- Roadmap Stage 2 previously combined both tasks and requested Bun-written prose during the neutral core. Boss approved Prince's recommendation on 2026-08-10: Task 2 now builds the neutral store/journal spine; Task 3 separately builds Vault/Repairs; Bun-written rendering waits for Stage 13.

## Required implementation

### Reproducible project and dependency boundary

- Add `CompanionCore.Memory` and `CompanionCore.Memory.Tests` to the solution with committed lock files.
- Use `Microsoft.Data.Sqlite` directly, not EF Core, because the accepted architecture specifies a small explicit append protocol and SQLite transaction boundary.
- Pin stable package versions and override any vulnerable transitive native SQLite bundle with a reviewed stable version. Locked restore remains mandatory.
- No runtime test requires a credential, network, real capture, production path, or private content.

### Data-root isolation

- Development defaults use the fixed `CompanionCore.Dev` application namespace; tests use a required isolated `CompanionCore.Tests` namespace and unique synthetic root.
- The future production namespace is `CompanionCore`, with a distinct database filename/root. It is recognizable for rejection but is not openable by Task 2's development/test store factory.
- Ordinary development configuration has no generic arbitrary-path override or production fallback. Any attempted configured override, especially the recognized production root, is rejected before directory or database creation.
- Public store-opening APIs accept only a validated environment/location capability, never a raw path string.
- Test helpers must require an explicit temporary root. They may not inspect, infer, default to, or fall back to the current user's real application-data root.
- Do not wire the WPF app to persistence in this task; launching the accepted skeleton must not create data as a side effect.

### Versioned committed store

- SQLite is the only committed/queryable authority, in WAL mode with foreign keys enabled and `synchronous=FULL` for durability.
- Schema version 1 records immutable append operations, memory records, and typed links. Unknown/newer schema versions fail closed; migration belongs to Task 3.
- A durable record includes at minimum: immutable record ID, schema version, UTC creation timestamp, scope, source kind, confidence, subject key, optional app/game/save/session references, neutral visible recollection payload, structured retrieval metadata, source-record links, correction/supersession/recurrence links, local operation ID, and checksum.
- One local operation may append one or more records atomically. Operation IDs are unique and the committed operation checksum is retained.
- Repeating the same operation ID and identical payload is an idempotent already-committed result. Reusing an operation ID with a different payload is a hard conflict, never a silent success.
- Logical `UPDATE` and `DELETE` against committed operation, record, or link rows are blocked by database triggers as defense in depth. The live commit method is assembly-internal so later API-facing projects cannot call it directly.
- Record and operation checksums use a deterministic canonical representation and are verified on retrieval.

### Append-only automated write gate

- `LocalWriteGate` is the sole public live-runtime/automated submission path.
- Its allowlist accepts only the typed append proposal. It exposes no update, delete, overwrite, replace-checkpoint, source-metadata mutation, or arbitrary SQL capability.
- Unknown/malicious proposal types or operation names are rejected with a typed reason and produce no journal or database write.
- Validation rejects malformed IDs, non-UTC timestamps, out-of-range confidence, invalid JSON metadata, missing relationship targets, inconsistent subjects for correction/supersession/recurrence, oversized payloads, and non-canonical duplicate links before durable append.

### Journal, transaction, and recovery protocol

- `SessionJournal` is an append-only, versioned binary frame log using explicit lengths and SHA-256 checksums.
- An append operation is written and flushed to disk before its SQLite transaction starts.
- After SQLite commits, a checksummed checkpoint frame records the highest confirmed journal sequence and is flushed.
- Once a journal append has been flushed, caller cancellation cannot create an ambiguous half-operation: commit/checkpoint either finishes or the valid recovery tail remains for startup replay.
- Startup scans through the last valid frame, ignores/truncates only one torn or checksum-invalid trailing frame, finds the last valid checkpoint, and replays later valid operations by unique operation ID.
- A crash after SQLite commit but before checkpoint is duplicate-safe. A crash after journal flush but before SQLite commit is recovered. Earlier valid records survive a torn later append.
- Journal rotation, backup cuts, archive creation, restore, and migration are Task 3 and must not be implemented now.

### Linked understanding and retrieval

- Corrections, supersession, source provenance, and recurrence are new typed links; no prior record is mutated or deleted.
- A correction/supersession target remains retrievable as historical context but is marked non-current by the derived query.
- Retrieval ranks current validated understanding ahead of records it corrects/supersedes. Within equivalent current status, source authority and confidence outrank simple recency, with deterministic tie-breaking.
- Recurrence links preserve every occurrence and do not mark either record superseded.
- Provide a neutral exact-subject retrieval surface only. Semantic search, consolidation, opinion grouping, lore rendering, and context-packet selection remain Task 10.

## Explicitly forbidden

Do not implement or introduce:

- `MaintenanceStore`, backup snapshots, `Da Bun Vault.zip`, restore, repair, journal rotation, schema migration, or any Task 3 capability;
- API bridges, semantic providers, network clients/calls, credentials, or model-generated memory;
- production BunDex creation/opening, production data import, Builder-memory transfer, or Companion Awakening behavior;
- personality voice, Prince-authored recollections, themed labels, final UI, animation, audio, photographs, or real gameplay observations;
- cache eviction, compaction, deletion, destructive duplicate merging, or cleanup that can touch committed records;
- capture, window discovery, authorization, privacy hotkeys, conversation, attention, or later-stage scaffolding with no Task 2 consumer;
- raw arbitrary database paths on any ordinary development/test entry point.

## Required tests and evidence

The handoff records exact commands/results. Tests use unique temporary directories and synthetic neutral fixtures only.

1. Clean locked restore and Release build on Windows with zero warnings/errors.
2. Schema v1 initializes with WAL, `synchronous=FULL`, foreign keys, immutable-row triggers, and no production path access.
3. Valid append round-trips across close/reopen with checksums and every minimum field preserved.
4. Automated update/delete/unknown proposals are rejected before journal/database writes; direct logical row update/delete also fails at SQLite.
5. Retrying an identical operation commits at most once; changing the payload under the same operation ID returns conflict.
6. A valid journal-only append replays after simulated process death and receives a checkpoint.
7. A torn/checksum-invalid trailing frame is discarded without losing earlier committed or recoverable frames.
8. A crash after SQLite commit but before checkpoint replays idempotently with no duplicate.
9. A correction is retrieved ahead of the conception it corrects, while the original remains queryable.
10. Recurrence preserves both records without making either non-current.
11. Missing/cross-subject relationship targets and malformed/oversized records are rejected atomically.
12. Development configuration rejects production-root injection through every public normal configuration surface and creates nothing at the rejected path.
13. Test configuration has no ambient application-data fallback; repository tests never open an eventual production root.
14. Unknown schema version and checksum tampering fail closed without rewriting committed data.
15. All 60 accepted Task 1 tests still pass alongside the new memory suite.

Deterministic crash tests may inject an exact on-disk torn frame through internal test-only access rather than relying on timing a nondeterministic OS kill. The test must exercise the same production parser/recovery path and may not add a shipping command-line crash surface.

## Allowed change scope

- `CompanionCore.slnx`;
- `src/CompanionCore.Memory/**`;
- `tests/CompanionCore.Memory.Tests/**`;
- `.github/workflows/ci.yml` only if required for locked-dependency generation/audit without weakening the existing build/test gate;
- `BUILD_LEDGER.md`, `README.md`, this task/archived packet, and `tasks/review/HANDOFF.md` for control/gate records;
- the Stage 2 section of `docs/Prince-Construction-Roadmap.md`, solely for the Boss-approved Task 2/Task 3 and neutral-rendering correction.

Do not modify Task 1 source/tests, the Design BunDex, neutral-core master packet, accepted architecture, direct workflow, archived packets, paused history, or unrelated files.

## Paw Gate

Gate result: **PASS** on 2026-08-10. The separate review confirmed:

- the implementation matches the §5.1 journal → SQLite → checkpoint protocol and preserves SQLite as the one committed authority;
- public/assembly dependency shape makes `LocalWriteGate` structural, with no automated update/delete or direct store-commit surface;
- the 49-path current-base diff is allowlisted and contains no Task 3 maintenance/backup capability, production opening path, network/API, capture, conversation, or personality behavior;
- crash, unresolved-tail, idempotency, append-only, correction-precedence, checksum, canonicality, and root-isolation negative tests pass;
- exact package locks resolve every SQLitePCLRaw component to 2.1.12 and the permanent direct/transitive vulnerability audit is clean;
- gate head `056dedceb120e48c01b9c71d9a1f2d31ad207a5d` passed locked restore, audit, Release build with 0 warnings/0 errors, and 94/94 Windows tests;
- limitations and Personal Round Judgments J1–J8 are recorded before Task 3 becomes active.

## Personal Round Judgment log

### J1 — Tree-identical local base after Git transport denial

The local Git network broker declined `git fetch` after PR #5 merged. The new local worktree therefore starts from local acceptance-record commit `be4955b`, whose exact tree `69eb847631d381360d8ececa3d580912e4a5ad18` is the same tree squash-merged to remote `main` as `29d0a24e05b58f8ef053c4ebe0b6cfeea7b1ea99`. Remote Task 2 publication must parent the actual remote `main` SHA; local/remote tree equality is rechecked before every published commit. This is transport bookkeeping only and is reversible by rebasing once normal fetch is available.

### J2 — SQLite package pin and vulnerable-transitive override

Official NuGet metadata on 2026-08-10 identifies `Microsoft.Data.Sqlite` 10.0.10 as the current stable package. Its default dependency floor can resolve `SQLitePCLRaw.bundle_e_sqlite3` 2.1.11, whose native package is deprecated with a high-severity vulnerability; stable 2.1.12 is available. Task 2 will pin `Microsoft.Data.Sqlite` 10.0.10 and directly pin the compatible 2.1.12 bundle so locked restore cannot select 2.1.11. The exact resolved graph and vulnerability output remain gate evidence; if restore reports incompatibility or any advisory, stop and revise before acceptance.

### J3 — Retain the canonical operation envelope in SQLite

The operation checksum cannot be independently verified from a subject-only row without either reconstructing every record in the multi-record operation or retaining the bytes that were checksummed. Schema v1 therefore stores the immutable canonical operation payload beside its SHA-256 checksum. Retrieval verifies the payload's checksum, canonical form, operation ID, and exact record-ID set, then verifies each returned record against both its row checksum and the operation envelope. This duplicates bounded canonical bytes inside the one SQLite authority; it does not make the journal a peer store or add a write path.

### J4 — Fixed database names make environment separation explicit

Development, tests, and the recognized future production root use different fixed database filenames as well as different namespaces and roots: `development-memory-v1.db`, `test-memory-v1.db`, and future `memory-v1.db`. Task 2 exposes the future path only for rejection tests; no production `DataRootKind`, location factory, or open capability exists.

### J5 — Reject aggregate frame overflow before durability

Per-field bounds can combine into an operation larger than the journal's 16 MiB frame ceiling. Canonical validation now rejects that aggregate size before entering the durable writer. The journal also builds and bounds a complete frame before marking itself faulted-on-failure, so an oversized proposal writes no bytes and does not poison the live repository. A focused negative test confirms that a valid append can immediately follow an oversized rejection.

### J6 — Cross-check journal checkpoints against the SQLite authority

A checksummed checkpoint is structurally valid journal data, but it cannot be allowed to declare an append committed when SQLite has no corresponding committed sequence. Startup recovery now compares the journal's confirmed cut with SQLite's maximum committed journal sequence and fails closed if the checkpoint advances beyond the one committed authority. A regression test constructs that exact crash/corruption state and proves no data is silently accepted. Task 3 may replace this with a deeper validated repair diagnosis, but may not weaken the fail-closed invariant.

### J7 — Reject duplicate metadata property names before canonicalization

JSON permits parsers to expose repeated object-property names, while sorting those names into a canonical checksum envelope would leave their semantic interpretation ambiguous. Validation now recursively rejects duplicate property names with ordinal comparison before any journal write. The existing malformed-input test proves the proposal leaves no durable trace. A later schema may define a different unambiguous policy before production, but Task 2 does not silently choose first- or last-property semantics.

### J8 — Require recovery before writing past an unconfirmed live tail

If the journal append becomes durable and the following SQLite commit fails, accepting a later live append could checkpoint beyond the unresolved operation and hide it from startup replay. The serial coordinator now refuses every later submission while `ConfirmedThrough` differs from the highest append sequence; closing and reopening performs the normal idempotent recovery before writes resume. A focused test proves the later proposal cannot cross the tail, then proves reopen recovers the stranded operation and permits the later append. This can be revised only by a future in-process recovery state machine that proves the same no-skip property.

## Deferred Findings

- Journal rotation and bounded archive cuts remain Task 3; until then the development/test journal grows append-only.
- Task 2 validates schema version, exact columns, required object names, operation envelopes, and record checksums. Task 3 repair/manifest work may add deeper schema-definition diagnostics; Task 2 does not claim protection from a hostile local account that can rewrite both data and validation metadata.
- Exact-subject retrieval is intentionally the only query surface. Semantic selection, grouping, consolidation, and context packets remain Task 10.
- Production opening, app wiring, model/API proposals, personality rendering, and every non-synthetic memory source remain deferred to their named stages.
