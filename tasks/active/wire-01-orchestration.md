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
10. **Regression.** All 744 accepted tests still pass on Windows CI.

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Orchestration/**` and `tests/CompanionCore.Orchestration.Tests/**`
- Production friend lines for `CompanionCore.Orchestration` in TargetAuth (reading the issued grant), Vault (backup and repair), and Keepsakes (Boss-requested deletion)
- Test-only friend lines for `CompanionCore.Orchestration.Tests`
- Any defect fix in an accepted component that composition exposes, recorded below with a regression test
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Pending.

## Personal Round Judgments

(Recorded during implementation.)

## Defects found by composition

(Recorded during implementation.)

## Deferred findings

(None yet.)
