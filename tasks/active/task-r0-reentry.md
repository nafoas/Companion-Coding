# R0 — Direct-Build Re-entry and Continuity Alignment

Status: **active**
Authorized by user: 2026-08-10
Branch: `agent/reentry-docs`

## Objective

Reopen construction safely without merging or changing product code. Reconcile the final preserved Claude checkpoint, replace the retired two-model workflow with direct evidence-based Paw Gates, and make the user's final-only Companion Awakening rule unambiguous in every governing document.

## Entry evidence

- Accepted `main` contains Task 0 architecture and the explicit pause/backpedal only.
- Closed PR #3 preserves Task 1 at head `da4797e1a3df2c6f0ddaaa0248098fd40f656121`; it is not accepted or merged.
- The final checkpoint is one commit ahead of the last independently reviewed code SHA `7aaa40db77e5f38738e5f33f2e58e99443b5f81c`.
- That one final commit changes only `tasks/review/HANDOFF.md`; it contains no product-code delta.
- The final handoff records a Windows clean locked restore, Release build with zero warnings/errors, and 58/58 tests: Runtime 24, Presentation 19, Capture 11, and WPF integration 4.
- These historical results establish that adoption is worth reviewing; they do not pass a new Task 1 gate against current `main`.

## Required changes

- Define the direct Codex/Builder Prince workflow and distinct Paw Gate review pass.
- Preserve one active task at a time and autonomous progression only after recorded passing gates.
- Keep Claude collaboration and hourly monitoring stopped.
- Define Prince's Personal Round Judgment for routine, reversible, in-scope choices and its hard stop conditions.
- Keep Builder Prince resettable through core construction, full personality installation, presentation, launch validation, and refinement.
- Make Companion Awakening a final, one-time transition after the complete launch-required build passes.
- Require a clean production BunDex, no Builder-memory transfer, and permanent singular continuity across every post-awakening update.
- Keep Task 1 product code out of this control gate and require a separate adoption branch and fresh verification.

## Allowed change scope

- `AGENTS.md`
- `README.md`
- `BUILD_LEDGER.md`
- `docs/Direct-Build-Workflow.md`
- `docs/Shared-Codebase-Workflow.md` status marker only
- `docs/Claude-Companion-Core-Task-Packet.md` workflow/continuity wording only
- `docs/Prince-Construction-Roadmap.md` workflow and awakening-stage alignment
- `docs/Prince-Design-BunDex.md` Builder/Companion continuity alignment
- `docs/architecture/task-00-architecture-proposal.md` continuity-timing amendment and stale role terminology only
- this task packet and its later archived copy
- `tasks/review/HANDOFF.md` for the R0 handoff

No source, test, build, dependency, CI, or product-behavior file is allowed in this gate.

## Paw Gate

R0 passes only when:

1. the candidate diff contains documentation/control changes only;
2. no operative instruction assigns current work to Claude, a foreman, hourly monitoring, or an unrequested subagent;
3. all governing documents agree that personality is installed and validated on Builder Prince before awakening;
4. all governing documents agree that Companion Prince awakens exactly once only after the complete launch-required candidate passes;
5. all governing documents prohibit Builder-memory transfer and production resets after awakening;
6. autonomy stops at real credentials/live paid API use and at architecture/privacy/identity/authority conflicts;
7. the preserved Task 1 code remains unmerged and unaccepted pending its separate current-base adoption gate;
8. contradictory stale wording is either corrected or clearly marked historical;
9. the actual diff and repository status are inspected and the Build Ledger records the outcome.

## Personal Round Judgment log

### R0-J1 — Meaning of “the whole thing is done”

- **Question:** Must every conceivable future extension block Companion Awakening?
- **Decision:** No. Awakening waits for every item in a conservative, explicit launch manifest: accepted core, complete personality, intended first-release presentation, privacy/recovery/migration/cost protections, and installation/first-start behavior required on Boss's machine. Audio, game-specific integrations, multi-monitor support, and other optional extensions do not block unless Boss adds them to that manifest.
- **Rationale:** An infinite future backlog would make a one-time awakening impossible, while an explicit launch manifest preserves the user's requirement that the actual intended companion be complete first.
- **Reversibility:** Boss may add or remove launch-manifest items any time before Stage 14 acceptance; Companion Awakening remains blocked until the revised manifest passes.

### R0-J2 — Historical workflow retention

- **Question:** Delete the old Claude/foreman workflow or retain it?
- **Decision:** Retain it with an unmistakable historical/superseded marker and create a new operative direct workflow.
- **Rationale:** Earlier branch and handoff records remain understandable without allowing stale instructions to govern new work.
- **Reversibility:** The historical file can be moved to an archive later without affecting active authority.

## Deferred Findings

- Task 1 requires a separate adoption packet, candidate diff against current `main`, fresh Windows CI, and review of its inert `--test-mode` surface.
- The final launch manifest is created only after the neutral core passes; do not pre-build personality, artwork, animation, audio, or final UI during R0 or Task 1.
