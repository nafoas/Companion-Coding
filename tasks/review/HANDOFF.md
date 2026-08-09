# Task Handoff

## Task

Task 0 — Repository survey and architecture proposal, per `tasks/active/task-00-architecture.md`. This handoff covers the revision round responding to `tasks/review/FOREMAN_REVIEW.md`, which requested changes rather than approving the first proposal.

## Completed

Revised `docs/architecture/task-00-architecture-proposal.md` to address all ten findings in `tasks/review/FOREMAN_REVIEW.md`:

- **R1** — stack recommendation changed from .NET 8 to .NET 10 LTS (§2), with the lifecycle rationale stated inline.
- **R2** — replaced the ambiguous SQLite/NDJSON peer-store description with one canonical durability protocol (§5.1): SQLite as sole committed authority, the session journal as a checksummed write-ahead recovery tail, exact append/commit/checkpoint ordering, deterministic torn-tail recovery, journal rotation gated on a validated backup, and privacy-cancellation fencing at the protocol boundary.
- **R3** — strengthened dev/production separation (§5.3): distinct app identifiers and fixed default roots, development binaries technically unable to reach the production root outside a guarded migration tool, no ambient path fallback, separate namespacing for mutex/credentials/db/backups/logs, and a named contract test.
- **R4** — redefined `CaptureWorker` as a required out-of-process boundary before real capture is accepted (§6.1): Task 1 now defines only the `ICaptureWorker` contract plus an in-process fake; the real worker becomes a separate OS process starting Task 5, with bounded/versioned/cancellable IPC that cannot reach identity or memory.
- **R5** — softened capture-support claims (§4): WGC is described as the selected mechanism, not a guarantee of fresh frames when unfocused/occluded/minimized; minimized and exclusive-fullscreen states are now treated as unsupported until a required pre-Task-5 feasibility spike proves otherwise, with a documented stop condition.
- **R6** — split the presentation contract (§6.2): `IPresentationSink` (renders opaque content, UI-facing) vs. the later `IPersonalityAdapter` (generates character content, installed at Stage 13), correcting the earlier backwards claim that presentation "never receives character-voice text."
- **R7** — clarified memory-write authority (§6.3): the runtime/API path through `LocalWriteGate` is unchanged (append-only, no update/delete), and a separate `MaintenanceStore` capability, unreachable from `ApiBridge`/`ISemanticProvider`, is named for backup restore, migration, and future explicit human-initiated operations.
- **R8** — removed the parallelization suggestion; Task 2 and Task 4 are now shown strictly sequential in the dependency map (§13), per the packet's one-task-at-a-time rule.
- **R9** — added generation-ID cancellation fencing to the privacy boundary (§11): every capture request/frame/semantic result carries a generation ID, privacy stop revokes the generation, and anything tagged with a stale generation is discarded before it can reach presentation, journaling, or memory.
- **R10** — rewrote §15 from "open decisions" to "resolved decisions," recording the foreman's five-point verdict.

The module dependency diagram (§8) and directory structure (§9) were also updated for consistency with R4/R6/R7 (out-of-process worker, `IPresentationSink`, `MaintenanceStore`), and the risk register (§12) gained three new rows (backup-mechanism race, worker-IPC boundary, generation-fencing) plus corrected wording on the two capture-limitation rows.

## Changed

- `docs/architecture/task-00-architecture-proposal.md` — revised in place per the above; a revision note at the top maps each finding to its section.
- `tasks/review/HANDOFF.md` (this file) — updated for the revision round.

No other files were touched. `AGENTS.md`, `BUILD_LEDGER.md`, the design/roadmap/packet docs, `tasks/active/task-00-architecture.md`, and `tasks/review/FOREMAN_REVIEW.md` are unmodified.

## Verification

No automated tests were run — this remains a documentation-only task; no source tree or test suite exists yet. Verification is a content review against the ten findings, which I traced one-by-one above.

## Remaining

Nothing remaining within this revision's scope. Per the foreman's explicit instruction ("Do not begin Task 1. Commit only the revised architecture proposal and updated handoff on the current branch, then stop for re-review."), no Task 1 scaffolding was started.

## Risks and assumptions

- Assumed "current .NET 10 patch version" is an acceptable level of specificity for §2/§10 rather than naming an exact patch number, since that number will have moved by the time Task 1 actually starts.
- The five pre-Task-5 feasibility spikes (§12, rows 1, 2, 11–13 as applicable) are still unrun — they were never authorized to run during Task 0/this revision, only specified as required gates before Task 5.
- Assumed the foreman's R8 finding means "no parallel task branches," not "no internal parallelism within a single gated task" (e.g. my own subagents working bounded pieces of one task) — flagging this reading in case it's wrong, since it affects how I use subagents once Task 1 is authorized.

## Review focus

- Whether §5.1's durability protocol (framed/checksummed journal + SQLite authority + checkpoint-gated rotation) actually closes the loss/duplication ambiguity R2 identified, or whether a specific failure mode is still underspecified.
- Whether §6.1's out-of-process worker boundary and §6.3's `MaintenanceStore` split are precise enough to scaffold directly in Task 1, or need another round before that contract is locked.
- Whether the R8 reading above (sequential task branches, not sequential internal work) matches the foreman's intent.

## Repository state

- Branch: `claude/multi-ai-code-collab-o5qhj1`.
- This revision is committed on top of `d668d3d4e417a2c580cdf7f823dbcf07452cf362` (`review: request Task 0 architecture revisions`, the foreman's commit containing `tasks/review/FOREMAN_REVIEW.md`), which itself sits on `7060465` / `4c9e0d5` (my original Task 0 submission).
- Worktree is clean except for this handoff file and the revised proposal, both included in this commit.

## Next safe task

Smallest safe next action, not yet started: await the foreman's re-review of this revision. If approved, begin Task 1 (reproducible skeleton) exactly as scoped in §9/§15 of the revised proposal — `CompanionCore.App`, `CompanionCore.Runtime`, `CompanionCore.Presentation` (with `IPresentationSink`/`NeutralPresentationSource`), `CompanionCore.Capture.Contracts`, and `CompanionCore.Capture.Fake`, with the single-instance guard and blank WPF shell — and nothing from `CompanionCore.Capture.Worker` (that's Task 5+, per R4).

## Credit status

Not credit-related. Normal task-boundary stop, per the foreman's explicit instruction to stop for re-review after this revision.
