# WIRE-01 — Companion Orchestration Core

Status: **active — implementation**
Authorized: 2026-10-10 by Boss ("For now, wire everything together, and do a bunch of tests to make sure everything works as it should. Test every task previously and make sure it all works fine, bugfix, and then let's roundtable again").
Accepted remote base: `32351f1f15b6211fb856473015886363edef91fc`
Working branch: `agent/wire-01-orchestration`
Builder: Claude

## Objective

Connect every accepted subsystem into one platform-neutral, testable runtime pipeline. Each accepted component keeps its own invariants; this packet only composes them, and fixes any defect that composition exposes.

The pipeline:

1. Authorized capture → attention sheet.
2. Braincase bridge (stateless, mock/offline only).
3. Attention engine.
4. Conversation coordinator.
5. Session transcript.

Around it:

- **Memory:** append-only, through `LocalWriteGate`.
- **Watchbun:** continuity and its quiet, exit, relaunch, and suspend semantics.
- **Recall:** consolidation at session close.
- **Keepsakes:** camera actions.
- **Da Bun Vault:** at session end, plus a repair command.
- **Restart:** state persisted and restored.

WIRE-02 (separate packet) supplies the Windows platform signals and the WPF composition root.

## Authority

- Boss's 2026-10-10 direction above, after the roundtable that named this wiring packet as the safe, credential-free next step.
- The neutral-core packet's core-gate list (Task 12) names the "complete placeholder-interface end-to-end demonstration" this packet builds toward. The live-API portions of Task 12 remain a stop condition and are **not** touched.
- Every accepted packet (Tasks 1–11, ERPP-01, KEEP-01, KEEP-02, R2–R5) remains the behavioral authority for its component.

## Required implementation

### New `CompanionCore.Orchestration` project (and tests)

It is platform-neutral (`net10.0`) and references the accepted component projects.

**`CompanionOrchestrator`.** A single-consumer mailbox serializes every input: target events, sheets, ticks, platform signals, structured game events, and Boss commands. Nothing races, and no component is re-entered.

- **Target authorized:** start an attention engine and a session transcript. Watchbun starts fresh, or reattaches by grant when it is awaiting relaunch, paused, or recovering for the same executable.
- **Sheet available:**
  - A pending camera action takes a keepsake photograph from the sheet's full-context region.
  - Otherwise, only while Watchbun allows full capture and semantic spending, one bridge interpretation runs off the mailbox. Its interpretation is mapped to attention evidence, a conversation game observation, and a Watchbun meaningful event.
  - Otherwise the sheet is disposed (RAM-only).
- **Privacy pause:** cancel pending bridge work immediately, outside the mailbox, and drop any pending camera action. Generation fencing remains the guarantee.
- **Ticks** advance attention, conversation, and Watchbun. Checkpoints persist on a bounded interval and at every Watchbun checkpoint request.
- **Watchbun intents** drive the rest:
  - alerts become notices;
  - spending pauses gate the bridge;
  - suspension cancels bridge work;
  - consolidation requests run Recall consolidation then a Vault backup;
  - a close ends the target session;
  - an exit prompt, a re-authorization request, or a paused adventure becomes a notice and/or an append-only adventure record.
- **Boss ending the target session** consolidates, backs up, and ends the transcript.
- **Notices** are one typed stream for presentation. Wording belongs to the adapter.

**`CompanionHost`.** It owns the memory repository, bridge, orchestrator, and locations for one validated development or test data root. `RepairAsync` stops everything, runs `DaBunVault.RestoreAsync`, reopens, and reports.

**Persistence.**

- Conversation and Watchbun checkpoints go through `VaultStateStore`, so they are inside the Vault.
- A consolidation intent is persisted before its commit, so an interrupted consolidation replays idempotently (Task 10 J3).

**`SheetPhotographSource`.** It decodes the capture worker's own sheet PNG format (strict: RGBA8, filter 0, CRCs) and crops the full-context region into an in-RAM BGRA frame for `KeepsakeCamera`. There is no IPC change.

**`IPlatformSignals`.** Foreground, input, target exit, target launch, and suspend/resume events. WIRE-02 implements it for Windows; the tests use a synthetic one.

## Explicitly forbidden

- Live or paid API, and real credentials (the offline provider shell or mock only).
- Weakening any accepted invariant or test.
- Production data roots.
- Personality wording.
- Durable writes outside keepsakes, state, transcript, Braincase journal, and memory.
- Capture of anything but the authorized target.

## Acceptance scenarios (end-to-end, real components with a scripted worker and mock provider)

