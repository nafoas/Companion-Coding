# CAL-03 — Failure-Mode Hardening and Known-Limitations Report (Stage 11, part 3)

Status: **accepted** — Paw Gate PASS; PR #49 squash-merged as `039a641fe5269d2b4c08650f775248df3b32c952`
Authorized: 2026-10-11 by Boss ("Stage 11 should start now, and then stop at Stage 12").
Accepted remote base: `07a2d5ba547e7c6da6e11eab2991761daeba6995`
Working branch: `agent/cal-03-failure-hardening`
Builder: Claude

## Objective

These are the Roadmap's Stage 11 failure deliverables:

- forced API failure;
- capture failure;
- disk full;
- corrupted database;
- sleep/wake;
- crash loop;
- database migration and upgrade rollback;
- the known-limitations report.

Most already have coverage at their own layer:

- API failure, nap, and outage: Task 7;
- the corrupted database and repair: Tasks 2–3 and KEEP-02;
- sleep/wake: Task 11 and WIRE-02;
- an unknown memory schema failing closed without changing data: Task 2.

This packet proves the composed runtime survives them, and fixes the defect that proving exposed.

## Defect found and fixed

- **F1 — Unreadable checkpoints were silently replaced.** The orchestrator started fresh when a checkpoint was unreadable (damaged, or written by a newer build after a rollback), and its next checkpoint overwrote the original. A conversation checkpoint therefore also lost the one Conversation Thread lineage. The same applied to the sessions list and the consolidation queue.

  The fix:
  - Any unreadable state entry is now preserved once under `<name>.unreadable`, as a valid entry, so it never blocks a Vault backup.
  - The first preserved copy is never replaced.
  - The conversation lineage id is kept whenever it can still be read.
  - The event is reported as a contained `Fault` notice (`UnreadableState:<name>`).

## Required implementation and evidence

`OrchestrationFailureTests` (7 tests):

- **Newer-build checkpoint:** preserved byte-exactly, lineage kept, reported once. The next start reads its own checkpoint; a later unreadable copy never replaces the first preserved one.
- **Empty-lineage checkpoint:** starts a fresh lineage instead of crashing startup.
- **Damaged checkpoint:** the raw bytes are preserved, and the following Vault backup succeeds.
- **Unparseable sessions list:** preserved, and startup and capture continue.
- **Disk-full checkpoint write:** the failure is contained as a fault; memory keeps committing; the next checkpoint succeeds once space returns.
- **Crash loop:** five restarts with the target authorized. Every memory is committed exactly once, the conversation lineage is constant, and there are zero faults.
- **Capture worker fault:** absorbed without an orchestrator fault; capture resumes and is interpreted again.

`docs/Known-Limitations.md` is the consolidated report.

## Personal Round Judgments

- **J1 — Preserve, never delete.** Unreadable state may come from a newer Prince (an upgrade rollback). Preserving it inside the Vault keeps it recoverable, and reporting it keeps it honest.
- **J2 — The lineage is identity.** Keeping the coordinator id preserves "one Conversation Thread" even when the rest of the checkpoint cannot be restored.

## Paw Gate

Gate result: **PASS** on 2026-10-11.

- **CI.** Head `dcface9`: push run `38099942669` and PR run `38099951518` each passed **963/963** on the first attempt.
- **Artifacts:**
  - archive digests verified;
  - attention-sheet PNG: 792×621, `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`.
- **Merge.**
  - The merge ref had exact parents `07a2d5b` and `dcface9`, and a tree equal to the head tree `99b0427`.
  - It was squash-merged through the expected-head fence as `039a641`.

Local: Orchestration 68/68 (stable across repeated runs). Mutation: 8 mutants, 8 killed.

## Deferred findings

None new. The open items are listed in `docs/Known-Limitations.md`.
