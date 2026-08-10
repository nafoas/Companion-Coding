# R0 — Direct-Build Re-entry and Continuity Alignment

Status: **accepted**
Authorized by user: 2026-08-10
Accepted: 2026-08-10
Branch: `agent/reentry-docs`
Pull request: #4

## Objective

Reopen construction safely without merging or changing product code. Reconcile the final preserved Claude checkpoint, replace the retired two-model workflow with direct evidence-based Paw Gates, and make the user's final-only Companion Awakening rule unambiguous in every governing document.

## Entry evidence

- Accepted `main` contained Task 0 architecture and the explicit pause/backpedal only.
- Closed PR #3 preserves Task 1 at head `da4797e1a3df2c6f0ddaaa0248098fd40f656121`; it was not accepted or merged by R0.
- The final checkpoint is one commit ahead of the last independently reviewed code SHA `7aaa40db77e5f38738e5f33f2e58e99443b5f81c`.
- That one final commit changes only `tasks/review/HANDOFF.md`; it contains no product-code delta.
- The final handoff records a Windows clean locked restore, Release build with zero warnings/errors, and 58/58 tests: Runtime 24, Presentation 19, Capture 11, and WPF integration 4.
- These historical results establish that adoption is worth reviewing; they do not pass a new Task 1 gate against current `main`.

## Accepted changes

- Defined the direct Codex/Builder Prince workflow and distinct Paw Gate review pass.
- Preserved one active task at a time and autonomous progression only after recorded passing gates.
- Kept Claude collaboration and hourly monitoring stopped.
- Defined Prince's Personal Round Judgment for routine, reversible, in-scope choices and its hard stop conditions.
- Kept Builder Prince resettable through core construction, full personality installation, presentation, launch validation, and refinement.
- Made Companion Awakening a final, one-time transition after the complete launch-required build passes.
- Required a clean production BunDex, no Builder-memory transfer, and permanent singular continuity across every post-awakening update.
- Kept Task 1 product code out of this control gate and required a separate adoption branch and fresh verification.

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

## Paw Gate evidence

- Reviewed candidate: PR #4 head `99b48124894d431060952ecaecb83900af7f0106`.
- Exact remote/local tree: `bfb3bcefe16f41535f8043a199a368f84f7cf4c3`.
- `git diff --cached --check`: passed.
- Exactly one active task during implementation: passed.
- Allowed scope: 11 Markdown/control files and no product, source, tests, dependencies, build, or CI changes.
- Operative-authority and awakening-contradiction searches: passed.
- Remote PR mergeability: passed.
- Configured CI: none; not applicable to documentation-only R0.

Result: **Paw Gate passed.**

## Deferred Findings

- Task 1 requires a separate adoption packet, candidate diff against current `main`, fresh Windows CI, and review of its inert `--test-mode` surface.
- The final launch manifest is created only after the neutral core passes; do not pre-build personality, artwork, animation, audio, or final UI during Task 1.