1. **Full loop.** Authorize, then a sheet goes through the bridge (mock), attention, conversation, transcript, and memory. Notices are emitted, and the sheet bytes are never written.
2. **Privacy.** A pause mid-bridge cancels the work and admits nothing. Resume refreshes the grant without breaking continuity.
3. **Watchbun.** Quiet hours pause semantic spending (no bridge calls), then close safely with consolidation, a Vault backup, and the session ending.
4. **Tab-away.** Structured urgent events raise target-only alerts while another window is foreground.
5. **Exit, relaunch, reattach.** Exit, prompt, and wait for relaunch, then re-authorization reattaches Watchbun to the new grant.
6. **Keepsake.** A camera action takes a verified photograph from the next sheet, paired with the action.
7. **Restart.** Checkpoints restore the conversation and Watchbun after a host restart. Watchbun recovers without capture until re-authorization.
8. **Consolidation idempotency.** A consolidation interrupted after its intent was persisted replays idempotently.
9. **Repair.** `CompanionHost.RepairAsync` restores a damaged BunDex, photographs, and state, and reports.
10. **Regression.** All 744 accepted tests still pass on Windows CI (793 with the new Recall regression and the 48 orchestration tests).

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Orchestration/**` and `tests/CompanionCore.Orchestration.Tests/**`
- Production friend lines for `CompanionCore.Orchestration` in TargetAuth (reading the issued grant), Vault (backup and repair), and Keepsakes (Boss-requested deletion)
- Test-only friend lines for `CompanionCore.Orchestration.Tests`
- Any defect fix in an accepted component that composition exposes, recorded below with a regression test
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Gate result: **PASS** on 2026-10-10 (pending the merge-ref check and merge, recorded at closure).

**CI evidence.**
- **First CI (head `414ee9d`):** push run `38069900594` and PR run `38069916752` each failed one test, `Ids_AndGameReferences_AreStableAndSafe`. This exposed C8, a platform-dependent product behavior, fixed in `2b1656f` with added cases.
- **Fix head `2b1656f`:** push run `38070682490` and PR run `38070686736` each passed **793/793** (17 test projects: 744 accepted + 1 Recall regression + 48 orchestration).
- **Artifacts:**
  - test-results and attention-sheet archive digests verified;
  - attention-sheet PNG: 792×621, `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.

**Actual-diff review.**
- The 26-path change stays inside the allowed scope. The only edits to accepted components are:
  - the C1 escape in `RecallSubjects.Summary`;
  - the internal `DaBunVault.BackUpAsync` wrapper;
  - friend lines.
- Every accepted test passes unedited, apart from one added Recall regression.
- No credentials, live API, production data root, or personality wording.

Local evidence:

- **Local gate.** Locked restore and a `--no-incremental` Release build with `/warnaserror`: 0 warnings, 0 errors. 0 vulnerable packages.
- **Local tests.** 780/780 on Linux across 16 projects (the App integration tests are Windows-only). The accepted suites pass unedited, apart from the one added Recall regression test.
- **Orchestration tests.** 48 tests:
  - `OrchestrationScenarioTests`: acceptance scenarios 1–9 plus 8b;
  - `OrchestrationMechanicsTests`;
  - `OrchestrationCoverageTests`.

  They passed five consecutive runs, then ten more under full-core CPU load.
- **Mutation pass.** 49 mutants over the orchestrator, evidence mapper, sheet decoder, host, and the C1 fix. The first pass left 24 survivors; each became a targeted test. Final result: **45 killed**, plus:
  - 3 equivalent (defense in depth):
    - the orchestrator's sheet-grant re-check (the controller fences stale sheets at both the event and the take);
    - the stale-session outcome check (bridge generation fencing stops the outcome first);
    - the summary/highlight filter (the planner already excludes consolidation output, as the undrained-replay test proves);
  - 1 unobservable: zeroing the photograph's BGRA buffer after use (RAM hygiene with no externally visible effect).
- **Simplification.** The mutation pass showed that the `_endingForExit` flag was dead once the C5 guard existed (a `TargetEnded` with no attached session is always ignored), so it was removed (J10).

## Personal Round Judgments

- **J1 — Photographs come from the sheet.** A keepsake photograph is cropped from the next sheet's full-context region, at the worker's sheet resolution. There is no new capture IPC, so the capture surface and the worker contract stay exactly as accepted. Full-resolution photographs are deferred (D2).
- **J2 — Scope of each component.** The conversation coordinator and its checkpoint are runtime-wide: one Conversation Thread. Attention engines and transcripts are per target session.
- **J3 — Bridge calls.** Bridge interpretation runs off the mailbox with at most one call in flight. A sheet arriving during a call, or while Watchbun pauses semantic spending, is disposed at once (RAM-only); the latest-sheet slot keeps only the newest.
- **J4 — Immediate privacy cancel.** A privacy pause cancels bridge work immediately on the event thread as well as through the mailbox. Generation fencing remains the guarantee; the early cancel only saves spend.
- **J5 — Braincase notices.** Braincase notices are emitted only when the outcome kind changes (Interpreted / Napping / Unavailable), so an offline Braincase produces one notice, not one per sheet.
- **J6 — No process monitor.** Without a platform process monitor, `TargetUnavailable` is treated as an unclean exit (crash semantics). A clean exit cannot be proven without that evidence, and Watchbun's crash path is the conservative one.
- **J7 — Durable consolidation queue.** Consolidation intents form a durable per-session queue bounded at 64. A rejected intent stays queued and is retried at every later consolidation and start. Dropping the oldest intent past the bound loses only the derived summary, never an original.
- **J8 — Topic keys.** Topic keys are transliterated (Unicode FormD, nonspacing marks removed) and then restricted to `[a-z0-9._-]`, at most 64 characters, so "Ünïcödé" becomes "unicode". Scripts with no Latin form fall back to the neutral "observation".
- **J9 — Friend lines.** Production friend lines were added only where the orchestrator needs an accepted internal authority:
  - the TargetAuth grant read;
  - Vault backup and repair;
  - Boss-requested keepsake deletion.

  All other friend lines are test-only.
- **J10 — One rule for `TargetEnded`.** It is acted on only while a session is attached. Our own ends, after a target exit or a Watchbun close, always close the session objects first, so no separate "ending for exit" flag is needed. A flag that could go stale is one more state to get wrong.

## Defects found by composition

Each was fixed at its root, with a regression test.

- **C1 — Recall summary subjects (Task 10).** Bridge session references (`target-session:<id>`) contain colons, and `RecallSubjects.Summary` produced a subject key the write gate rejects, so consolidation was refused for every real session. The session part is now escaped (`%` → `%25`, `:` → `%3A`). Regression: `RecallMechanicsTests.SessionSummary_ConsolidatesBridgeStyleSessionReferences`.
- **C2 — Consolidation aborted silently.** A session closed after a target exit had no game reference, and a failure in one session aborted the whole close without notice. The game now comes from the Watchbun binding when no session is attached, and each session's consolidation is contained (a fault notice, its intent replayed later), so the Vault backup and cleanup always run. Covered by the crash-then-consolidate and Watchbun-close scenarios.
- **C3 — Startup notices lost.** Notices raised during start, such as Recovering, were published before presentation could subscribe. `CompanionHostOptions.Notice` is now attached before every start, including after repairs. Regression: Scenario 7 asserts `Recovering`.
- **C4 — No repair path for a damaged BunDex.** A damaged BunDex made the host unopenable, so Bnuy Repairs could never run. `CompanionHost.RepairOfflineAsync` now restores with nothing holding the data root. Regression: Scenario 9.
- **C5 — Recovering Watchbun dissolved.** A `TargetEnded` arriving with no attached session (after a restart) consolidated and dissolved a recovering or paused Watchbun span that was Boss's to decide. It is now ignored. Regression: Scenario 7's end-then-reauthorize reattach.
- **C6 — Single-slot consolidation intent.** The single durable intent could be overwritten by another session's consolidation, losing the unfinished intent's summary. It is now a durable per-session queue (J7). Regressions:
  - `Scenario8b_AnotherSessionsConsolidationNeverOverwritesAnUnfinishedIntent`;
  - `RejectedConsolidationIntent_StaysQueuedAcrossOtherSessions`.
- **C7 — Leftover sessions dropped.** Sessions left by an interrupted close were cleared without consolidation when a fresh Watchbun started. They are now consolidated first. Regression: `SessionsLeftByAnInterruptedClose_AreConsolidatedBeforeAFreshWatch`.
- **C8 — Platform-dependent game references.** Found by the first Windows CI run of `414ee9d` (push run `38069900594`, PR run `38069916752`): 792/793, one failure. `GameReference` used `Path.GetFileNameWithoutExtension`, which reads `c:` as a drive on Windows, so one executable name produced different BunDex keys on different platforms. It now strips path separators and the last extension itself, identically everywhere. Regression: `Ids_AndGameReferences_AreStableAndSafe` gains Windows-path, POSIX-path, dotted, and extensionless cases.

## Deferred findings

- **D1 — Per-session consolidation bounds.** One session's consolidation reads at most `MemoryQuery.MaximumLimit` (1000) originals, and the planner proposes at most 128 records. Longer sessions keep every original but summarize only the first page. Paging or rolling consolidation belongs to Stage 11 calibration.
- **D2 — Full-resolution photographs.** Keepsake photographs are taken at sheet full-context resolution (J1). A full-resolution photograph needs a worker capture command and a new IPC contract.
- **D3 — Windows platform signals and the App composition root.** These are WIRE-02.
